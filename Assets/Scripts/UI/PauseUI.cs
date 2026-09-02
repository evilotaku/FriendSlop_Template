using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Handles ESC pause menu with voice player list, mute toggles, and leave option.
/// </summary>
public class PauseUI : MonoBehaviour
{
    PanelRenderer panelRenderer;
    private VisualElement _pausePanel;
    private ScrollView _playerList;
    private Button _leaveBtn;

    private bool _isPlaying;
    private bool _isPaused;

    private void Start()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        panelRenderer = GetComponent<PanelRenderer>();
        panelRenderer.RegisterUIReloadCallback(UIReload);
        GameManager.OnGameStarted += OnGameStarted;
    }

    private void UIReload(PanelRenderer panelRenderer, VisualElement root)
    {
        _pausePanel = root.Q<VisualElement>("pause-panel");
        _playerList = root.Q<ScrollView>("player-list");
        _leaveBtn = root.Q<Button>("pause-leave-btn");

        if (_leaveBtn != null) _leaveBtn.clicked += OnLeaveClicked;
    }

    private void OnDestroy()
    {
        GameManager.OnGameStarted -= OnGameStarted;
        panelRenderer.UnregisterUIReloadCallback(UIReload);
    }

    private void OnGameStarted()
    {
        _isPlaying = true;
    }
    
    private void Update()
    {
        if (!_isPlaying) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            TogglePause();
    }

    private void TogglePause()
    {
        if (_pausePanel == null) return;

        _isPaused = !_isPaused;

        if (_isPaused)
        {
            //RefreshPlayerList();
            _pausePanel.style.display = DisplayStyle.Flex;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }
        else
        {
            _pausePanel.style.display = DisplayStyle.None;
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
        }
    }

    private async void OnLeaveClicked()
    {
        // Unlock cursor before anything async — feels more responsive.
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;

        // Cleanly leave Vivox channels and shut down NGO.
        //VivoxService.Instance?.LeaveAllChannelsAsync();
        await GameManager.Instance?.Lobby.Session.LeaveAsync();
        // Wait for NGO shutdown to complete before reloading.
        var nm = NetworkManager.Singleton;
        float t = 0f;
        while (nm != null && (nm.ShutdownInProgress || nm.IsListening) && t < 5f)
        {
            await Task.Yield();
            t += Time.deltaTime;
        }

        // Full scene reload — resets all state cleanly without manual teardown.
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    
}
