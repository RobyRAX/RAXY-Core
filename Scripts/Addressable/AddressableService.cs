using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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
        public Dictionary<string, AsyncOperationHandle> handleDict = new();

#if UNITY_EDITOR
        [ShowInInspector]
        [TableList]
        List<AddressableServiceAssetDrawer> AssetDrawers
        {
            get
            {
                if (handleDict == null)
                    return new();

                var assetDrawers = new List<AddressableServiceAssetDrawer>();
                foreach (var handlePair in handleDict)
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

        public static async UniTask<T> LoadAssetAsync<T>(AssetReference reference) where T : class
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
                
                if (Instance.handleDict.TryGetValue(reference.AssetGUID, out var existingHandle))
                {
                    if (existingHandle.IsDone && existingHandle.Status == AsyncOperationStatus.Succeeded)
                    {
                        CustomDebug.Log($"Asset already loaded: {reference.RuntimeKey}");
                        return existingHandle.Result as T;
                    }

                    CustomDebug.Log($"Waiting for asset to load: {reference.RuntimeKey}");
                    await existingHandle.Task;
                    return existingHandle.Result as T;
                }

                CustomDebug.Log($"Starting async load for asset: {reference.RuntimeKey}");
                var handle = reference.LoadAssetAsync<T>();
                Instance.handleDict.Add(reference.AssetGUID, handle);

                await handle.Task;

                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    CustomDebug.LogError($"Failed to load asset: {reference.RuntimeKey}");
                    Instance.handleDict.Remove(reference.AssetGUID);
                    return null;
                }

                CustomDebug.Log($"Successfully loaded asset: {reference.RuntimeKey}");
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
            if (Instance.handleDict.TryGetValue(reference.AssetGUID, out var handle))
            {
                if (handle.IsDone && handle.Status == AsyncOperationStatus.Succeeded)
                {
                    return handle.Result as T;
                }
            }

            CustomDebug.LogWarning($"Asset with GUID: {reference.AssetGUID} isn't loaded yet");
            return null;
        }

        [TitleGroup("Test Function")]
        [Button]
        public static void Release(AssetReference reference)
        {
            if (Instance.handleDict.TryGetValue(reference.AssetGUID, out var handle))
            {
                Addressables.Release(handle);
                Instance.handleDict.Remove(reference.AssetGUID);
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

        public static async UniTask LoadAllAssetsParallel(params IList<AssetReference>[] referenceGroups)
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
                    tasks[index] = LoadAssetAsync<Object>(group[i]);
                    index++;
                }
            }

            await UniTask.WhenAll(tasks);
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

        public AddressableServiceAssetDrawer() { }
        public AddressableServiceAssetDrawer(string assetGuid, AsyncOperationHandle handle)
        {
            this.assetGuid = assetGuid;
            this.loadedAsset = (handle.IsDone && handle.Status == AsyncOperationStatus.Succeeded)
                                ? handle.Result
                                : null;
        }
    }
#endif
}