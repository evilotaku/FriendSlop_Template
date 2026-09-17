using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour
{
    PlayerInput input;
    float2 moveInput;
    quaternion lookInput;
    bool wantsToJump;

    private Queue<PlayerInputPayload> _inputHistory = new Queue<PlayerInputPayload>();
    private const int REDUNDANCY_DEPTH = 3;

    override public void OnNetworkSpawn()
    {
        if (!IsOwner) return;
        input = GetComponent<PlayerInput>();
        NetworkPhysicsManager.Instance.OnBeforeSimulationTick += OnLocalNetworkTick;
    }
    private void Update()
    {
        if (!IsOwner) return;

        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        // Gather structural movement variables
        var rawMoveInput = input.actions["Move"].ReadValue<Vector2>();

        // Calculate camera-relative movement
        Transform cameraTransform = mainCam.transform;
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 desiredMoveDirection = forward * rawMoveInput.y + right * rawMoveInput.x;

        if (math.lengthsq(desiredMoveDirection) > 1f)
        {
            desiredMoveDirection = math.normalize(desiredMoveDirection);
        }
        moveInput = new float2(desiredMoveDirection.x, desiredMoveDirection.z);

        if(input.actions["Jump"].triggered)
        {
            wantsToJump = true;
        }

        Ray ray = mainCam.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane groundPlane = new Plane(Vector3.up, transform.position); // Create a plane sitting at character height

        lookInput = transform.rotation; // Default fallback to current orientation

        if (groundPlane.Raycast(ray, out float rayDistance))
        {
            Vector3 worldMousePoint = ray.GetPoint(rayDistance);
            Vector3 lookDirection = (worldMousePoint - transform.position);
            lookDirection.y = 0f; // Lock calculations strictly to the horizontal Yaw plane

            if (lookDirection.sqrMagnitude > 0.001f)
            {
                // Create a clean LookRotation pointing straight along the horizontal surface
                lookInput = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            }
        }
    }

    private void OnLocalNetworkTick(int localTick)
    {
        // 2. Identify the active discrete tracking coordinate frame
        ulong rawEntityId = EntityId.ToULong(NetworkObject.GetEntityId());

        // Assemble the pure data network payload structure for this specific step slice
        PlayerInputPayload currentPayload = new PlayerInputPayload
        {
            TargetEntityIdRaw = rawEntityId,
            MoveDirection = moveInput,
            LookRotation = lookInput,
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
        if (NetworkPhysicsManager.Instance != null)
            NetworkPhysicsManager.Instance.OnBeforeSimulationTick -= OnLocalNetworkTick;
        base.OnNetworkDespawn();
    }
}
