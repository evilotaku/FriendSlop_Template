using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Manages in-game HUD visibility. ESC menu handled by PauseUI on same GameObject.
/// </summary>
public class HudUI : MonoBehaviour
{
    private VisualElement _root;
    private PanelRenderer _panelRenderer;

    private void Start()
    {
        _panelRenderer = GetComponent<PanelRenderer>();
        _panelRenderer.RegisterUIReloadCallback(UIReload);

        GameManager.OnGameStarted += Show;
    }
       

    private void UIReload(PanelRenderer panelRenderer, VisualElement root, int version)
    {
        root.style.display = DisplayStyle.None;
        _root = root;
    }

    private void OnDestroy()
    {
        GameManager.OnGameStarted -= Show;
        _panelRenderer.UnregisterUIReloadCallback(UIReload);
    }

    private void Show() => _root.style.display = DisplayStyle.Flex;
}
