using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// Dik açıyla birleşen duvarların köşesine (L, T, + birleşimleri) otomatik direk koyar ve
    /// köşe ortadan kalkınca direği kaldırır. Yalnızca o işlemde değişen köşelere bakar.
    ///
    /// "Duvar" sayılanlar: direk prefab'ıyla aynı kategorideki, en az duvar yüksekliğinin %75'i kadar uzun
    /// çizgi parçaları. Böylece çitler ve alçak duvarlar direk almaz.
    /// Otomatik direkler kökün altındaki "Otomatik Köşeler" grubunda durur.
    /// Ayar: Project Settings → World Kit → Auto Corner Posts / Corner Post Prefab.
    /// </summary>
    [InitializeOnLoad]
    static class AutoCornerPosts
    {
        public const string GroupName = "Otomatik Köşeler";

        static readonly HashSet<Vector3Int> s_Pending = new HashSet<Vector3Int>();
        static bool s_Busy;

        static AutoCornerPosts()
        {
            WorldKitEvents.PiecePlaced += Collect;
            WorldKitEvents.PieceRemoving += Collect;
            WorldKitEvents.PieceMoving += Collect;
            WorldKitEvents.PieceMoved += Collect;
            WorldKitEvents.EditFinished += Process;
        }

        static WorldKitSettings Settings => WorldKitSettings.Instance;

        /// <summary>Ayarlardaki direk, yoksa başlangıç içeriğindeki Kose_Diregi.</summary>
        static GameObject PostPrefab =>
            Settings.cornerPostPrefab != null
                ? Settings.cornerPostPrefab
                : AssetDatabase.LoadAssetAtPath<GameObject>(WorldKitStarterContent.CornerPostPath);

        static Vector3Int Key(Vector3 p) => Vector3Int.RoundToInt(p * 100f);
        static Vector3 FromKey(Vector3Int k) => (Vector3)k / 100f;

        static void Collect(GameObject piece)
        {
            if (s_Busy || !Settings.autoCornerPosts || !IsWall(piece, PostPrefab))
                return;
            if (!WorldKitScene.TryGetSegment(piece, out var a, out var b))
                return;

            s_Pending.Add(Key(a));
            s_Pending.Add(Key(b));
        }

        static bool IsWall(GameObject piece, GameObject post)
        {
            if (post == null || piece == null)
                return false;

            var source = WorldKitScene.GetSource(piece);
            if (source == null || source == post)
                return false;
            if (WorldKitBrush.GetCategory(source) != WorldKitBrush.GetCategory(post))
                return false;

            var renderer = piece.GetComponentInChildren<Renderer>();
            return renderer != null && renderer.bounds.size.y >= Settings.wallHeight * 0.75f;
        }

        static void Process()
        {
            if (s_Pending.Count == 0)
                return;

            var post = PostPrefab;
            if (post == null || !Settings.autoCornerPosts)
            {
                s_Pending.Clear();
                return;
            }

            s_Busy = true;
            try
            {
                // Her köşede hangi yönlerde duvar bitiyor?
                var axes = new Dictionary<Vector3Int, (bool x, bool z)>();
                foreach (var piece in WorldKitScene.AllPieces())
                {
                    if (!IsWall(piece, post) || !WorldKitScene.TryGetSegment(piece, out var a, out var b))
                        continue;

                    bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
                    foreach (var end in new[] { a, b })
                    {
                        var key = Key(end);
                        if (!s_Pending.Contains(key))
                            continue;
                        axes.TryGetValue(key, out var v);
                        axes[key] = alongX ? (true, v.z) : (v.x, true);
                    }
                }

                var group = WorldKitScene.FindRoot()?.transform.Find(GroupName);
                var existing = new Dictionary<Vector3Int, GameObject>();
                if (group != null)
                {
                    foreach (Transform child in group)
                        existing[Key(child.position)] = child.gameObject;
                }

                foreach (var key in s_Pending)
                {
                    bool needed = axes.TryGetValue(key, out var v) && v.x && v.z;
                    existing.TryGetValue(key, out var current);

                    if (needed && current == null)
                    {
                        var parent = WorldKitScene.GetGroup(GroupName);
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(post, parent);
                        go.transform.SetPositionAndRotation(FromKey(key), Quaternion.identity);
                        Undo.RegisterCreatedObjectUndo(go, "World Kit: Köşe Direği");
                    }
                    else if (!needed && current != null)
                    {
                        Undo.DestroyObjectImmediate(current);
                    }
                }
            }
            finally
            {
                s_Busy = false;
                s_Pending.Clear();
            }
        }
    }
}
