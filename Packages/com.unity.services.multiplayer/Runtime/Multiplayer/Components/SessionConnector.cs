using System;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Core.Internal;
using UnityEngine;
using UnityEngine.Events;

namespace Unity.Services.Multiplayer.Components
{
    /// <summary>
    /// Runs a create, create-or-join, or join session connector.
    /// </summary>
    /// <seealso cref="SessionConnectorType"/>
    /// <seealso cref="MultiplayerSession"/>
    /// <seealso cref="SessionConnectorBehaviour"/>
    public sealed class SessionConnector : ScriptableObject
    {
        [Tooltip(
            "The MultiplayerSession to assign the created or joined session to.")]
        [SerializeField]
        private MultiplayerSession m_MultiplayerSession;

        [Tooltip("Whether to create a new session, create or join by id, or join an existing one.")] [SerializeField]
        private SessionConnectorType m_ConnectorType = SessionConnectorType.Create;

        [Tooltip("Session ID for Create Or Join mode. The session id to create or join.")]
        [SerializeField]
        [Visibility(nameof(m_ConnectorType), SessionConnectorType.CreateOrJoin)]
        private string m_CreateOrJoinSessionId = string.Empty;

        [Tooltip(
            "Session options for Create Session, Create Or Join, and Quick Join modes (name, max players, privacy, password, etc.).")]
        [SerializeField]
        private CreateSessionOptions m_CreateSessionOptions = new CreateSessionOptions();

        [Tooltip("Network type and options for Create Session, Create Or Join, and Quick Join modes.")] [SerializeField]
        private SessionNetworkSettings m_SessionNetworkSettings = new SessionNetworkSettings();

        [Tooltip("Join options for Join Session mode (session id or code, password).")] [SerializeField]
        [Visibility(nameof(m_ConnectorType), SessionConnectorType.Join)]
        private JoinSessionOptions m_JoinSessionOptions = new JoinSessionOptions();

        [SerializeField]
        [Visibility(nameof(m_ConnectorType), SessionConnectorType.QuickJoin)]
        private QuickJoinSettings m_QuickJoinSettings = new QuickJoinSettings();

        [Tooltip("Connector events.")] [SerializeField]
        private SessionConnectorEvents m_Events = new SessionConnectorEvents();

        private Task m_ConnectionTask;

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
        /// <seealso cref="SessionConnectorType"/>
        public SessionConnectorType ConnectorType { get => m_ConnectorType; set => m_ConnectorType = value; }

        /// <summary>
        /// The session ID used when <see cref="Connector"/> is <see cref="SessionConnectorType.CreateOrJoin"/>.
        /// </summary>
        public string CreateOrJoinSessionId
        {
            get => m_CreateOrJoinSessionId;
            set => m_CreateOrJoinSessionId = value ?? string.Empty;
        }

        /// <summary>
        /// The options for session creation (name, max players, privacy, password).
        /// </summary>
        /// <seealso cref="CreateSessionOptions"/>
        internal CreateSessionOptions SessionOptions
        {
            get => m_CreateSessionOptions;
            set => m_CreateSessionOptions = value ?? new CreateSessionOptions();
        }

        /// <summary>
        /// The network configuration for the session (direct IP/port or relay).
        /// </summary>
        /// <seealso cref="NetworkOptionsSection"/>
        internal SessionNetworkSettings SessionNetwork
        {
            get => m_SessionNetworkSettings;
            set => m_SessionNetworkSettings = value ?? new SessionNetworkSettings();
        }

        /// <summary>
        /// The options for joining a session by ID or join code.
        /// </summary>
        /// <seealso cref="Components.JoinSessionOptions"/>
        internal JoinSessionOptions JoinSessionOptions
        {
            get => m_JoinSessionOptions;
            set => m_JoinSessionOptions = value ?? new JoinSessionOptions();
        }

        internal QuickJoinOptions QuickJoinOptions
        {
            get => GetQuickJoinOptions();
            set => WithQuickJoin(value);
        }

        /// <summary>
        /// The events raised during the connector lifecycle (started, success, failure).
        /// </summary>
/// <seealso cref="SessionConnectorEvents"/>
        public SessionConnectorEvents Events
        {
            get => m_Events;
            set => m_Events = value ?? new SessionConnectorEvents();
        }

        /// <summary>
        /// Raised when <see cref="Execute"/> is called. The string parameter is the session type.
        /// </summary>
        public UnityEvent<string> ExecutionStarted => m_Events.ExecutionStarted;

        /// <summary>
        /// Raised when the connector execution completes successfully.
        /// The parameter is the created or joined <see cref="ISession"/>.
        /// </summary>
        public UnityEvent<ISession> SuccessfulExecution => m_Events.SuccessfulExecution;

        /// <summary>
        /// Raised when the connector execution fails. The string parameter is the error message.
        /// </summary>
        public UnityEvent<string> FailedExecution => m_Events.FailedExecution;

        private string SessionType => m_MultiplayerSession?.SessionType;
        private IMultiplayerService m_MultiplayerService;

        /// <summary>
        /// Applies the values from a <see
        /// cref="Multiplayer.SessionOptions">SessionOptions</see>
        /// to the internal session creation settings.
        /// </summary>
        /// <param name="options">The session options to apply.</param>
        /// <returns>
        /// This <see cref="SessionConnector"/> for fluent chaining.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="options"/> is null.
        /// </exception>
        /// <remarks>
        /// <para>
        /// ⚠️ This method overrides all session settings with the settings
        /// from <paramref name="options"/>.
        /// To selectively update only some
        /// settings, as shown in the example below:
        /// <list type="number">
        /// <item>
        /// <description>
        /// Seed the <paramref name="options"/> by
        /// calling <see cref="GetSessionOptions"/>.
        /// </description>
        /// </item>
        /// <item><description>Change only the settings you want.</description></item>
        /// <item><description>Pass it back.</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// ⚠️ <see
        /// cref="Multiplayer.SessionOptions.MaxPlayers">MaxPlayers</see>
        /// defaults to <c>0</c> in a freshly constructed <see
        /// cref="Multiplayer.SessionOptions">SessionOptions</see>. Passing
        /// <c>new SessionOptions { Name = "Room" }</c> will set <see
        /// cref="Multiplayer.SessionOptions.MaxPlayers">MaxPlayers</see>
        /// to <c>0</c>, overwriting the inspector-configured value.
        /// </para>
        /// </remarks>
        /// <example>
        /// <para>
        /// Updating only the <see
        /// cref="Multiplayer.ISession.Name">session's name</see>:
        /// </para>
        /// <code>
        /// var sessionOptions = connector.GetSessionOptions();
        /// sessionOptions.Name = "NewName";
        /// connector.WithSessionOptions(sessionOptions);
        /// </code>
        /// <para>
        /// Removing the <see cref="Multiplayer.IHostSession.Password">session's
        /// password</see> by setting <see
        /// cref="Multiplayer.SessionOptions.Password"> the
        /// sessionOptions' password</see> to <see langword="null"/>:
        /// </para>
        /// <code>
        /// var sessionOptions = connector.GetSessionOptions();
        /// sessionOptions.Password = null;
        /// connector.WithSessionOptions(sessionOptions);
        /// </code>
        /// </example>
        public SessionConnector WithSessionOptions(SessionOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            m_CreateSessionOptions.IsLocked    = options.IsLocked;
            m_CreateSessionOptions.IsPrivate   = options.IsPrivate;
            m_CreateSessionOptions.MaxPlayers  = options.MaxPlayers;
            m_CreateSessionOptions.Password    = options.Password;
            m_CreateSessionOptions.SessionName = options.Name;

            return this;
        }

        /// <summary>
        /// Applies the values from a <see cref="DirectNetworkOptions"/> to the internal network settings,
        /// and sets the network type to Direct.
        /// </summary>
        /// <param name="options">The direct network options to apply.</param>
        /// <returns>This <see cref="SessionConnector"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
        public SessionConnector WithDirectNetworkOptions(DirectNetworkOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            m_SessionNetworkSettings.CreateNetwork = true;
            m_SessionNetworkSettings.Network = SessionNetworkSettings.NetworkType.Direct;
            m_SessionNetworkSettings.DirectIPPort.ListenIpAddress = options.ListenIp.NetworkEndpoint.AddressNoPort;
            m_SessionNetworkSettings.DirectIPPort.Ip = options.PublishIp.NetworkEndpoint.AddressNoPort;
            m_SessionNetworkSettings.DirectIPPort.Port = options.Port;
            return this;
        }

        /// <summary>
        /// Applies the values from a <see cref="RelayNetworkOptions"/> to the internal network settings,
        /// and sets the network type to Relay.
        /// </summary>
        /// <param name="options">The relay network options (region, preserve region) to apply.</param>
        /// <param name="protocol">
        /// The relay transport protocol to use. Defaults to <see cref="RelayProtocol.Default"/>
        /// (DTLS on most platforms, WSS on WebGL).
        /// </param>
        /// <returns>This <see cref="SessionConnector"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
        public SessionConnector WithRelayNetworkOptions(RelayNetworkOptions options, RelayProtocol protocol = RelayProtocol.Default)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            m_SessionNetworkSettings.CreateNetwork = true;
            m_SessionNetworkSettings.Network = SessionNetworkSettings.NetworkType.Relay;
            m_SessionNetworkSettings.RelayOptions.Region = options.Region;
            m_SessionNetworkSettings.RelayOptions.PreserveRegion = options.PreserveRegion;
            m_SessionNetworkSettings.RelayOptions.Protocol = protocol;
            return this;
        }

        /// <summary>
        /// Configures the connector to join a session by its ID, and sets the connector type to
        /// <see cref="SessionConnectorType.Join"/>.
        /// </summary>
        /// <param name="sessionId">The session ID to join.</param>
        /// <param name="password">Optional password. Pass <c>null</c> or omit if the session has no password.</param>
        /// <returns>This <see cref="SessionConnector"/> for fluent chaining.</returns>
        public SessionConnector WithJoinById(string sessionId, string password = null)
        {
            m_ConnectorType = SessionConnectorType.Join;
            m_JoinSessionOptions.JoinMode = JoinSessionMode.ById;
            m_JoinSessionOptions.SessionId = sessionId;
            m_JoinSessionOptions.Password = password;
            return this;
        }

        /// <summary>
        /// Configures the connector to join a session by its join code, and sets the connector type to
        /// <see cref="SessionConnectorType.Join"/>.
        /// </summary>
        /// <param name="sessionCode">The join code to use.</param>
        /// <param name="password">Optional password. Pass <c>null</c> or omit if the session has no password.</param>
        /// <returns>This <see cref="SessionConnector"/> for fluent chaining.</returns>
        public SessionConnector WithJoinByCode(string sessionCode, string password = null)
        {
            m_ConnectorType = SessionConnectorType.Join;
            m_JoinSessionOptions.JoinMode = JoinSessionMode.ByCode;
            m_JoinSessionOptions.SessionCode = sessionCode;
            m_JoinSessionOptions.Password = password;
            return this;
        }

        /// <summary>
        /// Configures the connector to create or join a session by the given ID, and sets the connector type to
        /// <see cref="SessionConnectorType.CreateOrJoin"/>.
        /// </summary>
        /// <param name="sessionId">The session ID to create or join.</param>
        /// <returns>This <see cref="SessionConnector"/> for fluent chaining.</returns>
        public SessionConnector WithCreateOrJoin(string sessionId)
        {
            m_ConnectorType = SessionConnectorType.CreateOrJoin;
            m_CreateOrJoinSessionId = sessionId ?? string.Empty;
            return this;
        }

        public SessionConnector WithCreate()
        {
            m_ConnectorType = SessionConnectorType.Create;
            return this;
        }
        public SessionConnector WithQuickJoin() => WithQuickJoin(GetQuickJoinOptions());
        public SessionConnector WithQuickJoin(QuickJoinOptions options)
        {
            m_ConnectorType = SessionConnectorType.QuickJoin;
            if (options != null)
            {
                m_QuickJoinSettings.Timeout = (float)options.Timeout.TotalSeconds;
                m_QuickJoinSettings.CreateSession = options.CreateSession;
            }
            return this;
        }

        /// <summary>
        /// Returns the current session creation settings as a <see cref="Multiplayer.SessionOptions"/>,
        /// using the same public type accepted by <see cref="WithSessionOptions"/>.
        /// </summary>
        /// <remarks>
        /// The returned object is a snapshot; changes to it do not affect the connector.
        /// An empty or whitespace <see cref="Multiplayer.SessionOptions.Name"/> or
        /// <see cref="Multiplayer.SessionOptions.Password"/> is returned as <c>null</c>,
        /// matching the convention used by <see cref="WithSessionOptions"/>.
        /// </remarks>
        /// <returns>A <see cref="Multiplayer.SessionOptions"/> populated with the connector's current create settings.</returns>
        public SessionOptions GetSessionOptions() => new SessionOptions
        {
            Name       = string.IsNullOrWhiteSpace(m_CreateSessionOptions.SessionName) ? null : m_CreateSessionOptions.SessionName.Trim(),
            MaxPlayers = m_CreateSessionOptions.MaxPlayers,
            IsPrivate  = m_CreateSessionOptions.IsPrivate,
            IsLocked   = m_CreateSessionOptions.IsLocked,
            Password   = string.IsNullOrWhiteSpace(m_CreateSessionOptions.Password) ? null : m_CreateSessionOptions.Password.Trim(),
        };

        /// <summary>
        /// Returns the current join settings as a <see cref="JoinOptions"/>,
        /// using the same public type accepted by <see cref="WithJoinByCode"/> and <see cref="WithJoinById"/>.
        /// </summary>
        /// <remarks>
        /// An empty or whitespace <see cref="JoinOptions.SessionId"/>, <see cref="JoinOptions.SessionCode"/>,
        /// or <see cref="JoinOptions.Password"/> is returned as <c>null</c>.
        /// The returned object is a snapshot; changes to it do not affect the connector.
        /// </remarks>
        /// <returns>A <see cref="JoinOptions"/> populated with the connector's current join settings.</returns>
        public JoinOptions GetJoinOptions()
        {
            var isById = m_JoinSessionOptions.JoinMode == JoinSessionMode.ById;

            return new JoinOptions(
                m_JoinSessionOptions.JoinMode,
                sessionId:   isById && !string.IsNullOrWhiteSpace(m_JoinSessionOptions.SessionId)
                                 ? m_JoinSessionOptions.SessionId.Trim() : null,
                sessionCode: !isById && !string.IsNullOrWhiteSpace(m_JoinSessionOptions.SessionCode)
                                 ? m_JoinSessionOptions.SessionCode.Trim() : null,
                password:    string.IsNullOrWhiteSpace(m_JoinSessionOptions.Password) ? null
                                 : m_JoinSessionOptions.Password.Trim()
            );
        }

        public QuickJoinOptions GetQuickJoinOptions()
        {
            return new QuickJoinOptions
            {
                Timeout = TimeSpan.FromSeconds(m_QuickJoinSettings.Timeout),
                CreateSession = m_QuickJoinSettings.CreateSession
            };
        }

        /// <summary>
        /// Returns the current relay network settings, or <c>null</c> if the connector
        /// is not configured for relay (i.e. network type is not Relay, or
        /// <c>CreateNetwork</c> is disabled in the Inspector).
        /// </summary>
        /// <remarks>
        /// The returned object is a snapshot; changes to it do not affect the connector.
        /// </remarks>
        /// <returns>A <see cref="RelayNetworkOptions"/> snapshot, or <c>null</c>.</returns>
        public RelayNetworkOptions GetRelayNetworkOptions()
        {
            if (!m_SessionNetworkSettings.CreateNetwork ||
                m_SessionNetworkSettings.Network != SessionNetworkSettings.NetworkType.Relay)
                return null;

            var r = m_SessionNetworkSettings.RelayOptions;
            return new RelayNetworkOptions(
                region: string.IsNullOrWhiteSpace(r.Region) ? null : r.Region.Trim(),
                preserveRegion: r.PreserveRegion);
        }

        /// <summary>
        /// Returns the current direct IP/port network settings, or <c>null</c> if the connector
        /// is not configured for direct networking (i.e. network type is not Direct, or
        /// <c>CreateNetwork</c> is disabled in the Inspector).
        /// </summary>
        /// <remarks>
        /// The returned object is a snapshot; changes to it do not affect the connector.
        /// </remarks>
        /// <returns>A <see cref="DirectNetworkOptions"/> snapshot, or <c>null</c>.</returns>
        public DirectNetworkOptions GetDirectNetworkOptions()
        {
            if (!m_SessionNetworkSettings.CreateNetwork ||
                m_SessionNetworkSettings.Network != SessionNetworkSettings.NetworkType.Direct)
                return null;

            var d = m_SessionNetworkSettings.DirectIPPort;
            return new DirectNetworkOptions(
                new ListenIPAddress(d.ListenIp),
                new PublishIPAddress(d.PublishIp),
                (ushort)d.Port);
        }

        /// <summary>
        /// Runs the session connector with the current settings (create,
        /// create-or-join, or join as specified by <see cref="Connector"/>),
        /// using the multiplayer service from <see cref="UnityServices"/>.
        /// </summary>
        /// <remarks>
        /// Parameterless so this overload can be bound to a <see
        /// cref="UnityEngine.Events.UnityEvent"/> in the Inspector.
        /// Results are reported via <see cref="ExecutionStarted"/>, <see
        /// cref="SuccessfulExecution"/>, and <see cref="FailedExecution"/>.
        /// </remarks>
        /// <exception cref="SessionException">When Multiplayer Session is null.</exception>
        public void Execute()
        {
            Execute(null);
        }

        /// <summary>
        /// Runs the session connector as specified by <see cref="Connector"/>,
        /// resolving <see cref="IMultiplayerService"/> from the given Unity Services instance.
        /// </summary>
        /// <param name="servicesRegistry">
        /// The <see cref="IUnityServices"/> registry used to obtain <see cref="IMultiplayerService"/>
        /// and associated with the linked <see cref="MultiplayerSession"/>.
        /// When <c>null</c>, <see cref="IMultiplayerService"/> is resolved from <see cref="UnityServices"/>.
        /// </param>
        /// <remarks>
        /// Results are reported via <see cref="ExecutionStarted"/>, <see
        /// cref="SuccessfulExecution"/>, and <see cref="FailedExecution"/>.
        /// </remarks>
        public void Execute(IUnityServices servicesRegistry = default)
        {
            m_Events.ExecutionStarted?.Invoke(SessionType ?? string.Empty);

            servicesRegistry ??= UnityServices.Instance;
            if (!IsValid(m_MultiplayerSession) || !IsValid(servicesRegistry))
            {
                return;
            }

            m_MultiplayerSession.ResetServices(servicesRegistry);
            RunExecute(servicesRegistry);
        }

        void RunExecute(IUnityServices servicesRegistry)
        {
            try
            {
                m_MultiplayerService = servicesRegistry.GetMultiplayerService();

                if (m_ConnectionTask is { IsCompleted: false })
                {
                    Logger.LogCallWarning(nameof(SessionConnector), "Connection already in progress.", this);
                    return;
                }

                m_ConnectionTask = ExecuteAsync();
            }
            catch (Exception e)
            {
                InvokeFailed(e.Message);
            }
        }

        private Task ExecuteAsync()
        {
            if (m_MultiplayerSession == null)
            {
                InvokeFailed(
                    "A Multiplayer Session is required. Assign one in the Inspector.");
                return Task.CompletedTask;
            }

            if (m_MultiplayerService == null)
            {
                InvokeFailed(
                    "Unity Services are not initialized. Ensure the project is linked and services are started.");
                return Task.CompletedTask;
            }

            return (m_ConnectorType) switch
            {
                SessionConnectorType.Create => ExecuteCreateAsync(),
                SessionConnectorType.CreateOrJoin => ExecuteCreateOrJoinAsync(),
                SessionConnectorType.Join => ExecuteJoinAsync(),
                SessionConnectorType.QuickJoin => ExecuteQuickJoinAsync(),
                _ => Task.CompletedTask
            };
        }

        private void InvokeFailed(string message)
        {
            Logger.LogCallWarning(nameof(SessionConnector), message, this);
            m_Events.FailedExecution?.Invoke(message);
        }

        private async Task ExecuteCreateAsync()
        {
            try
            {
                var options = BuildSessionOptions();
                var hostSession = await m_MultiplayerService.CreateSessionAsync(options);
                m_MultiplayerSession.SetSession(hostSession);
                m_Events.SuccessfulExecution?.Invoke(hostSession);
            }
            catch (SessionException e)
            {
                InvokeFailed(e.Message);
            }
            catch (Exception e)
            {
                InvokeFailed(e.Message);
            }
        }

        private async Task ExecuteCreateOrJoinAsync()
        {
            var sessionId = string.IsNullOrWhiteSpace(m_CreateOrJoinSessionId) ? null : m_CreateOrJoinSessionId.Trim();
            if (string.IsNullOrEmpty(sessionId))
            {
                InvokeFailed("Session ID is required for Create Or Join. Enter the session id in the Inspector.");
                return;
            }

            try
            {
                var options = BuildSessionOptions();
                var session = await m_MultiplayerService.CreateOrJoinSessionAsync(sessionId, options);
                m_MultiplayerSession.SetSession(session);
                m_Events.SuccessfulExecution?.Invoke(session);
            }
            catch (SessionException e)
            {
                InvokeFailed(e.Message);
            }
            catch (Exception e)
            {
                InvokeFailed(e.Message);
            }
        }

        private async Task ExecuteJoinAsync()
        {
            var joinOptions = BuildJoinSessionOptions();
            try
            {
                ISession session;
                if (m_JoinSessionOptions.JoinMode == JoinSessionMode.ById)
                {
                    var sessionId = string.IsNullOrWhiteSpace(m_JoinSessionOptions.SessionId)
                        ? null
                        : m_JoinSessionOptions.SessionId.Trim();
                    if (string.IsNullOrEmpty(sessionId))
                    {
                        InvokeFailed("Session ID is required when joining by ID.");
                        return;
                    }

                    session = await m_MultiplayerService.JoinSessionByIdAsync(sessionId, joinOptions);
                }
                else
                {
                    var sessionCode = string.IsNullOrWhiteSpace(m_JoinSessionOptions.SessionCode)
                        ? null
                        : m_JoinSessionOptions.SessionCode.Trim();
                    if (string.IsNullOrEmpty(sessionCode))
                    {
                        InvokeFailed("Session code is required when joining by code.");
                        return;
                    }

                    session = await m_MultiplayerService.JoinSessionByCodeAsync(sessionCode, joinOptions);
                }

                m_MultiplayerSession.SetSession(session);
                m_Events.SuccessfulExecution?.Invoke(session);
            }
            catch (SessionException e)
            {
                InvokeFailed(e.Message);
            }
            catch (Exception e)
            {
                InvokeFailed(e.Message);
            }
        }

        private async Task ExecuteQuickJoinAsync()
        {
            var quickJoinOptions = GetQuickJoinOptions();
            var sessionOptions = BuildSessionOptions();
            try
            {
                var session = await m_MultiplayerService.MatchmakeSessionAsync(quickJoinOptions, sessionOptions);
                m_MultiplayerSession.SetSession(session);
                m_Events.SuccessfulExecution?.Invoke(session);
            }
            catch (SessionException e)
            {
                InvokeFailed(e.Message);
            }
            catch (Exception e)
            {
                InvokeFailed(e.Message);
            }
        }

        private Multiplayer.JoinSessionOptions BuildJoinSessionOptions()
        {
            var joinOptions = new Multiplayer.JoinSessionOptions
            {
                Type = string.IsNullOrWhiteSpace(SessionType) ? null : SessionType,
                Password = string.IsNullOrWhiteSpace(m_JoinSessionOptions.Password)
                    ? null
                    : m_JoinSessionOptions.Password.Trim()
            };
            return joinOptions;
        }

        private SessionOptions BuildSessionOptions()
        {
            var sessionOptions = m_CreateSessionOptions;
            var options = new SessionOptions
            {
                Type = string.IsNullOrWhiteSpace(SessionType) ? Guid.NewGuid().ToString() : SessionType,
                Name =
                    string.IsNullOrWhiteSpace(sessionOptions.SessionName)
                        ? Guid.NewGuid().ToString()
                        : sessionOptions.SessionName,
                MaxPlayers = sessionOptions.MaxPlayers,
                IsPrivate = sessionOptions.IsPrivate,
                IsLocked = sessionOptions.IsLocked,
                Password = string.IsNullOrWhiteSpace(m_CreateSessionOptions.Password)
                    ? null
                    : m_CreateSessionOptions.Password.Trim()
            };

            if (!m_SessionNetworkSettings.CreateNetwork ||
                m_SessionNetworkSettings.Network == SessionNetworkSettings.NetworkType.None)
            {
                return options;
            }

            switch (m_SessionNetworkSettings.Network)
            {
                case SessionNetworkSettings.NetworkType.Direct:
                    options = options.WithDirectNetwork(m_SessionNetworkSettings.DirectIPPort.ListenIp,
                        m_SessionNetworkSettings.DirectIPPort.PublishIp,
                        m_SessionNetworkSettings.DirectIPPort.Port);
                    break;
                case SessionNetworkSettings.NetworkType.Relay:
                    options = options.WithNetworkOptions(new NetworkOptions()
                        {
                            RelayProtocol = m_SessionNetworkSettings.RelayOptions.Protocol
                        })
                        .WithRelayNetwork(m_SessionNetworkSettings.RelayOptions.ToRelayNetworkOptions());
                    break;
            }

            return options;
        }

        private QuickJoinOptions BuildQuickJoinOptions()
        {
            return GetQuickJoinOptions();
        }

        private bool IsValid(MultiplayerSession multiplayerSession)
        {
            if (multiplayerSession is not null)
            {
                return true;
            }

            InvokeFailed("Multiplayer Session must not be null.");
            return false;
        }

        private bool IsValid(IUnityServices registry)
        {
            if (registry is not null)
            {
                return true;
            }

            InvokeFailed("Unity Services have not been initialized yet, use UnityServices.InitializeAsync() first.");
            return false;
        }
    }
}
