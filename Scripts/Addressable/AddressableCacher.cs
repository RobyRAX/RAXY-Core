using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Sirenix.OdinInspector;

using Object = UnityEngine.Object;
using Cysharp.Threading.Tasks;
using RAXY.Utility;

namespace RAXY.Core.Addressable
{
    public class AddressableCacher : MonoBehaviour
    {
        static AddressableCacher _instance;
        public static AddressableCacher Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("Addressable Cacher");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<AddressableCacher>();
                }
                return _instance;
            }
        }

        [ShowInInspector, NonSerialized, HideReferenceObjectPicker]
        [DictionaryDrawerSettings(ValueLabel = "Container")]
        Dictionary<string, AddressableCacheContainer> _containers = new();

        // ──────────────────────────────
        // 🔹 STATIC ACCESS METHODS (UniTask version)
        // ──────────────────────────────

        public static T TryGetDirect<T>(string containerKey, AssetReference assetRef)
        {
            if (assetRef == null || string.IsNullOrEmpty(containerKey))
                return default;

            if (Instance._containers.TryGetValue(containerKey, out var baseContainer) &&
                baseContainer is AddressableCacheContainer<T> container &&
                container.TryGet(assetRef.AssetGUID, out var result))
            {
                return result;
            }
            return default;
        }

        public static UniTask<T> TryGet<T>(string containerKey, AssetReference assetRef)
            => Instance.DoTryGet<T>(containerKey, assetRef);

        public static UniTask Load<T>(string containerKey, AssetReference assetRef)
            => Instance.DoLoad<T>(containerKey, assetRef, false).AsUniTask();

        public static UniTask<string> TryGetAssetName<T>(string containerKey, AssetReference assetRef)
            => Instance.DoTryGetAssetName<T>(containerKey, assetRef);

        public static string TryGetAssetNameDirect(string containerKey, AssetReference assetRef)
        {
            if (assetRef == null || string.IsNullOrEmpty(containerKey))
                return default;

            if (Instance._containers.TryGetValue(containerKey, out var baseContainer) &&
                baseContainer.TryGetAssetName(assetRef.AssetGUID, out var result))
            {
                return result;
            }
            return null;
        }

        public static void CreateContainer<T>(string key)
            => Instance.DoCreateContainer<T>(key);

        public static void Release(string containerKey, string guid)
            => Instance.DoRelease(containerKey, guid);

        public static void ReleaseAll()
            => Instance.DoReleaseAll();

        // ──────────────────────────────
        // 🔸 INSTANCE INTERNAL METHODS (UniTask)
        // ──────────────────────────────

        async UniTask<T> DoTryGet<T>(string containerKey, AssetReference assetRef)
        {
            var (result, _) = await DoLoad<T>(containerKey, assetRef, true);
            return result;
        }

        async UniTask<string> DoTryGetAssetName<T>(string containerKey, AssetReference assetRef)
        {
            var (result, _) = await DoLoad<T>(containerKey, assetRef, true);

            if (result is Object unityObj)
                return unityObj.name;

            return result?.ToString() ?? "<null>";
        }

        // ✅ Switched to UniTaskCompletionSource
        private readonly Dictionary<string, UniTaskCompletionSource<bool>> _loadingTasks = new();

        async UniTask<(T result, bool loaded)> DoLoad<T>(string containerKey, AssetReference assetRef, bool returnResult = false)
        {
            if (assetRef == null || string.IsNullOrEmpty(containerKey))
                throw new ArgumentException("Invalid container key or asset reference.");

            string guid = assetRef.AssetGUID;

            if (!_containers.TryGetValue(containerKey, out var baseContainer))
            {
                DoCreateContainer<T>(containerKey);
                baseContainer = _containers[containerKey];
            }

            if (baseContainer is not AddressableCacheContainer<T> container)
                throw new Exception($"Container '{containerKey}' is not of type {typeof(T).Name}.");

            // already cached
            if (container.TryGet(guid, out var existing))
                return (existing, false);

            // wait if already loading
            if (_loadingTasks.TryGetValue(guid, out var existingTcs))
            {
                await existingTcs.Task;
                if (container.TryGet(guid, out var alreadyLoaded))
                    return (alreadyLoaded, false);
            }

            // create new task source
            var tcs = new UniTaskCompletionSource<bool>();
            _loadingTasks[guid] = tcs;

            try
            {
                AsyncOperationHandle<T> handle = assetRef.LoadAssetAsync<T>();
                await handle.Task; // ✅ use ToUniTask() for Unity handle integration
                container.Cache(guid, handle);
                tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
                throw;
            }
            finally
            {
                _loadingTasks.Remove(guid);
            }

            return (container.TryGet(guid, out var result) ? result : default, true);
        }

        async UniTask DoLoadInternal<T>(AddressableCacheContainer<T> container, AssetReference assetRef, string guid)
        {
            AsyncOperationHandle<T> handle = assetRef.LoadAssetAsync<T>();
            await handle.Task;
            container.Cache(guid, handle);
        }

        void DoCreateContainer<T>(string key)
        {
            if (_containers.ContainsKey(key))
                throw new Exception($"Container '{key}' already exists.");

            _containers[key] = new AddressableCacheContainer<T>();
        }

        void DoRelease(string containerKey, string guid)
        {
            if (_containers.TryGetValue(containerKey, out var container))
                container.Release(guid);
        }

        void DoReleaseAll()
        {
            foreach (var container in _containers.Values)
                container.ReleaseAll();

            _containers.Clear();
        }

        void OnDestroy()
        {
            DoReleaseAll();
        }
    }

    // ──────────────────────────────
    // 🔸 Base & Generic Container
    // ──────────────────────────────

    [HideReferenceObjectPicker]
    public abstract class AddressableCacheContainer
    {
        public abstract void ReleaseAll();
        public abstract void Release(string guid);
        public abstract bool TryGetAssetName(string id, out string assetName);
    }

    [HideReferenceObjectPicker]
    public class AddressableCacheContainer<T> : AddressableCacheContainer
    {
#if UNITY_EDITOR
        [ShowInInspector, HideReferenceObjectPicker, TableList]
        List<CacheContainerAssetDrawer<T>> cacheDrawer
        {
            get
            {
                List<CacheContainerAssetDrawer<T>> temp = new();
                foreach (var cache in _cache)
                    temp.Add(new CacheContainerAssetDrawer<T>(cache));
                return temp;
            }
        }
#endif

        [NonSerialized]
        Dictionary<string, CachedAddressableAsset<T>> _cache = new();

        public bool TryGet(string id, out T result)
        {
            if (_cache.TryGetValue(id, out var cache))
            {
                result = cache.handle.Result;
                return true;
            }
            result = default;
            return false;
        }

        public override bool TryGetAssetName(string id, out string assetName)
        {
            if (_cache.TryGetValue(id, out var cache))
            {
                assetName = cache.assetName;
                return true;
            }
            assetName = default;
            return false;
        }

        public void Cache(string id, AsyncOperationHandle<T> handle)
        {
            if (_cache.ContainsKey(id))
            {
                CustomDebug.Log($"Overwriting cached asset with GUID {id}");
                Addressables.Release(_cache[id].handle);
            }

            var newCache = new CachedAddressableAsset<T>
            {
                handle = handle,
                assetName = (handle.Result as Object)?.name ?? "<unnamed>"
            };
            _cache[id] = newCache;
        }

        public override void Release(string guid)
        {
            if (_cache.TryGetValue(guid, out var cache))
            {
                if (cache.handle.IsValid())
                {
                    Addressables.Release(cache.handle);
                    CustomDebug.Log($"Released -> {cache.assetName}");
                }
                else
                {
                    CustomDebug.LogWarning($"[AddressableCacher] Skipped releasing invalid handle for {guid}");
                }
                _cache.Remove(guid);
            }
        }

        public override void ReleaseAll()
        {
            foreach (var cache in _cache.Values)
            {
                if (cache.handle.IsValid())
                {
                    Addressables.Release(cache.handle);
                    CustomDebug.Log($"Released -> {cache.assetName}");
                }
                else
                {
                    CustomDebug.LogWarning("[AddressableCacher] Skipped releasing invalid handle in container.");
                }
            }
            _cache.Clear();
        }
    }

    public class CachedAddressableAsset<T>
    {
        public AsyncOperationHandle<T> handle;
        public string assetName;
    }

#if UNITY_EDITOR
    [HideReferenceObjectPicker]
    public class CacheContainerAssetDrawer<T>
    {
        KeyValuePair<string, CachedAddressableAsset<T>> AssetPair;

        [ShowInInspector] public string GUID => AssetPair.Key;
        [ShowInInspector] public string AssetName => AssetPair.Value.assetName;
        [ShowInInspector] public T CachedObject => AssetPair.Value.handle.Result;

        public CacheContainerAssetDrawer(KeyValuePair<string, CachedAddressableAsset<T>> pair)
        {
            AssetPair = pair;
        }
    }
#endif
}
