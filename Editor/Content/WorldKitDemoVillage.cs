using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// "Örnek Köy Oluştur": yeni bir sahnede başlangıç içeriğiyle küçük bir köy kurar; World Kit'in dünya kurma özelliklerini
    /// tek sahnede gösterir: doğal şekilli iki katlı tepe ve yumuşak rampalar, oval gölet, taş meydan, kıvrımlı patikalar
    /// (yüzey boyası), çitli çiftlik, ormanlık alanlar, kenar süsü, NPC'ler ve bölgeler.
    ///
    /// Yerleşim bir doluluk haritasıyla yapılır: bina, ağaç ve süsler patikaya, suya, tepeye veya birbirine binmez.
    /// Kenar stili (köşeli / yumuşak) ve doku proje ayarlarından gelir. Kod, kendi araçlarınız için de örnektir.
    /// </summary>
    public static class WorldKitDemoVillage
    {
        const int Size = 40;

        [MenuItem("Tools/World Kit/Örnek Köy Oluştur", priority = 120)]
        public static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (Load(WorldKitStarterContent.Building, "Ev_Kucuk_3x3") == null)
                WorldKitStarterContent.Generate();

            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new Builder().Build();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            var view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                float cs = WorldKitGrid.CellSize;
                view.LookAt(new Vector3(Size * cs * 0.5f, 0f, Size * cs * 0.45f), Quaternion.Euler(55f, 0f, 0f), Size * cs * 0.8f);
            }

            Debug.Log("[World Kit] Örnek köy oluşturuldu. Sahneyi kaydetmeyi unutma (Ctrl+S).");
        }

        static GameObject Load(string category, string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{WorldKitSettings.Instance.piecesFolder}/{category}/{name}.prefab");

        sealed class Builder
        {
            readonly System.Random m_Random = new System.Random(7);
            readonly HashSet<Vector2Int> m_Blocked = new HashSet<Vector2Int>();   // bina / ağaç konamaz
            WorldKitFloor m_Floor;
            float m_Cell;

            public void Build()
            {
                m_Floor = WorldKitBrush.GetFloor();
                m_Cell = WorldKitGrid.CellSize;
                int grass = FloorType("Çimen"), stone = FloorType("Taş"), water = FloorType("Su");

                // ---------- Zemin: çimen ----------
                m_Floor.SetCells(Rect(0, 0, Size, Size), 0, grass);

                // ---------- Tepe: doğal şekilli iki kat ----------
                var hillCenter = new Vector2(29.5f, 29.5f);
                var level1 = Blob(hillCenter, 8.5f, 0.12f);
                var level2 = Blob(hillCenter, 4.2f, 0.08f);
                m_Floor.SetCells(level1, 1, grass);
                m_Floor.SetCells(level2, 2, stone);

                // Yumuşak rampalar: tepenin güneyinden 1. kata, oradan 2. kata
                var rampDown = GentleRamp(new[] { 28, 29 }, 0, 1);
                var rampUp = GentleRamp(new[] { 29, 30 }, 1, 2);
                m_Floor.PaintType(rampUp, stone);

                // ---------- Gölet ----------
                var pond = new List<Vector2Int>();
                foreach (var c in Rect(0, 0, Size, Size))
                {
                    float dx = (c.x + 0.5f - 8.5f) / 5f, dz = (c.y + 0.5f - 30.5f) / 3.6f;
                    if (dx * dx + dz * dz <= 1f)
                        pond.Add(c);
                }
                m_Floor.PaintType(pond, water);

                WorldKitBrush.SyncFloorMaterials(m_Floor);
                WorldKitEvents.RaiseFloorChanged(m_Floor);

                foreach (var c in level1) m_Blocked.Add(c);
                foreach (var c in rampDown) Block(c, 1);
                foreach (var c in pond) Block(c, 1);

                // ---------- Meydan ve patikalar (yüzey boyası) ----------
                WorldKitStarterContent.EnsureDefaultPaintLayers();
                int pathLayer = PaintLayer("Patika", 0), squareLayer = PaintLayer("Taş Yol", 2), darkGrass = PaintLayer("Koyu Çimen", 1);

                var square = new Vector2(17f, 16f);
                PaintPath(new[] { World(square, 0) }, 3.2f, squareLayer);
                BlockCircle(square, 3.8f);

                int rampX = 28;
                int rampZ = rampDown.Count > 0 ? rampDown.Min(c => c.y) : 18;
                var entrance = Wobble(new Vector2(17.5f, 0f), new Vector2(17f, 12.8f), 0.9f, 1.5f);
                var toHill = Wobble(new Vector2(19.8f, 17.5f), new Vector2(rampX + 1f, rampZ - 0.5f), 1.1f, 2f);
                var toPond = Wobble(new Vector2(14.2f, 17.5f), new Vector2(12.5f, 27.2f), 0.8f, 1.5f);
                var toFarm = Wobble(new Vector2(14f, 15f), new Vector2(12.6f, 6.5f), 0.7f, 2.5f);
                foreach (var path in new[] { entrance, toHill, toPond, toFarm })
                {
                    PaintPath(path.Select(p => World(p, 0)).ToList(), 0.8f, pathLayer);
                    foreach (var p in path)
                        BlockCircle(p, 1.4f);
                }

                // Koyu çimen lekeleri (yolsuz yerlerde)
                for (int i = 0; i < 9; i++)
                {
                    var c = new Vector2(2f + (float)m_Random.NextDouble() * 36f, 2f + (float)m_Random.NextDouble() * 36f);
                    if (m_Blocked.Contains(Vector2Int.FloorToInt(c)))
                        continue;
                    PaintPath(new[] { World(c, 0), World(c + new Vector2(1.4f, 0.7f), 0) }, 1.2f + (float)m_Random.NextDouble(), darkGrass);
                }

                // ---------- Çiftlik: çitle çevrili avlu ve ahır ----------
                var farm = new RectInt(2, 2, 10, 8);   // x 2..12, z 2..10 (çit hücre sınırlarında)
                BuildFence(farm, gateEastZ: 6);
                foreach (var c in Rect(farm.xMin, farm.yMin, farm.width, farm.height))
                    m_Blocked.Add(c);
                PlaceAt(WorldKitStarterContent.Building, "Ahir_4x3", 3, 5);
                PlaceAt(WorldKitStarterContent.Decor, "Kasa", 9, 3);
                PlaceAt(WorldKitStarterContent.Decor, "Varil", 10, 4);
                PlaceAt(WorldKitStarterContent.Npc, "NPC_Koylu", 9, 7);

                // ---------- Köy: meydanın çevresinde binalar ----------
                TryPlace(WorldKitStarterContent.Building, "Han_Iki_Katli_5x4", 21, 20);
                TryPlace(WorldKitStarterContent.Building, "Dukkan_4x3", 10, 13);
                TryPlace(WorldKitStarterContent.Building, "Ev_Kucuk_3x3", 13, 21);
                TryPlace(WorldKitStarterContent.Building, "Ev_Kucuk_3x3", 22, 10);
                TryPlace(WorldKitStarterContent.Building, "Ev_Buyuk_4x5", 24, 3);
                TryPlace(WorldKitStarterContent.Building, "Kule_3x3", 34, 6);
                TryPlace(WorldKitStarterContent.Decor, "Varil", 15, 12);
                TryPlace(WorldKitStarterContent.Decor, "Kasa", 26, 18);

                // Tepenin üstü: kale kulesi
                PlaceAt(WorldKitStarterContent.Building, "Kale_Kulesi_3x3", 28, 28, level: 2);

                // ---------- Süsler ----------
                // Tepe kenarına kayalar; rampanın çevresi açık kalsın (yol kapanmasın)
                var decorCells = level1.Where(c => !rampDown.Any(r => Mathf.Abs(r.x - c.x) <= 1 && Mathf.Abs(r.y - c.y) <= 2)).ToList();
                FloorEdgeDecor.Build(m_Floor, decorCells);
                for (int i = 2; i < entrance.Count - 1; i += 5)
                {
                    var p = entrance[i] + new Vector2(i % 2 == 0 ? 1.6f : -1.6f, 0f);
                    PlaceFree(WorldKitStarterContent.Decor, "Fener", p, 0, 0f);
                }
                PlaceFree(WorldKitStarterContent.Decor, "Tabela", new Vector2(19.7f, 13.2f), 0, 220f);
                PlaceFree(WorldKitStarterContent.Decor, "Kaya_Kumesi", new Vector2(13.5f, 29.5f), 0, 30f);
                foreach (var path in new[] { entrance, toHill, toPond })
                    for (int i = 1; i < path.Count; i += 3)
                    {
                        if (m_Random.NextDouble() > 0.5)
                            continue;
                        var side = new Vector2((float)m_Random.NextDouble() - 0.5f, (float)m_Random.NextDouble() - 0.5f).normalized * 1.5f;
                        PlaceFree(WorldKitStarterContent.Decor, m_Random.NextDouble() > 0.5 ? "Ot" : "Cali", path[i] + side, 0, (float)m_Random.NextDouble() * 360f);
                    }

                // ---------- Orman ----------
                var forests = new[] { (new Vector2(35f, 17f), 5.5f), (new Vector2(4.5f, 19.5f), 4f), (new Vector2(17f, 36f), 4.5f), (new Vector2(37f, 37f), 3f), (new Vector2(30f, 3f), 3f) };
                var trees = new[] { Load(WorldKitStarterContent.Decor, "Agac_Cam"), Load(WorldKitStarterContent.Decor, "Agac_Yuvarlak") };
                foreach (var cell in Rect(0, 0, Size, Size))
                {
                    if (m_Blocked.Contains(cell) || !m_Floor.TryGetHeight(cell, out var h) || h != 0)
                        continue;
                    var p = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
                    float density = forests.Max(f => 1f - Vector2.Distance(p, f.Item1) / f.Item2) * 0.75f;
                    bool border = cell.x < 1 || cell.y < 1 || cell.x >= Size - 1 || cell.y >= Size - 1;
                    if (border) density = Mathf.Max(density, 0.45f);
                    if (m_Random.NextDouble() > Mathf.Max(density, 0.015f))
                        continue;
                    var jitter = new Vector2((float)m_Random.NextDouble() - 0.5f, (float)m_Random.NextDouble() - 0.5f) * 0.5f;
                    WorldKitBrush.PlacePiece(trees[m_Random.Next(trees.Length)], null, World(p + jitter, 0),
                        Quaternion.Euler(0f, m_Random.Next(360), 0f), Vector3.one * (0.85f + (float)m_Random.NextDouble() * 0.35f));
                    m_Blocked.Add(cell);
                }
                // Tepenin yamacında seyrek ağaçlar
                foreach (var cell in level1.Except(level2))
                {
                    if (rampUp.Contains(cell) || m_Random.NextDouble() > 0.12)
                        continue;
                    WorldKitBrush.PlacePiece(trees[m_Random.Next(trees.Length)], null, World(new Vector2(cell.x + 0.5f, cell.y + 0.5f), 1),
                        Quaternion.Euler(0f, m_Random.Next(360), 0f), Vector3.one * (0.8f + (float)m_Random.NextDouble() * 0.3f));
                }

                // ---------- Karakterler ve bölgeler ----------
                PlaceFree(WorldKitStarterContent.Npc, "NPC_Tuccar", new Vector2(13.5f, 12f), 0, 90f);
                PlaceFree(WorldKitStarterContent.Npc, "NPC_Koylu", new Vector2(16f, 17.5f), 0, 160f);
                PlaceFree(WorldKitStarterContent.Npc, "NPC_Muhafiz", new Vector2(rampX - 1.2f, rampZ - 1.5f), 0, 180f);
                PlaceFree(WorldKitStarterContent.Npc, "Dusman", new Vector2(35.5f, 18f), 0, 200f);
                PlaceFree(WorldKitStarterContent.Reference, "Oyuncu", new Vector2(17.5f, 3f), 0, 0f);
                WorldKitZones.Create(new RectInt(15, 0, 5, 3), 0, WorldKitZone.Kind.Spawn);
                WorldKitZones.Create(new RectInt(10, 26, 3, 3), 0, WorldKitZone.Kind.Quest);
                WorldKitZones.Create(new RectInt(32, 14, 6, 6), 0, WorldKitZone.Kind.Danger);

                WorldKitEvents.RaiseEditFinished();
            }

            // ---------------- Zemin yardımcıları ----------------

            /// <summary>Hafif dalgalı kenarlı yuvarlak alan (doğal tepe şekli).</summary>
            List<Vector2Int> Blob(Vector2 center, float radius, float wobble)
            {
                var cells = new List<Vector2Int>();
                foreach (var c in Rect(0, 0, Size, Size))
                {
                    var d = new Vector2(c.x + 0.5f, c.y + 0.5f) - center;
                    float angle = Mathf.Atan2(d.y, d.x);
                    float r = radius * (1f + wobble * Mathf.Sin(angle * 3f + 0.7f) + wobble * 0.6f * Mathf.Cos(angle * 5f));
                    if (d.magnitude <= r)
                        cells.Add(c);
                }
                return cells;
            }

            /// <summary>Verilen sütunlarda, from katındaki zeminden to katına güneyden çıkan 2 hücrelik yumuşak rampa.</summary>
            List<Vector2Int> GentleRamp(int[] columns, int from, int to)
            {
                var cells = new List<Vector2Int>();
                foreach (int x in columns)
                {
                    // Bu sütunda to katının en güneydeki hücresi
                    int z = Size;
                    for (int zz = 0; zz < Size; zz++)
                        if (m_Floor.TryGetHeight(new Vector2Int(x, zz), out var h) && h >= to) { z = zz; break; }
                    if (z < 2)
                        continue;
                    var a = new Vector2Int(x, z - 2);
                    var b = new Vector2Int(x, z - 1);
                    if (m_Floor.TryGetHeight(a, out var ha) && ha == from && m_Floor.TryGetHeight(b, out var hb) && hb == from)
                    {
                        cells.Add(a);
                        cells.Add(b);
                    }
                }
                m_Floor.AutoSlopeGentle(cells, false);
                return cells;
            }

            static List<Vector2Int> Rect(int x, int z, int width, int depth)
            {
                var cells = new List<Vector2Int>();
                for (int i = 0; i < width; i++)
                for (int j = 0; j < depth; j++)
                    cells.Add(new Vector2Int(x + i, z + j));
                return cells;
            }

            // ---------------- Patika ----------------

            /// <summary>a'dan b'ye hafif kıvrımlı yol noktaları.</summary>
            List<Vector2> Wobble(Vector2 a, Vector2 b, float amplitude, float waves)
            {
                var points = new List<Vector2>();
                var dir = b - a;
                var side = new Vector2(-dir.y, dir.x).normalized;
                int steps = Mathf.Max(8, Mathf.CeilToInt(dir.magnitude * 2f));
                float phase = (float)m_Random.NextDouble() * Mathf.PI;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float envelope = Mathf.Sin(t * Mathf.PI);   // uçlarda düz, ortada kıvrımlı
                    points.Add(a + dir * t + side * (Mathf.Sin(t * Mathf.PI * waves + phase) * amplitude * envelope));
                }
                return points;
            }

            void PaintPath(IList<Vector3> points, float radius, int layer) =>
                WorldKitSurfaceBrush.PaintPath(m_Floor, points, radius * m_Cell, layer);

            static int PaintLayer(string name, int fallback)
            {
                var layers = WorldKitSettings.Instance.paintLayers;
                int index = layers.FindIndex(l => l.name == name);
                return index >= 0 ? index : Mathf.Min(fallback, Mathf.Max(0, layers.Count - 1));
            }

            // ---------------- Doluluk ----------------

            void Block(Vector2Int cell, int margin)
            {
                for (int dx = -margin; dx <= margin; dx++)
                for (int dz = -margin; dz <= margin; dz++)
                    m_Blocked.Add(new Vector2Int(cell.x + dx, cell.y + dz));
            }

            void BlockCircle(Vector2 center, float radius)
            {
                int r = Mathf.CeilToInt(radius);
                for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                {
                    var c = new Vector2Int(Mathf.FloorToInt(center.x) + dx, Mathf.FloorToInt(center.y) + dz);
                    if (Vector2.Distance(new Vector2(c.x + 0.5f, c.y + 0.5f), center) <= radius)
                        m_Blocked.Add(c);
                }
            }

            /// <summary>Parçayı istenen yere ya da en yakın boş yere (4 hücre içinde) koyar; kapladığı yer ve 1 hücre çevresi dolar.</summary>
            void TryPlace(string category, string name, int x, int z)
            {
                var prefab = Load(category, name);
                if (prefab == null)
                    return;
                var footprint = WorldKitScene.GetFootprint(prefab);
                int w = Mathf.Max(1, footprint.x), d = Mathf.Max(1, footprint.y);
                for (int radius = 0; radius <= 4; radius++)
                for (int ox = -radius; ox <= radius; ox++)
                for (int oz = -radius; oz <= radius; oz++)
                {
                    if (Mathf.Max(Mathf.Abs(ox), Mathf.Abs(oz)) != radius)
                        continue;
                    int px = x + ox, pz = z + oz;
                    bool free = true;
                    for (int i = 0; i < w && free; i++)
                    for (int j = 0; j < d && free; j++)
                    {
                        var c = new Vector2Int(px + i, pz + j);
                        free = c.x >= 1 && c.y >= 1 && c.x < Size - 1 && c.y < Size - 1 && !m_Blocked.Contains(c)
                               && m_Floor.TryGetHeight(c, out var h) && h == 0;
                    }
                    if (!free)
                        continue;
                    PlaceAt(category, name, px, pz);
                    for (int i = -1; i <= w; i++)
                    for (int j = -1; j <= d; j++)
                        m_Blocked.Add(new Vector2Int(px + i, pz + j));
                    return;
                }
                Debug.LogWarning($"[World Kit] Örnek köy: '{name}' için boş yer bulunamadı.");
            }

            // ---------------- Yerleştirme ----------------

            /// <summary>Parçayı sol-alt köşesi (x, z) hücresinde olacak şekilde, grid'e koyar.</summary>
            void PlaceAt(string category, string name, int x, int z, int rotationSteps = 0, int level = 0)
            {
                var prefab = Load(category, name);
                if (prefab == null)
                {
                    Debug.LogWarning($"[World Kit] Örnek köy: '{category}/{name}' bulunamadı, atlandı.");
                    return;
                }
                var footprint = WorldKitGrid.RotatedFootprint(WorldKitScene.GetFootprint(prefab), rotationSteps);
                var center = new Vector2(x + Mathf.Max(1, footprint.x) * 0.5f, z + Mathf.Max(1, footprint.y) * 0.5f);
                WorldKitBrush.PlacePiece(prefab, null, World(center, level), Quaternion.Euler(0f, rotationSteps * 90f, 0f));
            }

            /// <summary>Grid'siz yerleştirme (hücre biriminde konum, derece cinsinden yön).</summary>
            void PlaceFree(string category, string name, Vector2 cellPosition, int level, float yaw)
            {
                var prefab = Load(category, name);
                if (prefab != null)
                    WorldKitBrush.PlacePiece(prefab, null, World(cellPosition, level), Quaternion.Euler(0f, yaw, 0f));
            }

            /// <summary>Çit: dikdörtgenin hücre sınırları boyunca 2 m'lik çitler; doğu kenarında kapı boşluğu.</summary>
            void BuildFence(RectInt area, int gateEastZ)
            {
                var fence = Load(WorldKitStarterContent.Decor, "Cit_2m");
                if (fence == null)
                    return;
                for (int x = area.xMin; x < area.xMax; x += 2)
                {
                    WorldKitBrush.PlacePiece(fence, null, World(new Vector2(x + 1f, area.yMin), 0), Quaternion.identity);
                    WorldKitBrush.PlacePiece(fence, null, World(new Vector2(x + 1f, area.yMax), 0), Quaternion.identity);
                }
                for (int z = area.yMin; z < area.yMax; z += 2)
                {
                    WorldKitBrush.PlacePiece(fence, null, World(new Vector2(area.xMin, z + 1f), 0), Quaternion.Euler(0f, 90f, 0f));
                    if (z != gateEastZ)   // kapı
                        WorldKitBrush.PlacePiece(fence, null, World(new Vector2(area.xMax, z + 1f), 0), Quaternion.Euler(0f, 90f, 0f));
                }
            }

            Vector3 World(Vector2 cellPosition, int level) =>
                new Vector3(cellPosition.x * m_Cell, level * WorldKitGrid.LevelHeight, cellPosition.y * m_Cell);

            static int FloorType(string name)
            {
                var types = WorldKitSettings.Instance.floorTypes;
                for (int i = 0; i < types.Count; i++)
                    if (string.Equals(types[i].name, name, System.StringComparison.OrdinalIgnoreCase))
                        return i + 1;
                return 0;
            }
        }
    }
}
