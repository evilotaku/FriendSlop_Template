using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Provide some victory condition and update all players
/// </summary>
public class VictoryZone : NetworkBehaviour
{
    public NetworkVariable<bool> IsComplete = new NetworkVariable<bool>(
        false, //init as false
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public UnityEvent OnItemDelivered;
    public GameObject[] gameObjects;


    private void DisableGravity()
    {
        for (int i = 0; i < gameObjects.Length; i++)
        {
            if (gameObjects[i] == null) continue;

            var rb = gameObjects[i].GetComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.AddForce(Vector3.up * 1f, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 0.5f, ForceMode.Impulse);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!IsServer || IsComplete.Value) return;

        var item = other.GetComponent<AttachableBehaviour>();
        if (item == null || item.transform.parent == null) return; // AttachableBehaviour will be child of the player when held, so we check if it has a parent

        IsComplete.Value = true;
    }

    public override void OnNetworkSpawn()
    {
        IsComplete.OnValueChanged += (_, won) =>
        {
            if (won)
            {
                OnItemDelivered?.Invoke();
                DisableGravity();
            }
        };
    }

}
