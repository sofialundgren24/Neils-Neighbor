using UnityEngine;

namespace NG.Data
{
    public enum ClutterAction
    {
        /// <summary>Rensa med spade (ogräs, skräp).</summary>
        Clear,
        /// <summary>Vattna med vattenkanna (torrt gräs).</summary>
        Water
    }

    /// <summary>
    /// Skräp på den nedgångna startgatan. Spelaren trycker eller drar fingret över det för att rensa/vattna,
    /// och det försvinner för gott. Tutorialen (nivå 0) bygger på det här.
    /// </summary>
    [CreateAssetMenu(menuName = "NG/Clutter", fileName = "DEF_Clutter_New")]
    public class ClutterDefinition : PlaceableDefinition
    {
        public ClutterAction action = ClutterAction.Clear;
        [Min(0)] public int clearXp = 2;
        [Min(0)] public int clearCoins;
    }
}
