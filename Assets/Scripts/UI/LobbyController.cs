using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.SinglePlayer;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Multiplayer;
using Unity.Services.Vivox;
using UnityEngine;

/// <summary>
/// Coordinates lobby flow between UI, Relay, and NetworkManager.
/// </summary>
public class LobbyController : MonoBehaviour
{
    private LobbyUI _ui;
    private SessionObserver _sessionObserver;

    private void Start()
    {
        Debug.Log("[LobbyController] Start");
        _ui = FindAnyObjectByType<LobbyUI>();
        
        var lobby = GameManager.Instance.Lobby;
        if (lobby != null)
        {
            // Subscribe to MultiplayerSession component events
            lobby.SessionLifecycle.SessionAdded.AddListener(OnSessionAdded);
            lobby.SessionLifecycle.RemovedFromSession.AddListener(OnSessionRemoved);
            lobby.SessionLifecycle.Deleted.AddListener(OnSessionRemoved);
            
            // Check if already in a session
            if (lobby.Session != null)
            {
                Debug.Log("[LobbyController] Session already exists in Start");
                OnSessionAdded(lobby.Session);
            }
        }
        else
        {
            Debug.LogError("[LobbyController] GameManager.Lobby is null!");
        }

        _ui.StartClicked += OnStart;
        _ui.ReadyClicked += OnReady;
        _ui.QuitClicked += () => Application.Quit();

        GameManager.OnGameStarted += OnGameStarted;
        PlayerData.OnPlayerStateChanged += OnPlayerStateChanged;
        NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;
    }

    private void OnDestroy()
    {
        GameManager.OnGameStarted -= OnGameStarted;
        PlayerData.OnPlayerStateChanged -= OnPlayerStateChanged;
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;

        var lobby = GameManager.Instance.Lobby;
        if (lobby != null)
        {
            lobby.SessionLifecycle.SessionAdded.RemoveListener(OnSessionAdded);
            lobby.SessionLifecycle.RemovedFromSession.RemoveListener(OnSessionRemoved);
            lobby.SessionLifecycle.Deleted.RemoveListener(OnSessionRemoved);
        }
    }

    private void OnSessionAdded(ISession session)
    {
        Debug.Log($"[LobbyController] Session added: {session.Id}. IsHost: {session.IsHost}");
        _ui.SetSessionState(true, session.IsHost);
    }

    private void OnSessionRemoved(ISession session)
    {
        Debug.Log($"[LobbyController] Session removed: {session.Id}");
        _ui.SetSessionState(false, false);
    }

    private void OnReady()
{
        var localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (localPlayer != null)
        {
            var ps = localPlayer.GetComponent<PlayerData>();
            if (ps != null) ps.SetReadyRpc(!ps.IsReady.Value);
        }
    }

    private void OnPlayerStateChanged()
    {
        // Update local UI text
        var localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (localPlayer != null)
        {
            var player = localPlayer.GetComponent<PlayerData>();
            if (player != null) _ui.SetReadyText(player.IsReady.Value);
        }

        // Host: check all players and enable Start if everyone is ready
        if (NetworkManager.Singleton.IsServer)
        {
            var allPlayers = FindObjectsByType<PlayerData>(FindObjectsInactive.Exclude);
bool allReady = allPlayers.Length > 0;
            foreach (var p in allPlayers)
            {
                if (!p.IsReady.Value)
                {
                    allReady = false;
                    break;
                }
            }
            _ui.SetStartEnabled(allReady);
        }
    }

    // If the relay transport dies (e.g. network drop), recover gracefully.
    private async void OnTransportFailure()
    {
        Debug.LogWarning("[LobbyController] Transport failure — returning to local host.");

        // Clean up voice before the network session fully collapses.
        try { await VivoxService.Instance.LeaveAllChannelsAsync(); }
        catch (System.Exception e) { Debug.LogWarning($"[LobbyController] Vivox leave failed: {e.Message}"); }

        //_relay.ResetIsHosting();
        await WaitForShutdown();
        StartLocalHost();
        _ui.SetIdle();
    }

    // Yield until NGO has fully completed its deferred shutdown.
    private static async Task WaitForShutdown()
    {
        var nm = NetworkManager.Singleton;
        float timer = 0f;
        while ((nm.ShutdownInProgress || nm.IsListening) && timer < 5f)
        {
            await Task.Yield();
            timer += Time.deltaTime;
        }
    }

    private void OnStart()
    {
        // NetworkManager is always running (local or Relay) — just start the game.
        GameManager.Instance.StartGameRpc();
    }

    private void OnGameStarted()
    {
        _ui.gameObject.SetActive(false);
    }

    // Use port 0 for local-only sessions so the OS picks a free port.
    private void StartLocalHost()
    {
        var transport = NetworkManager.Singleton.GetComponent<SinglePlayerTransport>();
        NetworkManager.Singleton.NetworkConfig.NetworkTransport = transport;
        NetworkManager.Singleton.StartHost();
        _ui.SetHosting("Local");
    }
}
