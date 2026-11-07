using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;

namespace NG.Data
{
    [CreateAssetMenu(menuName = "NG/Resource", fileName = "RES_New")]
    public class ResourceDefinition : DefinitionBase
    {
        [Min(1)] public int sellPrice = 1;
    }
}
