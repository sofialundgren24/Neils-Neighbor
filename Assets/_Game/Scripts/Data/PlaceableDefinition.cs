using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;

namespace NG.Data
{
    [CreateAssetMenu(menuName = "NG/Placeable", fileName = "DEF_New")]
    public class PlaceableDefinition : DefinitionBase
    {
        [Header("Visuellt")]
        public PlaceableCategory category = PlaceableCategory.Decor;
        public AssetReferenceGameObject prefab;
        [Tooltip("Fotavtryck i rutnätsceller (0,5 m).")]
        public Vector2Int footprint = Vector2Int.one;

        [Header("Ekonomi")]
        [Min(1)] public int unlockLevel = 1;
        public CurrencyCost[] cost = Array.Empty<CurrencyCost>();
        public ResourceAmount[] materials = Array.Empty<ResourceAmount>();
        [Min(0)] public int buildTimeSec;
        [Min(0)] public int xpReward = 5;

        [Header("Trädgårdsbetyg")]
        [Min(0)] public int beautyScore = 1;
        public StyleTag styleTags;
        public bool premium;
    }
}
