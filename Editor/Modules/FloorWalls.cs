using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// Zemin "Duvarla Çevir" işlemi: seçilen hücre alanının dış kenarlarına duvar örer.
    /// Yalnızca zemini olan hücreler sayılır; bir kenar, komşu hücre seçimin dışındaysa veya farklı
    /// yükseklikteyse dış kenardır. Duvar her hücrenin kendi yüksekliğine oturur.
    ///
    /// Duvar 1 hücre uzunluğunda bir çizgi parçası olmalı (ör. Duvar_1m). Daha uzun duvarlarda kenar boyunca
    /// sığdığı kadar uzun parça, kalan yerlere (varsa) 1 hücrelik varsayılan duvar konur.
    /// Köşe direkleri ve kapı açma modülleri bu duvarları da görür.
    /// </summary>
    static class FloorWalls
    {
        static readonly Vector2Int[] k_Directions = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        public static void Build(WorldKitFloor floor, ICollection<Vector2Int> cells, GameObject wall)
        {
            if (wall == null)
            {
                Debug.LogWarning("[World Kit] Duvarla Çevir: duvar prefab'ı seçilmemiş (Zemin sekmesi).");
                return;
            }

            var info = wall.GetComponent<WorldKitPiece>();
            if (info == null || !WorldKitLayout.IsLinear(info.footprint) || Mathf.Max(info.footprint.x, info.footprint.y) == 0)
            {
                Debug.LogWarning($"[World Kit] Duvarla Çevir: '{wall.name}' bir çizgi parçası değil (footprint'in bir ekseni 0, diğeri ≥ 1 olmalı).");
                return;
            }

            int length = Mathf.Max(info.footprint.x, info.footprint.y);
            bool localAxisIsX = info.footprint.x >= info.footprint.y;
            var shortWall = length == 1 ? null : AssetDatabase.LoadAssetAtPath<GameObject>(WorldKitStarterContent.DefaultWallPath);

            // Dış kenarları topla: (hücre, yön) → yükseklik
            var selected = new HashSet<Vector2Int>(cells);
            var edges = new HashSet<(Vector2Int cell, Vector2Int dir)>();
            foreach (var cell in selected)
            {
                if (!floor.TryGetHeight(cell, out var height))
                    continue;

                foreach (var dir in k_Directions)
                {
                    var neighbour = cell + dir;
                    bool inside = selected.Contains(neighbour) && floor.TryGetHeight(neighbour, out var nh) && nh == height;
                    if (!inside)
                        edges.Add((cell, dir));
                }
            }

            float cellSize = WorldKitGrid.CellSize;
            var done = new HashSet<(Vector2Int, Vector2Int)>();

            // Aynı hattaki komşu kenarları birleştirip uzun duvarlarla doldur.
            foreach (var edge in edges)
            {
                if (done.Contains(edge))
                    continue;

                // Kenarın uzandığı eksen: sağ/sol kenarlar Z boyunca, üst/alt kenarlar X boyunca.
                var step = edge.dir.x != 0 ? Vector2Int.up : Vector2Int.right;
                floor.TryGetHeight(edge.cell, out var height);

                // Hattın başına git
                var start = edge.cell;
                while (edges.Contains((start - step, edge.dir)) && SameHeight(floor, start - step, height))
                    start -= step;

                // Hat boyunca say
                var run = new List<Vector2Int>();
                for (var c = start; edges.Contains((c, edge.dir)) && SameHeight(floor, c, height); c += step)
                {
                    run.Add(c);
                    done.Add((c, edge.dir));
                }

                PlaceRun(run, edge.dir, step, height, wall, length, localAxisIsX, shortWall, cellSize);
            }
        }

        static bool SameHeight(WorldKitFloor floor, Vector2Int cell, int height) =>
            floor.TryGetHeight(cell, out var h) && h == height;

        static void PlaceRun(List<Vector2Int> run, Vector2Int dir, Vector2Int step, int height, GameObject wall,
            int length, bool localAxisIsX, GameObject shortWall, float cellSize)
        {
            bool alongX = step.x != 0;
            var rotation = Quaternion.Euler(0f, alongX == localAxisIsX ? 0f : 90f, 0f);
            float y = height * WorldKitGrid.LevelHeight;

            int i = 0;
            while (i < run.Count)
            {
                bool fitsLong = i + length <= run.Count;
                var prefab = fitsLong ? wall : shortWall;
                int span = fitsLong ? length : 1;
                if (prefab == null)
                    break;

                // Parçanın ortası: kapladığı hücrelerin kenar orta noktalarının ortası
                var first = WorldKitGrid.CellCenter(run[i], 0);
                var last = WorldKitGrid.CellCenter(run[i + span - 1], 0);
                var center = (first + last) * 0.5f + new Vector3(dir.x, 0f, dir.y) * (cellSize * 0.5f);
                center.y = y;

                WorldKitBrush.PlacePiece(prefab, null, center, rotation);
                i += span;
            }
        }
    }
}
