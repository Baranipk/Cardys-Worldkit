using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

namespace WorldKit.Editor
{
    /// <summary>
    /// Başlangıç içeriği: duvar, bina, dekor, NPC ve ölçü referansı parçalarını ProBuilder ile üretip
    /// proje ayarlarındaki parçalar klasörüne prefab olarak kaydeder. Zemin materyallerini de ayarlara atar.
    ///
    /// Parçalar 1 hücre = 1 birim olarak tasarlanmıştır ve yatayda grid boyutuyla ölçeklenir;
    /// yükseklikler (duvar, karakter) metredir ve ayarlardan gelir. İsimlerdeki "1m", "2x2" gibi
    /// ifadeler hücre sayısını belirtir.
    ///
    /// Prefab'lar yerinde güncellendiği için yeniden üretmek sahnedeki kopyaları bozmaz.
    /// Bu sınıf tamamen isteğe bağlıdır: kendi prefab'larınızla da World Kit çalışır.
    /// </summary>
    public static class WorldKitStarterContent
    {
        public const string Structure = "Yapi";
        public const string Decor = "Dekor";
        public const string Reference = "Referans";
        public const string Building = "Bina";
        public const string Npc = "NPC";

        static WorldKitSettings Settings => WorldKitSettings.Instance;
        static string PiecesRoot => Settings.piecesFolder;
        static string MaterialsFolder => Settings.materialsFolder;

        /// <summary>Oyuncu referans prefab'ının yolu (test oyuncusunun görseli olarak kullanılır).</summary>
        public static string PlayerReferencePath => $"{PiecesRoot}/{Reference}/Oyuncu.prefab";

        /// <summary>Köşe direği prefab'ının yolu (ayarlarda başka direk seçilmemişse otomatik köşelerde kullanılır).</summary>
        public static string CornerPostPath => $"{PiecesRoot}/{Structure}/Kose_Diregi.prefab";

        /// <summary>Zemin "Duvarla Çevir" işleminin varsayılan duvarı.</summary>
        public static string DefaultWallPath => $"{PiecesRoot}/{Structure}/Duvar_1m.prefab";

        static float CharacterHeight => Settings.characterHeight;

        [MenuItem("Tools/World Kit/Başlangıç İçeriğini Üret", priority = 20)]
        static void GenerateFromMenu()
        {
            if (Directory.Exists(PiecesRoot) && !EditorUtility.DisplayDialog(
                    "World Kit",
                    "Başlangıç içeriği üretilecek: " + PiecesRoot + "\n\n" +
                    "Aynı isimli prefab'ların üzerine yazılır ve sahnedeki kopyalar güncellenir. " +
                    "Kendi eklediğin prefab'lara dokunulmaz.",
                    "Üret", "Vazgeç"))
                return;

            Generate();
        }

        public static void Generate()
        {
            WorldKitSettings.EnsureFolder(MaterialsFolder);
            var grid = CreateGridTexture();

            var ground = CreateMaterial("Zemin", new Color(0.62f, 0.62f, 0.64f), grid);
            var wall = CreateMaterial("Duvar", new Color(0.84f, 0.84f, 0.87f), grid);
            var platform = CreateMaterial("Platform", new Color(0.86f, 0.63f, 0.4f), grid);

            // Zemin fırçası bu materyalleri kullanır (atanmamışsa).
            var settings = Settings;
            if (settings.floorGroundMaterial == null)
                settings.floorGroundMaterial = ground;
            if (settings.floorRaisedMaterial == null)
                settings.floorRaisedMaterial = platform;

            // Hazır zemin türleri (yalnızca hiç tür tanımlanmamışsa).
            if (settings.floorTypes.Count == 0)
            {
                void AddType(string typeName, string file, Color color, float smoothness = 0.1f)
                {
                    var material = CreateMaterial(file, color, grid);
                    if (material.HasProperty("_Smoothness"))
                        material.SetFloat("_Smoothness", smoothness);
                    settings.floorTypes.Add(new WorldKitSettings.FloorType { name = typeName, material = material });
                }

                AddType("Çimen", "Zemin_Cimen", new Color(0.45f, 0.68f, 0.36f));
                AddType("Toprak", "Zemin_Toprak", new Color(0.55f, 0.42f, 0.3f));
                AddType("Taş", "Zemin_Tas", new Color(0.5f, 0.52f, 0.58f));
                AddType("Kum", "Zemin_Kum", new Color(0.86f, 0.78f, 0.55f));
                AddType("Su", "Zemin_Su", new Color(0.3f, 0.55f, 0.85f), 0.85f);
                AddType("Yol", "Zemin_Yol", new Color(0.72f, 0.66f, 0.56f));
            }
            EditorUtility.SetDirty(settings);
            var rock = CreateMaterial("Kaya", new Color(0.52f, 0.52f, 0.57f), grid);
            var foliage = CreateMaterial("Bitki", new Color(0.42f, 0.66f, 0.36f), grid);
            var wood = CreateMaterial("Ahsap", new Color(0.6f, 0.43f, 0.28f), grid);
            var reference = CreateMaterial("Referans", new Color(0.92f, 0.3f, 0.3f), grid);

            float h = settings.wallHeight;
            float t = settings.wallThickness * 0.5f;

            // ---------- Yapı ----------
            // Zemin ve platformlar prefab değil: palette "Zemin" fırçasıyla tek mesh olarak boyanır (WorldKitFloor).

            Save(Structure, "Duvar_1m", new Vector2Int(1, 0), Box(new Vector3(-0.5f, 0f, -t), new Vector3(0.5f, h, t), wall));
            Save(Structure, "Duvar_2m", new Vector2Int(2, 0), Box(new Vector3(-1f, 0f, -t), new Vector3(1f, h, t), wall));
            Save(Structure, "Duvar_4m", new Vector2Int(4, 0), Box(new Vector3(-2f, 0f, -t), new Vector3(2f, h, t), wall));
            Save(Structure, "Alcak_Duvar_2m", new Vector2Int(2, 0), Box(new Vector3(-1f, 0f, -t), new Vector3(1f, 1f, t), wall));
            Save(Structure, "Kose_Diregi", new Vector2Int(0, 0), Box(new Vector3(-0.15f, 0f, -0.15f), new Vector3(0.15f, h + 0.05f, 0.15f), wall));
            Save(Structure, "Kapili_Duvar_3m", new Vector2Int(3, 0),
                Box(new Vector3(-1.5f, 0f, -t), new Vector3(-0.7f, h, t), wall),
                Box(new Vector3(0.7f, 0f, -t), new Vector3(1.5f, h, t), wall),
                Box(new Vector3(-0.7f, 2.3f, -t), new Vector3(0.7f, h, t), wall));

            Save(Structure, "Sutun", new Vector2Int(1, 1), Box(new Vector3(-0.3f, 0f, -0.3f), new Vector3(0.3f, h, 0.3f), wall));
            Save(Structure, "Yuvarlak_Sutun", new Vector2Int(1, 1), Cylinder(Vector3.zero, 0.35f, h, 8, wall));

            Save(Structure, "Merdiven_2x2", new Vector2Int(2, 2), Stair(new Vector3(-1f, 0f, -1f), new Vector3(2f, 1f, 2f), 5, platform));
            Save(Structure, "Rampa_2x2", new Vector2Int(2, 2), Wedge(new Vector3(-1f, 0f, -1f), new Vector3(1f, 1f, 1f), platform));

            // Pencere ve geçitler
            float sill = Mathf.Min(1f, h * 0.35f), lintel = Mathf.Min(2f, h - 0.4f);
            Save(Structure, "Pencereli_Duvar_2m", new Vector2Int(2, 0),
                Box(new Vector3(-1f, 0f, -t), new Vector3(1f, sill, t), wall),
                Box(new Vector3(-1f, lintel, -t), new Vector3(1f, h, t), wall),
                Box(new Vector3(-1f, sill, -t), new Vector3(-0.5f, lintel, t), wall),
                Box(new Vector3(0.5f, sill, -t), new Vector3(1f, lintel, t), wall));
            float gate = Mathf.Min(2.6f, h - 0.3f);
            Save(Structure, "Genis_Gecit_4m", new Vector2Int(4, 0),
                Box(new Vector3(-2f, 0f, -t), new Vector3(-1.2f, h, t), wall),
                Box(new Vector3(1.2f, 0f, -t), new Vector3(2f, h, t), wall),
                Box(new Vector3(-1.2f, gate, -t), new Vector3(1.2f, h, t), wall));

            // Kale duvarı: daha yüksek ve kalın, üstünde mazgallar
            float castle = h + 1f;
            Save(Structure, "Kale_Duvari_2m", new Vector2Int(2, 0),
                Box(new Vector3(-1f, 0f, -0.3f), new Vector3(1f, castle, 0.3f), wall),
                Box(new Vector3(-1f, castle, -0.3f), new Vector3(-0.6f, castle + 0.6f, 0.3f), wall),
                Box(new Vector3(-0.2f, castle, -0.3f), new Vector3(0.2f, castle + 0.6f, 0.3f), wall),
                Box(new Vector3(0.6f, castle, -0.3f), new Vector3(1f, castle + 0.6f, 0.3f), wall));

            // Köprü: üst yüzü 1. katta, iki platformu birbirine bağlar (Z boyunca 4 hücre)
            float deck = WorldKitGrid.LevelHeight;
            Save(Structure, "Kopru_2x4", new Vector2Int(2, 4),
                Box(new Vector3(-1f, deck - 0.2f, -2f), new Vector3(1f, deck, 2f), wood),
                Box(new Vector3(-1f, deck, -2f), new Vector3(-0.9f, deck + 0.9f, 2f), wood),
                Box(new Vector3(0.9f, deck, -2f), new Vector3(1f, deck + 0.9f, 2f), wood));

            // İskele: yer seviyesinde güverte, suya (alt kata) inen direkler
            Save(Structure, "Iskele_2x3", new Vector2Int(2, 3),
                Box(new Vector3(-1f, -0.15f, -1.5f), new Vector3(1f, 0.05f, 1.5f), wood),
                Cylinder(new Vector3(-0.85f, -1.2f, -1.35f), 0.1f, 1.2f, 6, wood),
                Cylinder(new Vector3(0.85f, -1.2f, -1.35f), 0.1f, 1.2f, 6, wood),
                Cylinder(new Vector3(-0.85f, -1.2f, 1.35f), 0.1f, 1.2f, 6, wood),
                Cylinder(new Vector3(0.85f, -1.2f, 1.35f), 0.1f, 1.2f, 6, wood));

            // ---------- Dekor ----------
            Save(Decor, "Kaya_Kucuk", new Vector2Int(1, 1), Rock(Vector3.zero, new Vector3(0.9f, 0.6f, 0.8f), 1, rock));
            Save(Decor, "Kaya_Buyuk", new Vector2Int(2, 2), Rock(Vector3.zero, new Vector3(1.9f, 1.3f, 1.7f), 2, rock));
            Save(Decor, "Cali", new Vector2Int(1, 1), Rock(Vector3.zero, new Vector3(0.9f, 0.6f, 0.9f), 3, foliage));
            Save(Decor, "Agac_Cam", new Vector2Int(1, 1),
                Cylinder(Vector3.zero, 0.15f, 0.8f, 6, wood),
                Cone(new Vector3(0f, 0.6f, 0f), 0.9f, 2.6f, 7, foliage));
            Save(Decor, "Agac_Yuvarlak", new Vector2Int(1, 1),
                Cylinder(Vector3.zero, 0.18f, 1.2f, 6, wood),
                Rock(new Vector3(0f, 1f, 0f), new Vector3(1.8f, 1.6f, 1.8f), 4, foliage));
            Save(Decor, "Cit_2m", new Vector2Int(2, 0),
                Box(new Vector3(-1f, 0f, -0.06f), new Vector3(-0.88f, 1.1f, 0.06f), wood),
                Box(new Vector3(-0.06f, 0f, -0.06f), new Vector3(0.06f, 1.1f, 0.06f), wood),
                Box(new Vector3(0.88f, 0f, -0.06f), new Vector3(1f, 1.1f, 0.06f), wood),
                Box(new Vector3(-1f, 0.45f, -0.04f), new Vector3(1f, 0.55f, 0.04f), wood),
                Box(new Vector3(-1f, 0.85f, -0.04f), new Vector3(1f, 0.95f, 0.04f), wood));
            Save(Decor, "Varil", new Vector2Int(1, 1), Cylinder(Vector3.zero, 0.35f, 1f, 10, wood));
            Save(Decor, "Kasa", new Vector2Int(1, 1), Box(new Vector3(-0.4f, 0f, -0.4f), new Vector3(0.4f, 0.8f, 0.4f), wood));
            Save(Decor, "Kaya_Kumesi", new Vector2Int(2, 2),
                Rock(new Vector3(-0.4f, 0f, -0.3f), new Vector3(1.2f, 0.9f, 1.1f), 5, rock),
                Rock(new Vector3(0.5f, 0f, 0.2f), new Vector3(0.8f, 0.6f, 0.7f), 6, rock),
                Rock(new Vector3(-0.2f, 0f, 0.6f), new Vector3(0.5f, 0.4f, 0.5f), 7, rock));
            Save(Decor, "Ot", new Vector2Int(1, 1),
                Cone(new Vector3(0f, 0f, 0f), 0.12f, 0.6f, 4, foliage),
                Cone(new Vector3(0.18f, 0f, 0.1f), 0.1f, 0.45f, 4, foliage),
                Cone(new Vector3(-0.15f, 0f, 0.12f), 0.1f, 0.5f, 4, foliage),
                Cone(new Vector3(0.05f, 0f, -0.18f), 0.1f, 0.4f, 4, foliage));
            Save(Decor, "Fener", new Vector2Int(1, 1),
                Cylinder(Vector3.zero, 0.06f, 2.2f, 6, wood),
                Box(new Vector3(-0.15f, 2.1f, -0.15f), new Vector3(0.15f, 2.45f, 0.15f), CreateMaterial("Isik", new Color(1f, 0.85f, 0.45f), grid)),
                Cone(new Vector3(0f, 2.45f, 0f), 0.22f, 0.2f, 4, wood));
            Save(Decor, "Tabela", new Vector2Int(1, 1),
                Cylinder(Vector3.zero, 0.05f, 1.4f, 6, wood),
                Box(new Vector3(-0.4f, 1f, -0.04f), new Vector3(0.4f, 1.35f, 0.04f), wood));

            // ---------- Bina (kapı her zaman +Z, yani mavi okun gösterdiği taraf) ----------
            var roof = CreateMaterial("Cati", new Color(0.72f, 0.33f, 0.26f), grid);
            var door = CreateMaterial("Kapi", new Color(0.36f, 0.25f, 0.18f), grid);

            Save(Building, "Ev_Kucuk_3x3", new Vector2Int(3, 3),
                Box(new Vector3(-1.5f, 0f, -1.5f), new Vector3(1.5f, 2.6f, 1.5f), wall),
                Prism(new Vector3(-1.7f, 2.6f, -1.7f), new Vector3(1.7f, 4f, 1.7f), roof),
                Door(1.5f, 0.9f, 1.6f, door));
            Save(Building, "Ev_Buyuk_4x5", new Vector2Int(4, 5),
                Box(new Vector3(-2f, 0f, -2.5f), new Vector3(2f, 3f, 2.5f), wall),
                Prism(new Vector3(-2.2f, 3f, -2.7f), new Vector3(2.2f, 4.8f, 2.7f), roof),
                Door(2.5f, 1f, 1.7f, door));
            Save(Building, "Han_Iki_Katli_5x4", new Vector2Int(5, 4),
                Box(new Vector3(-2.5f, 0f, -2f), new Vector3(2.5f, 5f, 2f), wall),
                Prism(new Vector3(-2.7f, 5f, -2.2f), new Vector3(2.7f, 6.8f, 2.2f), roof),
                Box(new Vector3(-2.7f, 2.5f, -2.2f), new Vector3(2.7f, 2.7f, 2.2f), door), // kat ayrımı
                Door(2f, 1.4f, 1.8f, door));
            Save(Building, "Dukkan_4x3", new Vector2Int(4, 3),
                Box(new Vector3(-2f, 0f, -1.5f), new Vector3(2f, 2.6f, 1.5f), wall),
                Box(new Vector3(-2.2f, 2.6f, -1.7f), new Vector3(2.2f, 2.8f, 1.7f), roof),
                Box(new Vector3(-1.8f, 1.9f, 1.5f), new Vector3(1.8f, 2f, 2.4f), roof),     // tente
                Box(new Vector3(-1.4f, 0f, 1.6f), new Vector3(0.6f, 0.9f, 2.1f), door),     // tezgah
                Door(1.5f, 0.9f, 1.6f, door, 1.3f));
            Save(Building, "Kule_3x3", new Vector2Int(3, 3),
                Cylinder(Vector3.zero, 1.4f, 6f, 8, wall),
                Cone(new Vector3(0f, 6f, 0f), 1.75f, 2.2f, 8, roof),
                Door(1.3f, 0.9f, 1.6f, door));
            Save(Building, "Kale_Kulesi_3x3", new Vector2Int(3, 3),
                Box(new Vector3(-1.5f, 0f, -1.5f), new Vector3(1.5f, 7f, 1.5f), wall),
                // Mazgallar: dört köşe ve kenar ortaları
                Box(new Vector3(-1.5f, 7f, -1.5f), new Vector3(-0.9f, 7.6f, -0.9f), wall),
                Box(new Vector3(0.9f, 7f, -1.5f), new Vector3(1.5f, 7.6f, -0.9f), wall),
                Box(new Vector3(-1.5f, 7f, 0.9f), new Vector3(-0.9f, 7.6f, 1.5f), wall),
                Box(new Vector3(0.9f, 7f, 0.9f), new Vector3(1.5f, 7.6f, 1.5f), wall),
                Box(new Vector3(-0.3f, 7f, -1.5f), new Vector3(0.3f, 7.6f, -1.1f), wall),
                Box(new Vector3(-0.3f, 7f, 1.1f), new Vector3(0.3f, 7.6f, 1.5f), wall),
                Box(new Vector3(-1.5f, 7f, -0.3f), new Vector3(-1.1f, 7.6f, 0.3f), wall),
                Box(new Vector3(1.1f, 7f, -0.3f), new Vector3(1.5f, 7.6f, 0.3f), wall),
                Door(1.5f, 1.1f, 2f, door));
            Save(Building, "Ahir_4x3", new Vector2Int(4, 3),
                Box(new Vector3(-2f, 0f, -1.5f), new Vector3(2f, 2.4f, 1.5f), wood),
                Prism(new Vector3(-2.2f, 2.4f, -1.7f), new Vector3(2.2f, 3.8f, 1.7f), roof),
                Door(1.5f, 1.8f, 2f, door));

            // ---------- NPC (chibi, boy: ayarlardaki karakter boyu) ----------
            var villager = CreateMaterial("NPC_Koylu", new Color(0.95f, 0.78f, 0.35f), grid);
            var merchant = CreateMaterial("NPC_Tuccar", new Color(0.4f, 0.72f, 0.42f), grid);
            var guard = CreateMaterial("NPC_Muhafiz", new Color(0.35f, 0.55f, 0.9f), grid);
            var enemy = CreateMaterial("NPC_Dusman", new Color(0.6f, 0.35f, 0.75f), grid);
            var skin = CreateMaterial("Ten", new Color(0.96f, 0.82f, 0.68f), grid);

            Save(Npc, "NPC_Koylu", new Vector2Int(1, 1), Chibi(villager, skin));
            Save(Npc, "NPC_Tuccar", new Vector2Int(1, 1), Chibi(merchant, skin,
                Box(new Vector3(-0.22f, 0.35f, -0.46f), new Vector3(0.22f, 0.85f, -0.24f), wood))); // sırt çantası
            Save(Npc, "NPC_Muhafiz", new Vector2Int(1, 1), Chibi(guard, skin,
                Cylinder(new Vector3(0.38f, 0f, 0.05f), 0.035f, 2f, 6, wood)));                     // mızrak
            RenameIfExists(Npc, "Dusman_1.5m", "Dusman");
            Save(Npc, "Dusman", new Vector2Int(1, 1), Chibi(enemy, enemy,
                Cone(new Vector3(-0.17f, CharacterHeight - 0.1f, 0f), 0.07f, 0.25f, 5, enemy),                       // boynuzlar
                Cone(new Vector3(0.17f, CharacterHeight - 0.1f, 0f), 0.07f, 0.25f, 5, enemy)));

            // ---------- Ölçü referansı (build'e girmez: EditorOnly) ----------
            // Eski isimli kopyalar yeniden adlandırılır (GUID korunur, sahnedekiler bozulmaz).
            RenameIfExists(Reference, "Oyuncu_1.8m", "Oyuncu");
            RenameIfExists(Reference, "Oyuncu_1.5m", "Oyuncu");
            Save(Reference, "Oyuncu", new Vector2Int(1, 1), Chibi(reference, skin));
            Save(Reference, "Buyuk_Dusman_3m", new Vector2Int(2, 2),
                Cylinder(Vector3.zero, 0.9f, 3f, 12, reference),
                Box(new Vector3(-0.15f, 2.3f, 0.85f), new Vector3(0.15f, 2.6f, 1.15f), reference));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            WorldKitThumbnails.Clear();
            Debug.Log($"[World Kit] Parçalar üretildi: {PiecesRoot}");
        }

        // ---------------- Kayıt ----------------

        static void Save(string category, string name, Vector2Int footprint, params ProBuilderMesh[] parts)
        {
            var target = parts[0];
            if (parts.Length > 1)
            {
                CombineMeshes.Combine(parts, target);
                foreach (var part in parts.Skip(1))
                {
                    if (part != null)
                        Object.DestroyImmediate(part.gameObject);
                }
            }

            // Parçalar 1 hücre = 1 birim tasarlandı: yatayda grid boyutuna ölçekle, yükseklik metre kalsın.
            float cell = Settings.cellSize;
            if (!Mathf.Approximately(cell, 1f))
                target.positions = target.positions.Select(p => new Vector3(p.x * cell, p.y, p.z * cell)).ToArray();

            target.ToMesh();
            target.Refresh();

            var go = target.gameObject;
            go.name = name;
            go.AddComponent<WorldKitPiece>().footprint = footprint;

            // Referanslar build'e girmez. NPC'ler yalnızca konum işareti; collider'ları olmaz.
            if (category == Reference)
                go.tag = "EditorOnly";
            else if (category != Npc && go.GetComponent<MeshCollider>() == null)
                go.AddComponent<MeshCollider>();

            var folder = $"{PiecesRoot}/{category}";
            WorldKitSettings.EnsureFolder(folder);
            PrefabUtility.SaveAsPrefabAsset(go, $"{folder}/{name}.prefab");
            Object.DestroyImmediate(go);
        }

        // ---------------- Materyaller ----------------

        static Texture2D CreateGridTexture()
        {
            var path = MaterialsFolder + "/GB_Grid.png";
            const int size = 128;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool edge = x < 2 || x >= size - 2 || y < 2 || y >= size - 2;
                    bool half = x == size / 2 || x == size / 2 - 1 || y == size / 2 || y == size / 2 - 1;
                    byte v = edge ? (byte)160 : half ? (byte)222 : (byte)255;
                    pixels[y * size + x] = new Color32(v, v, v, 255);
                }
            }

            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material CreateMaterial(string name, Color color, Texture2D grid)
        {
            var path = $"{MaterialsFolder}/GB_{name}.mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.color = color;
            material.mainTexture = grid;
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.1f);

            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------------- Mesh yardımcıları ----------------
        // Tüm yardımcılar transform'u sıfırlayıp konumu doğrudan vertex'lere yazar,
        // böylece parçalar birleştirilince pivot her zaman alt-ortada kalır.

        static ProBuilderMesh Box(Vector3 min, Vector3 max, Material material)
        {
            var pb = Bake(ShapeGenerator.GenerateCube(PivotLocation.Center, max - min));
            Translate(pb, min - GetBounds(pb).min);
            pb.SetMaterial(pb.faces, material);
            return pb;
        }

        static ProBuilderMesh Cylinder(Vector3 bottomCenter, float radius, float height, int sides, Material material)
        {
            var pb = Bake(ShapeGenerator.GenerateCylinder(PivotLocation.Center, sides, radius, height, 0));
            MoveBottomCenterTo(pb, bottomCenter);
            pb.SetMaterial(pb.faces, material);
            return pb;
        }

        static ProBuilderMesh Cone(Vector3 bottomCenter, float radius, float height, int sides, Material material)
        {
            var pb = Bake(ShapeGenerator.GenerateCone(PivotLocation.Center, radius, height, sides));
            MoveBottomCenterTo(pb, bottomCenter);
            pb.SetMaterial(pb.faces, material);
            return pb;
        }

        static ProBuilderMesh Stair(Vector3 min, Vector3 size, int steps, Material material)
        {
            var pb = Bake(ShapeGenerator.GenerateStair(PivotLocation.Center, size, steps, true));
            Translate(pb, min - GetBounds(pb).min);
            pb.SetMaterial(pb.faces, material);
            return pb;
        }

        /// <summary>+Z yönünde yükselen rampa.</summary>
        static ProBuilderMesh Wedge(Vector3 min, Vector3 max, Material material)
        {
            var p0 = new Vector3(min.x, min.y, min.z);
            var p1 = new Vector3(max.x, min.y, min.z);
            var p2 = new Vector3(min.x, min.y, max.z);
            var p3 = new Vector3(max.x, min.y, max.z);
            var p4 = new Vector3(min.x, max.y, max.z);
            var p5 = new Vector3(max.x, max.y, max.z);

            // Her yüzün kendi vertex'leri olsun ki kenarlar keskin kalsın.
            var positions = new List<Vector3>();
            var faces = new List<Face>();

            void AddFace(Vector3[] verts, int[] triangles)
            {
                var offset = positions.Count;
                positions.AddRange(verts);
                faces.Add(new Face(triangles.Select(i => i + offset).ToArray()));
            }

            AddFace(new[] { p0, p1, p3, p2 }, new[] { 0, 1, 2, 0, 2, 3 }); // alt
            AddFace(new[] { p2, p3, p5, p4 }, new[] { 0, 1, 2, 0, 2, 3 }); // arka
            AddFace(new[] { p0, p1, p5, p4 }, new[] { 0, 3, 2, 0, 2, 1 }); // eğim
            AddFace(new[] { p0, p2, p4 }, new[] { 0, 1, 2 });              // sol
            AddFace(new[] { p1, p3, p5 }, new[] { 0, 2, 1 });              // sağ

            var pb = ProBuilderMesh.Create(positions, faces);
            pb.SetMaterial(pb.faces, material);
            return pb;
        }

        /// <summary>Hafif bozulmuş low-poly yuvarlak form (kaya, çalı, ağaç tepesi).</summary>
        static ProBuilderMesh Rock(Vector3 bottomCenter, Vector3 size, int seed, Material material)
        {
            var pb = Bake(ShapeGenerator.GenerateIcosahedron(PivotLocation.Center, 0.5f, 0));
            var random = new System.Random(seed);
            var offsets = new Dictionary<Vector3Int, Vector3>();

            pb.positions = pb.positions.Select(p =>
            {
                // Aynı noktadaki vertex'ler aynı miktarda kaysın ki mesh'te delik açılmasın.
                var key = Vector3Int.RoundToInt(p * 1000f);
                if (!offsets.TryGetValue(key, out var offset))
                {
                    var scale = 0.85f + (float)random.NextDouble() * 0.3f;
                    offset = p * scale;
                    offsets[key] = offset;
                }

                return Vector3.Scale(offset, size);
            }).ToArray();

            MoveBottomCenterTo(pb, bottomCenter);
            pb.SetMaterial(pb.faces, material);
            return pb;
        }

        /// <summary>Çatı: sırtı Z ekseni boyunca uzanan üçgen prizma, verilen kutuya sığdırılır.</summary>
        static ProBuilderMesh Prism(Vector3 min, Vector3 max, Material material)
        {
            var pb = Bake(ShapeGenerator.GeneratePrism(PivotLocation.Center, Vector3.one));
            FitToBox(pb, min, max);
            pb.SetMaterial(pb.faces, material);
            return pb;
        }

        static ProBuilderMesh Sphere(Vector3 min, Vector3 max, Material material)
        {
            var pb = Bake(ShapeGenerator.GenerateIcosahedron(PivotLocation.Center, 0.5f, 1));
            FitToBox(pb, min, max);
            pb.SetMaterial(pb.faces, material);
            return pb;
        }

        /// <summary>Binanın +Z cephesine yapışık kapı. frontZ: cephenin Z konumu.</summary>
        static ProBuilderMesh Door(float frontZ, float width, float height, Material material, float offsetX = 0f)
        {
            return Box(
                new Vector3(offsetX - width * 0.5f, 0f, frontZ - 0.02f),
                new Vector3(offsetX + width * 0.5f, height, frontZ + 0.06f),
                material);
        }

        /// <summary>
        /// Chibi karakter: kısa gövde + büyük kafa, toplam CharacterHeight. Kafadaki küçük çıkıntı ön yönü (+Z) gösterir.
        /// </summary>
        static ProBuilderMesh[] Chibi(Material body, Material head, params ProBuilderMesh[] extras)
        {
            float bodyHeight = CharacterHeight * 0.57f;              // ~0.85 m
            float headSize = CharacterHeight - bodyHeight + 0.02f;       // ~0.67 m, gövdeye hafif gömülü
            float half = headSize * 0.5f;

            var parts = new List<ProBuilderMesh>
            {
                Cylinder(Vector3.zero, 0.28f, bodyHeight, 8, body),
                Sphere(new Vector3(-half, CharacterHeight - headSize, -half), new Vector3(half, CharacterHeight, half), head),
                Box(new Vector3(-0.06f, CharacterHeight - half - 0.05f, half - 0.04f), new Vector3(0.06f, CharacterHeight - half + 0.05f, half + 0.08f), body),
            };
            parts.AddRange(extras);
            return parts.ToArray();
        }

        /// <summary>Eski isimli prefab'ı yeniden adlandırır; GUID korunduğu için sahnedeki kopyalar bozulmaz.</summary>
        static void RenameIfExists(string category, string oldName, string newName)
        {
            var oldPath = $"{PiecesRoot}/{category}/{oldName}.prefab";
            var newPath = $"{PiecesRoot}/{category}/{newName}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(oldPath) != null && AssetDatabase.LoadAssetAtPath<GameObject>(newPath) == null)
                AssetDatabase.MoveAsset(oldPath, newPath);
        }

        /// <summary>Mesh'i ölçekleyip taşıyarak verilen kutuya tam sığdırır.</summary>
        static void FitToBox(ProBuilderMesh pb, Vector3 min, Vector3 max)
        {
            var bounds = GetBounds(pb);
            var target = max - min;
            var scale = new Vector3(
                bounds.size.x > 0f ? target.x / bounds.size.x : 1f,
                bounds.size.y > 0f ? target.y / bounds.size.y : 1f,
                bounds.size.z > 0f ? target.z / bounds.size.z : 1f);
            pb.positions = pb.positions.Select(p => min + Vector3.Scale(p - bounds.min, scale)).ToArray();
        }

        static ProBuilderMesh Bake(ProBuilderMesh pb)
        {
            var transform = pb.transform;
            var matrix = transform.localToWorldMatrix;
            pb.positions = pb.positions.Select(p => matrix.MultiplyPoint3x4(p)).ToArray();
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;
            return pb;
        }

        static void Translate(ProBuilderMesh pb, Vector3 delta)
        {
            pb.positions = pb.positions.Select(p => p + delta).ToArray();
        }

        static void MoveBottomCenterTo(ProBuilderMesh pb, Vector3 target)
        {
            var bounds = GetBounds(pb);
            Translate(pb, target - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
        }

        static Bounds GetBounds(ProBuilderMesh pb)
        {
            var positions = pb.positions;
            var bounds = new Bounds(positions[0], Vector3.zero);
            for (int i = 1; i < positions.Count; i++)
                bounds.Encapsulate(positions[i]);
            return bounds;
        }
    }
}
