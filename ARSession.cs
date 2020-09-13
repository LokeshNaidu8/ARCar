using System;
using System.Collections;
using UnityEngine.XR.ARSubsystems;

namespace UnityEngine.XR.ARFoundation
{
    
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(ARUpdateOrder.k_Session)]
    [HelpURL("https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@latest?preview=1&subfolder=/api/UnityEngine.XR.ARFoundation.ARSession.html")]
    public sealed class ARSession : SubsystemLifecycleManager<XRSessionSubsystem, XRSessionSubsystemDescriptor>
    {
        [SerializeField]
        [Tooltip("If enabled, the session will attempt to update a supported device if its AR software is out of date.")]
        bool m_AttemptUpdate = true;
        public bool attemptUpdate
        {
            get { return m_AttemptUpdate; }
            set { m_AttemptUpdate = value; }
        }

        [SerializeField]
        [Tooltip("If enabled, the Unity frame will be synchronized with the AR session. Otherwise, the AR session will be updated independently of the Unity frame.")]
        bool m_MatchFrameRate = true;

<c>WaitForTargetFPS</c> at the
<c>ARSession.Update</c>
        
        public bool matchFrameRate
        {
            get
            {
                return m_MatchFrameRate;
            }

            set
            {
                if (m_MatchFrameRate == value)
                    return;

                if (descriptor != null)
                {
                    // At runtime
                    SetMatchFrameRateEnabled(value);
                }
                else
                {
                    // In the Editor, or if there is no subsystem
                    m_MatchFrameRate = value;
                }
            }
        }

        public static ARSessionState state
        {
            get { return s_State; }
            private set
            {
                if (s_State == value)
                    return;

                s_State = value;

                UpdateNotTrackingReason();

                if (stateChanged != null)
                    stateChanged(new ARSessionStateChangedEventArgs(state));
            }
        }
        public static NotTrackingReason notTrackingReason
        {
            get { return s_NotTrackingReason; }
        }

        public void Reset()
        {
            if (subsystem != null)
                subsystem.Reset();

            if (state > ARSessionState.Ready)
                state = ARSessionState.SessionInitializing;
        }

        void SetMatchFrameRateEnabled(bool enabled)
        {
            if (descriptor.supportsMatchFrameRate)
                subsystem.matchFrameRate = enabled;

            m_MatchFrameRate = subsystem.matchFrameRate;

            if (m_MatchFrameRate)
            {
                Application.targetFrameRate = subsystem.frameRate;
                QualitySettings.vSyncCount = 0;
            }
        }

        void WarnIfMultipleARSessions()
        {
            var sessions = FindObjectsOfType<ARSession>();
            if (sessions.Length > 1)
            {       
                string sessionNames = "";
                foreach (var session in sessions)
                {
                    sessionNames += string.Format("\t{0}\n", session.name);
                }

                Debug.LogWarningFormat(
                    "Multiple active AR Sessions found. " +
                    "These will conflict with each other, so " +
                    "you should only have one active ARSession at a time. " +
                    "Found these active sessions:\n{0}", sessionNames);
            }
        }
        
        public static IEnumerator CheckAvailability()
        {
            while (state == ARSessionState.CheckingAvailability)
            {
                yield return null;
            }

            // Availability has already been determined if we make it here and the state is not None.
            if (state != ARSessionState.None)
                yield break;
            s_Instance.CreateSubsystemIfNecessary();

            if (s_Instance.subsystem == null)
            {
                state = ARSessionState.Unsupported;
            }
            else if (state == ARSessionState.None)
            {
                state = ARSessionState.CheckingAvailability;
                var availabilityPromise = s_Instance.subsystem.GetAvailabilityAsync();
                yield return availabilityPromise;
                s_Availability = availabilityPromise.result;

                if (s_Availability.IsSupported() && s_Availability.IsInstalled())
                {
                    state = ARSessionState.Ready;
                }
                else if (s_Availability.IsSupported() && !s_Availability.IsInstalled())
                {
                    state = s_Instance.subsystem.SubsystemDescriptor.supportsInstall ? ARSessionState.NeedsInstall : ARSessionState.Unsupported;
                }
                else
                {
                    state = ARSessionState.Unsupported;
                }
            }
        }
        /// <returns>An <c>IEnumerator</c> used for a coroutine.</returns>
        public static IEnumerator Install()
        {
            while ((state == ARSessionState.Installing) || (state == ARSessionState.CheckingAvailability))
            {
                yield return null;
            }

            switch (state)
            {
                case ARSessionState.Installing:
                case ARSessionState.NeedsInstall:
                    break;
                case ARSessionState.None:
                    throw new InvalidOperationException("Cannot install until availability has been determined. Have you called CheckAvailability()?");
                case ARSessionState.Ready:
                case ARSessionState.SessionInitializing:
                case ARSessionState.SessionTracking:
                    yield break;
                case ARSessionState.Unsupported:
                    throw new InvalidOperationException("Cannot install because XR is not supported on this platform.");
            }

            if (s_Instance.subsystem == null)
                throw new InvalidOperationException("The subsystem was destroyed while attempting to install AR software.");

            state = ARSessionState.Installing;
            var installPromise = s_Instance.subsystem.InstallAsync();
            yield return installPromise;
            var installStatus = installPromise.result;

            switch (installStatus)
            {
                case SessionInstallationStatus.Success:
                    state = ARSessionState.Ready;
                    s_Availability = (s_Availability | SessionAvailability.Installed);
                    break;
                case SessionInstallationStatus.ErrorUserDeclined:
                    state = ARSessionState.NeedsInstall;
                    break;
                default:
                    state = ARSessionState.Unsupported;
                    break;
            }
        }
        protected override void OnEnable()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            WarnIfMultipleARSessions();
#endif
            CreateSubsystemIfNecessary();

            if (subsystem != null)
                StartCoroutine(Initialize());
        }

        IEnumerator Initialize()
        {
            // Make sure we've checked for availability
            if (state <= ARSessionState.CheckingAvailability)
                yield return CheckAvailability();

            // Make sure we didn't get disabled while checking for availability
            if (!enabled)
                yield break;

            // Complete install if necessary
            if (((state == ARSessionState.NeedsInstall) && attemptUpdate) ||
                (state == ARSessionState.Installing))
            {
                yield return Install();
            }

            // If we're still enabled and everything is ready, then start.
            if (state == ARSessionState.Ready && enabled)
            {
                StartSubsystem();
            }
            else
            {
                enabled = false;
            }
        }

        void StartSubsystem()
        {
            SetMatchFrameRateEnabled(m_MatchFrameRate);
            subsystem.Start();
        }

        void Awake()
        {
            s_Instance = this;
            s_NotTrackingReason = NotTrackingReason.None;
        }

        void Update()
        {
            if (subsystem != null && subsystem.running)
            {
                subsystem.Update(new XRSessionUpdateParams
                {
                    screenOrientation = Screen.orientation,
                    screenDimensions = new Vector2Int(Screen.width, Screen.height)
                });

                switch (subsystem.trackingState)
                {
                    case TrackingState.None:
                    case TrackingState.Limited:
                        state = ARSessionState.SessionInitializing;
                        break;
                    case TrackingState.Tracking:
                        state = ARSessionState.SessionTracking;
                        break;
                }
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (subsystem == null)
                return;

            if (paused)
                subsystem.OnApplicationPause();
            else
                subsystem.OnApplicationResume();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            // Only set back to ready if we were previously running
            if (state > ARSessionState.Ready)
                state = ARSessionState.Ready;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            // Only set back to ready if we were previously running
            if (state > ARSessionState.Ready)
                state = ARSessionState.Ready;

            s_Instance = null;
        }

        static void UpdateNotTrackingReason()
        {
            switch (state)
            {
                case ARSessionState.None:
                case ARSessionState.SessionInitializing:
                    s_NotTrackingReason = (s_Instance == null || s_Instance.subsystem == null) ?
                        NotTrackingReason.Unsupported : s_Instance.subsystem.notTrackingReason;
                    break;
                case ARSessionState.Unsupported:
                    s_NotTrackingReason = NotTrackingReason.Unsupported;
                    break;
                case ARSessionState.CheckingAvailability:
                case ARSessionState.NeedsInstall:
                case ARSessionState.Installing:
                case ARSessionState.Ready:
                case ARSessionState.SessionTracking:
                    s_NotTrackingReason = NotTrackingReason.None;
                    break;
            }
        }
        static ARSessionState s_State;
        static NotTrackingReason s_NotTrackingReason;
        static SessionAvailability s_Availability;
        static ARSession s_Instance;
    }
}
