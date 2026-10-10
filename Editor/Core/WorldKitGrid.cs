using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>Grid ölçüleri ve snap hesapları. Ölçüler proje ayarlarından okunur.</summary>
    public static class WorldKitGrid
    {
        /// <summary>Bir grid hücresinin kenar uzunluğu (m).</summary>
        public static float CellSize => WorldKitSettings.Instance.cellSize;

        /// <summary>Bir seviyenin yüksekliği (m).</summary>
        public static float LevelHeight => WorldKitSettings.Instance.levelHeight;

        public static Vector2Int RotatedFootprint(Vector2Int footprint, int rotationSteps)
        {
            return (rotationSteps & 1) == 0 ? footprint : new Vector2Int(footprint.y, footprint.x);
        }

        /// <summary>
        /// Tek sayılı genişlikler hücre ortasına, çift sayılı (ve 0) genişlikler grid çizgisine oturur.
        /// </summary>
        public static Vector3 Snap(Vector3 point, Vector2Int footprint, int rotationSteps, int level)
        {
            var f = RotatedFootprint(footprint, rotationSteps);
            return new Vector3(SnapAxis(point.x, f.x), level * LevelHeight, SnapAxis(point.z, f.y));
        }

        static float SnapAxis(float value, int cells)
        {
            float cell = CellSize;
            if ((cells & 1) == 1)
                return (Mathf.Floor(value / cell) + 0.5f) * cell;
            return Mathf.Round(value / cell) * cell;
        }

        /// <summary>Noktanın içinde bulunduğu hücre.</summary>
        public static Vector2Int ToCell(Vector3 point) => new Vector2Int(
            Mathf.FloorToInt(point.x / CellSize),
            Mathf.FloorToInt(point.z / CellSize));

        /// <summary>Noktaya en yakın grid köşesi (kesişim).</summary>
        public static Vector2Int ToCorner(Vector3 point) => new Vector2Int(
            Mathf.RoundToInt(point.x / CellSize),
            Mathf.RoundToInt(point.z / CellSize));

        public static Vector3 CornerToWorld(Vector2Int corner, int level) => new Vector3(
            corner.x * CellSize, level * LevelHeight, corner.y * CellSize);

        public static Vector3 CellCenter(Vector2Int cell, int level) => new Vector3(
            (cell.x + 0.5f) * CellSize, level * LevelHeight, (cell.y + 0.5f) * CellSize);

        /// <summary>İki nokta arasındaki hücre dikdörtgeni (iki uç dahil).</summary>
        public static RectInt CellRect(Vector3 from, Vector3 to)
        {
            var a = ToCell(from);
            var b = ToCell(to);
            var min = Vector2Int.Min(a, b);
            var max = Vector2Int.Max(a, b);
            return new RectInt(min, max - min + Vector2Int.one);
        }
    }
}
