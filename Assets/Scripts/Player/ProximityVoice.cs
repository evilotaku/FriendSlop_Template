using System;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using Unity.Services.Vivox;
using UnityEngine;

/// <summary>
/// Manages Vivox 3D positional voice chat per player instance.
/// </summary>
public class ProximityVoice : NetworkBehaviour
{
    // Silence beyond this distance (metres). Channel3DProperties requires int.
    [SerializeField] private int _audibleDistance = 32;

    // Full volume within this distance.
    [SerializeField] private int _conversationalDistance = 8;

    PlayerData _playerData;

    private bool _inChannel;
    private string _channelName;
    LoginOptions _loginOptions = new();

    public string ChannelName => _channelName;
    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        _playerData = GetComponent<PlayerData>();

        _loginOptions = new()
        {
            PlayerId = AuthenticationService.Instance.PlayerId,
            DisplayName = AuthenticationService.Instance.PlayerName,
            EnableTTS = true
        };

        LoginToVivox();

    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        if (_inChannel)
            LeaveChannelAsync();
    }

    private async void LoginToVivox()
    {
        if (VivoxService.Instance.IsLoggedIn) await VivoxService.Instance.LogoutAsync();
        await VivoxService.Instance.LoginAsync(_loginOptions);
        print("[ProximityVoice] Logged in to Vivox as " + _loginOptions.DisplayName);
        if (GameManager.Instance.Lobby.Session == null)
        {
            GameManager.Instance.Lobby.SessionLifecycle.SessionAdded.AddListener(JoinChannelAsync);
        }
        else
        {
            JoinChannelAsync(GameManager.Instance.Lobby.Session);
        }
    }

    async void JoinChannelAsync(ISession session)
    {
        _channelName = $"{GameManager.Instance.Lobby.SessionType}_{session.Name}";
        try
        {
            await VivoxService.Instance.JoinPositionalChannelAsync(
                _channelName,
                ChatCapability.AudioOnly,
                new Channel3DProperties(
                    _audibleDistance,
                    _conversationalDistance,
                    1f,
                    AudioFadeModel.InverseByDistance));

            _inChannel = true;
            Debug.Log($"[ProximityVoice] Joined channel '{_channelName}'");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ProximityVoice] Channel join failed: {e.Message}");
        }
    }

    private async void LeaveChannelAsync()
    {
        _inChannel = false;
        try { await VivoxService.Instance.LeaveChannelAsync(_channelName); }
        catch (Exception e) { Debug.LogWarning($"[ProximityVoice] Leave failed: {e.Message}"); }
    }

    private void Update()
    {
        if (!IsOwner || !_inChannel) return;

        VivoxService.Instance.Set3DPosition(
            transform.position,
            transform.position,
            transform.forward,
            transform.up,
            _channelName,
            false);

    }
}
