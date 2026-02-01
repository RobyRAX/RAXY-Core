using System;
using System.Collections.Generic;
using System.Linq;
using RAXY.Utility;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;
using Cysharp.Threading.Tasks;

#if UNITY_EDITOR
using Sirenix.Utilities.Editor;
using UnityEditor;
#endif

namespace RAXY.Core
{
    public abstract class SepManagerBase<T> : Singleton<T> where T : MonoBehaviour
    {
        public const string UNDEFINED_GROUP_ID = "Undefined";

        [TitleGroup("Sep Groups")]
        [ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = "Label")]
        [FormerlySerializedAs("SepGroups")]
        [HideInPlayMode]
        public List<SepGroupEntry> PreDefinedSepGroups = new();

        [TitleGroup("Runtime")]
        [ShowInInspector]
        [ListDrawerSettings(DraggableItems = false)]
        [HideInEditorMode]
        public List<SepGroupRuntime> RuntimeSepGroups { get; set; }

        [TitleGroup("Runtime")]
        [Button]
        [HideInEditorMode]
        public void InitPreDefinedToRuntime()
        {
            RuntimeSepGroups = new();
            foreach (var groupEntry in PreDefinedSepGroups)
            {
                var newGroup = new SepGroupRuntime(groupEntry);

                if (!RuntimeSepGroups.Exists(x => x.GroupName == newGroup.GroupName))
                    RuntimeSepGroups.Add(newGroup);
            }

            var newEntry = new SepGroupEntry()
            {
                GroupName = UNDEFINED_GROUP_ID,
                ExecutionType = SepGroupExecutionType.Sequential
            };
            var undefinedGroup = new SepGroupRuntime(newEntry);

            if (!RuntimeSepGroups.Exists(x => x.GroupName == UNDEFINED_GROUP_ID))
                RuntimeSepGroups.Add(undefinedGroup);
            
            CustomDebug.Log($"<color=yellow>[{gameObject.name}]</color> PreDefined Group Applied");
        }

        [HorizontalGroup("Runtime/Op")]
        [Button]
        [HideInEditorMode]
        public void RegisterSepObject(ISepObject sepObject)
        {
            var selected = GetSepGroup(sepObject);
            if (selected == null)
                return;

            selected.AddSepObject(sepObject);
        }

        [HorizontalGroup("Runtime/Op")]
        [Button]
        [HideInEditorMode]
        public void UnregisterSepObject(ISepObject sepObject)
        {
            var selected = GetSepGroup(sepObject);
            if (selected == null)
                return;

            selected.RemoveSepObject(sepObject);
        }

        SepGroupRuntime GetSepGroup(ISepObject sepObject)
        {
            if (RuntimeSepGroups == null || RuntimeSepGroups.Count <= 0)
                InitPreDefinedToRuntime();

            string sepObjectGroup = sepObject.SepGroup;
            SepGroupRuntime selectedGroup = RuntimeSepGroups.Find(x => x.GroupName == sepObjectGroup);

            if (selectedGroup == null)
                selectedGroup = RuntimeSepGroups.Find(x => x.GroupName == UNDEFINED_GROUP_ID);

            return selectedGroup;
        }

        [SerializeField]
        [PropertyOrder(-2)]
        protected bool _initAllSepGroupOnStart;

        protected override void Awake()
        {
            base.Awake();
            InitPreDefinedToRuntime();
        }

        protected virtual void Start()
        {
            if (_initAllSepGroupOnStart)
                InitAllSepGroup().Forget();
        }

        protected async UniTask InitAllSepGroup()
        {
            if (RuntimeSepGroups == null || RuntimeSepGroups.Count <= 0)
                InitPreDefinedToRuntime();

            foreach (var runtimeGroup in RuntimeSepGroups)
            {
                await InitSepGroup(runtimeGroup);
                await UniTask.Yield();
            }
        }

        /// <summary>
        /// Initializes a list of ISepObject asynchronously (with error protection).
        /// Executes all PreInit() calls first, then all Init() calls.
        /// </summary>
        protected async UniTask InitSepGroup(SepGroupRuntime sepGroup)
        {
            await UniTask.Yield();

            List<ISepObject> sepObjectsToInit = sepGroup.SepObjects;

            if (sepObjectsToInit == null || sepObjectsToInit.Count == 0)
                return;

            // Filter objects that need PreInit
            var objectsNeedingPreInit = sepObjectsToInit.Where(obj => obj != null && obj.UsePreInit).ToList();
            
            // Execute all PreInit() calls first
            if (objectsNeedingPreInit.Count > 0)
            {
                await ExecutePreInitPhase(objectsNeedingPreInit, sepGroup.ExecutionType);
            }

            // Then execute all Init() calls
            await ExecuteInitPhase(sepObjectsToInit, sepGroup.ExecutionType);
        }

        private async UniTask ExecutePreInitPhase(List<ISepObject> objects, SepGroupExecutionType executionType)
        {
            if (executionType == SepGroupExecutionType.Sequential)
            {
                foreach (var obj in objects)
                {
                    if (obj == null)
                        continue;

                    try
                    {
                        await UniTask.WaitUntil(() => BootstrapPauser.IsPaused == false);
                        CustomDebug.Log($"<color=cyan>[{obj.GetGameObject.name}]</color> PreInit Start");
                        await obj.PreInit();
                        await UniTask.Yield();
                        CustomDebug.Log($"<color=cyan>[{obj.GetGameObject.name}]</color> PreInit Done");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[SEP PREINIT ERROR] {obj}: {e}");
                    }
                }
            }
            else // Parallel
            {
                var tasks = new List<UniTask>();

                foreach (var obj in objects)
                {
                    if (obj == null)
                        continue;

                    var target = obj;
                    var safeName = target?.GetType().Name ?? "Unknown";

                    tasks.Add(UniTask.Create(async () =>
                    {
                        try
                        {
                            CustomDebug.Log($"<color=cyan>[{target.GetGameObject.name}]</color> PreInit Start");
                            await target.PreInit();
                            CustomDebug.Log($"<color=cyan>[{target.GetGameObject.name}]</color> PreInit Done");
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[SEP PREINIT ERROR] {safeName}: {e}");
                        }
                    }));
                }

                try
                {
                    await UniTask.WaitUntil(() => BootstrapPauser.IsPaused == false);
                    await UniTask.WhenAll(tasks);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SEP PREINIT PARALLEL ERROR] {e}");
                }
            }
        }

        private async UniTask ExecuteInitPhase(List<ISepObject> objects, SepGroupExecutionType executionType)
        {
            if (executionType == SepGroupExecutionType.Sequential)
            {
                foreach (var obj in objects)
                {
                    if (obj == null)
                        continue;

                    try
                    {
                        await UniTask.WaitUntil(() => BootstrapPauser.IsPaused == false);
                        CustomDebug.Log($"<color=yellow>[{obj.GetGameObject.name}]</color> Init Start");
                        await obj.Init();
                        await UniTask.Yield();
                        CustomDebug.Log($"<color=yellow>[{obj.GetGameObject.name}]</color> Init Done");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[SEP INIT ERROR] {obj}: {e}");
                    }
                }
            }
            else // Parallel
            {
                var tasks = new List<UniTask>();

                foreach (var obj in objects)
                {
                    if (obj == null)
                        continue;

                    var target = obj;
                    var safeName = target?.GetType().Name ?? "Unknown";

                    tasks.Add(UniTask.Create(async () =>
                    {
                        try
                        {
                            CustomDebug.Log($"<color=yellow>[{obj.GetGameObject.name}]</color> Init Start");
                            await target.Init();
                            CustomDebug.Log($"<color=yellow>[{target.GetGameObject.name}]</color> Init Done");
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[SEP INIT ERROR] {safeName}: {e}");
                        }
                    }));
                }

                try
                {
                    await UniTask.WaitUntil(() => BootstrapPauser.IsPaused == false);
                    await UniTask.WhenAll(tasks);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SEP INIT PARALLEL ERROR] {e}");
                }
            }
        }
    }

    public enum SepGroupExecutionType
    {
        Sequential,
        Parallel
    }

    [Serializable]
    public class SepGroupEntry
    {
        public string GroupName;
        [FormerlySerializedAs("GroupType")]
        public SepGroupExecutionType ExecutionType;

        public string Label => $"{GroupName} - {ExecutionType}";

#if UNITY_EDITOR
        [ListDrawerSettings(ElementColor = "GetElementColor", OnTitleBarGUI = "DrawRefreshBtn", DraggableItems = false)]
        [LabelText("Order  | Pre Init | Sep Objects")]
        [InfoBox("Non SEP Objects detected!", InfoMessageType = InfoMessageType.Error, VisibleIf = "HasInvalidObjects")]
#endif
        public List<SepObjectEntry> SepObjects = new();

#if UNITY_EDITOR
        /// <summary>
        /// Checks if any object in the list is invalid or missing ISepObject.
        /// </summary>
        private bool HasInvalidObjects
        {
            get
            {
                if (SepObjects == null || SepObjects.Count == 0)
                    return false;

                foreach (var sepObj in SepObjects)
                {
                    if (sepObj == null || sepObj.SepObject == null)
                        return true;

                    if (!sepObj.SepObject.TryGetComponent<ISepObject>(out _))
                        return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Refresh button shown in Odin title bar to reorder entries by Order.
        /// </summary>
        private void DrawRefreshBtn()
        {
            if (SirenixEditorGUI.ToolbarButton(EditorIcons.Refresh))
            {
                Refresh();
            }
        }

        #region List Element Color
        /// <summary>
        /// Colorizes list rows: normal, invalid, or missing SEP component.
        /// </summary>
        private Color GetElementColor(int index)
        {
            bool isEven = (index % 2 == 0);
            var entry = SepObjects[index];
            bool isDarkTheme = EditorGUIUtility.isProSkin;

            // Invalid or missing entry
            if (entry == null || entry.SepObject == null)
                return GetReddishColor(isEven, isDarkTheme);

            // Missing ISepObject component
            if (!entry.SepObject.TryGetComponent<ISepObject>(out _))
                return GetReddishColor(isEven, isDarkTheme);

            // Valid entry
            return GetNeutralColor(isEven, isDarkTheme);
        }
        private Color GetNeutralColor(bool isEven, bool dark)
        {
            if (dark)
                return isEven ? new Color(0.219f, 0.219f, 0.219f) : new Color(0.192f, 0.192f, 0.192f);
            else
                return isEven ? new Color(0.925f, 0.925f, 0.925f) : new Color(0.961f, 0.961f, 0.961f);
        }
        private Color GetReddishColor(bool isEven, bool dark)
        {
            if (dark)
                return isEven ? new Color(0.28f, 0.15f, 0.15f) : new Color(0.25f, 0.12f, 0.12f);
            else
                return isEven ? new Color(1.0f, 0.88f, 0.88f) : new Color(0.97f, 0.82f, 0.82f);
        }
        #endregion

        public void Refresh()
        {
            if (SepObjects == null || SepObjects.Count == 0)
                return;

            SepObjects.Sort((a, b) => a.Order.CompareTo(b.Order));
        }
#endif
    }

    [Serializable]
    public class SepObjectEntry
    {
        [HorizontalGroup(50)]
        [HideLabel]
        public int Order;

        [HorizontalGroup(50)]
        [LabelText(" ")]
        [LabelWidth(15)]
        public bool UsePreInit;

        [HorizontalGroup]
        [HideLabel]
        public GameObject SepObject;
    }

    [HideReferenceObjectPicker]
    public class SepGroupRuntime
    {
        SepGroupEntry _entry;

        [ShowInInspector]
        public string GroupName => _entry.GroupName;

        [ShowInInspector]
        public SepGroupExecutionType ExecutionType => _entry.ExecutionType;

        public List<ISepObject> SepObjects { get; set; }

#if UNITY_EDITOR
        [ShowInInspector]
        [TableList(ShowIndexLabels = true)]
        List<ISepObjectDrawer> _allSepObjectsDrawer
        {
            get
            {
                if (SepObjects == null)
                    return null;

                List<ISepObjectDrawer> temp = new List<ISepObjectDrawer>();

                foreach (var sepObj in SepObjects)
                {
                    temp.Add(new ISepObjectDrawer() { SepObject = sepObj });
                }

                return temp;
            }
        }
#endif

        public SepGroupRuntime() { }
        public SepGroupRuntime(SepGroupEntry entry)
        {
            SepObjects = new();
            _entry = entry;

            foreach (var objectEntry in _entry.SepObjects)
            {
                var sepObj = objectEntry.SepObject.GetComponent<ISepObject>();
                if (sepObj == null)
                    continue;

                sepObj.Order = objectEntry.Order;
                sepObj.UsePreInit = objectEntry.UsePreInit;
                sepObj.SepGroup = GroupName;

                SepObjects.Add(sepObj);
            }
        }

        public void Refresh()
        {
            if (SepObjects == null || SepObjects.Count == 0)
                return;

            SepObjects.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

        public void AddSepObject(ISepObject sepObject)
        {
            if (SepObjects.Contains(sepObject))
                return;

            SepObjects.Add(sepObject);
            Refresh();
        }

        public void RemoveSepObject(ISepObject sepObject)
        {
            if (SepObjects.Contains(sepObject) == false)
                return;

            SepObjects.Remove(sepObject);
            Refresh();
        }
    }
}
