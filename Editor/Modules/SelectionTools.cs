using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// Seçim araçları (serbest yerleştirilmiş dekor için):
    /// - Yere Oturt: seçili parçaları altlarındaki yüzeye (zemin, platform, obje) indirir/çıkarır.
    /// - Rastgele Çeşitlendir: seçili parçalara rastgele dönüş ve boyut verir (serbest yerleştirme ayarlarıyla).
    /// İkisi de tek Undo'dur.
    /// </summary>
    public static class WorldKitSelectionTools
    {
        [MenuItem("Tools/World Kit/Seçim/Yere Oturt", priority = 60)]
        public static void DropToGround()
        {
            var pieces = WorldKitScene.SelectedPieces();
            if (pieces.Count == 0)
                return;

            Physics.SyncTransforms();
            var ignored = new HashSet<Collider>(pieces.SelectMany(p => p.GetComponentsInChildren<Collider>()));
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            int moved = 0;

            foreach (var piece in pieces)
            {
                var top = Top(piece) + 0.1f;
                var origin = new Vector3(piece.transform.position.x, top, piece.transform.position.z);
                var hits = Physics.RaycastAll(origin, Vector3.down, 1000f, ~(1 << 2), QueryTriggerInteraction.Ignore)
                    .Where(h => !ignored.Contains(h.collider))
                    .OrderBy(h => h.distance)
                    .ToList();
                if (hits.Count == 0)
                    continue;

                var position = piece.transform.position;
                if (Mathf.Abs(position.y - hits[0].point.y) < 0.001f)
                    continue;

                WorldKitEvents.RaisePieceMoving(piece);
                Undo.RecordObject(piece.transform, "World Kit: Yere Oturt");
                piece.transform.position = new Vector3(position.x, hits[0].point.y, position.z);
                WorldKitEvents.RaisePieceMoved(piece);
                moved++;
            }

            WorldKitEvents.RaiseEditFinished();
            Undo.SetCurrentGroupName("World Kit: Yere Oturt");
            Undo.CollapseUndoOperations(group);
            Debug.Log($"[World Kit] Yere Oturt: {moved} parça.");
        }

        [MenuItem("Tools/World Kit/Seçim/Rastgele Çeşitlendir", priority = 61)]
        public static void Randomize()
        {
            var pieces = WorldKitScene.SelectedPieces();
            if (pieces.Count == 0)
                return;

            bool yaw = WorldKitFreePlacement.RandomYaw, scale = WorldKitFreePlacement.RandomScale;
            if (!yaw && !scale)
            {
                EditorUtility.DisplayDialog("World Kit", "Serbest yerleştirme ayarlarında \"Rastgele dönüş\" veya \"Rastgele boyut\" açık olmalı.", "Tamam");
                return;
            }

            float min = Mathf.Min(WorldKitFreePlacement.ScaleMin, WorldKitFreePlacement.ScaleMax);
            float max = Mathf.Max(WorldKitFreePlacement.ScaleMin, WorldKitFreePlacement.ScaleMax);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            foreach (var piece in pieces)
            {
                Undo.RecordObject(piece.transform, "World Kit: Rastgele Çeşitlendir");
                if (yaw)
                    piece.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                if (scale)
                {
                    var source = WorldKitScene.GetSource(piece);
                    var baseScale = source != null ? source.transform.localScale : Vector3.one;
                    piece.transform.localScale = baseScale * Random.Range(min, max);
                }
                WorldKitEvents.RaisePieceMoved(piece);
            }

            WorldKitEvents.RaiseEditFinished();
            Undo.SetCurrentGroupName("World Kit: Rastgele Çeşitlendir");
            Undo.CollapseUndoOperations(group);
        }

        [MenuItem("Tools/World Kit/Seçim/Yere Oturt", true)]
        [MenuItem("Tools/World Kit/Seçim/Rastgele Çeşitlendir", true)]
        static bool Validate() => WorldKitScene.SelectedPieces().Count > 0;

        static float Top(GameObject piece)
        {
            var renderers = piece.GetComponentsInChildren<Renderer>();
            return renderers.Length > 0 ? renderers.Max(r => r.bounds.max.y) : piece.transform.position.y + 1f;
        }
    }

    sealed class DropToGroundItem : WorldKitToolbarItem
    {
        public override string Label => "Yere Oturt";
        public override string Tooltip => "Seçili parçaları altlarındaki yüzeye (zemin, platform, obje) indirir.";
        public override int Order => 26;
        public override string Group => "Seçim";
        public override Texture2D Icon => WorldKitIcons.DropDown;
        public override bool IsEnabled => WorldKitScene.SelectedPieces().Count > 0;
        public override void Execute() => WorldKitSelectionTools.DropToGround();
    }

    sealed class RandomizeItem : WorldKitToolbarItem
    {
        public override string Label => "Çeşitlendir";
        public override string Tooltip => "Seçili parçalara rastgele dönüş ve boyut verir (serbest yerleştirme ayarlarıyla).";
        public override int Order => 27;
        public override string Group => "Seçim";
        public override Texture2D Icon => WorldKitIcons.Shuffle;
        public override bool IsEnabled => WorldKitScene.SelectedPieces().Count > 0;
        public override void Execute() => WorldKitSelectionTools.Randomize();
    }
}
