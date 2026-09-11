using Box3D;
using Box3D.Hybrid;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Netcode;
using Unity.Services.Relay.Models;
using UnityEngine;
using static Unity.Collections.AllocatorManager;
using static Unity.U2D.Physics.PhysicsWorld;

public class NetworkPhysicsManager : NetworkBehaviour
{
    public static NetworkPhysicsManager Instance { get; private set; }

    private World _box3DWorld;
    private const float TICK_DELTA = 1f / 60f;
    private const int BUFFER_SIZE = 128;
   
    // --- PRE-ALLOCATED ZERO-ALLOCATION SNAPSHOT STRATEGY ---
    // A fixed pool of managed byte arrays matching your maximum expected snapshot size
    private byte[][] _snapshotBytePool = new byte[BUFFER_SIZE][];
    private int[] _snapshotDataLengths = new int[BUFFER_SIZE];

    // Tracks which ring slots actively hold valid structural ticks
    private int[] _bufferedTicks = new int[BUFFER_SIZE];


    // Direct mapping for applying simulation inputs
    private Dictionary<ulong, Body> _entityToBody = new Dictionary<ulong, Body>();

    // --- INPUT RING BUFFER SETUP ---
    // Maps each integer tick to a nested sub-dictionary tracking individual entity payloads
    private Dictionary<ulong, PlayerInputPayload>[] _inputHistoryBuffer = new Dictionary<ulong, PlayerInputPayload>[BUFFER_SIZE];

    private float _renderTimeAccumulator = 0f;
    private List<VisualBodyProxy> _activeVisualProxies = new List<VisualBodyProxy>();

    // Keep tracking registrations clean
    public void TrackVisualProxy(VisualBodyProxy proxy) => _activeVisualProxies.Add(proxy);
    public void UntrackVisualProxy(VisualBodyProxy proxy) => _activeVisualProxies.Remove(proxy);



    private void Awake()
    {
        Instance = this;
        _box3DWorld = Box3DWorld.Instance.World;

        // Pre-instantiate internal slot dictionary components to prevent real-time GC overhead
        for (int i = 0; i < BUFFER_SIZE; i++)
        {
            _inputHistoryBuffer[i] = new Dictionary<ulong, PlayerInputPayload>();

            _snapshotBytePool[i] = new byte[128 * 1024];
            _bufferedTicks[i] = -1; // -1 represents an empty historical slot
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            // The server drives its execution timeline directly using NGO's fixed network tick interval
            NetworkManager.NetworkTickSystem.Tick += OnServerNetworkTick;
        }
        else
        {
            NetworkManager.OnReanticipate += OnGlobalReanticipate;
        }
    }

    public void RegisterNewSpawnedBody(ulong entityRawBits, Body body)
    {
        _entityToBody[entityRawBits] = body;
    }

    private void Update()
    {
        // 1. If we are running standard tick sequences, track how much variable time has elapsed
        _renderTimeAccumulator += Time.deltaTime;
    }

    private void LateUpdate()
    {
        // 2. Calculate the Alpha Interpolation parameter (normalized value between 0.0 and 1.0)
        // This represents exactly how far we are between two rigid 60Hz physics steps
        float alpha = Mathf.Clamp01(_renderTimeAccumulator / TICK_DELTA);

        // 3. Process the visual interpolation smoothing pass across all active objects
        foreach (var proxy in _activeVisualProxies)
        {
            if (proxy != null)
            {
                proxy.InterpolateVisuals(alpha);
            }
        }
    }

    /// <summary>
    /// The core server execution engine loop. Fires strictly forward at your configured TickRate.
    /// </summary>
    private void OnServerNetworkTick()
    {
        int currentServerTick = (int)NetworkManager.ServerTime.Tick;

        // 1. Process inputs received from all connected clients for this specific tick window
        ApplyInputsForTick(currentServerTick);

        // 2. Advance the authoritative simulation world state cleanly
        _box3DWorld.Step(TICK_DELTA, subStepCount: 4);

        // 3. Synchronize server-side GameObjects so visual things like triggers/raycasts work on the host
        SynchronizeVisualTransforms();

        // 4. Capture the absolute snapshot of the newly resolved world state boundary
        Recording authoritativeSnapshot = Recording.Create();
        _box3DWorld.StartRecording(authoritativeSnapshot);
        _box3DWorld.StopRecording();

        // 5. Package and broadcast the state down to all client simulation nodes
        StateSnapshotPacket packet = new StateSnapshotPacket
        {
            StateTick = currentServerTick,
            CompressedWorldData = authoritativeSnapshot.GetData().ToArray() // Extract raw byte serialization buffer
        };

        // Important: Always clean up native unmanaged allocations to prevent massive memory leaks!
        authoritativeSnapshot.Destroy();

        // Broadcast to everyone using the modern universal attribute mapping flow
        BroadcastStateToClientsRpc(packet);
    }

    private void SynchronizeVisualTransforms()
    {
        // Clear out the spent time remainder window since we just processed a real step boundary
        _renderTimeAccumulator = 0f;

        foreach (BodyMoveEvent moveEvent in _box3DWorld.GetBodyMoveEvents())
        {
            if (moveEvent.UserData != null)
            {
                ulong persistentEntityIdRaw = (ulong)moveEvent.UserData;
                EntityId unityId = EntityId.FromULong(persistentEntityIdRaw);
                GameObject gameObj = Resources.EntityIdToObject(unityId) as GameObject;

                if (gameObj != null)
                {
                    // Instead of assigning the transform directly, update the caching proxy!
                    if (gameObj.TryGetComponent<VisualBodyProxy>(out var proxy))
                    {
                        proxy.UpdatePhysicsTargetState(moveEvent.Transform.Position, moveEvent.Transform.Rotation);
                        proxy.ServerUpdateNetworkVariable(moveEvent.Transform.Position, moveEvent.Transform.Rotation);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Call this from your Player Controllers during the regular local prediction sequence
    /// </summary>
    public void RecordLocalInput(int currentLocalTick, PlayerInputPayload inputPayload)
    {
        int ringIndex = currentLocalTick % BUFFER_SIZE;

        // Accumulate or update the entry slot associated with this specific network frame
        _inputHistoryBuffer[ringIndex][inputPayload.TargetEntityIdRaw] = inputPayload;
    }

    private void OnGlobalReanticipate(double lastRoundTripTime)
    {      
        // 1. Calculate how many discrete ticks are represented by the RTT duration
        float tickRate = NetworkManager.NetworkTickSystem.TickRate;
        int rttTicksAgo = Mathf.RoundToInt((float)lastRoundTripTime * tickRate);

        // 2. Identify the absolute integer timeline boundaries
        int currentPredictedTick = (int)NetworkManager.LocalTime.Tick;
        int serverTick = currentPredictedTick - rttTicksAgo;
       
        // Ensure we don't pass negative bounds or attempt impossible rollbacks
        if (serverTick < 0 || serverTick >= currentPredictedTick) return;
        
        // 3. Fire the unmanaged Box3D re-simulation chain
        ExecuteRollbackAndStep(serverTick, currentPredictedTick);
    }

    public void ExecuteRollbackAndStep(int targetServerTick, int currentPredictedTick)
    {
        // Use standard integer math for the ring buffer index calculations
        int rollbackIndex = targetServerTick % BUFFER_SIZE;

        if (_bufferedTicks[rollbackIndex] != targetServerTick)
        {
            Debug.LogWarning($"Rollback failed: Historical frame for tick {targetServerTick} has already expired.");
            return;
        }

        byte[] rawStateBuffer = _snapshotBytePool[rollbackIndex];
        int stateBufferLength = _snapshotDataLengths[rollbackIndex];



        // 1. Restore the World state
        var replayer = ReplayPlayer.Create(rawStateBuffer, workerCount: 1);
        {
            _box3DWorld = replayer.World;
        }

        _entityToBody.Clear();

        // 2. Re-simulate forward using explicit integer tick bounds
        int framesToReSimulate = currentPredictedTick - targetServerTick;

        for (int i = 0; i < framesToReSimulate; i++)
        {
            int currentSimTick = targetServerTick + i;


            ApplyInputsForTick(currentSimTick);

            _box3DWorld.Step(TICK_DELTA, subStepCount: 4);

            foreach (BodyMoveEvent moveEvent in _box3DWorld.GetBodyMoveEvents())
            {
                if (moveEvent.UserData != null)
                {
                    ulong persistentEntityIdRaw = (ulong)moveEvent.UserData;
                    _entityToBody[persistentEntityIdRaw] = new Body { Id = moveEvent.BodyId };

                    EntityId unityId = EntityId.FromULong(persistentEntityIdRaw);
                    GameObject visualGameObj = Resources.EntityIdToObject(unityId) as GameObject;

                    if (visualGameObj != null)
                    {
                        // Pushes target updates to the proxy caching script 
                        // instead of overwriting transform components immediately!
                        if (visualGameObj.TryGetComponent<VisualBodyProxy>(out var proxy))
                        {
                            proxy.UpdatePhysicsTargetState(moveEvent.Transform.Position, moveEvent.Transform.Rotation);
                        }
                    }
                }
            }

            // Overwrite the circular index buffer directly using integer ticks
            SaveCurrentFrameSnapshot(currentSimTick);

            replayer.Destroy();
        }
    }

    private void ApplyInputsForTick(int targetTick)
    {
        int ringIndex = targetTick % BUFFER_SIZE;
        Dictionary<ulong, PlayerInputPayload> tickInputs = _inputHistoryBuffer[ringIndex];

        // Process every registered object interaction stored for this exact historic tick
        foreach (var pair in tickInputs)
        {
            ulong entityRawId = pair.Key;
            PlayerInputPayload payload = pair.Value;

            // Apply modifications strictly if the proxy pointer exists in our current timeline slice
            if (_entityToBody.TryGetValue(entityRawId, out Body activeBody))
            {
                float3 force = new float3(payload.MoveDirection.x, 0, payload.MoveDirection.y) * 15f;
                activeBody.SetLinearVelocity(new float3(force.x, activeBody.GetLinearVelocity().y, force.z));

                if (payload.JumpPressed)
                {
                    activeBody.ApplyLinearImpulse(new float3(0f, 6.0f, 0f), activeBody.Position, wake: true);
                }
            }
        }
    }

    private void SaveCurrentFrameSnapshot(int tick) 
    {
        int index = tick % BUFFER_SIZE;

        Recording snapshot = Recording.Create();
        _box3DWorld.StartRecording(snapshot);
        _box3DWorld.StopRecording();

        byte[] snapshotData = snapshot.GetData().ToArray();
        int requiredLength = snapshotData.Length;

        // Dynamic buffer guard: If the scene grows larger than our pre-allocated array, expand it
        if (requiredLength > _snapshotBytePool[index].Length)
        {
            Array.Resize(ref _snapshotBytePool[index], requiredLength * 2);
        }

        //High - performance, allocation - free block memory copy directly into our pool array
        Buffer.BlockCopy(snapshotData, 0, _snapshotBytePool[index], 0, requiredLength);
        _snapshotDataLengths[index] = requiredLength;
        _bufferedTicks[index] = tick;

       
        snapshot.Destroy(); // Clean up the temporary snapshot after copying
    }

    [Rpc(SendTo.Server)]
    public void SubmitInputDataRpc(InputPacket packet, RpcParams rpcParams = default)
    {
        // 1. Identify which client sent this packet for security auditing
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        // 2. Unpack the redundant input stream entries
        foreach (var payload in packet.RedundantInputs)
        {
            // Determine the precise frame this component targets
            // (Calculated down from the main packet's historical index)
            int payloadTick = packet.TargetTick - (packet.RedundantInputs.Length - 1);

            // SECURITY CHECK: Ensure the requesting client actually owns the NetworkObject 
            // tied to this EntityId before processing physics inputs!
            if (ValidateClientOwnership(senderClientId, payload.TargetEntityId))
            {
                // 3. Inject the client's input directly into the server's history timeline
                int ringIndex = payloadTick % BUFFER_SIZE;
                _inputHistoryBuffer[ringIndex][payload.TargetEntityIdRaw] = payload;
            }
        }
    }

    /// <summary>
    /// Unified modern RPC method routing authoritative server milestones to all clients.
    /// </summary>
    [Rpc(SendTo.NotServer)]
    private void BroadcastStateToClientsRpc(StateSnapshotPacket packet)
    {
        // Server handles bypass this packet since they already calculated it natively
        if (IsServer) return;

        // 1. Calculate the target index inside our pre-allocated snapshot arrays
        int ringIndex = packet.StateTick % BUFFER_SIZE;
        int incomingLength = packet.CompressedWorldData.Length;

        // Dynamic buffer guard: Expand our local pool element if the server state outgrows it
        if (incomingLength > _snapshotBytePool[ringIndex].Length)
        {
            System.Array.Resize(ref _snapshotBytePool[ringIndex], incomingLength * 2);
        }

        // 2. High-performance, zero-allocation memory block transfer straight into our array cache
        System.Buffer.BlockCopy(packet.CompressedWorldData, 0, _snapshotBytePool[ringIndex], 0, incomingLength);

        // Update tracking parameters so ExecuteRollbackAndStep knows this frame slice is authoritative
        _snapshotDataLengths[ringIndex] = incomingLength;
        _bufferedTicks[ringIndex] = packet.StateTick;

        // 3. Unity Netcode for GameObjects' Client Anticipation layer (like AnticipatedNetworkTransform)
        // will now evaluate for state deviation. If a misprediction occurred, OnReanticipate will trigger,
        // and our ExecuteRollbackAndStep routine will automatically feed from this exact array slice.

    }

    private bool ValidateClientOwnership(ulong senderId, EntityId targetId)
    {
        GameObject obj = Resources.EntityIdToObject(targetId) as GameObject;
        if (obj == null) return false;

        if (obj.TryGetComponent<NetworkObject>(out var netObj))
        {
            return netObj.OwnerClientId == senderId;
        }
        return false;
    }

    public void UnregisterSpawnedBodyOnly(ulong entityRawBits)
    {
        if (_entityToBody.TryGetValue(entityRawBits, out Body body))
        {
            _entityToBody.Remove(entityRawBits);
        }
    }
    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.NetworkTickSystem.Tick -= OnServerNetworkTick;
        }
        else
        {
            NetworkManager.OnReanticipate -= OnGlobalReanticipate;
        }
        base.OnNetworkDespawn();
    }
}
