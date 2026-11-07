using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;

namespace NG.Data
{
    [CreateAssetMenu(menuName = "NG/Plant", fileName = "DEF_Plant_New")]
    public class PlantDefinition : PlaceableDefinition
    {
        [Tooltip("Sekunder per tillväxtsteg: Sprout -> Growing -> Bloom.")]
        public int[] growthStageSec = { 60, 300, 900 };
        public BloomSeason seasons = BloomSeason.All;
    }
}
