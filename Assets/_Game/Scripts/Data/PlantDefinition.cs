using UnityEngine;

namespace NG.Data
{
    /// <summary>
    /// Grödor, blommor, träd och buskar. Växer genom Stage0_Sprout -> Stage1_Growing -> Stage2_Bloom.
    /// Med en skörd satt kan den skördas i Stage2 och börjar sedan om. Utan skörd är den bara dekor.
    /// </summary>
    [CreateAssetMenu(menuName = "NG/Plant", fileName = "DEF_Plant_New")]
    public class PlantDefinition : PlaceableDefinition
    {
        [Tooltip("Total tid från sådd till blom/skörd. Halva tiden = Stage1.")]
        [Min(1)] public int growTimeSec = 60;
        [Tooltip("Lämna resursen tom för växter som bara är dekor.")]
        public ResourceAmount harvest;
        public BloomSeason seasons = BloomSeason.All;

        public bool IsHarvestable => harvest.resource != null && harvest.amount > 0;
    }
}
