using System;
using UnityEngine;

namespace Unity.Services.Multiplayer.Components
{
    /// <summary>
    /// Settings for quick join (timeout, create session fallback).
    /// </summary>
    [Serializable]
    internal class QuickJoinSettings
    {
        [Tooltip("The matchmaking timeout in seconds.")]
        [SerializeField]
        private float m_Timeout = 5f;

        [Tooltip("Determines if a session is created if none is found.")]
        [SerializeField]
        private bool m_CreateSession = true;

        /// <summary>
        /// The matchmaking timeout in seconds.
        /// </summary>
        public float Timeout { get => m_Timeout; set => m_Timeout = value; }

        /// <summary>
        /// Whether a session should be created if none is found during quick join.
        /// </summary>
        public bool CreateSession { get => m_CreateSession; set => m_CreateSession = value; }
    }
}
