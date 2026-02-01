using System;
using RAXY.Utility;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RAXY.Core
{
    public class BootstrapPauser : MonoBehaviour
    {
        private static Lazy<BootstrapPauser> _instance = new Lazy<BootstrapPauser>(
            () =>
            {
                var existing = FindAnyObjectByType<BootstrapPauser>();
                if (existing != null)
                    return existing;

                var gameObject = new GameObject(nameof(BootstrapPauser));
                var instance = gameObject.AddComponent<BootstrapPauser>();
                DontDestroyOnLoad(gameObject);
                return instance;
            }
        );

        public static BootstrapPauser Instance => _instance.Value;

        bool _isPaused;
        [ShowInInspector]
        public static bool IsPaused
        {
            get => Instance._isPaused;
            private set => Instance._isPaused = value;
        }

        [Button]
        public static void Pause()
        {
            IsPaused = true;
            CustomDebug.Log("[Bootstrap Pauser] PAUSE");
        }

        [Button]
        public static void Resume()
        {
            IsPaused = false;
            CustomDebug.Log("[Bootstrap Pauser] RESUME");
        }
    }
}
