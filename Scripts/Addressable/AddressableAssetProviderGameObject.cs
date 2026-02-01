using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace RAXY.Core.Addressable
{
    [Serializable]
    public class AddressableAssetProviderGameObject : IAddressableAssetProvider<GameObject>
    {
        [SerializeField]
        protected bool useAddressable;
        public bool UseAddressable => useAddressable;

        [HideIf("UseAddressable")]
        [SerializeField]
        protected GameObject directAsset;
        public GameObject DirectAsset => directAsset;

        [ShowIf("UseAddressable")]
        [SerializeField]
        protected AssetReferenceGameObject assetReference;

        public AssetReferenceT<GameObject> AssetReference => assetReference;
        public GameObject CachedAddressableAsset { get; set; }

        public GameObject Asset
        {
            get
            {
                if (useAddressable)
                { 
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        return AssetReference?.editorAsset;
#endif

                    if (CachedAddressableAsset == null)
                    {
                        CachedAddressableAsset = AddressableService.GetLoadedAsset<GameObject>(AssetReference);

                        if (CachedAddressableAsset == null)
                        {
                            Debug.LogWarning($"[GameObject] Addressable asset NOT loaded yet for {AssetReference.AssetGUID}");
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
