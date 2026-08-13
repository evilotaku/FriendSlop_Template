using System;
using UnityEngine;

namespace Unity.Services.Multiplayer.Components
{
    /// <summary>
    /// Options for relay connections.
    /// </summary>
    [Serializable]
    internal sealed class RelayOptions
    {
        [Tooltip("Relay transport protocol: UDP, DTLS, or WSS.")] [SerializeField]
        private RelayProtocol m_Protocol = RelayProtocol.DTLS;

        [Tooltip("Optional region for the relay server (e.g. \"eu\", \"us\"). Leave empty for default.")]
        [SerializeField]
        private string m_Region;

        [Tooltip("When true, reuses the same region when reallocating relay during host migration.")] [SerializeField]
        private bool m_PreserveRegion;

        /// <summary>
        /// Transport protocol.
        /// </summary>
        public RelayProtocol Protocol { get => m_Protocol; set => m_Protocol = value; }

        /// <summary>
        /// Relay region. Leave empty for default.
        /// </summary>
        public string Region { get => m_Region; set => m_Region = value; }

        /// <summary>
        /// When true, keeps the same region on host migration.
        /// </summary>
        public bool PreserveRegion { get => m_PreserveRegion; set => m_PreserveRegion = value; }

        /// <summary>
        /// Returns relay options for session creation.
        /// </summary>
        public RelayNetworkOptions ToRelayNetworkOptions()
        {
            return new RelayNetworkOptions(
                string.IsNullOrWhiteSpace(m_Region) ? null : m_Region.Trim(),
                m_PreserveRegion);
        }
    }
}
