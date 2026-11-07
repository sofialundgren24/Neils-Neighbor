using System;
using UnityEngine;

namespace NG.Data
{
    /// <summary>Byggnad som förädlar varor, t.ex. affären (vete -> bröd) eller trädgårdsbutiken.</summary>
    [CreateAssetMenu(menuName = "NG/Production Building", fileName = "DEF_Production_New")]
    public class ProductionDefinition : PlaceableDefinition
    {
        public RecipeDefinition[] recipes = Array.Empty<RecipeDefinition>();
        [Tooltip("Hur många beställningar som får stå i kö samtidigt.")]
        [Min(1)] public int queueSize = 3;
    }
}
