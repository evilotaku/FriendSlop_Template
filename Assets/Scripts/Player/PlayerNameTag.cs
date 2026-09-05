using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerNameTag : NetworkBehaviour
{
    PanelRenderer panelRenderer;
    Label nameLabel;
    PlayerData playerData;

    private void OnEnable()
    {
        nameLabel = new();
        playerData = GetComponentInParent<PlayerData>();
        panelRenderer = GetComponent<PanelRenderer>();
        panelRenderer.RegisterUIReloadCallback(OnUIReload);
        playerData.PlayerName.OnValueChanged += (oldValue, newValue) =>
        {
            nameLabel.text = newValue.ToString();
        };
    }

    void OnUIReload(PanelRenderer renderer, VisualElement rootElement, int version)
    {
        nameLabel.text = playerData.PlayerName.Value.ToString();
        nameLabel.style.unityTextAutoSize = new StyleTextAutoSize(new TextAutoSize(TextAutoSizeMode.BestFit, minSize: 10, maxSize: 20));
        nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        nameLabel.style.color = Color.white;

        rootElement.Add(nameLabel);
    }    

    private void Update()
    {
       transform.LookAt(Camera.main.transform);
    }
}
