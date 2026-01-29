using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using System;
using UnityEngine;

namespace RAXY.Core
{
    public interface ISepObject
    {
        public UniTask PreInit();
        public UniTask Init();
        public GameObject GetGameObject { get; }
        public bool FirstInitDone { get; set; }

        public int Order { get; set; } 
        public string SepGroup { get; set; }
        public bool UsePreInit { get; set; }
    }

#if UNITY_EDITOR
    [HideReferenceObjectPicker]
    public class ISepObjectDrawer
    {
        public ISepObject SepObject;

        [ShowInInspector]
        [TableColumnWidth(50, false)]
        public int Order => SepObject.Order;

        [ShowInInspector]
        [TableColumnWidth(75, false)]
        public bool InitDone => SepObject.FirstInitDone;
    }
#endif
}