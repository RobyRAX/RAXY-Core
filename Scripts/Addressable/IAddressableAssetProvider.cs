using UnityEngine;
using UnityEngine.AddressableAssets;

namespace RAXY.Core.Addressable
{
    public interface IAddressableAssetProvider<T> where T : Object
    {
        public bool UseAddressable { get; }
        public T DirectAsset { get; }
        public AssetReferenceT<T> AssetReference { get; }
    }
}
