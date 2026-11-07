using System;
using UnityEngine;

namespace NG.World
{
    /// <summary>
    /// Rutnät för en tomt (cellstorlek 0,5 m). Håller reda på vilka celler som är upptagna
    /// och kontrollerar att ett fotavtryck (med rotation) får plats.
    /// </summary>
    public sealed class PlotGrid
    {
        public const float CellSize = 0.5f;

        readonly bool[,] _occupied;
        public int Width { get; }
        public int Height { get; }

        public PlotGrid(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException();
            Width = width;
            Height = height;
            _occupied = new bool[width, height];
        }

        /// <summary>Fotavtryck efter rotation (0..3 steg om 90°).</summary>
        public static Vector2Int Rotated(Vector2Int footprint, int rotation) =>
            (rotation & 1) == 0 ? footprint : new Vector2Int(footprint.y, footprint.x);

        public bool CanPlace(int x, int y, Vector2Int footprint, int rotation)
        {
            var size = Rotated(footprint, rotation);
            if (x < 0 || y < 0 || x + size.x > Width || y + size.y > Height) return false;
            for (int i = x; i < x + size.x; i++)
                for (int j = y; j < y + size.y; j++)
                    if (_occupied[i, j]) return false;
            return true;
        }

        public void Occupy(int x, int y, Vector2Int footprint, int rotation) => Set(x, y, footprint, rotation, true);
        public void Free(int x, int y, Vector2Int footprint, int rotation) => Set(x, y, footprint, rotation, false);

        void Set(int x, int y, Vector2Int footprint, int rotation, bool value)
        {
            var size = Rotated(footprint, rotation);
            for (int i = x; i < x + size.x; i++)
                for (int j = y; j < y + size.y; j++)
                    _occupied[i, j] = value;
        }

        /// <summary>Lokal position (tomtens origo nere till vänster) för objektets pivot = fotavtryckets mitt.</summary>
        public static Vector3 CellToLocal(int x, int y, Vector2Int footprint, int rotation)
        {
            var size = Rotated(footprint, rotation);
            return new Vector3((x + size.x * 0.5f) * CellSize, 0f, (y + size.y * 0.5f) * CellSize);
        }

        public static Vector2Int LocalToCell(Vector3 local) =>
            new Vector2Int(Mathf.FloorToInt(local.x / CellSize), Mathf.FloorToInt(local.z / CellSize));
    }
}
