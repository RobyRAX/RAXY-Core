using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace RAXY.Core.Addressable
{
    public class AddressableLoaderBase : MonoBehaviour
    {
        public List<AssetReference> assetReferences;

        [TitleGroup("Debug Function")]
        [Button]
        public async UniTask LoadAssets()
        {
            if (assetReferences == null || assetReferences.Count == 0)
                return;

            var tasks = new List<UniTask>();

            foreach (var reference in assetReferences)
            {
                if (reference == null)
                    continue;

                tasks.Add(AddressableService.Instance.LoadAssetAsync<Object>(reference));
            }

            await UniTask.WhenAll(tasks);
        }

        [TitleGroup("Debug Function")]
        [Button]
        public void ReleaseAssets()
        {
            foreach (var reference in assetReferences)
            {
                AddressableService.Instance.Release(reference);
            }
        }
    }
}