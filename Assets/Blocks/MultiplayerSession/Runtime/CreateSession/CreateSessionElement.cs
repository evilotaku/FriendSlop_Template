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
    public partial class CreateSessionElement : VisualElement
    {
        const string k_EnterSessionNamePlaceholder = "Enter Session Name";
        const string k_CreateButtonText = "CREATE";

        [CreateProperty, UxmlAttribute]
        public ScriptableObject SessionSettings
        {
            get => m_SessionSettings;
            set
            {
                if (m_SessionSettings == value)
                    return;

                m_SessionSettings = value;
                if (panel != null)
                    UpdateBindings();
            }
        }
        ScriptableObject m_SessionSettings;

        CreateSessionViewModel m_ViewModel;

        readonly List<DataBinding> m_Bindings = new();

        public CreateSessionElement()
        {
            AddToClassList(BlocksTheme.ContainerHorizontal);

            var enabledBinding = new DataBinding
            {
                dataSourcePath = new PropertyPath(nameof(m_ViewModel.CanRegisterSession)),
                bindingMode = BindingMode.ToTarget
            };
            SetBinding(new BindingId(nameof(enabledSelf)), enabledBinding);
            m_Bindings.Add(enabledBinding);

            var sessionNameTextField = new TextField
            {
                textEdition =
                {
                    placeholder = k_EnterSessionNamePlaceholder,
                    hidePlaceholderOnFocus = true
                }
            };
            sessionNameTextField.AddToClassList(BlocksTheme.TextField);
            sessionNameTextField.AddToClassList(BlocksTheme.SpaceRight);
            var sessionNameBinding = new DataBinding
            {
                dataSourcePath = new PropertyPath(nameof(m_ViewModel.SessionName)),
                bindingMode = BindingMode.ToSource
            };
            sessionNameTextField.SetBinding("value", sessionNameBinding);
            Add(sessionNameTextField);
            m_Bindings.Add(sessionNameBinding);

            var createSessionButton = new Button
            {
                text = k_CreateButtonText
            };
            createSessionButton.AddToClassList(BlocksTheme.Button);
            var createSessionBinding = new DataBinding
            {
                dataSourcePath = new PropertyPath(nameof(m_ViewModel.HasSessionName)),
                bindingMode = BindingMode.ToTarget
            };
            createSessionButton.SetBinding(new BindingId(nameof(enabledSelf)), createSessionBinding);
            createSessionButton.clicked += CreateSession;
            Add(createSessionButton);
            m_Bindings.Add(createSessionBinding);

            RegisterCallback<AttachToPanelEvent>(_ => UpdateBindings());
            RegisterCallback<DetachFromPanelEvent>(_ => CleanupBindings());
        }

        void CreateSession()
        {
            if (!m_SessionSettings)
            {
                Debug.LogError("SessionSettings is null, it needs to be assigned in the uxml.");
                return;
            }
            if (!m_ViewModel.AreMultiplayerServicesInitialized())
            {
                Debug.LogError("Multiplayer Services are not initialized. You can initialize them with default settings by adding a ServicesInitialization and PlayerAuthentication components in your scene.");
                return;
            }

            Unity.Services.Multiplayer.SessionOptions options = null;
            if (m_SessionSettings is MatchmakingSessionConnector matchmakingConnector)
            {
                options = matchmakingConnector.ToSessionOptions();
            }
            else if (m_SessionSettings is SessionConnector sessionConnector)
            {
                var method = typeof(SessionConnector).GetMethod("BuildSessionOptions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                options = (Unity.Services.Multiplayer.SessionOptions)method?.Invoke(sessionConnector, null);
            }
            else if (m_SessionSettings is SessionSettings oldSettings)
            {
                options = oldSettings.ToSessionOptions();
            }

            _ = m_ViewModel.CreateSessionAsync(options);
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

            m_ViewModel = new CreateSessionViewModel(sessionType);
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
