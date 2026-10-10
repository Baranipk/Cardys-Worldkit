using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// Bir çizgi parçası (duvar, çit) aynı hatta başka bir çizgi parçasının üstüne konunca/taşınınca
    /// eskisini kaldırır. Duvara kapı açmak böylece "kapılı duvarı duvarın üstüne koymak" olur.
    ///
    /// Kısmen örtüşen eski parçalar da kaldırılır; kalan boşluk kısa duvarlarla doldurulabilir.
    /// Ayar: Project Settings → World Kit → Replace Overlapping Lines.
    /// </summary>
    [InitializeOnLoad]
    static class LineReplacement
    {
        const float Epsilon = 0.01f;

        static LineReplacement()
        {
            WorldKitEvents.PiecePlaced += Replace;
            WorldKitEvents.PieceMoved += Replace;
        }

        static void Replace(GameObject placed)
        {
            if (!WorldKitSettings.Instance.replaceOverlappingLines)
                return;
            if (!WorldKitScene.TryGetSegment(placed, out var a, out var b))
                return;

            // Seçili parçalara dokunma: birlikte taşınan duvarlar taşıma sırasında birbirini silmesin.
            var selected = new HashSet<GameObject>(WorldKitScene.SelectedPieces());

            var doomed = new List<GameObject>();
            foreach (var other in WorldKitScene.AllPieces())
            {
                if (other == placed || selected.Contains(other) || !WorldKitScene.TryGetSegment(other, out var c, out var d))
                    continue;
                if (Overlaps(a, b, c, d))
                    doomed.Add(other);
            }

            foreach (var go in doomed)
            {
                WorldKitEvents.RaisePieceRemoving(go);
                Undo.DestroyObjectImmediate(go);
            }
        }

        /// <summary>İki doğru parçası aynı seviyede, aynı hat üzerinde ve uzunluğu sıfırdan büyük bir aralıkta örtüşüyor mu?</summary>
        static bool Overlaps(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            if (Mathf.Abs(a.y - c.y) > WorldKitGrid.LevelHeight * 0.5f)
                return false;

            var dir = Flat(b - a).normalized;
            var other = Flat(d - c).normalized;
            if (Mathf.Abs(dir.x * other.z - dir.z * other.x) > Epsilon)
                return false; // paralel değil

            var normal = new Vector3(-dir.z, 0f, dir.x);
            if (Mathf.Abs(Vector3.Dot(Flat(c - a), normal)) > Epsilon)
                return false; // paralel ama farklı hatlarda

            float a1 = Vector3.Dot(Flat(b - a), dir);
            float c0 = Vector3.Dot(Flat(c - a), dir);
            float c1 = Vector3.Dot(Flat(d - a), dir);
            float overlap = Mathf.Min(a1, Mathf.Max(c0, c1)) - Mathf.Max(0f, Mathf.Min(c0, c1));
            return overlap > Epsilon;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
