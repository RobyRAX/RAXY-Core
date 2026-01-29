using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RAXY.Core.Addressable
{
    public static class AddressableAssetResolver
    {
        public static async UniTask<T> Resolve<T>(IAddressableAssetProvider<T> provider) where T : Object
        {
            if (!provider.UseAddressable)
            {
                return provider.DirectAsset;
            }

            if (provider.AssetReference != null && provider.AssetReference.RuntimeKeyIsValid())
            {
                return await AddressableService.Instance.LoadAssetAsync<T>(provider.AssetReference);
            }

            Debug.LogError($"[Resolver] Missing asset for type {typeof(T)}");
            return null;
        }
    }
}
