using Box3D;
using NUnit.Framework;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class VisualBodyProxy : NetworkBehaviour
{
    [SerializeField]
    List<ShapeConfig> _shapes = new() { ShapeConfig.Default };

    private Body _myNativeBody;
    public Body MyNativeBody => _myNativeBody;

    public void SetNativeBody(Body body) => _myNativeBody = body;

    // --- REPLICATED STATE VARIABLE FOR LATE JOINS ---
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

    // Track a history of our target simulation coordinates indexed by tick ring placement
    private Vector3[] _historicalPositions = new Vector3[128];
    private Quaternion[] _historicalRotations = new Quaternion[128];

    public Vector3 GetHistoricalTargetPosition(int ringIndex) => _historicalPositions[ringIndex];
    public Quaternion GetHistoricalTargetRotation(int ringIndex) => _historicalRotations[ringIndex];
    public PhysicsStatePayload CurrentServerAuthoritativeState => _networkedState.Value;

    private void Awake()
    {
        ResetVisualSmoothingHistory(transform.position, transform.rotation);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
                

        // 1. Fetch Unity 6's persistent entity identifier for this instance
        ulong entityRawBits = EntityId.ToULong(NetworkObject.GetEntityId());

        // 2. Define structural parameters for manual instantiation
        BodyDef bodyDef = BodyDef.Default;
        bodyDef.Type = BodyType.Dynamic;
        bodyDef.Position = transform.position;
        bodyDef.Rotation = transform.rotation;

        // Stamp the persistent EntityId bits straight to the native unmanaged UserData slot
        bodyDef.UserData = (System.IntPtr)entityRawBits;

        // 3. Create the body manually within the manager's active unmanaged World context
        _myNativeBody = NetworkPhysicsManager.Instance.ActiveWorld.CreateBody(bodyDef);

        // 4.  --- MANUAL SHAPE CONSTRUCT EXTRACTIONpass ---
        // Iterate through all custom shapes configured in the inspector and construct the pure native shapes
        for (int i = 0; i < _shapes.Count; i++)
        {
            CreateNativeShapeFromConfig(_myNativeBody, _shapes[i]);
        }

        // 5. Late Join State Correction
        if (!IsServer)
        {
            PhysicsStatePayload synchronizedInitialState = _networkedState.Value;
            _myNativeBody.SetTransform(synchronizedInitialState.Position, synchronizedInitialState.Rotation);
            ResetVisualSmoothingHistory(synchronizedInitialState.Position, synchronizedInitialState.Rotation);
            _networkedState.OnValueChanged += (previous, current) => NetworkPhysicsManager.Instance.VerifyClientSimulationState(this, current);
        }

        // 6. Complete manager indexing handshakes
        NetworkPhysicsManager.Instance.RegisterNewSpawnedBody(NetworkObject.GetEntityId(), _myNativeBody);
        NetworkPhysicsManager.Instance.TrackVisualProxy(this);
    }

    private void CreateNativeShapeFromConfig(Body targetBody, ShapeConfig config)
    {
        // Construct shapes manually without relying on external Unity Component workflows
        switch (config.ShapeType)
        {
            
            case ShapeType.Box:
                BoxHull boxDef = BoxHull.Create(config.BoxHalfExtents.x, config.BoxHalfExtents.y, config.BoxHalfExtents.z);
                var shape = targetBody.CreateHullShape(ShapeDef.Default, in boxDef);
                shape.SetSurfaceMaterial(new() { Friction = config.Friction, Restitution = config.Restitution });
                shape.SetDensity(config.Density, true);
                break;

            case ShapeType.Sphere:
                Sphere sphereDef = new Sphere
                {
                    Radius = config.SphereRadius
                };
                shape = targetBody.CreateSphereShape(ShapeDef.Default, in sphereDef);
                shape.SetSurfaceMaterial(new() { Friction = config.Friction, Restitution = config.Restitution });
                shape.SetDensity(config.Density, true);
                break;

            case ShapeType.Capsule:
                Capsule capsuleDef = new Capsule
                {
                    Radius = config.CapsuleRadius
                };
                shape = targetBody.CreateCapsuleShape(ShapeDef.Default, in capsuleDef);
                shape.SetSurfaceMaterial(new() { Friction = config.Friction, Restitution = config.Restitution });
                shape.SetDensity(config.Density, true);
                break;
            default:
                Debug.LogError($"Unsupported shape type: {config.ShapeType}");
                break;
        }

        
    }

    public override void OnNetworkDespawn()
    {
        // 7. Explicitly destroy the body manually to prevent unmanaged C++ memory leaks
        if (NetworkPhysicsManager.Instance != null)
        {
            EntityId entityId = NetworkObject.GetEntityId();

            NetworkPhysicsManager.Instance.UntrackVisualProxy(this);
            NetworkPhysicsManager.Instance.UnregisterAndDestroyBody(entityId, _myNativeBody);
        }

        base.OnNetworkDespawn();
    }

    public void UpdatePhysicsTargetState(int tick, float3 targetPos, quaternion targetRot)
    {
        _previousRenderPosition = _targetPhysicsPosition;
        _previousRenderRotation = _targetPhysicsRotation;

        _targetPhysicsPosition = targetPos;
        _targetPhysicsRotation = targetRot;

        int ringIndex = tick % 128;
        _historicalPositions[ringIndex] = targetPos;
        _historicalRotations[ringIndex] = targetRot;
    }

    public void ServerUpdateNetworkVariable(int tick, float3 targetPos, quaternion targetRot)
    {
        if (!IsServer) return;

        _networkedState.Value = new PhysicsStatePayload
        {
            StateTick = tick,
            Position = targetPos,
            Rotation = targetRot
        };
    }

    public void ResetVisualSmoothingHistory(Vector3 targetPos, Quaternion targetRot)
    {
        _previousRenderPosition = _targetPhysicsPosition = targetPos;
        _previousRenderRotation = _targetPhysicsRotation = targetRot;

        transform.position = targetPos;
        transform.rotation = targetRot;
    }

    public void InterpolateVisuals(float alphaTimeRemainder)
    {
        transform.position = Vector3.Lerp(_previousRenderPosition, _targetPhysicsPosition, alphaTimeRemainder);
        transform.rotation = Quaternion.Slerp(_previousRenderRotation, _targetPhysicsRotation, alphaTimeRemainder);
    }
}
