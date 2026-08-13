using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles player input for picking up, dropping, and rotating held items.
/// </summary>
public class PlayerInteraction : NetworkBehaviour
{
    [SerializeField]
    private float _rotateSensitivity = 0.15f;

    private PickupItem _heldItem;
    private Quaternion _heldRotation = Quaternion.identity;

    // Per-player input actions — MPPM-safe.
    private InputAction _interactAction;
    private InputAction _rotateAction;

    public bool IsRotatingItem { get; private set; }
    Ray InteractRay  = new Ray();
    public float InteractRange = 15f;

    private void Awake()
    {
        // Right-click item rotation is not in the shared asset — bind inline.
        _rotateAction = new InputAction("RotateItem", InputActionType.Button);
        _rotateAction.AddBinding("<Mouse>/rightButton");
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        var playerInput = GetComponent<PlayerInput>();
        playerInput.currentActionMap.Enable();
        _interactAction = playerInput.actions["Interact"];
        _rotateAction.Enable();
        _interactAction.Enable();

        _interactAction.performed += HandleInteract;

        _rotateAction.performed += HandleItemRotation;
        _rotateAction.canceled +=(ctx) => IsRotatingItem = false;
    }

    private void HandleInteract(InputAction.CallbackContext ctx)
    {
        if (_heldItem != null)
            DropItem();
        else
            TryPickUp();
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            _rotateAction?.Disable();
        }
        _rotateAction?.Dispose();
    }

    private void TryPickUp()
    {
        InteractRay.origin = Camera.main.transform.position;
        InteractRay.direction = Camera.main.transform.forward;
        Debug.DrawRay(InteractRay.origin, InteractRay.direction * InteractRange, Color.green, 1f);
        if (!Physics.Raycast(InteractRay, out var hit, InteractRange)) return;

        var item = hit.collider.GetComponentInParent<PickupItem>();
        if (item == null) return;

        item.PickUpRpc();
        _heldItem = item;
        _heldRotation = Quaternion.identity;
    }

    private void DropItem()
    {
        _heldItem.DropRpc();
        _heldItem = null;
        _heldRotation = Quaternion.identity;
    }

    private void HandleItemRotation(InputAction.CallbackContext ctx)
    {
        if(_heldItem == null) return; 

        // TODO: wire Look delta through if needed.
        var mouseAction = GetComponent<PlayerInput>()?.actions["Look"];
        if (mouseAction == null) return;

        var delta = mouseAction.ReadValue<Vector2>() * _rotateSensitivity;

        _heldRotation = Quaternion.AngleAxis(delta.x, Vector3.up)
                      * Quaternion.AngleAxis(-delta.y, Vector3.right)
                      * _heldRotation;

        _heldItem.SetHeldRotationRpc(_heldRotation);
    }
}
