using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;

namespace NG.Data
{
    [CreateAssetMenu(menuName = "NG/Renovatable", fileName = "DEF_House_New")]
    public class RenovatableDefinition : PlaceableDefinition
    {
        public string decayedVariant = "Phase0_Decayed";
        public RenovationPhase[] phases =
        {
            new RenovationPhase { variantName = "Phase1_Repaired" },
            new RenovationPhase { variantName = "Phase2_Painted" },
            new RenovationPhase { variantName = "Phase3_Decorated" }
        };
        public PaintScheme[] paintSchemes = Array.Empty<PaintScheme>();
    }
}
