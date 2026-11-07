using System;
using System.Collections.Generic;
using UnityEngine;

namespace NG.Data
{
    /// <summary>
    /// Globala spelinställningar och kartan: startlayout (den nedgångna gatan) och områden som låses upp
    /// med nivåer. Skapas av NG > Content > Import All från CSV-filerna i Assets/_Game/Data.
    /// </summary>
    [CreateAssetMenu(menuName = "NG/Game Config", fileName = "GameConfig")]
    public class GameConfig : DefinitionBase
    {
        public const float CellSize = 0.5f;

        public LevelTable levelTable;

        [Header("Ny spelare")]
        [Min(0)] public int startCoins = 500;
        [Min(0)] public int startGems = 20;

        [Header("Karta")]
        [Tooltip("Hela kartans storlek i celler (0,5 m). Kartans mitt ligger i världens origo.")]
        public Vector2Int mapSize = new Vector2Int(96, 96);
        public List<StartPlacement> startLayout = new List<StartPlacement>();
        public List<MapExpansion> expansions = new List<MapExpansion>();

        [Header("Renovering")]
        [Tooltip("När ett hus är färdigrenoverat får allt grått inom denna radie (meter) tillbaka färgen.")]
        [Min(0)] public float restoreRadius = 7f;

        [Header("Beställningar")]
        [Min(1)] public int orderSlots = 3;
        [Min(1)] public int orderRefillSec = 30;
        [Min(0)] public float orderRewardMultiplier = 1.5f;

        [Header("Försäljning")]
        [Range(0f, 1f)] public float sellRefund = 0.25f;

        /// <summary>Världsposition för cell (0, 0):s hörn.</summary>
        public Vector3 MapOriginWorld => new Vector3(-mapSize.x * CellSize * 0.5f, 0f, -mapSize.y * CellSize * 0.5f);
    }

    [Serializable]
    public class StartPlacement
    {
        public PlaceableDefinition definition;
        public Vector2Int cell;
        [Range(0, 3)] public int rotation;
        [Tooltip("Grå och sliten från start. Hus börjar då orenoverade.")]
        public bool decayed;
    }

    [Serializable]
    public class MapExpansion
    {
        public string id;
        public string name;
        [Tooltip("Område i celler: x, y, bredd, höjd.")]
        public RectInt area;
        [Min(1)] public int requiredLevel = 1;
        [Min(0)] public int coinCost;
    }
}
