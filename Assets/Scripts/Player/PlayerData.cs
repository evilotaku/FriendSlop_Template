using System;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;

/// <summary>
/// Manages and synchronizes the ready state for each player in the lobby.
/// </summary>
public class PlayerData : NetworkBehaviour
{
    public static event Action OnPlayerStateChanged;

    public NetworkVariable<FixedString64Bytes> PlayerName = new NetworkVariable<FixedString64Bytes>(writePerm: NetworkVariableWritePermission.Owner);

    // Synced so other clients can find the Vivox participant for this player.
    public NetworkVariable<FixedString64Bytes> PlayerId = new NetworkVariable<FixedString64Bytes>(
        "",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    public NetworkVariable<bool> IsReady = new NetworkVariable<bool>(
        false, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            PlayerId.Value = AuthenticationService.Instance.PlayerId;
            PlayerName.Value = AuthenticationService.Instance.PlayerName;
        }
        IsReady.OnValueChanged += (oldValue, newValue) => OnPlayerStateChanged?.Invoke();
        OnPlayerStateChanged?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        OnPlayerStateChanged?.Invoke();
    }

    [Rpc(SendTo.Server)]
    public void SetReadyRpc(bool ready)
    {
        IsReady.Value = ready;
    }
}
