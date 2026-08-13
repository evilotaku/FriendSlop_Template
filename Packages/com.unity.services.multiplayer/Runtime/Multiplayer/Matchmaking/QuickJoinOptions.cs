using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Services.Multiplayer
{
    /// <summary>
    /// Options to provide quick join filters, timeout and backup flow
    /// </summary>
    [Serializable]
    public class QuickJoinOptions
    {
        /// <summary>
        /// The filters to apply to the session quick join
        /// </summary>
        public List<FilterOption> Filters { get; set; } = new List<FilterOption>();

        /// <summary>
        /// The matchmaking timeout in seconds.
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        /// Determines if a session is created if none is found
        /// </summary>
        public bool CreateSession { get; set; }
    }
}
