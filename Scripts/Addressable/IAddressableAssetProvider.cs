using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;

using Object = UnityEngine.Object;

namespace RAXY.Core.Addressable
{
    public interface IAddressableAssetProvider<T> where T : Object
    {
        public bool UseAddressable { get; }
        public T DirectAsset { get; }
        public AssetReferenceT<T> AssetReference { get; }
        public T CachedAddressableAsset { get; set; }
    }
}
