using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// "Üste Kopyala": seçili parçaların bir kopyasını tam üstlerine, kat yüksekliğine yuvarlanmış olarak koyar.
    /// Binaya kat eklemek, kule ve duvar yükseltmek için. Yeni kopyalar seçilir; tekrar basınca bir kat daha eklenir.
    /// </summary>
    public static class StackSelection
    {
        [MenuItem("Tools/World Kit/Seçimi Üste Kopyala")]
        public static void Execute()
        {
            var pieces = WorldKitScene.SelectedPieces();
            if (pieces.Count == 0)
                return;

            float levelHeight = WorldKitGrid.LevelHeight;
            float bottom = pieces.Min(p => p.transform.position.y);
            float top = pieces.Max(Top);
            int levels = Mathf.Max(1, Mathf.CeilToInt((top - bottom) / levelHeight - 0.01f));
            var offset = Vector3.up * (levels * levelHeight);

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            var copies = new List<GameObject>();
            foreach (var piece in pieces)
            {
                var source = WorldKitScene.GetSource(piece);
                var copy = source != null
                    ? (GameObject)PrefabUtility.InstantiatePrefab(source, piece.transform.parent)
                    : Object.Instantiate(piece, piece.transform.parent);
                copy.name = piece.name;
                copy.transform.SetPositionAndRotation(piece.transform.position + offset, piece.transform.rotation);
                copy.transform.localScale = piece.transform.localScale;
                Undo.RegisterCreatedObjectUndo(copy, "World Kit: Üste Kopyala");
                WorldKitEvents.RaisePiecePlaced(copy);
                copies.Add(copy);
            }

            Selection.objects = copies.Cast<Object>().ToArray();
            WorldKitEvents.RaiseEditFinished();
            Undo.SetCurrentGroupName("World Kit: Üste Kopyala");
            Undo.CollapseUndoOperations(group);
        }

        [MenuItem("Tools/World Kit/Seçimi Üste Kopyala", true)]
        static bool Validate() => WorldKitScene.SelectedPieces().Count > 0;

        /// <summary>Parçanın en üst noktası (renderer'ı yoksa konumu).</summary>
        static float Top(GameObject piece)
        {
            var renderers = piece.GetComponentsInChildren<Renderer>();
            return renderers.Length > 0 ? renderers.Max(r => r.bounds.max.y) : piece.transform.position.y;
        }
    }

    sealed class StackSelectionItem : WorldKitToolbarItem
    {
        public override string Label => "Üste Kopyala";
        public override string Tooltip => "Seçili parçaların bir kopyasını üstlerine koyar (kat ekle, kule yükselt). Tekrar bas: bir kat daha.";
        public override int Order => 25;
        public override string Group => "Seçim";
        public override Texture2D Icon => WorldKitIcons.StackUp;
        public override bool IsEnabled => WorldKitScene.SelectedPieces().Count > 0;
        public override void Execute() => StackSelection.Execute();
    }
}
