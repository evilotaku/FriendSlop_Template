using Blocks.Common;
using System;
using Unity.Properties;
using Unity.Services.Multiplayer.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Blocks.Sessions
{
    [UxmlElement]
    public partial class QuickJoinButton : Button
    {
        const string k_QuickJoinButtonText = "QUICK JOIN";

        [UxmlAttribute, CreateProperty]
        public SessionConnector SessionSettings
        {
            get => m_SessionSettings;
            set
            {
                if (m_SessionSettings == value)
                {
                    return;
                }

                m_SessionSettings = value;
                if (panel != null)
                {
                    UpdateBindings();
                }
            }
        }
        SessionConnector m_SessionSettings;

        [UxmlAttribute]
        public QuickJoinSettings QuickJoinSettings;

        DataBinding m_DataBinding;
        QuickJoinViewModel m_ViewModel;

        public QuickJoinButton()
        {
            text = k_QuickJoinButtonText;

            AddToClassList(BlocksTheme.Button);
            m_DataBinding = new DataBinding()
            {
                dataSourcePath = new PropertyPath(nameof(QuickJoinViewModel.CanClickButton)),
                bindingMode = BindingMode.ToTarget
            };
            SetBinding("enabledSelf", m_DataBinding);
            clicked += OnQuickJoinButtonClicked;

            RegisterCallback<AttachToPanelEvent>(_ => UpdateBindings());
            RegisterCallback<DetachFromPanelEvent>(_ => CleanupBindings());
        }

        void OnQuickJoinButtonClicked()
        {
            if (!SessionSettings)
            {
                Debug.LogError("SessionSettings is null, it needs to be assigned in the uxml.");
                return;
            }
            if (!m_ViewModel.AreMultiplayerServicesInitialized())
            {
                Debug.LogError("Multiplayer Services are not initialized. You can initialize them with default settings by adding a ServicesInitialization and PlayerAuthentication components in your scene.");
                return;
            }

            _ = m_ViewModel.MatchmakeSessionAsync(SessionSettings);
            //_ = m_ViewModel.CreateOrJoinSessionAsync(m_SessionSettings);
        }

        void UpdateBindings()
        {
            CleanupBindings();
            m_ViewModel = new QuickJoinViewModel(SessionSettings?.MultiplayerSession.SessionType);
            m_DataBinding.dataSource = m_ViewModel;
        }

        void CleanupBindings()
        {
            if (m_DataBinding.dataSource is IDisposable disposable)
            {
                disposable.Dispose();
            }

            m_ViewModel = null;
            m_DataBinding.dataSource = null;
        }
    }
}
