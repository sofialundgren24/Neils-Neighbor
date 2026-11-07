using System;
using UnityEngine;

namespace NG.Data
{
    /// <summary>Ett recept i en produktionsbyggnad: varor in -> vara ut efter en viss tid.</summary>
    [CreateAssetMenu(menuName = "NG/Recipe", fileName = "RCP_New")]
    public class RecipeDefinition : DefinitionBase
    {
        public ResourceAmount[] inputs = Array.Empty<ResourceAmount>();
        public ResourceAmount output;
        [Min(1)] public int durationSec = 60;
        [Min(0)] public int xpReward = 5;
        [Min(1)] public int unlockLevel = 1;
    }
}
