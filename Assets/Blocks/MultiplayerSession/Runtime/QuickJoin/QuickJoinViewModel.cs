using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unity.Properties;
using Unity.Services.Multiplayer;
using Unity.Services.Multiplayer.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Blocks.Sessions
{
    public class QuickJoinViewModel : INotifyBindablePropertyChanged, IDataSourceViewHashProvider, IDisposable
    {
        SessionObserver m_SessionObserver;
        ISession m_Session;
        long m_UpdateVersion;

        /// <summary>
        /// This property is bound to <see cref="QuickJoinButton.enabledSelf"/> so that the button is enabled or disabled
        /// based on whether a session can be joined. <br/>
        /// It is a property using [CreateProperty] attribute to allow for data binding in UIToolkit
        /// and using <see cref="Notify"/> to optimize UIToolkit redraws.
        /// </summary>
        [CreateProperty]
        public bool CanClickButton
        {
            get => m_CanClickButton;
            set
            {
                if (m_CanClickButton == value)
                {
                    return;
                }

                m_CanClickButton = value;
                ++m_UpdateVersion;
                Notify();
            }
        }

        bool m_CanClickButton = true;

        public QuickJoinViewModel(string sessionType)
        {
            m_SessionObserver = new SessionObserver(sessionType);
            m_SessionObserver.AddingSessionStarted += OnAddingSessionStarted;
            m_SessionObserver.SessionAdded += OnSessionAdded;
            m_SessionObserver.AddingSessionFailed += OnAddingSessionFailed;

            if (m_SessionObserver.Session != null)
            {
                OnSessionAdded(m_SessionObserver.Session);
            }
        }

        void OnAddingSessionFailed(AddingSessionOptions session, Exception exception)
        {
            CanClickButton = true;
        }

        void OnAddingSessionStarted(AddingSessionOptions sessionOptions)
        {
            CanClickButton = false;
        }

        void OnSessionAdded(ISession newSession)
        {
            m_Session = newSession;
            m_Session.RemovedFromSession += OnSessionRemoved;
            m_Session.Deleted += OnSessionRemoved;
            CanClickButton = false;
        }

        void OnSessionRemoved()
        {
            CanClickButton = true;
            CleanupSession();
        }

        void CleanupSession()
        {
            m_Session.RemovedFromSession -= OnSessionRemoved;
            m_Session.Deleted -= OnSessionRemoved;
            m_Session = null;
        }

        public bool AreMultiplayerServicesInitialized()
        {
            return MultiplayerService.Instance != null;
        }

        public async Task<ISession> MatchmakeSessionAsync(SessionConnector connector)
        {
            try
            {           
                connector.WithQuickJoin();
                connector.Execute();
                return connector.MultiplayerSession.Session;
            }
            catch (Exception exception) 
            { 
                Debug.LogError(exception.Message);
                return await Task.FromResult<ISession>(null);
            }
            
        }

        public Task CreateOrJoinSessionAsync(SessionConnector connector)
        {
            connector.Execute();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (m_SessionObserver != null)
            {
                m_SessionObserver.Dispose();
                m_SessionObserver = null;
            }

            if (m_Session != null)
            {
                CleanupSession();
            }
        }

        /// <summary>
        /// This method is used by UIToolkit to determine if any data bound to the UI has changed.
        /// Instead of hashing the data, an m_UpdateVersion counter is incremented when changes occur.
        /// </summary>
        public long GetViewHashCode() => m_UpdateVersion;

        /// <summary>
        /// Suggested implementation of INotifyBindablePropertyChanged from UIToolkit.
        /// </summary>
        public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

        void Notify([CallerMemberName] string property = null)
        {
            propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(property));
        }
    }
}
