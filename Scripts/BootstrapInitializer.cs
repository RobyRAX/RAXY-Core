using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using RAXY.Utility;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RAXY.Core
{
    public class BootstrapInitializer : MonoBehaviour
    {
        [TitleGroup("Bootstrap Settings")]
        public bool useAddressables;

        [TitleGroup("Bootstrap Settings")]
        [ShowIf("@useAddressables")]
        [ListDrawerSettings(ElementColor = "GetElementColor_Addressable", ShowIndexLabels = true)]
        [InfoBox("Non Bootstrapper reference detected!", InfoMessageType = InfoMessageType.Error, VisibleIf = "HasInvalidObjects_Addressable")]
        public List<AssetReferenceGameObject> addressableBootstrappers = new();

        [TitleGroup("Bootstrap Settings")]
        [SerializeField]
        [ReadOnly]
        [ShowIf("@useAddressables")]
        List<string> _bootstrapperNames;

        [TitleGroup("Bootstrap Settings")]
        [HideIf("@useAddressables")]
        [ListDrawerSettings(ElementColor = "GetElementColor", ShowIndexLabels = true)]
        [InfoBox("Non Bootstrapper reference detected!", InfoMessageType = InfoMessageType.Error, VisibleIf = "HasInvalidObjects")]
        public List<GameObject> prefabBootstrappers = new();

        public List<IBootstrapper> Bootstrappers { get; private set; } = new();

#if UNITY_EDITOR
        void OnValidate()
        {
            _bootstrapperNames = new();

            foreach (var addressableBootstrap in addressableBootstrappers)
            {
                _bootstrapperNames.Add(addressableBootstrap.editorAsset.name);
            }

            EditorUtility.SetDirty(this);
        }

        [TitleGroup("Spawned Bootstrapper")]
        [ShowInInspector, TableList(ShowIndexLabels = true)]
        private List<IBootstrapperDrawer> _allBootstrappersDrawer
        {
            get
            {
                if (Bootstrappers == null)
                    return null;

                return Bootstrappers
                    .Select(b => new IBootstrapperDrawer { Bootstrapper = b })
                    .ToList();
            }
        }
#endif

        // ────────────────────────────────────────────────────────────────────────────────
        private async void Start()
        {
            CustomDebug.Log("<color=green>[BootstrapManager]</color> Starting bootstrap sequence...");
            await SpawnBootstrappersAsync();
            await InitializeSequentiallyAsync();
            CustomDebug.Log("<color=green>[BootstrapManager]</color> All systems ready!");
        }

        // ────────────────────────────────────────────────────────────────────────────────
        #region SPAWN LOGIC

        [TitleGroup("Debug Function")]
        [Button("Spawn Bootstrappers")]
        public async UniTask SpawnBootstrappersAsync()
        {
            Bootstrappers ??= new List<IBootstrapper>();

            if (useAddressables)
                await SpawnAddressableBootstrappers();
            else
                SpawnPrefabBootstrappers();

            // Clean up destroyed / null Unity objects
            Bootstrappers.RemoveAll(b =>
            {
                if (b == null) // C# null
                    return true;

                // Use UnityEngine.Object typed equality to catch "destroyed" Unity objects
                var unityObj = b as UnityEngine.Object;
                return unityObj == null;
            });
        }

        private async UniTask SpawnAddressableBootstrappers()
        {
            if (addressableBootstrappers == null || addressableBootstrappers.Count == 0)
            {
                CustomDebug.Log("<color=green>[BootstrapManager]</color> No Addressable bootstrappers assigned.");
                return;
            }

            for (int index = 0; index < addressableBootstrappers.Count; index++)
            {
                var refObj = addressableBootstrappers[index];
                if (refObj == null)
                    continue;

                try
                {
                    var expectedName = _bootstrapperNames[index];
                    var existing = FindExistingBootstrapper(expectedName);
                    if (existing != null)
                    {
                        CustomDebug.Log(
                            $"<color=green>[BootstrapManager]</color> Bootstrapper '{expectedName}' already exists. Skipping load.");

                        if (!Bootstrappers.Contains(existing))
                            Bootstrappers.Add(existing);

                        continue;
                    }

                    CustomDebug.Log(
                        $"<color=green>[BootstrapManager]</color> Loading '{refObj.RuntimeKey}'...");

                    var handle = refObj.LoadAssetAsync<GameObject>();
                    await handle.Task;

                    var prefab = handle.Result;
                    if (prefab == null)
                    {
                        CustomDebug.Log(
                            $"<color=green>[BootstrapManager]</color> Failed to load asset '{refObj.RuntimeKey}'.");
                        continue;
                    }

                    var instance = Instantiate(prefab);
                    instance.name = prefab.name;

                    if (instance.TryGetComponent<IBootstrapper>(out var bootstrapper))
                    {
                        Bootstrappers.Add(bootstrapper);
                    }
                    else
                    {
                        CustomDebug.Log(
                            $"<color=green>[BootstrapManager]</color> '{prefab.name}' does not implement IBootstrapper!");
                        Destroy(instance);
                    }
                }
                catch (System.Exception e)
                {
                    CustomDebug.Log(
                        $"<color=green>[BootstrapManager]</color> Error loading Addressable: {e}");
                }
            }
        }

        private void SpawnPrefabBootstrappers()
        {
            if (prefabBootstrappers == null || prefabBootstrappers.Count == 0)
            {
                CustomDebug.Log("<color=green>[BootstrapManager]</color> No prefab bootstrappers assigned.");
                return;
            }

            foreach (var prefab in prefabBootstrappers)
            {
                if (prefab == null)
                    continue;

                var existing = FindExistingBootstrapper(prefab.name); // 🟢 use name-based lookup
                if (existing != null)
                {
                    CustomDebug.Log($"<color=green>[BootstrapManager]</color> Bootstrapper '{prefab.name}' already exists. Skipping spawn.");
                    if (!Bootstrappers.Contains(existing))
                        Bootstrappers.Add(existing);
                    continue;
                }

                var instance = Instantiate(prefab);
                instance.name = prefab.name;

                if (instance.TryGetComponent<IBootstrapper>(out var bootstrapper))
                    Bootstrappers.Add(bootstrapper);
                else
                {
                    CustomDebug.Log($"<color=green>[BootstrapManager]</color> '{prefab.name}' does not implement IBootstrapper!");
                    Destroy(instance);
                }
            }
        }

        #endregion
        // ────────────────────────────────────────────────────────────────────────────────

        #region INITIALIZATION

        [TitleGroup("Debug Function")]
        [Button("Initialize All (Sequential)")]
        public async UniTask InitializeSequentiallyAsync()
        {
            if (Bootstrappers == null || Bootstrappers.Count == 0)
            {
                CustomDebug.Log("<color=green>[BootstrapManager]</color> No bootstrappers to initialize.");
                return;
            }

            foreach (var bootstrapper in Bootstrappers)
            {
                if (bootstrapper == null || bootstrapper.IsInitDone)
                    continue;

                try
                {
                    await bootstrapper.InitializeAsync_FirstTime();
                    CustomDebug.Log($"<color=green>[BootstrapManager]</color> Initialized {bootstrapper}");
                }
                catch (System.Exception ex)
                {
                    CustomDebug.Log($"<color=green>[BootstrapManager]</color> Error initializing {bootstrapper}: {ex}");
                }

                await UniTask.Yield();
            }
        }

        private IBootstrapper FindExistingBootstrapper(string bootstrapperName)
        {
            return FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .OfType<IBootstrapper>()
                .FirstOrDefault(b => (b as MonoBehaviour)?.name == bootstrapperName);
        }

        [TitleGroup("Debug Function")]
        [Button("Find All Bootstrappers (Scene)")]
        public void FindAllBootstrappers()
        {
            Bootstrappers = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .OfType<IBootstrapper>()
                .ToList();

            CustomDebug.Log($"<color=green>[BootstrapManager]</color> Found {Bootstrappers.Count} bootstrappers in scene.");
        }

        #endregion
        // ────────────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private bool HasInvalidObjects => prefabBootstrappers.Any(p => p == null || !p.TryGetComponent<IBootstrapper>(out _));

        private bool HasInvalidObjects_Addressable
        {
            get
            {
                if (addressableBootstrappers == null || addressableBootstrappers.Count == 0)
                    return false;

                foreach (var addressable in addressableBootstrappers)
                {
                    if (addressable == null)
                        return true;

                    var editorAsset = addressable.editorAsset;
                    if (editorAsset == null)
                        return true;

                    if (!editorAsset.TryGetComponent<IBootstrapper>(out _))
                        return true;
                }

                return false;
            }
        }

        private Color GetElementColor(int index)
        {
            bool isEven = (index % 2 == 0);
            var entry = prefabBootstrappers[index];
            bool isDarkTheme = EditorGUIUtility.isProSkin;

            if (entry == null)
                return GetReddishColor(isEven, isDarkTheme);

            if (!entry.TryGetComponent<IBootstrapper>(out _))
                return GetReddishColor(isEven, isDarkTheme);

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

        private Color GetElementColor_Addressable(int index)
        {
            bool isEven = (index % 2 == 0);
            bool isDarkTheme = EditorGUIUtility.isProSkin;

            if (addressableBootstrappers == null || index < 0 || index >= addressableBootstrappers.Count)
                return GetReddishColor(isEven, isDarkTheme);

            var entry = addressableBootstrappers[index];
            if (entry == null)
                return GetReddishColor(isEven, isDarkTheme);

            var editorAsset = entry.editorAsset;
            if (editorAsset == null)
                return GetReddishColor(isEven, isDarkTheme);

            if (!editorAsset.TryGetComponent<IBootstrapper>(out _))
                return GetReddishColor(isEven, isDarkTheme);

            return GetNeutralColor(isEven, isDarkTheme);
        }
#endif
    }
}
