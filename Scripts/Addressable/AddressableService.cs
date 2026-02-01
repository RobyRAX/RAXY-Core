using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using RAXY.Utility;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

using Object = UnityEngine.Object;

namespace RAXY.Core.Addressable
{
    public class AddressableService : MonoBehaviour
    {
        private static AddressableService _instance;
        public static AddressableService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<AddressableService>();
                    
                    if (_instance == null)
                    {
                        var go = new GameObject(nameof(AddressableService));
                        _instance = go.AddComponent<AddressableService>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        [HideInInspector]
        public Dictionary<string, AddressableLoadedAsset> LoadedAssetDict { get; set; } = new();

#if UNITY_EDITOR
        [ShowInInspector]
        [TableList]
        List<AddressableServiceAssetDrawer> AssetDrawers
        {
            get
            {
                if (LoadedAssetDict == null)
                    return new();

                var assetDrawers = new List<AddressableServiceAssetDrawer>();
                foreach (var handlePair in LoadedAssetDict)
                {
                    assetDrawers.Add(new AddressableServiceAssetDrawer(handlePair.Key, handlePair.Value));
                }
                return assetDrawers;
            }
        }
#endif

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        public static async UniTask<T> LoadAssetAsync<T>(string assetKey, AssetReference reference) where T : class
        {
            return await LoadAssetAsync<T>(reference, assetKey);
        }

        public static async UniTask LoadAllAssetsParallel(string assetKey, params IList<AssetReference>[] referenceGroups)
        {
            int totalCount = 0;
            for (int g = 0; g < referenceGroups.Length; g++)
            {
                totalCount += referenceGroups[g].Count;
            }

            var tasks = new UniTask<Object>[totalCount];
            int index = 0;

            for (int g = 0; g < referenceGroups.Length; g++)
            {
                var group = referenceGroups[g];
                for (int i = 0; i < group.Count; i++)
                {
                    tasks[index] = LoadAssetAsync<Object>(group[i], assetKey);
                    index++;
                }
            }

            await UniTask.WhenAll(tasks);
        }

        public static async UniTask<T> LoadAssetAsync<T>(AssetReference reference, string assetKey = null) where T : class
        {
            try
            {
                // Validate reference
                if (reference == null)
                {
                    CustomDebug.LogError("AssetReference is null");
                    return null;
                }

                if (!reference.RuntimeKeyIsValid())
                {
                    CustomDebug.LogError($"AssetReference is not valid: {reference.AssetGUID}");
                    return null;
                }

                CustomDebug.Log($"Loading asset: {reference.RuntimeKey}");
                
                if (Instance.LoadedAssetDict.TryGetValue(reference.AssetGUID, out var loadedAsset))
                {
                    if (loadedAsset.handle.IsDone && loadedAsset.handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        CustomDebug.Log($"Asset already loaded: {reference.RuntimeKey}");

                        loadedAsset.AddKey(assetKey);
                        return loadedAsset.handle.Result as T;
                    }

                    CustomDebug.Log($"Waiting for asset to load: {reference.RuntimeKey}");
                    await loadedAsset.handle.Task;

                    loadedAsset.AddKey(assetKey);
                    return loadedAsset.handle.Result as T;
                }

                CustomDebug.Log($"Starting async load for asset: {reference.RuntimeKey}");

                var handle = reference.LoadAssetAsync<T>();
                Instance.LoadedAssetDict.Add(reference.AssetGUID, new AddressableLoadedAsset(reference.AssetGUID, handle));

                await handle.Task;

                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    CustomDebug.LogError($"Failed to load asset: {reference.RuntimeKey}");
                    Instance.LoadedAssetDict.Remove(reference.AssetGUID);
                    return null;
                }

                CustomDebug.Log($"Successfully loaded asset: {reference.RuntimeKey}");
                Instance.LoadedAssetDict[reference.AssetGUID].AddKey(assetKey);
                return handle.Result;
            }
            catch (Exception ex)
            {
                CustomDebug.LogError($"Exception while loading asset: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Can return null if asset isn't loaded yet
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reference"></param>
        /// <returns></returns>
        public static T GetLoadedAsset<T>(AssetReference reference) where T : class
        {
            if (Instance.LoadedAssetDict.TryGetValue(reference.AssetGUID, out var loadedAsset))
            {
                if (loadedAsset.handle.IsDone && loadedAsset.handle.Status == AsyncOperationStatus.Succeeded)
                {
                    return loadedAsset.handle.Result as T;
                }
            }

            CustomDebug.LogWarning($"Asset with GUID: {reference.AssetGUID} isn't loaded yet");
            return null;
        }

        [TitleGroup("Test Function")]
        [Button]
        public static void Release(AssetReference reference)
        {
            if (Instance.LoadedAssetDict.TryGetValue(reference.AssetGUID, out var handle))
            {
                Addressables.Release(handle);
                Instance.LoadedAssetDict.Remove(reference.AssetGUID);
            }
        }

        [TitleGroup("Test Function")]
        [Button]
        public static void ReleaseByKey(string assetKey)
        {
            var keysToRemove = new List<string>();

            foreach (var kvp in Instance.LoadedAssetDict)
            {
                if (kvp.Value.keys.Contains(assetKey))
                {
                    kvp.Value.RemoveKey(assetKey);

                    if (kvp.Value.keys.Count == 0)
                    {
                        Addressables.Release(kvp.Value.handle);
                        keysToRemove.Add(kvp.Key);
                    }
                }
            }

            foreach (var key in keysToRemove)
            {
                Instance.LoadedAssetDict.Remove(key);
            }
        }

        [TitleGroup("Test Function")]
        [Button]
        void TestLoad_GameObject(AssetReference reference)
        {
            LoadAssetAsync<GameObject>(reference).Forget();
        }

        [TitleGroup("Test Function")]
        [Button]
        GameObject TestGet_GameObject(AssetReference reference)
        {
            return GetLoadedAsset<GameObject>(reference);
        }

        public static async UniTask<T> ResolveAsync<T>(IAddressableAssetProvider<T> provider) where T : Object
        {
            if (provider == null)
            {
                Debug.LogError("[Resolver] Provider is NULL");
                return null;
            }

            try
            {
                if (provider.UseAddressable)
                {
#if UNITY_EDITOR
                    if (provider.AssetReference != null && provider.AssetReference.editorAsset != null)
                    {
                        return provider.AssetReference.editorAsset;
                    }
#endif

                    if (provider.AssetReference != null && provider.AssetReference.RuntimeKeyIsValid())
                    {
                        provider.CachedAddressableAsset = await LoadAssetAsync<T>(provider.AssetReference);
                        return provider.CachedAddressableAsset;
                    }
                }
                else
                {
                    return provider.DirectAsset;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Resolver] Failed loading {typeof(T)} from Addressables.\n" +
                               $"Key: {provider.AssetReference?.RuntimeKey}\nException: {e}");
            }

            Debug.LogError($"[Resolver] Missing asset for type {typeof(T)}");
            return null;
        }

        public static T Resolve<T>(IAddressableAssetProvider<T> provider) where T : Object
        {
            if (provider == null)
            {
                Debug.LogError("[Resolver] Provider is NULL");
                return null;
            }

            try
            {
                if (provider.UseAddressable)
                {
#if UNITY_EDITOR
                    if (provider.AssetReference != null && provider.AssetReference.editorAsset != null)
                    {
                        return provider.AssetReference.editorAsset;
                    }
#endif

                    if (provider.AssetReference != null && provider.AssetReference.RuntimeKeyIsValid())
                    {
                        return GetLoadedAsset<T>(provider.AssetReference);
                    }
                }
                else
                {
                    return provider.DirectAsset;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Resolver] Failed resolving loaded asset {typeof(T)}.\nException: {e}");
            }

            Debug.LogError($"[Resolver] Missing asset for type {typeof(T)}");
            return null;
        }
    }

    [Serializable]
    public class AddressableLoadedAsset
    {
        public string assetGuid;
        public AsyncOperationHandle handle;
        public HashSet<string> keys = new HashSet<string>();

        public void AddKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            if (!keys.Contains(key))
            {
                keys.Add(key);
            }
        }

        public void RemoveKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            if (keys.Contains(key))
            {
                keys.Remove(key);
            }
        }

        public AddressableLoadedAsset() { }
        public AddressableLoadedAsset(string guid, AsyncOperationHandle handle)
        {
            this.handle = handle;
            this.assetGuid = guid;
        }
    }

#if UNITY_EDITOR
    [Serializable]
    public class AddressableServiceAssetDrawer
    {
        [TableColumnWidth(100, false)]
        [ShowInInspector]
        string assetGuid;

        [ShowInInspector]
        object loadedAsset;

        [ShowInInspector]
        HashSet<string> keys;

        public AddressableServiceAssetDrawer() { }
        public AddressableServiceAssetDrawer(string assetGuid, AddressableLoadedAsset loadedAsset)
        {
            this.assetGuid = assetGuid;
            this.loadedAsset = (loadedAsset.handle.IsDone && loadedAsset.handle.Status == AsyncOperationStatus.Succeeded)
                                ? loadedAsset.handle.Result
                                : null;
            this.keys = loadedAsset.keys;
        }
    }
#endif
}