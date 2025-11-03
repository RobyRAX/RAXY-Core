using System;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RAXY.Core
{
    public interface IBootstrapper
    {
        /// <summary>
        /// The bool indicates whether it's the first initialization.
        /// </summary>
        public event Action<bool> OnInitDone;
        public bool IsInitDone_FirstTime { get; set; }
        public bool IsInitDone { get; set; }

        public UniTask InitializeAsync_FirstTime();
    }
    
    public interface IBootstrapper<T> : IBootstrapper
    {
        public static T Instance { get; set; }
    }

#if UNITY_EDITOR
    [HideReferenceObjectPicker]
    public class IBootstrapperDrawer
    {
        public IBootstrapper Bootstrapper;

        [ShowInInspector]
        [TableColumnWidth(75, false)]
        public bool FirstInitDone => Bootstrapper.IsInitDone_FirstTime;

        [ShowInInspector]
        [TableColumnWidth(75, false)]
        public bool InitDone => Bootstrapper.IsInitDone;
    }
#endif
}
