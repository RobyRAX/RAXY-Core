using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using RAXY.Utility;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace RAXY.Core.Addressable
{
    public class AddressableService : Singleton<AddressableService>
    {
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

        public async UniTask<T> LoadAssetAsync<T>(AssetReference reference) where T : class
        {
            if (handleDict.TryGetValue(reference.AssetGUID, out var existingHandle))
            {
                if (existingHandle.IsDone && existingHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    return existingHandle.Result as T;
                }

                await existingHandle.Task;
                return existingHandle.Result as T;
            }

            var handle = reference.LoadAssetAsync<T>();
            handleDict.Add(reference.AssetGUID, handle);

            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                handleDict.Remove(reference.AssetGUID);
                throw new Exception($"Failed to load Addressable: {reference.RuntimeKey}");
            }

            return handle.Result;
        }

        /// <summary>
        /// Can return null if asset isn't loaded yet
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reference"></param>
        /// <returns></returns>
        public T GetLoadedAsset<T>(AssetReference reference) where T : class
        {
            if (handleDict.TryGetValue(reference.AssetGUID, out var handle))
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
        public void Release(AssetReference reference)
        {
            if (handleDict.TryGetValue(reference.AssetGUID, out var handle))
            {
                Addressables.Release(handle);
                handleDict.Remove(reference.AssetGUID);
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