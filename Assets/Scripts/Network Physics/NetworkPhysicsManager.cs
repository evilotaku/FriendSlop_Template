using Box3D;
using Box3D.Hybrid;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class NetworkPhysicsManager : NetworkBehaviour
{
    public static NetworkPhysicsManager Instance { get; private set; }

    public event Action<int> OnBeforeSimulationTick;

    private World _box3DWorld;

    public World ActiveWorld => _box3DWorld;

    public const float TICK_RATE = 60f;
    private const float TICK_DELTA = 1f / 60f;
    private const int BUFFER_SIZE = 128;

    // Configurable precision boundary (Adjust depending on acceptable network tolerance)
    private const float DESYNC_THRESHOLD_POS = 0.001f; // ~3.1cm positional tolerance

    // Cosine of the maximum allowed angular divergence angle.
    // For a 0.5-degree angular limit: Cos(0.5° / 2) ≈ 0.99999
    private const float DESYNC_THRESHOLD_ROT = 0.99999f;

    [SerializeField]
    public float RotationSpeed = 15f;

    // --- PRE-ALLOCATED ZERO-ALLOCATION SNAPSHOT STRATEGY ---
    // A fixed pool of managed byte arrays matching your maximum expected snapshot size
    private byte[][] _snapshotBytePool = new byte[BUFFER_SIZE][];
    private int[] _snapshotDataLengths = new int[BUFFER_SIZE];

    // Tracks which ring slots actively hold valid structural ticks
    private int[] _bufferedTicks = new int[BUFFER_SIZE];

    // Tracks the last server frame validation checkpoint we processed to prevent duplicate executions
    private int _lastProcessedServerTick = -1;


    // Direct mapping for applying simulation inputs
   
    private Dictionary<EntityId, Body> _entityToBody = new Dictionary<EntityId, Body>();

    // --- INPUT RING BUFFER SETUP ---
    // Maps each integer tick to a nested sub-dictionary tracking individual entity payloads    
    private Dictionary<EntityId, PlayerInputPayload>[] _inputHistoryBuffer = new Dictionary<EntityId, PlayerInputPayload>[BUFFER_SIZE];

    private float _renderTimeAccumulator = 0f;
    private List<VisualBodyProxy> _activeVisualProxies = new List<VisualBodyProxy>();

    // Keep tracking registrations clean
    public void TrackVisualProxy(VisualBodyProxy proxy) => _activeVisualProxies.Add(proxy);
    public void UntrackVisualProxy(VisualBodyProxy proxy) => _activeVisualProxies.Remove(proxy);
    public void RegisterNewSpawnedBody(EntityId entityId, Body body) => _entityToBody[entityId] = body;
    public void UnregisterAndDestroyBody(EntityId entityId, Body body)
    {
        _entityToBody.Remove(entityId);
        if (body.IsValid) body.Destroy();
    }



    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _box3DWorld = World.Create(WorldDef.Default);
        FindAnyObjectByType<UnityTerrainToBox3D>().BuildBox3DTerrain(_box3DWorld);
        // Pre-instantiate internal slot dictionary components to prevent real-time GC overhead
        for (int i = 0; i < BUFFER_SIZE; i++)
        {
            _inputHistoryBuffer[i] = new Dictionary<EntityId, PlayerInputPayload>();

            _snapshotBytePool[i] = new byte[128 * 1024];
            _bufferedTicks[i] = -1; // -1 represents an empty historical slot
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        NetworkManager.NetworkTickSystem.Tick += OnNetworkTickExecution;
    }
        
    private void OnNetworkTickExecution()
    {
        int tick = IsServer ? (int)NetworkManager.ServerTime.Tick : (int)NetworkManager.LocalTime.Tick;
        OnBeforeSimulationTick?.Invoke(tick);

        // Host targets skip manual client validation passes
        if (IsServer)
        {
            OnServerNetworkTick();
            return;
        }

        // Client Only: Advance the local timeline simulation forward normally.
        // If a desync event arrived earlier via the network event packet, 
        // the history ring buffer was already fixed before this tick runs!
        int currentPredictedTick = NetworkManager.LocalTime.Tick;

        ApplyInputsForTick(currentPredictedTick);
        _box3DWorld.Step(TICK_DELTA, subStepCount: 4);
        SynchronizeVisualTransforms(currentPredictedTick);
        SaveCurrentFrameSnapshot(currentPredictedTick);
    }

    public void VerifyClientSimulationState(VisualBodyProxy proxy, PhysicsStatePayload serverSnap)
    {
        // Guard Clause: Prevent reprocessing a historical milestone tick we have already reconciled
        if (serverSnap.StateTick <= _lastProcessedServerTick) return;

        int currentPredictedTick = NetworkManager.LocalTime.Tick;
        int historicalIndex = serverSnap.StateTick % BUFFER_SIZE;

        // Ensure our local buffer actually contains matching data records for that historic tick
        if (_bufferedTicks[historicalIndex] == serverSnap.StateTick)
        {
            Vector3 localPredictedPos = proxy.GetHistoricalTargetPosition(historicalIndex);
            float sqDist = Vector3.SqrMagnitude(localPredictedPos - serverSnap.Position);

            Quaternion localPredictedRot = proxy.GetHistoricalTargetRotation(historicalIndex); 
            float absoluteRotDot = Mathf.Abs(Quaternion.Dot(localPredictedRot, serverSnap.Rotation));

            if (sqDist > DESYNC_THRESHOLD_POS || absoluteRotDot < DESYNC_THRESHOLD_ROT)
            {
                Debug.LogWarning($"[Physics Correction] Misprediction caught at server frame {serverSnap.StateTick}. Distance: {Mathf.Sqrt(sqDist)}m. Restoring timeline.");

                // Execute rollback from the mispredicted server tick back up to our active client tick
                ExecuteRollbackAndStep(serverSnap.StateTick, currentPredictedTick, proxy, serverSnap);

                // Mark this tick milestone as successfully validated
                _lastProcessedServerTick = serverSnap.StateTick;
            }
        }
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
        int currentServerTick = NetworkManager.ServerTime.Tick;

        // 1. Process client inputs and execute physics steps forward
        ApplyInputsForTick(currentServerTick);
        if (!_box3DWorld.IsValid)
        {
            print("Invalid Box3D World");
            return;
        }
        _box3DWorld.Step(TICK_DELTA, subStepCount: 4);

        // 2. Sync all bodies to their visual proxies and network variables
        SynchronizeVisualTransforms(currentServerTick);
        SaveCurrentFrameSnapshot(currentServerTick);
    }

    private void SynchronizeVisualTransforms(int currentSimTick)
    {
        // Clear out the spent time remainder window since we just processed a real step boundary
        _renderTimeAccumulator = 0f;

        foreach (var proxy in _activeVisualProxies)
        {
            if (proxy != null && proxy.MyNativeBody.IsValid)
            {
                float3 pos = proxy.MyNativeBody.Position;
                quaternion rot = proxy.MyNativeBody.Rotation;

                proxy.UpdatePhysicsTargetState(currentSimTick, pos, rot);
                
                if (IsServer)
                {
                    proxy.ServerUpdateNetworkVariable(currentSimTick, pos, rot);
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
        _inputHistoryBuffer[ringIndex][inputPayload.TargetEntityId] = inputPayload;
    }
      

    public void ExecuteRollbackAndStep(int rollbackServerTick, int currentPredictedTick, VisualBodyProxy misPredictedProxy, PhysicsStatePayload serverState)
    {
        print("Rollback initiated from server tick " + rollbackServerTick + " to predicted tick " + currentPredictedTick);
        int rollbackIndex = rollbackServerTick % BUFFER_SIZE;

        if (_bufferedTicks[rollbackIndex] != rollbackServerTick)
        {
            Debug.LogWarning($"Rollback failed: Historical frame for tick {rollbackServerTick} has already expired.");
            return;
        }

        byte[] rawStateBuffer = _snapshotBytePool[rollbackIndex];

        // 1. Restore the World state from snapshot
        var replayer = ReplayPlayer.Create(rawStateBuffer, workerCount: 1);        
        _box3DWorld = replayer.World;
        
        // 2. Important: Update all handles in our dictionaries and proxies to point to the new World's bodies
        foreach (var proxy in _activeVisualProxies)
        {
            if (proxy != null)
            {
                // Find the matching body in the new world using the stable ID
                if (_box3DWorld.TryGetBody(proxy.MyNativeBody.Id, out Body newBody))
                {
                    proxy.SetNativeBody(newBody);
                    _entityToBody[proxy.NetworkObject.GetEntityId()] = newBody;
                }
            }
        }

        // 3. Apply the authoritative server state correction for the specific mispredicted body
        EntityId misPredictedId = misPredictedProxy.NetworkObject.GetEntityId();
        if (_entityToBody.TryGetValue(misPredictedId, out Body correctedBody))
        {
            correctedBody.SetTransform(serverState.Position, serverState.Rotation);
        }
      
        // 4. Re-simulate forward using explicit integer tick bounds
        int framesToReSimulate = currentPredictedTick - rollbackServerTick;

        for (int i = 0; i < framesToReSimulate; i++)
        {
            int currentSimTick = rollbackServerTick + i;

            ApplyInputsForTick(currentSimTick);
            _box3DWorld.Step(TICK_DELTA, subStepCount: 4);
            SynchronizeVisualTransforms(currentSimTick);
            SaveCurrentFrameSnapshot(currentSimTick);
        }

        replayer.Destroy();
    }

    private void ApplyInputsForTick(int rollbackTick)
    {
        int ringIndex = rollbackTick % BUFFER_SIZE;
        Dictionary<EntityId, PlayerInputPayload> tickInputs = _inputHistoryBuffer[ringIndex];
        // Process every registered object interaction stored for this exact historic tick
        foreach (var pair in tickInputs)
        {
            EntityId entityId = pair.Key;
            PlayerInputPayload payload = pair.Value;
            // Apply modifications strictly if the proxy pointer exists in our current timeline slice
            if (_entityToBody.TryGetValue(entityId, out Body activeBody))
            {
                float3 force = new float3(payload.MoveDirection.x, 0, payload.MoveDirection.y) * 15f;
                activeBody.SetLinearVelocity(new float3(force.x, activeBody.GetLinearVelocity().y, force.z));

                if (payload.JumpPressed)
                {
                    activeBody.ApplyLinearImpulse(new float3(0f, 6.0f, 0f), activeBody.Position, wake: true);
                }
                
                quaternion nextRotation = math.slerp(activeBody.Rotation, payload.LookRotation, RotationSpeed * TICK_DELTA);
                activeBody.SetTransform(activeBody.Position, nextRotation);
                activeBody.SetAngularVelocity(float3.zero);

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
        for (int i = 0; i < packet.RedundantInputs.Length; i++)
        {
            var payload = packet.RedundantInputs[i];
            
            // Determine the precise frame this component targets
            // (Calculated down from the main packet's historical index)
            int payloadTick = packet.TargetTick - (packet.RedundantInputs.Length - 1 - i);

            // SECURITY CHECK: Ensure the requesting client actually owns the NetworkObject 
            // tied to this EntityId before processing physics inputs!
            if (ValidateClientOwnership(senderClientId, payload.TargetEntityId))
            {                
                // 3. Inject the client's input directly into the server's history timeline
                int ringIndex = payloadTick % BUFFER_SIZE;
                _inputHistoryBuffer[ringIndex][payload.TargetEntityId] = payload;
            }
        }
    }

    private bool ValidateClientOwnership(ulong senderId, EntityId targetId)
    {
        NetworkObject obj = Resources.EntityIdToObject(targetId) as NetworkObject;
        if (obj == null)
        {
            print("Network object not found for EntityId: " + targetId);
            return false;
        }

        if (obj.TryGetComponent<NetworkObject>(out var netObj))
        {
            return netObj.OwnerClientId == senderId;
        }
        return false;
    }
    
    public override void OnNetworkDespawn()
    {
        if (NetworkManager != null && NetworkManager.NetworkTickSystem != null)
        {
            NetworkManager.NetworkTickSystem.Tick -= OnNetworkTickExecution;
        }
        _box3DWorld.Destroy();
        base.OnNetworkDespawn();
    }
}
