using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// Şablonlar (stamp): seçili parçaları ve altlarındaki zemini tek bir prefab olarak kaydeder.
    /// Şablonlar parçalar klasöründeki "Sablonlar" alt klasörüne kaydedilir, böylece palette kendi sekmeleriyle
    /// görünür ve her prefab gibi yerleştirilir (R ile döner). Yerleşince şablon açılır: parçalar normal parçalar
    /// gibi kendi gruplarına dağılır ve köşe direği/kapı açma gibi modüller onları görür.
    /// </summary>
    [InitializeOnLoad]
    static class Stamps
    {
        public const string FolderName = "Sablonlar";

        static Stamps()
        {
            WorldKitEvents.PiecePlaced += Unpack;
        }

        // ---------------- Oluşturma ----------------

        [MenuItem("Tools/World Kit/Seçimden Şablon Oluştur", priority = 42)]
        public static void CreateFromSelection()
        {
            // Otomatik köşe direkleri şablona girmez; şablon açılınca zaten yeniden oluşurlar.
            var pieces = WorldKitScene.SelectedPieces()
                .Where(p => p.GetComponent<WorldKitStamp>() == null && p.transform.parent.name != AutoCornerPosts.GroupName)
                .ToList();
            if (pieces.Count == 0)
            {
                EditorUtility.DisplayDialog("World Kit", "Önce şablona girecek World Kit parçalarını seç.", "Tamam");
                return;
            }

            var settings = WorldKitSettings.Instance;
            var folder = $"{settings.piecesFolder}/{FolderName}";
            WorldKitSettings.EnsureFolder(folder);
            var path = EditorUtility.SaveFilePanelInProject("Şablonu Kaydet", "Yeni_Sablon", "prefab",
                "Şablonun adı (palette bu adla görünür).", folder);
            if (string.IsNullOrEmpty(path))
                return;

            float cell = WorldKitGrid.CellSize;
            float levelHeight = WorldKitGrid.LevelHeight;
            const float epsilon = 0.01f;

            // Parçaların kapladığı hücre dikdörtgeni
            var bounds = pieces.SelectMany(p => p.GetComponentsInChildren<Renderer>()).Select(r => r.bounds)
                .Aggregate((a, b) => { a.Encapsulate(b); return a; });
            var min = new Vector2Int(Mathf.FloorToInt((bounds.min.x + epsilon) / cell), Mathf.FloorToInt((bounds.min.z + epsilon) / cell));
            var max = new Vector2Int(Mathf.CeilToInt((bounds.max.x - epsilon) / cell) - 1, Mathf.CeilToInt((bounds.max.z - epsilon) / cell) - 1);
            var size = max - min + Vector2Int.one;
            int baseLevel = pieces.Min(p => Mathf.RoundToInt(p.transform.position.y / levelHeight));

            // Pivot: dikdörtgenin alt-ortası. Snap kuralıyla uyumlu (tek boyut → hücre ortası, çift → çizgi).
            var pivot = new Vector3((min.x + size.x * 0.5f) * cell, baseLevel * levelHeight, (min.y + size.y * 0.5f) * cell);

            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            root.transform.position = pivot;
            root.AddComponent<WorldKitPiece>().footprint = size;
            var stamp = root.AddComponent<WorldKitStamp>();

            foreach (var piece in pieces)
            {
                var source = WorldKitScene.GetSource(piece);
                var copy = source != null
                    ? (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform)
                    : Object.Instantiate(piece, root.transform);
                copy.name = piece.name;
                copy.transform.SetPositionAndRotation(piece.transform.position, piece.transform.rotation);
                copy.transform.localScale = piece.transform.lossyScale;
            }

            // Dikdörtgenin altındaki zemin hücreleri
            var floor = WorldKitScene.FindRoot() != null ? WorldKitScene.FindRoot().GetComponentInChildren<WorldKitFloor>() : null;
            if (floor != null)
            {
                for (int z = min.y; z <= max.y; z++)
                for (int x = min.x; x <= max.x; x++)
                {
                    var c = new Vector2Int(x, z);
                    if (!floor.TryGetHeight(c, out var height))
                        continue;
                    floor.TryGetType(c, out var type);
                    var center = WorldKitGrid.CellCenter(c, 0);
                    stamp.floorCells.Add(new WorldKitStamp.FloorCell
                    {
                        localCenter = new Vector3(center.x - pivot.x, 0f, center.z - pivot.z),
                        height = height - baseLevel,
                        type = type
                    });
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log($"[World Kit] Şablon kaydedildi: {path} ({pieces.Count} parça, {stamp.floorCells.Count} zemin hücresi, {size.x}×{size.y})");
        }

        [MenuItem("Tools/World Kit/Seçimden Şablon Oluştur", true)]
        static bool ValidateCreate() => WorldKitScene.SelectedPieces().Count > 0;

        // ---------------- Yerleştirme (açma) ----------------

        static void Unpack(GameObject placed)
        {
            var stamp = placed != null ? placed.GetComponent<WorldKitStamp>() : null;
            if (stamp == null)
                return;

            if (PrefabUtility.IsPartOfPrefabInstance(placed))
                PrefabUtility.UnpackPrefabInstance(placed, PrefabUnpackMode.OutermostRoot, InteractionMode.UserAction);

            // Zemin hücreleri (şablonun dönüşüyle birlikte)
            if (stamp.floorCells.Count > 0)
            {
                int baseLevel = Mathf.RoundToInt(placed.transform.position.y / WorldKitGrid.LevelHeight);
                var floor = WorldKitBrush.GetFloor();
                Undo.RecordObject(floor, "World Kit: Şablon Zemini");
                foreach (var group in stamp.floorCells.GroupBy(c => (c.height, c.type)))
                {
                    var cells = group.Select(c => WorldKitGrid.ToCell(placed.transform.TransformPoint(c.localCenter))).ToList();
                    floor.SetCells(cells, baseLevel + group.Key.height, group.Key.type);
                }
                WorldKitBrush.SyncFloorMaterials(floor);
                EditorUtility.SetDirty(floor);
                WorldKitEvents.RaiseFloorChanged(floor);
            }

            // Parçaları kendi kategorilerine dağıt
            var parent = placed.transform.parent;
            var children = placed.transform.Cast<Transform>().Select(t => t.gameObject).ToList();
            foreach (var child in children)
            {
                var source = WorldKitScene.GetSource(child);
                var category = source != null ? WorldKitBrush.GetCategory(source) : "Diger";
                Undo.SetTransformParent(child.transform, WorldKitScene.GetGroup(category), "World Kit: Şablonu Aç");
            }

            Undo.DestroyObjectImmediate(placed);
            if (parent != null && parent.childCount == 0)
                Undo.DestroyObjectImmediate(parent.gameObject);

            // Diğer modüller (kapı açma, köşe direkleri) parçaları tek tek görsün.
            foreach (var child in children)
                WorldKitEvents.RaisePiecePlaced(child);
        }
    }

    /// <summary>Palette "Şablon Kaydet" düğmesi.</summary>
    sealed class SaveStampItem : WorldKitToolbarItem
    {
        public override string Label => "Şablon Kaydet";
        public override string Tooltip => "Seçili parçaları (ve altlarındaki zemini) palete şablon olarak kaydeder.";
        public override int Order => 30;
        public override bool IsEnabled => WorldKitScene.SelectedPieces().Count > 0;
        public override void Execute() => Stamps.CreateFromSelection();
    }
}
