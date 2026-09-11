using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour
{
    PlayerInput input;
    float2 moveInput;
    bool wantsToJump;

    private Queue<PlayerInputPayload> _inputHistory = new Queue<PlayerInputPayload>();
    private const int REDUNDANCY_DEPTH = 3;

    override public void OnNetworkSpawn()
    {
        if (!IsOwner) return;
        input = GetComponent<PlayerInput>();
        NetworkManager.NetworkTickSystem.Tick += OnLocalNetworkTick;
    }
    private void Update()
    {
        if (!IsOwner) return;

        // Gather structural movement variables
        var rawMoveInput = input.actions["Move"].ReadValue<Vector2>();
        if(math.lengthsq(rawMoveInput) > 1f)
        {
            rawMoveInput = math.normalize(rawMoveInput);
        }
        moveInput = rawMoveInput;
        if(input.actions["Jump"].triggered)
        {
            wantsToJump = true;
        }
    }

    private void OnLocalNetworkTick()
    {
        // 2. Identify the active discrete tracking coordinate frame
        int localTick = (int)NetworkManager.LocalTime.Tick;
        ulong rawEntityId = EntityId.ToULong(GetEntityId());

        // Assemble the pure data network payload structure for this specific step slice
        PlayerInputPayload currentPayload = new PlayerInputPayload
        {
            TargetEntityIdRaw = rawEntityId,
            MoveDirection = moveInput,
            JumpPressed = wantsToJump
        };

        // Reset volatile latch fields immediately following consumption
        wantsToJump = false;

        // 3. Stash locally inside our manager's history buffer so immediate local prediction loops have access to it
        NetworkPhysicsManager.Instance.RecordLocalInput(localTick, currentPayload);

        // 4. Update the redundant sliding history window cache
        _inputHistory.Enqueue(currentPayload);
        if (_inputHistory.Count > REDUNDANCY_DEPTH)
        {
            _inputHistory.Dequeue();
        }

        // 5. Replicate data up to the Host using our modern universal RPC protocol
        InputPacket packet = new InputPacket
        {
            TargetTick = localTick,
            RedundantInputs = _inputHistory.ToArray()
        };

        NetworkPhysicsManager.Instance.SubmitInputDataRpc(packet);
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;
        NetworkManager.NetworkTickSystem.Tick -= OnLocalNetworkTick;
        base.OnNetworkDespawn();
    }
}
