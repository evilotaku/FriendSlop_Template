using System.Collections.Generic;
using Blocks.Common;
using Blocks.Sessions.Common;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

using Unity.Services.Multiplayer.Components;

namespace Blocks.Sessions
{
    [UxmlElement]
    public partial class JoinSessionByCode : VisualElement
    {
        const string k_SessionCodeTextFieldPlaceholder = "Enter Session Code";
        const string k_JoinButtonText = "JOIN";

        [UxmlAttribute, CreateProperty]
        ScriptableObject SessionSettings
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
        ScriptableObject m_SessionSettings;

        JoinSessionByCodeViewModel m_ViewModel;

        readonly List<DataBinding> m_Bindings = new();

        public JoinSessionByCode()
        {
            AddToClassList(BlocksTheme.ContainerHorizontal);

            var sessionCodeTextField = new TextField
            {
                textEdition =
                {
                    placeholder = k_SessionCodeTextFieldPlaceholder,
                    hidePlaceholderOnFocus = true
                }
            };
            sessionCodeTextField.AddToClassList(BlocksTheme.TextField);
            sessionCodeTextField.AddToClassList(BlocksTheme.SpaceRight);
            var sessionCodeBinding = new DataBinding
            {
                dataSourcePath = new PropertyPath(nameof(m_ViewModel.SessionCode)),
                bindingMode = BindingMode.ToSource
            };
            sessionCodeTextField.SetBinding("value", sessionCodeBinding);
            Add(sessionCodeTextField);
            m_Bindings.Add(sessionCodeBinding);

            var createSessionButton = new Button
            {
                text = k_JoinButtonText
            };
            createSessionButton.AddToClassList(BlocksTheme.Button);
            var createSessionBinding = new DataBinding
            {
                dataSourcePath = new PropertyPath(nameof(m_ViewModel.CanJoinSession)),
                bindingMode = BindingMode.ToTarget
            };
            createSessionButton.SetBinding(new BindingId(nameof(enabledSelf)), createSessionBinding);
            createSessionButton.clicked += JoinSession;
            Add(createSessionButton);
            m_Bindings.Add(createSessionBinding);

            RegisterCallback<AttachToPanelEvent>(_ => UpdateBindings());
            RegisterCallback<DetachFromPanelEvent>(_ => CleanupBindings());
        }

        void JoinSession()
        {
            if (!m_ViewModel.AreMultiplayerServicesInitialized())
            {
                Debug.LogError("Multiplayer Services are not initialized. You can initialize them with default settings by adding a Servicesinitialization and PlayerAuthentication components in your scene.");
                return;
            }

            Unity.Services.Multiplayer.JoinSessionOptions options = null;
            if (m_SessionSettings is MatchmakingSessionConnector matchmakingConnector)
            {
                options = matchmakingConnector.ToJoinSessionOptions();
            }
            else if (m_SessionSettings is SessionConnector sessionConnector)
            {
                options = new Unity.Services.Multiplayer.JoinSessionOptions
                {
                    Type = sessionConnector.MultiplayerSession?.SessionType,
                    Password = sessionConnector.GetJoinOptions().Password
                };
            }
            else if (m_SessionSettings is SessionSettings oldSettings)
            {
                options = oldSettings.ToJoinSessionOptions();
            }

            _ = m_ViewModel.JoinSessionByCodeAsync(options);
        }

        void UpdateBindings()
        {
            CleanupBindings();

            string sessionType = null;
            if (m_SessionSettings is MatchmakingSessionConnector matchmakingConnector)
            {
                sessionType = matchmakingConnector.MultiplayerSession?.SessionType;
            }
            else if (m_SessionSettings is SessionConnector sessionConnector)
            {
                sessionType = sessionConnector.MultiplayerSession?.SessionType;
            }
            else if (m_SessionSettings is SessionSettings oldSettings)
            {
                sessionType = oldSettings.sessionType;
            }

            m_ViewModel = new JoinSessionByCodeViewModel(sessionType);
            foreach (var binding in m_Bindings)
            {
                binding.dataSource = m_ViewModel;
            }
        }

        void CleanupBindings()
        {
            m_ViewModel?.Dispose();
            m_ViewModel = null;

            foreach (var binding in m_Bindings)
            {
                binding.dataSource = null;
            }
        }
    }
}
