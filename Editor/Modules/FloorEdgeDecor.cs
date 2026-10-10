using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// Zemin "Kenar Süsle" işlemi: seçilen alanın dış kenarlarına (uçurum, platform kenarı, kıyı) kaya, çalı, çit
    /// gibi parçaları rastgele ama tekrarlanabilir şekilde dizer. Grid dışıdır: konum, açı ve boyut hafifçe oynar.
    ///
    /// - Parçalar Zemin sekmesinde seçilir (birden fazlaysa her kenarda biri seçilir).
    /// - Yoğunluk: kenarların ne kadarına parça konacağı.
    /// - "Uçurum dibine": kenarın dışındaki komşu daha alçaksa parça aşağıya, uçurumun dibine konur.
    /// Rastgelelik kenar konumundan türetilir: aynı alanı tekrar süslemek aynı sonucu verir (üst üste binmez).
    /// </summary>
    public static class FloorEdgeDecor
    {
        const string PrefabsKey = "WorldKit.EdgeDecor.Prefabs";
        const string DensityKey = "WorldKit.EdgeDecor.Density";
        const string BottomKey = "WorldKit.EdgeDecor.Bottom";

        static List<GameObject> s_Prefabs;

        /// <summary>Kenara konacak parçalar (EditorPrefs'te GUID olarak saklanır).</summary>
        public static List<GameObject> Prefabs
        {
            get
            {
                if (s_Prefabs != null)
                    return s_Prefabs;

                s_Prefabs = EditorPrefs.GetString(PrefabsKey, "")
                    .Split(';')
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                    .Where(p => p != null)
                    .ToList();

                // İlk kullanımda başlangıç içeriğindeki kayalar
                if (s_Prefabs.Count == 0)
                {
                    foreach (var name in new[] { "Kaya_Kucuk", "Kaya_Buyuk", "Cali" })
                    {
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{WorldKitSettings.Instance.piecesFolder}/{WorldKitStarterContent.Decor}/{name}.prefab");
                        if (prefab != null)
                            s_Prefabs.Add(prefab);
                    }
                }
                return s_Prefabs;
            }
        }

        public static void SavePrefabs()
        {
            var guids = Prefabs.Where(p => p != null).Select(p => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(p)));
            EditorPrefs.SetString(PrefabsKey, string.Join(";", guids));
        }

        /// <summary>Kenarların ne kadarına parça konsun (0-1).</summary>
        public static float Density
        {
            get => EditorPrefs.GetFloat(DensityKey, 0.6f);
            set => EditorPrefs.SetFloat(DensityKey, Mathf.Clamp01(value));
        }

        /// <summary>true ise dışarıdaki komşu alçaksa parça uçurumun dibine konur.</summary>
        public static bool PlaceAtBottom
        {
            get => EditorPrefs.GetBool(BottomKey, true);
            set => EditorPrefs.SetBool(BottomKey, value);
        }

        public static void Build(WorldKitFloor floor, ICollection<Vector2Int> cells)
        {
            var prefabs = Prefabs.Where(p => p != null).ToList();
            if (prefabs.Count == 0)
            {
                Debug.LogWarning("[World Kit] Kenar Süsle: Zemin sekmesinde en az bir parça seç.");
                return;
            }

            float cellSize = WorldKitGrid.CellSize;
            float levelHeight = WorldKitGrid.LevelHeight;
            float density = Density;
            bool bottom = PlaceAtBottom;

            foreach (var (cell, dir) in FloorWalls.OuterEdges(floor, cells))
            {
                floor.TryGetHeight(cell, out var height);
                var random = new System.Random(unchecked(cell.x * 73856093 ^ cell.y * 19349663 ^ (dir.x + 2) * 83492791 ^ (dir.y + 5) * 2654435));
                if (random.NextDouble() > density)
                    continue;

                // Kenarın ortası, kenar boyunca hafif kaydırılmış
                var normal = new Vector3(dir.x, 0f, dir.y);
                var along = new Vector3(-dir.y, 0f, dir.x);
                var center = WorldKitGrid.CellCenter(cell, 0) + normal * (cellSize * 0.5f)
                             + along * ((float)random.NextDouble() - 0.5f) * cellSize * 0.6f;

                // Dışarısı daha alçaksa uçurumun dibine, değilse kenarın üstüne
                bool lowerOutside = floor.TryGetHeight(cell + dir, out var outside) ? outside < height : true;
                float y;
                if (bottom && lowerOutside)
                {
                    int baseLevel = floor.TryGetHeight(cell + dir, out var o) ? o : Mathf.Min(height, 0);
                    y = baseLevel * levelHeight;
                    center += normal * cellSize * 0.3f;
                }
                else
                {
                    y = height * levelHeight;
                    center -= normal * cellSize * 0.25f;
                }
                center.y = y;

                var prefab = prefabs[random.Next(prefabs.Count)];
                var rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                float scale = 0.8f + (float)random.NextDouble() * 0.4f;

                // Aynı noktada bu parçalardan biri zaten varsa (yeniden süsleme) koyma
                WorldKitBrush.PlacePiece(prefab, null, center, rotation, Vector3.one * scale, prefabs);
            }
        }
    }
}
