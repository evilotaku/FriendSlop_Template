using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Networked item that can be picked up, held, and dropped by players.
/// </summary>
public class PickupItem : NetworkBehaviour
{
    [SerializeField]
    private AttachableBehaviour _attachable;
    private AttachableNode _heldByNode;

    private Rigidbody _rb;
    private Collider _col;
    private Quaternion _heldRotation = Quaternion.identity;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _col = GetComponentInChildren<Collider>();
        if (_attachable == null)
        {
            _attachable = GetComponentInChildren<AttachableBehaviour>();
        }
    }

    public override void OnNetworkSpawn()
    {
       _attachable.AttachStateChange += OnStateChange;
    }

    public override void OnNetworkDespawn()
    {
        _attachable.AttachStateChange -= OnStateChange;
    }

    private void OnStateChange(AttachableBehaviour.AttachState state, AttachableNode node)
    {
        switch (state)
        {
            case AttachableBehaviour.AttachState.Attached:
                _col.enabled = false;
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.isKinematic = true;
                _rb.interpolation = RigidbodyInterpolation.None;
                _heldByNode = node;
                break;
            case AttachableBehaviour.AttachState.Detached:               
                _rb.isKinematic = false;
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
                _heldByNode = null;
                break;
        }
    }

    [Rpc(SendTo.Server)]
    public void SetHeldRotationRpc(Quaternion rotation)
    {
        _heldRotation = rotation;
        if (_attachable != null && _attachable.transform.parent != null)
        {
            _attachable.transform.localRotation = _heldRotation;
        }
    }

    [Rpc(SendTo.Server)]
    public void PickUpRpc(RpcParams rpcParams = default)
    {
        var node = NetworkManager.Singleton.ConnectedClients[rpcParams.Receive.SenderClientId].PlayerObject.GetComponentInChildren<AttachableNode>();
        if (node == null || _attachable == null) return;

        _attachable.Attach(node);
        _heldRotation = Quaternion.identity;
        _attachable.transform.localRotation = _heldRotation;
        
    }

    [Rpc(SendTo.Server)]
    public void DropRpc(RpcParams rpcParams = default)
    {
        if (_heldByNode == null) return;

        Vector3 dropPos = transform.position;
        Vector3 throwDir = Vector3.forward;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out var client)
            && client.PlayerObject != null)
        {
            throwDir = client.PlayerObject.transform.forward;
        }

        if (_attachable != null)
        {
            dropPos = _attachable.transform.position;
            _attachable.Detach();
        }

        // Snap root to where the item was visually held
        transform.position = dropPos;

        _heldByNode = null;
        _rb.AddForce(throwDir * 1f, ForceMode.Impulse);
    }
}
