using System;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using Unity.Services.Multiplayer.Components;
using UnityEngine;

namespace Blocks.Sessions
{
    /// <summary>
    /// Type of session connector that a <see cref="MatchmakingSessionConnector"/> can run.
    /// </summary>
    public enum MatchmakingConnectorType
    {
        /// <summary>
        /// Create a new session or join an existing one by session ID.
        /// </summary>
        CreateOrJoin = 0,

        /// <summary>
        /// Create a new session.
        /// </summary>
        Create = 1,

        /// <summary>
        /// Join an existing session by session ID or join code.
        /// </summary>
        Join = 2,

        /// <summary>
        /// Find and join a session using QuickJoin filters.
        /// </summary>
        QuickJoin = 3
    }

    /// <summary>
    /// A version of SessionConnector that includes an option for MatchMaking Quickjoin.
    /// </summary>
    [CreateAssetMenu(fileName = "MatchmakingSessionConnector", menuName = "Services/Multiplayer/Session Connector/QuickJoin Session Connector")]
    public sealed class MatchmakingSessionConnector : ScriptableObject
    {
        [Tooltip("The MultiplayerSession to assign the created or joined session to.")]
        [SerializeField]
        private MultiplayerSession m_MultiplayerSession;

        [Tooltip("Whether to create a new session, create or join by id, join an existing one, or quick join.")]
        [SerializeField]
        private MatchmakingConnectorType m_ConnectorType = MatchmakingConnectorType.QuickJoin;

        [Tooltip("Session options for Create Session and Create Or Join modes (name, max players, privacy, password, etc.).")]
        [SerializeField]
        private MatchmakingCreateOptions m_CreateSessionOptions = new MatchmakingCreateOptions();

        [Tooltip("Network type and options for Create Session and Create Or Join modes.")]
        [SerializeField]
        private MatchmakingNetworkSettings m_SessionNetworkSettings = new MatchmakingNetworkSettings();

        [Tooltip("Join options for Join Session mode (session id or code, password).")]
        [SerializeField]
        private MatchmakingJoinOptions m_JoinSessionOptions = new MatchmakingJoinOptions();

        [Tooltip("QuickJoin settings for QuickJoin mode.")]
        [SerializeField]
        private QuickJoinSettings m_QuickJoinSettings;

        [Tooltip("Connector events.")]
        [SerializeField]
        private SessionConnectorEvents m_Events = new SessionConnectorEvents();

        /// <summary>
        /// The <see cref="MultiplayerSession"/> asset that receives the created or joined session.
        /// </summary>
        public MultiplayerSession MultiplayerSession
        {
            get => m_MultiplayerSession;
            set => m_MultiplayerSession = value;
        }

        /// <summary>
        /// Type of the session connector to configure and execute.
        /// </summary>
        public MatchmakingConnectorType ConnectorType
        {
            get => m_ConnectorType;
            set => m_ConnectorType = value;
        }

        /// <summary>
        /// Connector events.
        /// </summary>
        public SessionConnectorEvents Events => m_Events;

        /// <summary>
        /// Runs the session connector with the current settings.
        /// </summary>
        public void Execute(IUnityServices servicesRegistry = default)
        {
            _ = ExecuteAsync(servicesRegistry);
        }

        /// <summary>
        /// Runs the session connector asynchronously.
        /// </summary>
        public async Task<ISession> ExecuteAsync(IUnityServices servicesRegistry = default)
        {
            m_Events.ExecutionStarted?.Invoke(m_MultiplayerSession?.SessionType ?? string.Empty);

            servicesRegistry ??= UnityServices.Instance;
            if (m_MultiplayerSession == null)
            {
                InvokeFailed("A Multiplayer Session is required.");
                return null;
            }

            ResetServicesOnMultiplayerSession(servicesRegistry);
            var multiplayerService = servicesRegistry.GetMultiplayerService();

            try
            {
                ISession session = null;
                switch (m_ConnectorType)
                {
                    case MatchmakingConnectorType.Create:
                        session = await multiplayerService.CreateSessionAsync(BuildSessionOptions());
                        break;
                    case MatchmakingConnectorType.CreateOrJoin:
                        var sessionId = m_JoinSessionOptions.SessionId;
                        if (string.IsNullOrEmpty(sessionId)) throw new Exception("Session ID is required for Create Or Join.");
                        session = await multiplayerService.CreateOrJoinSessionAsync(sessionId, BuildSessionOptions());
                        break;
                    case MatchmakingConnectorType.Join:
                        if (m_JoinSessionOptions.JoinMode == JoinSessionMode.ById)
                        {
                            if (string.IsNullOrEmpty(m_JoinSessionOptions.SessionId)) throw new Exception("Session ID is required for Join By ID.");
                            session = await multiplayerService.JoinSessionByIdAsync(m_JoinSessionOptions.SessionId, BuildJoinSessionOptions());
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(m_JoinSessionOptions.SessionCode)) throw new Exception("Session Code is required for Join By Code.");
                            session = await multiplayerService.JoinSessionByCodeAsync(m_JoinSessionOptions.SessionCode, BuildJoinSessionOptions());
                        }
                        break;
                    case MatchmakingConnectorType.QuickJoin:
                        var quickJoinOptions = m_QuickJoinSettings != null ? m_QuickJoinSettings.ToQuickJoinOptions() : new QuickJoinOptions();
                        session = await multiplayerService.MatchmakeSessionAsync(quickJoinOptions, BuildSessionOptions());
                        break;
                }

                if (session != null)
                {
                    SetSessionOnMultiplayerSession(session);
                    m_Events.SuccessfulExecution?.Invoke(session);
                }
                return session;
            }
            catch (Exception e)
            {
                InvokeFailed(e.Message);
                return null;
            }
        }

        public MatchmakingSessionConnector WithQuickJoin(QuickJoinSettings settings = null)
        {
            m_ConnectorType = MatchmakingConnectorType.QuickJoin;
            if (settings != null)
            {
                m_QuickJoinSettings = settings;
            }
            return this;
        }

        public Unity.Services.Multiplayer.JoinSessionOptions ToJoinSessionOptions()
        {
            return BuildJoinSessionOptions();
        }

        public Unity.Services.Multiplayer.SessionOptions ToSessionOptions()
        {
            return BuildSessionOptions();
        }

        private SessionOptions BuildSessionOptions()
        {
            var options = new SessionOptions
            {
                Name = string.IsNullOrWhiteSpace(m_CreateSessionOptions.SessionName) ? Guid.NewGuid().ToString() : m_CreateSessionOptions.SessionName,
                MaxPlayers = m_CreateSessionOptions.MaxPlayers,
                IsPrivate = m_CreateSessionOptions.IsPrivate,
                IsLocked = m_CreateSessionOptions.IsLocked,
                Password = string.IsNullOrWhiteSpace(m_CreateSessionOptions.Password) ? null : m_CreateSessionOptions.Password,
                Type = m_MultiplayerSession.SessionType
            };

            if (m_SessionNetworkSettings.CreateNetwork)
            {
                if (m_SessionNetworkSettings.Network == MatchmakingNetworkType.Direct)
                {
                    options.WithDirectNetwork(m_SessionNetworkSettings.DirectOptions.ListenIp, m_SessionNetworkSettings.DirectOptions.Ip, m_SessionNetworkSettings.DirectOptions.Port);
                }
                else if (m_SessionNetworkSettings.Network == MatchmakingNetworkType.Relay)
                {
                    options.WithRelayNetwork(new RelayNetworkOptions(m_SessionNetworkSettings.RelayOptions.Region, m_SessionNetworkSettings.RelayOptions.PreserveRegion));
                }
            }

            return options;
        }

        private Unity.Services.Multiplayer.JoinSessionOptions BuildJoinSessionOptions()
        {
            return new Unity.Services.Multiplayer.JoinSessionOptions
            {
                Type = m_MultiplayerSession.SessionType,
                Password = string.IsNullOrWhiteSpace(m_JoinSessionOptions.Password) ? null : m_JoinSessionOptions.Password
            };
        }

        private void InvokeFailed(string message)
        {
            Debug.LogWarning($"[MatchmakingSessionConnector] {message}");
            m_Events.FailedExecution?.Invoke(message);
        }

        private void SetSessionOnMultiplayerSession(ISession session)
        {
            var method = typeof(MultiplayerSession).GetMethod("SetSession", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(m_MultiplayerSession, new object[] { session });
        }

        private void ResetServicesOnMultiplayerSession(IUnityServices services)
        {
            var method = typeof(MultiplayerSession).GetMethod("ResetServices", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(m_MultiplayerSession, new object[] { services });
        }
    }

    [Serializable]
    public class MatchmakingCreateOptions
    {
        public int MaxPlayers = 4;
        public string SessionName = string.Empty;
        public bool IsPrivate;
        public bool IsLocked;
        public string Password = string.Empty;
    }

    public enum MatchmakingNetworkType { None, Direct, Relay }

    [Serializable]
    public class MatchmakingNetworkSettings
    {
        public bool CreateNetwork = true;
        public MatchmakingNetworkType Network = MatchmakingNetworkType.Direct;
        public MatchmakingDirectOptions DirectOptions = new MatchmakingDirectOptions();
        public MatchmakingRelayOptions RelayOptions = new MatchmakingRelayOptions();
    }

    [Serializable]
    public class MatchmakingDirectOptions
    {
        public string Ip = "127.0.0.1";
        public int Port = 7777;
        public string ListenIp = "0.0.0.0";
    }

    [Serializable]
    public class MatchmakingRelayOptions
    {
        public string Region;
        public bool PreserveRegion;
    }

    [Serializable]
    public class MatchmakingJoinOptions
    {
        public JoinSessionMode JoinMode = JoinSessionMode.ById;
        public string SessionId = string.Empty;
        public string SessionCode = string.Empty;
        public string Password = string.Empty;
    }
}
