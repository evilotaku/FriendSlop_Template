namespace Unity.Services.Multiplayer.Components
{
    /// <summary>
    /// The join settings configured on a <see cref="SessionConnector"/>,
    /// as returned by <see cref="SessionConnector.GetJoinOptions"/>.
    /// </summary>
    public readonly struct JoinOptions
    {
        /// <summary>Whether the connector joins by session ID or by join code.</summary>
        public readonly JoinSessionMode Mode;

        /// <summary>
        /// The session ID when <see cref="Mode"/> is <see cref="JoinSessionMode.ById"/>,
        /// or <c>null</c> when joining by code.
        /// </summary>
        public readonly string SessionId;

        /// <summary>
        /// The join code when <see cref="Mode"/> is <see cref="JoinSessionMode.ByCode"/>,
        /// or <c>null</c> when joining by session ID.
        /// </summary>
        public readonly string SessionCode;

        /// <summary>The optional password, or <c>null</c> if none is set.</summary>
        public readonly string Password;

        internal JoinOptions(JoinSessionMode mode, string sessionId, string sessionCode, string password)
        {
            Mode        = mode;
            SessionId   = sessionId;
            SessionCode = sessionCode;
            Password    = password;
        }
    }
}
