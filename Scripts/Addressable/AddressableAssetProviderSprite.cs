using System;
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
        public Sprite DirectAsset => directAsset;

        [ShowIf("UseAddressable")]
        [SerializeField]
        protected AssetReferenceSprite assetReference;

        public AssetReferenceT<Sprite> AssetReference => assetReference;
        public Sprite CachedAddressableAsset { get; set; }

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

                    if (CachedAddressableAsset == null)
                    {
                        CachedAddressableAsset = AddressableService.GetLoadedAsset<Sprite>(AssetReference);

                        if (CachedAddressableAsset == null)
                        {
                            Debug.LogWarning($"[Sprite] Addressable asset NOT loaded yet for {AssetReference?.AssetGUID}");
                        }
                    }

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
