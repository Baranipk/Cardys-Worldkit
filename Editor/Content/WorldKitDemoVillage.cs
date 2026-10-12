using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// "Örnek Köy Oluştur": yeni bir sahnede başlangıç içeriğiyle küçük bir köy kurar. Yeni ekip arkadaşlarının
    /// aracı tanıması ve özellikleri (zemin türleri, yumuşak rampa, su, kenar süsü, binalar, NPC'ler) denemesi için.
    /// Her şey World Kit'in kendi API'siyle yapılır; kod, kendi araçlarınız için de örnektir.
    /// </summary>
    public static class WorldKitDemoVillage
    {
        const int Size = 32;

        [MenuItem("Tools/World Kit/Örnek Köy Oluştur", priority = 120)]
        public static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (Load(WorldKitStarterContent.Building, "Ev_Kucuk_3x3") == null)
                WorldKitStarterContent.Generate();

            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Build();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            var view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                float cs = WorldKitGrid.CellSize;
                view.LookAt(new Vector3(Size * cs * 0.5f, 0f, Size * cs * 0.45f), Quaternion.Euler(50f, 0f, 0f), Size * cs * 0.75f);
            }

            Debug.Log("[World Kit] Örnek köy oluşturuldu. Sahneyi kaydetmeyi unutma (Ctrl+S).");
        }

        static void Build()
        {
            var floor = WorldKitBrush.GetFloor();
            int grass = FloorType("Çimen"), road = FloorType("Yol"), stone = FloorType("Taş"), water = FloorType("Su"), dirt = FloorType("Toprak");

            // ---------- Zemin ----------
            floor.SetCells(Rect(0, 0, Size, Size), 0, grass);
            floor.PaintType(Rect(15, 0, 2, Size), road);           // kuzey-güney yolu
            floor.PaintType(Rect(0, 15, Size, 2), road);           // doğu-batı yolu
            floor.PaintType(Rect(2, 2, 6, 5), dirt);               // ahır avlusu
            floor.PaintType(Rect(3, 22, 6, 7), water);             // gölet

            // Taş platform (1. kat) ve ona çıkan yumuşak rampa
            var platform = Rect(22, 22, 8, 8);
            floor.SetCells(platform, 1, stone);
            var ramp = Rect(25, 20, 2, 2);
            floor.PaintType(ramp, stone);
            floor.AutoSlopeGentle(ramp, false);

            WorldKitBrush.SyncFloorMaterials(floor);
            WorldKitEvents.RaiseFloorChanged(floor);

            // ---------- Yüzey boyası: kıvrımlı patikalar ve koyu çimen lekeleri ----------
            WorldKitStarterContent.EnsureDefaultPaintLayers();
            float cs = WorldKitGrid.CellSize;
            var toPond = new List<Vector3>();      // ana yoldan göletin kıyısına
            for (int i = 0; i <= 24; i++)
            {
                float t = i / 24f;
                toPond.Add(World(15f - t * 6f, 0, 23f + t * 2f + Mathf.Sin(t * Mathf.PI * 2f) * 1.2f));
            }
            WorldKitSurfaceBrush.PaintPath(floor, toPond, 0.75f * cs, 0);

            var toRamp = new List<Vector3>();      // ana yoldan taş platformun rampasına
            for (int i = 0; i <= 30; i++)
            {
                float t = i / 30f;
                toRamp.Add(World(17f + t * 9f, 0, 16.5f + t * 3.5f + Mathf.Sin(t * Mathf.PI * 3f) * 0.8f));
            }
            WorldKitSurfaceBrush.PaintPath(floor, toRamp, 0.65f * cs, 0);

            var patches = new System.Random(3);
            for (int i = 0; i < 7; i++)
            {
                var center = World(2f + (float)patches.NextDouble() * 28f, 0, 2f + (float)patches.NextDouble() * 28f);
                WorldKitSurfaceBrush.PaintPath(floor, new[] { center, center + new Vector3(1.2f, 0f, 0.6f) * cs }, (1.2f + (float)patches.NextDouble()) * cs, 1);
            }

            // ---------- Binalar ----------
            Place(WorldKitStarterContent.Building, "Ahir_4x3", 3, 3);
            Place(WorldKitStarterContent.Building, "Ev_Buyuk_4x5", 8, 8);
            Place(WorldKitStarterContent.Building, "Ev_Kucuk_3x3", 10, 18);
            Place(WorldKitStarterContent.Building, "Ev_Kucuk_3x3", 19, 10);
            Place(WorldKitStarterContent.Building, "Dukkan_4x3", 19, 18);
            Place(WorldKitStarterContent.Building, "Han_Iki_Katli_5x4", 20, 4);
            Place(WorldKitStarterContent.Building, "Kale_Kulesi_3x3", 26, 26, level: 1);

            // ---------- Çit (göletin güneyi) ----------
            var fence = Load(WorldKitStarterContent.Decor, "Cit_2m");
            for (int x = 3; x < 9; x += 2)
                WorldKitBrush.PlacePiece(fence, null, World(x + 1f, 0, 22f), Quaternion.identity);

            // ---------- Kenar süsü (platformun çevresi) ----------
            FloorEdgeDecor.Build(floor, platform);

            // ---------- Fenerler (yol boyunca) ----------
            foreach (int z in new[] { 4, 10, 20, 26 })
                Place(WorldKitStarterContent.Decor, "Fener", 14, z);
            Place(WorldKitStarterContent.Decor, "Tabela", 17, 13);
            Place(WorldKitStarterContent.Decor, "Varil", 23, 18);
            Place(WorldKitStarterContent.Decor, "Kasa", 24, 18);

            // ---------- Ağaçlar ve kayalar (kenarlarda, tekrarlanabilir rastgele) ----------
            var random = new System.Random(7);
            var trees = new[] { Load(WorldKitStarterContent.Decor, "Agac_Cam"), Load(WorldKitStarterContent.Decor, "Agac_Yuvarlak") };
            var used = new HashSet<Vector2Int>();
            for (int i = 0; i < 60; i++)
            {
                var cell = new Vector2Int(random.Next(Size), random.Next(Size));
                bool border = cell.x < 2 || cell.y < 2 || cell.x >= Size - 2 || cell.y >= Size - 2;
                bool onRoad = (cell.x >= 15 && cell.x <= 16) || (cell.y >= 15 && cell.y <= 16);
                if (!border || onRoad || !used.Add(cell))
                    continue;
                var prefab = trees[random.Next(trees.Length)];
                var rotation = Quaternion.Euler(0f, random.Next(360), 0f);
                WorldKitBrush.PlacePiece(prefab, null, World(cell.x + 0.5f, 0, cell.y + 0.5f), rotation, Vector3.one * (0.85f + (float)random.NextDouble() * 0.3f));
            }
            Place(WorldKitStarterContent.Decor, "Kaya_Kumesi", 11, 26);
            Place(WorldKitStarterContent.Decor, "Cali", 12, 24);

            // ---------- Karakterler ----------
            Place(WorldKitStarterContent.Npc, "NPC_Koylu", 13, 13);
            Place(WorldKitStarterContent.Npc, "NPC_Tuccar", 20, 17);
            Place(WorldKitStarterContent.Npc, "NPC_Muhafiz", 24, 20);
            Place(WorldKitStarterContent.Npc, "Dusman", 27, 3);
            Place(WorldKitStarterContent.Reference, "Oyuncu", 15, 12);

            WorldKitEvents.RaiseEditFinished();
        }

        // ---------------- Yardımcılar ----------------

        static GameObject Load(string category, string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{WorldKitSettings.Instance.piecesFolder}/{category}/{name}.prefab");

        /// <summary>Parçayı sol-alt köşesi (x, z) hücresinde olacak şekilde koyar.</summary>
        static void Place(string category, string name, int x, int z, int rotationSteps = 0, int level = 0)
        {
            var prefab = Load(category, name);
            if (prefab == null)
            {
                Debug.LogWarning($"[World Kit] Örnek köy: '{category}/{name}' bulunamadı, atlandı.");
                return;
            }

            var footprint = WorldKitGrid.RotatedFootprint(WorldKitScene.GetFootprint(prefab), rotationSteps);
            var center = World(x + Mathf.Max(1, footprint.x) * 0.5f, level, z + Mathf.Max(1, footprint.y) * 0.5f);
            WorldKitBrush.PlacePiece(prefab, null, center, Quaternion.Euler(0f, rotationSteps * 90f, 0f));
        }

        static Vector3 World(float cellX, int level, float cellZ)
        {
            float cs = WorldKitGrid.CellSize;
            return new Vector3(cellX * cs, level * WorldKitGrid.LevelHeight, cellZ * cs);
        }

        static List<Vector2Int> Rect(int x, int z, int width, int depth)
        {
            var cells = new List<Vector2Int>();
            for (int i = 0; i < width; i++)
            for (int j = 0; j < depth; j++)
                cells.Add(new Vector2Int(x + i, z + j));
            return cells;
        }

        /// <summary>Ayarlardaki zemin türünün numarası (yoksa 0 = varsayılan zemin).</summary>
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
