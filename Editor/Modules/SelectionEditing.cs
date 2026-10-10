using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// Fırça kapalıyken seçili World Kit parçalarını klavyeyle grid'e bağlı düzenler:
    /// Oklar: 1 hücre taşı (ekrana göre) · PgUp/PgDn: kat · R / Shift+R: 90° döndür ·
    /// G: grid'e oturt · Delete: sil (köşe direkleri de güncellenir) · Ctrl+D: Unity'nin çoğaltması.
    /// Kısayollar yalnızca Scene odaktayken ve seçimde parça varken geçerlidir; diğer zamanlarda
    /// Unity'nin kendi tuşları (ör. R = ölçekle) aynen çalışır.
    /// </summary>
    [InitializeOnLoad]
    static class SelectionEditing
    {
        static bool s_HasPieces;
        static readonly SelectionContext s_Context = new SelectionContext();

        class SelectionContext : IShortcutContext
        {
            public bool active => s_HasPieces && !WorldKitBrush.IsActive && EditorWindow.focusedWindow is SceneView;
        }

        static SelectionEditing()
        {
            ShortcutManager.RegisterContext(s_Context);
            Selection.selectionChanged += UpdateSelection;
            SceneView.duringSceneGui += DrawHud;
            UpdateSelection();
        }

        static void UpdateSelection()
        {
            s_HasPieces = Selection.gameObjects.Any(g => WorldKitScene.GetPiece(g) != null);
            SceneView.RepaintAll();
        }

        // ---------------- Kısayollar ----------------

        [Shortcut("World Kit/Seçim/Yukarı Taşı", typeof(SelectionContext), KeyCode.UpArrow)]
        static void MoveUp() => Move(ScreenAxis(0, 1), 0);

        [Shortcut("World Kit/Seçim/Aşağı Taşı", typeof(SelectionContext), KeyCode.DownArrow)]
        static void MoveDown() => Move(ScreenAxis(0, -1), 0);

        [Shortcut("World Kit/Seçim/Sağa Taşı", typeof(SelectionContext), KeyCode.RightArrow)]
        static void MoveRight() => Move(ScreenAxis(1, 0), 0);

        [Shortcut("World Kit/Seçim/Sola Taşı", typeof(SelectionContext), KeyCode.LeftArrow)]
        static void MoveLeft() => Move(ScreenAxis(-1, 0), 0);

        [Shortcut("World Kit/Seçim/Kat Yukarı", typeof(SelectionContext), KeyCode.PageUp)]
        static void LevelUp() => Move(Vector2Int.zero, 1);

        [Shortcut("World Kit/Seçim/Kat Aşağı", typeof(SelectionContext), KeyCode.PageDown)]
        static void LevelDown() => Move(Vector2Int.zero, -1);

        [Shortcut("World Kit/Seçim/Saat Yönünde Döndür", typeof(SelectionContext), KeyCode.R)]
        static void RotateClockwise() => Rotate(1);

        [Shortcut("World Kit/Seçim/Saat Yönü Tersine Döndür", typeof(SelectionContext), KeyCode.R, ShortcutModifiers.Shift)]
        static void RotateCounterClockwise() => Rotate(-1);

        [Shortcut("World Kit/Seçim/Grid'e Oturt", typeof(SelectionContext), KeyCode.G)]
        static void SnapShortcut() => SnapToGrid();

        [Shortcut("World Kit/Seçim/Sil", typeof(SelectionContext), KeyCode.Delete)]
        static void Delete() => Edit("World Kit: Sil", pieces =>
        {
            foreach (var piece in pieces)
            {
                WorldKitEvents.RaisePieceRemoving(piece);
                Undo.DestroyObjectImmediate(piece);
            }
        });

        [MenuItem("Tools/World Kit/Seçimi Grid'e Oturt", priority = 41)]
        public static void SnapToGrid() => Edit("World Kit: Grid'e Oturt", pieces =>
        {
            foreach (var piece in pieces)
                Transform(piece, piece.transform.position, piece.transform.rotation);
        });

        [MenuItem("Tools/World Kit/Seçimi Grid'e Oturt", true)]
        static bool ValidateSnap() => s_HasPieces;

        // ---------------- İşlemler ----------------

        /// <summary>Ekrandaki bir yönü (sağ/yukarı) Scene kamerasına göre en yakın dünya eksenine çevirir.</summary>
        static Vector2Int ScreenAxis(int right, int up)
        {
            var sceneView = SceneView.lastActiveSceneView;
            var forward = sceneView != null ? sceneView.rotation * Vector3.forward : Vector3.forward;
            var f = Mathf.Abs(forward.x) >= Mathf.Abs(forward.z)
                ? new Vector2Int((int)Mathf.Sign(forward.x), 0)
                : new Vector2Int(0, (int)Mathf.Sign(forward.z));
            var r = new Vector2Int(f.y, -f.x);
            return f * up + r * right;
        }

        static void Move(Vector2Int cells, int levels)
        {
            var offset = new Vector3(cells.x * WorldKitGrid.CellSize, levels * WorldKitGrid.LevelHeight, cells.y * WorldKitGrid.CellSize);
            Edit("World Kit: Taşı", pieces =>
            {
                foreach (var piece in pieces)
                    Transform(piece, piece.transform.position + offset, piece.transform.rotation);
            });
        }

        /// <summary>Seçimi, merkezine en yakın grid köşesi etrafında döndürür.</summary>
        static void Rotate(int steps)
        {
            var rotation = Quaternion.Euler(0f, steps * 90f, 0f);
            Edit("World Kit: Döndür", pieces =>
            {
                var center = pieces.Aggregate(Vector3.zero, (sum, p) => sum + p.transform.position) / pieces.Count;
                var pivot = pieces.Count == 1
                    ? center
                    : WorldKitGrid.CornerToWorld(WorldKitGrid.ToCorner(center), 0);
                pivot.y = 0f;

                foreach (var piece in pieces)
                {
                    var t = piece.transform;
                    Transform(piece, pivot + rotation * (t.position - pivot), rotation * t.rotation);
                }
            });
        }

        /// <summary>Parçayı yeni konum/dönüşe taşır ve footprint'ine göre grid'e oturtur.</summary>
        static void Transform(GameObject piece, Vector3 position, Quaternion rotation)
        {
            if (piece == null)
                return; // işlem sırasında başka bir modül tarafından kaldırılmış olabilir
            int steps = Mathf.RoundToInt(rotation.eulerAngles.y / 90f) & 3;
            int level = Mathf.RoundToInt(position.y / WorldKitGrid.LevelHeight);
            // Snap, döndürülmüş footprint'in tek/çift kuralını uygular: bu, çizgi parçalarının
            // uçlarını da köşelere oturtur (ör. 3×0 duvarın ortası hücre ortasına, ince ekseni çizgiye).
            var snapped = WorldKitGrid.Snap(position, WorldKitScene.GetFootprint(piece), steps, level);

            WorldKitEvents.RaisePieceMoving(piece);
            Undo.RecordObject(piece.transform, "World Kit: Taşı");
            piece.transform.SetPositionAndRotation(snapped, Quaternion.Euler(0f, steps * 90f, 0f));
            WorldKitEvents.RaisePieceMoved(piece);
        }

        /// <summary>Seçimdeki parçalar üzerinde tek bir Undo grubu olarak işlem yapar.</summary>
        static void Edit(string name, System.Action<List<GameObject>> action)
        {
            var pieces = WorldKitScene.SelectedPieces();
            if (pieces.Count == 0)
                return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            action(pieces);
            WorldKitEvents.RaiseEditFinished();

            Undo.SetCurrentGroupName(name);
            Undo.CollapseUndoOperations(group);
            UpdateSelection();
        }

        // ---------------- Bilgi kutusu ----------------

        static GUIStyle s_Style;

        static void DrawHud(SceneView sceneView)
        {
            if (Event.current.type != EventType.Repaint || !s_HasPieces || WorldKitBrush.IsActive)
                return;

            s_Style ??= new GUIStyle(EditorStyles.helpBox) { fontSize = 11, richText = true, padding = new RectOffset(8, 8, 6, 6) };
            int count = WorldKitScene.SelectedPieces().Count;
            var content = new GUIContent(
                $"<b>{count} parça seçili</b>\n" +
                "Oklar: taşı   PgUp/PgDn: kat   R: döndür   G: grid'e oturt   Ctrl+D: çoğalt   Delete: sil");

            Handles.BeginGUI();
            var height = sceneView.camera.pixelRect.height / EditorGUIUtility.pixelsPerPoint;
            var size = s_Style.CalcSize(content);
            GUI.Label(new Rect(10f, height - size.y - 10f, size.x, size.y), content, s_Style);
            Handles.EndGUI();
        }
    }
}
