using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// Zemin setini uygular: zemin verisinden her kat kenarı için duvar, köşe ve çimen saçağı parçalarını hesaplar,
    /// zeminin altındaki "Zemin_Seti" objesine dizer. Zemin her değiştiğinde kendiliğinden güncellenir; yalnızca değişen
    /// parçalar eklenir/silinir (Undo'lu, düzenlemeyle aynı Undo grubunda).
    ///
    /// Hesap, katman katman (her kat dilimi k: k..k+1) yapılır: bir hücre o dilimde "dolu"dur, eğer yüksekliği k'dan büyükse
    /// (rampalı hücrenin eğimli dilimi dolu sayılmaz). Dolu ile boş arasındaki her kenara duvar, her köşeye köşe dolgusu konur.
    /// </summary>
    [InitializeOnLoad]
    public static class WorldKitFloorSetBuilder
    {
        public const string ContainerName = "Zemin_Seti";

        static WorldKitFloorSetBuilder()
        {
            WorldKitEvents.FloorChanged += floor => Rebuild(floor);
        }

        enum Role { Wall, WallOuter, WallInner, Trim, TrimOuter, TrimInner }

        struct Item
        {
            public GameObject Prefab;
            public Vector3 Position;
            public float Yaw;
        }

        /// <summary>Sahnedeki zemini (yoksa hiçbir şey yapmaz) ayarlardaki setle yeniden kurar.</summary>
        [MenuItem("Tools/World Kit/Zemin Setini Yeniden Kur", priority = 30)]
        public static void RebuildActive()
        {
            var root = WorldKitScene.FindRoot();
            var floor = root != null ? root.GetComponentInChildren<WorldKitFloor>() : null;
            if (floor != null)
                Rebuild(floor);
        }

        public static void Rebuild(WorldKitFloor floor)
        {
            if (floor == null)
                return;

            var set = WorldKitSettings.Instance.floorSet;
            var container = floor.transform.Find(ContainerName);
            if (set == null)
            {
                if (container != null)
                    Undo.DestroyObjectImmediate(container.gameObject);
                return;
            }

            var desired = Compute(floor, set);

            if (container == null)
            {
                var go = new GameObject(ContainerName);
                go.transform.SetParent(floor.transform, false);
                Undo.RegisterCreatedObjectUndo(go, "World Kit: Zemin Seti");
                container = go.transform;
            }

            // Mevcut parçalar: ad = anahtar
            var existing = new Dictionary<string, Transform>();
            foreach (Transform child in container)
                existing[child.name] = child;

            foreach (var pair in existing)
                if (!desired.ContainsKey(pair.Key))
                    Undo.DestroyObjectImmediate(pair.Value.gameObject);

            foreach (var pair in desired)
            {
                if (existing.ContainsKey(pair.Key))
                    continue;
                var item = pair.Value;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(item.Prefab, container);
                instance.name = pair.Key;
                instance.transform.localPosition = item.Position;
                instance.transform.localRotation = Quaternion.Euler(0f, item.Yaw, 0f);
                Undo.RegisterCreatedObjectUndo(instance, "World Kit: Zemin Seti");
            }
        }

        static Dictionary<string, Item> Compute(WorldKitFloor floor, WorldKitFloorSet set)
        {
            var result = new Dictionary<string, Item>();
            float cs = WorldKitGrid.CellSize;
            float lh = WorldKitGrid.LevelHeight;
            var cells = new HashSet<Vector2Int>(floor.Cells);
            if (cells.Count == 0)
                return result;

            int Height(Vector2Int c) => floor.TryGetHeight(c, out var h) ? h : int.MinValue;
            bool IsRamp(Vector2Int c) => floor.IsSloped(c);
            bool Exists(Vector2Int c) => cells.Contains(c);
            // k dilimi (k..k+1) dolu mu? Dilimler 0. kattan başlar; rampanın eğimli dilimi dolu sayılmaz.
            bool Solid(Vector2Int c, int k) => k >= 0 && Exists(c) && Height(c) > k;
            // Hücrenin üst yüzü tam bu dilimin tepesinde mi (saçak için)?
            bool TopAt(Vector2Int c, int k) => Exists(c) && Height(c) == k + 1 && !(set.skipRampTops && IsRamp(c));

            void Add(Role role, List<GameObject> list, Vector3 position, float yaw)
            {
                if (list == null || list.Count == 0)
                    return;
                var key = $"{role}_{Mathf.RoundToInt(position.x * 100f)}_{Mathf.RoundToInt(position.y * 100f)}_{Mathf.RoundToInt(position.z * 100f)}_{Mathf.RoundToInt(Mathf.Repeat(yaw, 360f))}";
                int hash = unchecked(key.GetHashCode() & int.MaxValue);
                var prefab = list[hash % list.Count];
                if (prefab == null)
                    return;
                result[key + "_" + prefab.name] = new Item { Prefab = prefab, Position = position, Yaw = yaw };
            }

            float YawOf(Vector2Int dir) => Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

            int maxLevel = cells.Max(Height);
            var directions = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

            // ---------- Kenarlar: duvar ve saçak ----------
            foreach (var cell in cells)
            {
                foreach (var dir in directions)
                {
                    var other = cell + dir;
                    bool otherExists = Exists(other);
                    if (!otherExists && !set.wallsAtMapEdge)
                        continue;
                    int top = Height(cell);
                    for (int k = 0; k < top; k++)
                    {
                        if (!Solid(cell, k) || Solid(other, k))
                            continue;
                        // Kenar ortası, dışa (other'a) bakan
                        var mid = new Vector3((cell.x + 0.5f + dir.x * 0.5f) * cs, k * lh, (cell.y + 0.5f + dir.y * 0.5f) * cs);
                        Add(Role.Wall, set.wall, mid, YawOf(dir));
                        if (TopAt(cell, k))
                            Add(Role.Trim, set.trim, new Vector3(mid.x, (k + 1) * lh, mid.z), YawOf(dir));
                    }
                }
            }

            // ---------- Köşeler ----------
            // Köşe noktası (ix, iz); çevresindeki dört hücre: SW, SE, NW, NE
            var corners = new HashSet<Vector2Int>();
            foreach (var cell in cells)
                for (int dx = 0; dx <= 1; dx++)
                for (int dz = 0; dz <= 1; dz++)
                    corners.Add(new Vector2Int(cell.x + dx, cell.y + dz));

            var offsets = new[] { new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(0, 0) };
            foreach (var corner in corners)
            {
                var around = offsets.Select(o => corner + o).ToArray();
                if (!set.wallsAtMapEdge && around.Any(c => !Exists(c)))
                    continue;
                int top = around.Max(Height);
                for (int k = 0; k < top && k <= maxLevel; k++)
                {
                    var solid = around.Where(c => Solid(c, k)).ToList();
                    var p = new Vector3(corner.x * cs, k * lh, corner.y * cs);

                    if (solid.Count == 1 || (solid.Count == 2 && solid[0].x != solid[1].x && solid[0].y != solid[1].y))
                    {
                        // Dış köşe(ler): her dolu hücre için, hücreden köşeye doğru dışarı bakan
                        foreach (var c in solid)
                        {
                            var face = new Vector2Int(c.x < corner.x ? 1 : -1, c.y < corner.y ? 1 : -1);
                            float yaw = CornerYaw(face);
                            Add(Role.WallOuter, set.wallOuterCorner, p, yaw);
                            if (TopAt(c, k))
                                Add(Role.TrimOuter, set.trimOuterCorner, new Vector3(p.x, (k + 1) * lh, p.z), yaw);
                        }
                    }
                    else if (solid.Count == 3)
                    {
                        // İç köşe: boş hücreye doğru bakan
                        var empty = around.First(c => !Solid(c, k));
                        var face = new Vector2Int(empty.x < corner.x ? -1 : 1, empty.y < corner.y ? -1 : 1);
                        float yaw = CornerYaw(face);
                        Add(Role.WallInner, set.wallInnerCorner, p, yaw);
                        // Saçak iç köşesi: boş hücreye komşu iki hücrenin tepesi bu dilimde
                        var sideA = new Vector2Int(empty.x == corner.x ? corner.x - 1 : corner.x, empty.y);
                        var sideB = new Vector2Int(empty.x, empty.y == corner.y ? corner.y - 1 : corner.y);
                        if (TopAt(sideA, k) && TopAt(sideB, k))
                            Add(Role.TrimInner, set.trimInnerCorner, new Vector3(p.x, (k + 1) * lh, p.z), yaw);
                    }
                }
            }

            return result;
        }

        /// <summary>Köşe parçasının yönü: yerel (+X, +Z) çaprazı face yönüne dönsün.</summary>
        static float CornerYaw(Vector2Int face) => Mathf.Atan2(face.x, face.y) * Mathf.Rad2Deg - 45f;
    }
}
