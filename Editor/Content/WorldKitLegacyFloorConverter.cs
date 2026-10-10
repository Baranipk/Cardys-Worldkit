using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// Seçili blok parçalarını (zemin karosu, platform...) tek mesh'li WorldKitFloor hücrelerine çevirir
    /// ve objeleri siler. Her parçanın kapladığı hücreler footprint'inden, yüksekliği üst yüzeyinden bulunur.
    /// Yüzlerce ayrı zemin objesini tek objeye indirmek için kullanılır.
    /// </summary>
    public static class WorldKitLegacyFloorConverter
    {
        const string MenuPath = "Tools/World Kit/Seçili Parçaları Zemine Çevir";

        [MenuItem(MenuPath, priority = 40)]
        static void ConvertSelection()
        {
            var pieces = Selection.gameObjects
                .Select(g => PrefabUtility.GetOutermostPrefabInstanceRoot(g) ?? g)
                .Where(g => g.scene.IsValid() && g.GetComponentInChildren<Renderer>() != null)
                .Distinct()
                .ToList();

            if (pieces.Count == 0)
            {
                EditorUtility.DisplayDialog("World Kit", "Önce Hierarchy'de zemine çevrilecek parçaları seç.", "Tamam");
                return;
            }

            if (!EditorUtility.DisplayDialog("World Kit",
                    $"{pieces.Count} obje zemin hücrelerine dönüştürülüp silinecek.\nCtrl+Z ile geri alınabilir.",
                    "Dönüştür", "Vazgeç"))
                return;

            Convert(pieces);
        }

        [MenuItem(MenuPath, true)]
        static bool ValidateConvertSelection() => Selection.gameObjects.Length > 0;

        public static void Convert(IReadOnlyCollection<GameObject> pieces)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            float cell = WorldKitGrid.CellSize;
            float levelHeight = WorldKitGrid.LevelHeight;

            // Aynı hücreye birden fazla parça düşerse en yükseği kazanır.
            var cells = new Dictionary<Vector2Int, int>();
            foreach (var go in pieces)
            {
                var piece = go.GetComponent<WorldKitPiece>();
                var bounds = go.GetComponentsInChildren<Renderer>().Select(r => r.bounds)
                    .Aggregate((a, b) => { a.Encapsulate(b); return a; });

                int steps = Mathf.RoundToInt(go.transform.eulerAngles.y / 90f) & 3;
                var footprint = piece != null
                    ? WorldKitGrid.RotatedFootprint(piece.footprint, steps)
                    : new Vector2Int(Mathf.Max(1, Mathf.RoundToInt(bounds.size.x / cell)), Mathf.Max(1, Mathf.RoundToInt(bounds.size.z / cell)));

                var p = go.transform.position;
                int top = Mathf.RoundToInt(bounds.max.y / levelHeight);
                int minX = Mathf.RoundToInt(p.x / cell - footprint.x * 0.5f);
                int minZ = Mathf.RoundToInt(p.z / cell - footprint.y * 0.5f);

                for (int z = 0; z < Mathf.Max(1, footprint.y); z++)
                for (int x = 0; x < Mathf.Max(1, footprint.x); x++)
                {
                    var c = new Vector2Int(minX + x, minZ + z);
                    cells[c] = cells.TryGetValue(c, out var existing) ? Mathf.Max(existing, top) : top;
                }
            }

            var floor = WorldKitBrush.GetFloor();
            Undo.RecordObject(floor, "World Kit: Zemine Dönüştür");
            foreach (var byHeight in cells.GroupBy(c => c.Value))
                floor.SetCells(byHeight.Select(c => c.Key), byHeight.Key);
            EditorUtility.SetDirty(floor);

            foreach (var go in pieces)
                Undo.DestroyObjectImmediate(go);

            Undo.SetCurrentGroupName("World Kit: Zemine Dönüştür");
            Undo.CollapseUndoOperations(group);
            Debug.Log($"[World Kit] {pieces.Count} obje → {cells.Count} zemin hücresi.");
        }
    }
}
