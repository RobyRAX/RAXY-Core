using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace RAXY.Core.Addressable
{
    public interface IAddressableAssetRequester
    {
        public List<AssetReference> AssetReferences { get; }
    }
}
