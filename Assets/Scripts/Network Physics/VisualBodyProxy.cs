using Box3D;
using Box3D.Hybrid;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;

public class VisualBodyProxy : NetworkBehaviour
{
    private Box3DBody _box3DComponent;

    /// <summary>
    /// Exposes a copy of the stack-allocated handle struct tracking this entity in the active unmanaged World.
    /// </summary>
    public Body MyNativeBody => _box3DComponent != null ? _box3DComponent.Body : default;

    private readonly NetworkVariable<PhysicsStatePayload> _networkedState = new NetworkVariable<PhysicsStatePayload>(
        new PhysicsStatePayload(),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // --- VISUAL SMOOTHING CACHES ---
    private Vector3 _previousRenderPosition;
    private Quaternion _previousRenderRotation;

    private Vector3 _targetPhysicsPosition;
    private Quaternion _targetPhysicsRotation;

    private void Awake()
    {
        _box3DComponent = GetComponent<Box3DBody>();

        // Establish baseline rendering anchors matching starting scene coordinates
        ResetVisualSmoothingHistory(transform.position, transform.rotation);
    }

    // --- NETWORK LIFECYCLE HOOKS ---
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // 1. Fetch Unity 6's persistent entity identifier for this instance
        ulong entityRawBits = EntityId.ToULong(GetEntityId());

        // 2. We skip body/shape creation entirely!
        // Instead, grab the native body handle already configured by the Box3DBody component.
        Body existingBody = _box3DComponent.Body;

        // 3. Inject the persistent EntityId bits straight into the native unmanaged UserData slot
        existingBody.UserData = (System.IntPtr)entityRawBits;

        _box3DComponent.enabled = false; // Disable Box3DBody's own update loop to avoid double-tracking

        if (!IsServer)
        {
            // Extract the current authoritative positions replicated to this client on connection
            PhysicsStatePayload synchronizedInitialState = _networkedState.Value;

            // Direct modification of unmanaged C++ world coordinates to overwrite default scene bounds
            existingBody.SetTransform(synchronizedInitialState.Position, synchronizedInitialState.Rotation);

            // Snaps visual anchors to prevent a visual interpolation glitch from the original scene spawn coordinate
            ResetVisualSmoothingHistory(synchronizedInitialState.Position, synchronizedInitialState.Rotation);
        }

        // 4. Initialize standard and visual manager execution lists
        NetworkPhysicsManager.Instance.RegisterNewSpawnedBody(entityRawBits, existingBody);
        NetworkPhysicsManager.Instance.TrackVisualProxy(this);
               
    }

    public void ServerUpdateNetworkVariable(float3 targetPos, quaternion targetRot)
    {
        if (!IsServer) return;

        _networkedState.Value = new PhysicsStatePayload
        {
            Position = targetPos,
            Rotation = targetRot
        };
    }

    public override void OnNetworkDespawn()
    {
        // Clean registration bounds safely.
        // We do NOT destroy the native body here because Box3DBody's internal OnDestroy will clean it up.
        if (NetworkPhysicsManager.Instance != null)
        {
            ulong entityRawBits = EntityId.ToULong(GetEntityId());

            NetworkPhysicsManager.Instance.UntrackVisualProxy(this);
            NetworkPhysicsManager.Instance.UnregisterSpawnedBodyOnly(entityRawBits);
        }

        base.OnNetworkDespawn();
    }

    // --- PHYSICS TARGET MUTATIONS ---

    /// <summary>
    /// Invoked automatically by the NetworkPhysicsManager tracking loops whenever a step or rollback settles a fresh coordinate state.
    /// </summary>
    public void UpdatePhysicsTargetState(float3 targetPos, quaternion targetRot)
    {
        // Pivot structural checkpoints: Previous target position shifts back to form our previous visual anchor
        _previousRenderPosition = _targetPhysicsPosition;
        _previousRenderRotation = _targetPhysicsRotation;

        // Update the forward physics target boundary
        _targetPhysicsPosition = targetPos;
        _targetPhysicsRotation = targetRot;
    }

    /// <summary>
    /// Forces an immediate alignment override of visual caches. Use this when spawning or instantly teleporting players.
    /// </summary>
    public void ResetVisualSmoothingHistory(Vector3 targetPos, Quaternion targetRot)
    {
        _previousRenderPosition = _targetPhysicsPosition = targetPos;
        _previousRenderRotation = _targetPhysicsRotation = targetRot;

        transform.position = targetPos;
        transform.rotation = targetRot;
    }

    // --- VISUAL INTERPOLATION RENDER EXECUTION ---

    /// <summary>
    /// Driven continuously during LateUpdate by the manager's alpha accumulator loop to isolate rigid 60Hz steps from 144Hz+ monitors.
    /// </summary>
    public void InterpolateVisuals(float alphaTimeRemainder)
    {
        // Blend visuals completely inside Unity's rendering pipeline. 
        // This leaves the Box3D simulation threads safely undisturbed.
        transform.position = Vector3.Lerp(_previousRenderPosition, _targetPhysicsPosition, alphaTimeRemainder);
        transform.rotation = Quaternion.Slerp(_previousRenderRotation, _targetPhysicsRotation, alphaTimeRemainder);
    }
}
