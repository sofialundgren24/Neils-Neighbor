using System;
using UnityEngine;

namespace NG.World
{
    /// <summary>
    /// Kartans rutnät (cellstorlek 0,5 m). Varje cell vet vilket objekt (instance-id) som står där,
    /// så att ett objekt kan flyttas utan att krocka med sig självt.
    /// </summary>
    public sealed class PlotGrid
    {
        public const float CellSize = 0.5f;

        readonly string[,] _occupant;
        public int Width { get; }
        public int Height { get; }

        public PlotGrid(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException();
            Width = width;
            Height = height;
            _occupant = new string[width, height];
        }

        /// <summary>Fotavtryck efter rotation (0..3 steg om 90°).</summary>
        public static Vector2Int Rotated(Vector2Int footprint, int rotation) =>
            (rotation & 1) == 0 ? footprint : new Vector2Int(footprint.y, footprint.x);

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public string OccupantAt(int x, int y) => InBounds(x, y) ? _occupant[x, y] : null;

        /// <summary>Sant om alla celler är inom kartan och lediga (eller upptagna av ignoreId).</summary>
        public bool IsFree(int x, int y, Vector2Int footprint, int rotation, string ignoreId = null)
        {
            var size = Rotated(footprint, rotation);
            if (x < 0 || y < 0 || x + size.x > Width || y + size.y > Height) return false;
            for (int i = x; i < x + size.x; i++)
                for (int j = y; j < y + size.y; j++)
                {
                    var o = _occupant[i, j];
                    if (o != null && o != ignoreId) return false;
                }
            return true;
        }

        public void Occupy(string id, int x, int y, Vector2Int footprint, int rotation) => Set(x, y, footprint, rotation, id);
        public void Free(int x, int y, Vector2Int footprint, int rotation) => Set(x, y, footprint, rotation, null);

        void Set(int x, int y, Vector2Int footprint, int rotation, string value)
        {
            var size = Rotated(footprint, rotation);
            for (int i = Mathf.Max(0, x); i < Mathf.Min(Width, x + size.x); i++)
                for (int j = Mathf.Max(0, y); j < Mathf.Min(Height, y + size.y); j++)
                    _occupant[i, j] = value;
        }

        /// <summary>Lokal position (kartans hörn = origo) för objektets pivot = fotavtryckets mitt.</summary>
        public static Vector3 CellToLocal(int x, int y, Vector2Int footprint, int rotation)
        {
            var size = Rotated(footprint, rotation);
            return new Vector3((x + size.x * 0.5f) * CellSize, 0f, (y + size.y * 0.5f) * CellSize);
        }

        public static Vector2Int LocalToCell(Vector3 local) =>
            new Vector2Int(Mathf.FloorToInt(local.x / CellSize), Mathf.FloorToInt(local.z / CellSize));
    }
}
