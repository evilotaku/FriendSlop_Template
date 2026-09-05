using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Handles lobby UI element visibility, input events, and display state.
/// </summary>
public class LobbyUI : MonoBehaviour
{
    public event Action HostClicked;
    public event Action JoinClicked;
    public event Action StartClicked;
    public event Action LeaveClicked;
    public event Action QuitClicked;
    public event Action ReadyClicked;

    public string JoinCode { get; private set; } = "";
    private PanelRenderer panelRenderer;

    private Button _hostBtn, _joinBtn, _startBtn, _leaveBtn, _quitBtn, _copyBtn, _readyBtn;
    private TextField _joinCodeField;
    private Label _statusLabel;
    private VisualElement _sessionMenu;

    private string _currentJoinCode;
    private bool _isHostUI;

    private static readonly Color32 Green = new Color32(117, 255, 81, 255);
    private static readonly Color32 Red = new Color32(220, 80, 80, 255);
    private static readonly Color32 DarkText = new Color32(8, 8, 14, 255);
    private static readonly Color32 LightText = new Color32(240, 240, 248, 255);

    private void Awake()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        panelRenderer = GetComponent<PanelRenderer>();
        panelRenderer.RegisterUIReloadCallback(UIReload);

       

        SetIdle();
    }

    private void UIReload(PanelRenderer panelRenderer, VisualElement root, int version)
    {
        _hostBtn = root.Q<Button>("host-btn");
        _joinBtn = root.Q<Button>("join-btn");
        _startBtn = root.Q<Button>("start-btn");
        _leaveBtn = root.Q<Button>("leave-btn");
        _quitBtn = root.Q<Button>("quit-btn");
        _copyBtn = root.Q<Button>("copy-btn");
        _readyBtn = root.Q<Button>("ready-btn");
        _joinCodeField = root.Q<TextField>("join-code-field");
        _statusLabel = root.Q<Label>("status-label");
        _sessionMenu = root.Q<VisualElement>("SessionMenu");

        if (_hostBtn != null) _hostBtn.clicked += () => HostClicked?.Invoke();
        if (_joinBtn != null) _joinBtn.clicked += () => JoinClicked?.Invoke();
        if (_startBtn != null) _startBtn.clicked += () => StartClicked?.Invoke();
        if (_leaveBtn != null) _leaveBtn.clicked += () => LeaveClicked?.Invoke();
        if (_quitBtn != null) _quitBtn.clicked += () => QuitClicked?.Invoke();
        if (_readyBtn != null) _readyBtn.clicked += () => ReadyClicked?.Invoke();
        if (_copyBtn != null) _copyBtn.clicked += () => GUIUtility.systemCopyBuffer = _currentJoinCode;

        if (_joinCodeField != null)
            _joinCodeField.RegisterValueChangedCallback(e => JoinCode = e.newValue);
    }

    private void Start()
    {
    }

    public void SetIdle()
    {
        _isHostUI = false;
        Show(_hostBtn); Show(_joinBtn); Show(_joinCodeField); Show(_quitBtn); Show(_sessionMenu);
        Hide(_leaveBtn); Hide(_copyBtn); Hide(_readyBtn); Hide(_startBtn);
        if (_hostBtn != null) { _hostBtn.style.backgroundColor = new StyleColor(Red); _hostBtn.style.color = new StyleColor(LightText); _hostBtn.text = "Host"; }
        if (_statusLabel != null) _statusLabel.text = "Not hosting";
        SetReadyText(false);
    }

    public void SetHosting(string joinCode)
    {
        _isHostUI = true;
        _currentJoinCode = joinCode;
        Show(_hostBtn); Show(_quitBtn); Show(_copyBtn);
        Hide(_joinBtn); Hide(_joinCodeField); Hide(_leaveBtn); Hide(_startBtn); Hide(_readyBtn); Hide(_sessionMenu);
        if (_hostBtn != null) { _hostBtn.style.backgroundColor = new StyleColor(Green); _hostBtn.style.color = new StyleColor(DarkText); _hostBtn.text = "Leave"; }
        if (_statusLabel != null) _statusLabel.text = $"Join code: {joinCode}";
        SetStartEnabled(false);
        SetReadyText(false);
    }

    public void SetClient()
    {
        _isHostUI = false;
        Show(_leaveBtn); Show(_quitBtn);
        Hide(_hostBtn); Hide(_joinBtn); Hide(_joinCodeField); Hide(_startBtn); Hide(_copyBtn); Hide(_readyBtn); Hide(_sessionMenu);
        if (_statusLabel != null) _statusLabel.text = "Connected to host";
        SetReadyText(false);
    }

    public void SetSessionState(bool isInSession, bool isHost)
    {
        _isHostUI = isHost;
        if (isInSession)
        {
            Show(_readyBtn);
            Hide(_sessionMenu);
            if (_isHostUI) Show(_startBtn);
        }
        else
        {
            Hide(_readyBtn);
            Hide(_startBtn);
            Show(_sessionMenu);
        }
    }

    public void SetReadyText(bool ready)
    {
        if (_readyBtn == null) return;
        _readyBtn.text = ready ? "Ready!" : "Ready?";
        _readyBtn.style.backgroundColor = new StyleColor(ready ? Green : new Color32(24, 24, 24, 255));
        _readyBtn.style.color = new StyleColor(ready ? DarkText : LightText);
    }

    public void SetStartEnabled(bool enabled)
    {
        if (_startBtn == null) return;
        _startBtn.SetEnabled(enabled);
        _startBtn.style.opacity = enabled ? 1f : 0.5f;
    }

    private void Show(VisualElement el) { if (el != null) el.style.display = DisplayStyle.Flex; }
    private void Hide(VisualElement el) { if (el != null) el.style.display = DisplayStyle.None; }

    private void OnDestroy()
    {
        panelRenderer.UnregisterUIReloadCallback(UIReload);
    }
}
