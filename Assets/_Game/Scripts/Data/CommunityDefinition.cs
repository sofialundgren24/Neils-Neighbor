using UnityEngine;

namespace NG.Data
{
    /// <summary>
    /// Kvartersbyggnad som gör gatan levande och ger mynt/XP med jämna mellanrum:
    /// lekparkens delar (gungor, sandlåda, rutschkana) och skolan.
    /// </summary>
    [CreateAssetMenu(menuName = "NG/Community Building", fileName = "DEF_Community_New")]
    public class CommunityDefinition : PlaceableDefinition
    {
        [Min(0)] public int incomeCoins = 20;
        [Min(0)] public int incomeXp = 5;
        [Tooltip("Sekunder mellan varje intäkt.")]
        [Min(10)] public int incomeIntervalSec = 300;
    }
}
