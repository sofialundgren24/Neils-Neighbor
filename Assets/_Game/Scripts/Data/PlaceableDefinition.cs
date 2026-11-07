using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace NG.Data
{
    /// <summary>Allt som kan stå på kartan: hus, växter, dekor, mark, produktionsbyggnader.</summary>
    [CreateAssetMenu(menuName = "NG/Placeable", fileName = "DEF_New")]
    public class PlaceableDefinition : DefinitionBase
    {
        [Header("Visuellt")]
        public PlaceableCategory category = PlaceableCategory.Decor;
        public AssetReferenceGameObject prefab;
        [Tooltip("Fotavtryck i rutnätsceller (0,5 m).")]
        public Vector2Int footprint = Vector2Int.one;

        [Header("Ekonomi")]
        [Tooltip("Nivå som krävs för att köpa (eller, för hus, för att renovera).")]
        [Min(0)] public int unlockLevel = 1;
        public CurrencyCost[] cost = Array.Empty<CurrencyCost>();
        public ResourceAmount[] materials = Array.Empty<ResourceAmount>();
        [Min(0)] public int buildTimeSec;
        [Min(0)] public int xpReward = 5;

        [Header("Butik")]
        [Tooltip("Visas i byggmenyn. Hus på startgatan har false.")]
        public bool inShop = true;
        [Tooltip("Max antal på kartan. 0 = obegränsat.")]
        [Min(0)] public int maxCount;

        [Header("Trädgårdsbetyg")]
        [Min(0)] public int beautyScore = 1;
        public StyleTag styleTags;
        public bool premium;
    }
}
