using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;

namespace NG.Data
{
    [CreateAssetMenu(menuName = "NG/District", fileName = "DIST_New")]
    public class DistrictDefinition : DefinitionBase
    {
        [Min(1)] public int requiredLevel = 1;
        public CurrencyCost unlockCost;
        public AssetReference sceneChunk;
        public PlotDefinition[] plots = Array.Empty<PlotDefinition>();
        public BloomMilestone[] milestones = Array.Empty<BloomMilestone>();
    }
}
