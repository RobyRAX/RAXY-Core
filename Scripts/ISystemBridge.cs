using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RAXY.Core
{
    public interface ISystemBridge
    {
        public IBridgeable SystemA { get; }
        public IBridgeable SystemB { get; }
        public void InitBridge();
        public bool FirstInitDone { get; set; }

        public bool WaitForInitDone_SystemA { get; }
        public bool WaitForInitDone_SystemB { get; }
    }

    public interface IBridgeable
    {
        public bool FirstInitDone { get; set; }
    }

#if UNITY_EDITOR
    [Serializable]
    public class ISystemBridgeDrawer
    {
        public ISystemBridge SystemBridge;

        [HorizontalGroup("System A")]
        [ShowInInspector]
        public IBridgeable SystemA
        {
            get
            {
                if (SystemBridge == null)
                    return null;

                if (SystemBridge.SystemA == null)
                    return null;

                return SystemBridge.SystemA;
            }
        }

        [HorizontalGroup("System A")]
        [ShowInInspector]
        [ToggleLeft]
        public bool SystemA_InitDone
        {
            get
            {
                if (SystemA == null)
                    return false;

                return SystemA.FirstInitDone;
            }
        }

        [HorizontalGroup("System B")]
        [ShowInInspector]
        public IBridgeable SystemB
        {
            get
            {
                if (SystemBridge == null)
                    return null;

                if (SystemBridge.SystemB == null)
                    return null;

                return SystemBridge.SystemB;
            }
        }

        [HorizontalGroup("System B")]
        [ShowInInspector]
        [ToggleLeft]
        public bool SystemB_InitDone
        {
            get
            {
                if (SystemB == null)
                    return false;

                return SystemB.FirstInitDone;
            }
        }

        [ShowInInspector]
        public bool InitDone
        {
            get
            {
                if (SystemBridge == null)
                    return false;

                return SystemBridge.FirstInitDone;
            }
        }
    }
#endif
}
