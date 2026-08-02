using System;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace RAXY.Core.Addressable
{
    [Serializable]
    public class AddressableAssetProviderSprite : IAddressableAssetProvider<Sprite>
    {
        [SerializeField]
        protected bool useAddressable;
        public bool UseAddressable => useAddressable;

        [HideIf("UseAddressable")]
        [SerializeField]
        protected Sprite directAsset;

        [JsonIgnore]
        public Sprite DirectAsset => directAsset;

        [ShowIf("UseAddressable")]
        [SerializeField]
        protected AssetReferenceSprite assetReference;

        [JsonIgnore]
        public AssetReferenceT<Sprite> AssetReference => assetReference;

        [JsonIgnore]
        public Sprite CachedAddressableAsset { get; set; }

        [JsonIgnore]
        public Sprite Asset
        {
            get
            {
                if (useAddressable)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        return AssetReference?.editorAsset as Sprite;
                    }
#endif

                    CachedAddressableAsset = AddressableService.TryGetLoadedAsset<Sprite>(AssetReference);
                    return CachedAddressableAsset;
                }
                else
                {
                    return directAsset;
                }
            }
            set
            {
                if (useAddressable)
                {
                    CachedAddressableAsset = value;
                }
                else
                {
                    directAsset = value;
                }
            }
        }
    }
}
