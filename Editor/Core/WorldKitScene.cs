using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WorldKit.Editor
{
    /// <summary>
    /// Sahnedeki World Kit hiyerarşisi için yardımcılar: <Kök> / <Grup> / <Parça>.
    /// Parça = kökün torunu olan (gruptaki) obje. Zemin (WorldKitFloor) kökün doğrudan çocuğudur, parça sayılmaz.
    /// </summary>
    public static class WorldKitScene
    {
        static WorldKitSettings Settings => WorldKitSettings.Instance;

        /// <summary>Aktif sahnenin kök objesi (yoksa null).</summary>
        public static GameObject FindRoot()
        {
            var rootName = Settings.rootName;
            return SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == rootName);
        }

        /// <summary>Kök objeyi bulur, yoksa Undo'lu oluşturur.</summary>
        public static Transform GetRoot()
        {
            var root = FindRoot();
            if (root == null)
            {
                root = new GameObject(Settings.rootName);
                Undo.RegisterCreatedObjectUndo(root, "World Kit: Kök Oluştur");
            }

            return root.transform;
        }

        /// <summary>Kökün altındaki grubu bulur, yoksa Undo'lu oluşturur.</summary>
        public static Transform GetGroup(string name)
        {
            var root = GetRoot();
            var group = root.Find(name);
            if (group == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root, false);
                Undo.RegisterCreatedObjectUndo(go, "World Kit: Grup Oluştur");
                group = go.transform;
            }

            return group;
        }

        /// <summary>Verilen objenin ait olduğu parça (alt objesine tıklansa bile), parça değilse null.</summary>
        public static GameObject GetPiece(GameObject any)
        {
            if (any == null)
                return null;

            var t = any.transform;
            while (t.parent != null && t.parent.parent != null)
                t = t.parent;

            var root = t.parent != null ? t.parent.parent : null;
            if (root == null || root.parent != null || root.name != Settings.rootName)
                return null;

            return t.gameObject;
        }

        /// <summary>Aktif sahnedeki tüm parçalar.</summary>
        public static IEnumerable<GameObject> AllPieces()
        {
            var root = FindRoot();
            if (root == null)
                yield break;

            foreach (Transform group in root.transform)
            foreach (Transform piece in group)
                yield return piece.gameObject;
        }

        /// <summary>Seçimdeki parçalar (alt objeler seçiliyse parçalarına çevrilir, tekrarlar atılır).</summary>
        public static List<GameObject> SelectedPieces()
        {
            return Selection.gameObjects.Select(GetPiece).Where(p => p != null).Distinct().ToList();
        }

        /// <summary>Parçanın footprint'i (WorldKitPiece yoksa 1×1).</summary>
        public static Vector2Int GetFootprint(GameObject piece)
        {
            var info = piece.GetComponent<WorldKitPiece>();
            return info != null ? info.footprint : Vector2Int.one;
        }

        /// <summary>Parçanın kaynak prefab'ı (prefab kopyası değilse null).</summary>
        public static GameObject GetSource(GameObject piece) => PrefabUtility.GetCorrespondingObjectFromSource(piece);

        /// <summary>
        /// Çizgi parçasının (duvar, çit) iki ucu. Çizgi parçası değilse veya uzunluğu 0 ise false.
        /// </summary>
        public static bool TryGetSegment(GameObject piece, out Vector3 a, out Vector3 b)
        {
            a = b = Vector3.zero;
            var info = piece.GetComponent<WorldKitPiece>();
            if (info == null || !WorldKitLayout.IsLinear(info.footprint))
                return false;

            int length = Mathf.Max(info.footprint.x, info.footprint.y);
            if (length == 0)
                return false;

            var localAxis = info.footprint.x >= info.footprint.y ? Vector3.right : Vector3.forward;
            var half = piece.transform.rotation * localAxis * (length * WorldKitGrid.CellSize * 0.5f);
            a = piece.transform.position + half;
            b = piece.transform.position - half;
            return true;
        }
    }
}
