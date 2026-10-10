using System.Collections.Generic;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>Bir parçanın konacağı konum, dönüş ve (isteğe bağlı) ölçek.</summary>
    public struct Placement
    {
        public Vector3 Position;
        public Quaternion Rotation;
        /// <summary>Düzgün ölçek. 0 veya negatif = 1 (varsayılan).</summary>
        public float Scale;

        public Placement(Vector3 position, Quaternion rotation, float scale = 1f)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        public Vector3 ScaleVector => Vector3.one * (Scale > 0f ? Scale : 1f);
    }

    /// <summary>Fırçanın sürükleme ekseni kilidi.</summary>
    public enum DragAxis { None, X, Z }

    /// <summary>
    /// Tut-çek yerleşiminde hangi parçanın nereye geleceğini hesaplar.
    ///
    /// İki tür parça var:
    /// - Alan parçaları (kaya, sütun, kasa...): footprint'leri hücreleri kaplar, hücre grid'ine snap olur.
    /// - Çizgi parçaları (duvar, çit, köşe direği): footprint'in bir ekseni 0'dır. Grid köşeleri arasına
    ///   oturur; parçanın uzun ekseni yerel X (veya footprint öyleyse Z) boyuncadır.
    ///
    /// Çizgi parçalarında her şey köşe tabanlıdır: imleç en yakın köşeye yapışır, parça o köşeden
    /// fırça yönüne doğru uzanır. Sürükleme aynı köşeden başlar, böylece önizleme hiç zıplamaz.
    /// </summary>
    public static class WorldKitLayout
    {
        public const int MaxPlacements = 2500;

        public static bool IsLinear(Vector2Int footprint) => footprint.x == 0 || footprint.y == 0;

        static int Length(Vector2Int footprint) => Mathf.Max(footprint.x, footprint.y);

        static bool LocalAxisIsX(Vector2Int footprint) => footprint.x >= footprint.y;

        static Vector2Int ToCorner(Vector3 point) => WorldKitGrid.ToCorner(point);

        static Vector3 CornerToWorld(Vector2Int corner, int level) => WorldKitGrid.CornerToWorld(corner, level);

        /// <summary>
        /// Ham sürükleme farkından eksen kilidini günceller. İlk belirgin harekette kilitlenir,
        /// imleç başlangıç noktasına dönünce çözülür; çapraz sürüklerken titreme olmaz.
        /// </summary>
        public static DragAxis UpdateAxisLock(DragAxis current, Vector3 anchor, Vector3 point)
        {
            float dx = Mathf.Abs(point.x - anchor.x);
            float dz = Mathf.Abs(point.z - anchor.z);
            float cell = WorldKitGrid.CellSize;

            if (dx < cell * 0.5f && dz < cell * 0.5f)
                return DragAxis.None;
            if (current != DragAxis.None)
                return current;
            if (dx < cell * 0.75f && dz < cell * 0.75f)
                return DragAxis.None;
            return dx >= dz ? DragAxis.X : DragAxis.Z;
        }

        /// <summary>Noktayı kilitli eksene izdüşürür (diğer ekseni başlangıçta sabitler).</summary>
        public static Vector3 Constrain(Vector3 anchor, Vector3 point, DragAxis axis)
        {
            switch (axis)
            {
                case DragAxis.X: return new Vector3(point.x, point.y, anchor.z);
                case DragAxis.Z: return new Vector3(anchor.x, point.y, point.z);
                default: return anchor;
            }
        }

        // ---------------- Çizgi parçaları ----------------

        /// <summary>Uzun ekseni verilen dünya eksenine hizalayan açılardan, kullanıcının seçtiğine en yakın olanı.</summary>
        static float AlignedAngle(Vector2Int footprint, bool alongWorldX, int rotationSteps)
        {
            // Yerel uzun eksen X ise: X boyunca 0°/180°, Z boyunca 90°/270°.
            float baseAngle = (alongWorldX == LocalAxisIsX(footprint)) ? 0f : 90f;
            float preferred = rotationSteps * 90f;
            float a = baseAngle, b = baseAngle + 180f;
            return Mathf.Abs(Mathf.DeltaAngle(a, preferred)) <= Mathf.Abs(Mathf.DeltaAngle(b, preferred)) ? a : b;
        }

        /// <summary>Çizgi parçasının iki ucundan, verilen noktaya uzak olanı (zincirin devam edeceği köşe).</summary>
        public static Vector3 FarEnd(Placement placement, Vector2Int footprint, Vector3 from)
        {
            var localAxis = LocalAxisIsX(footprint) ? Vector3.right : Vector3.forward;
            var half = placement.Rotation * localAxis * (Length(footprint) * WorldKitGrid.CellSize * 0.5f);
            var a = placement.Position + half;
            var b = placement.Position - half;
            from.y = a.y;
            return (a - from).sqrMagnitude >= (b - from).sqrMagnitude ? a : b;
        }

        /// <summary>Tek çizgi parçası: köşeden fırça yönüne doğru uzanır.</summary>
        public static Placement LinearSingle(Vector3 point, Vector2Int footprint, int rotationSteps, int level)
        {
            var corner = CornerToWorld(ToCorner(point), level);
            var rotation = Quaternion.Euler(0f, rotationSteps * 90f, 0f);
            var localAxis = LocalAxisIsX(footprint) ? Vector3.right : Vector3.forward;
            var direction = rotation * localAxis;
            return new Placement(corner + direction * (Length(footprint) * WorldKitGrid.CellSize * 0.5f), rotation);
        }

        /// <summary>
        /// Köşeden kilitli eksen boyunca düz hat. Uzunluk parça boyunun katlarına yuvarlanır, hat asla taşmaz.
        /// </summary>
        public static void Line(List<Placement> result, Vector3 anchor, Vector3 point, Vector2Int footprint, int rotationSteps, int level, DragAxis axis)
        {
            if (axis == DragAxis.None)
            {
                result.Add(LinearSingle(anchor, footprint, rotationSteps, level));
                return;
            }

            var start = ToCorner(anchor);
            var end = ToCorner(Constrain(anchor, point, axis));
            var delta = end - start;
            int distance = axis == DragAxis.X ? delta.x : delta.y;
            int sign = distance >= 0 ? 1 : -1;
            var step = axis == DragAxis.X ? new Vector2Int(sign, 0) : new Vector2Int(0, sign);

            AddRun(result, start, step, Mathf.Abs(distance), footprint, rotationSteps, level, includeEnds: true);
        }

        /// <summary>
        /// Dikdörtgen oda: dört kenar. Kenar uzunlukları parça boyunun katlarına yuvarlanır,
        /// böylece köşeler tam birleşir. Köşe direği gibi noktasal parçalar yalnızca dört köşeye konur.
        /// </summary>
        public static void Room(List<Placement> result, Vector3 anchor, Vector3 point, Vector2Int footprint, int rotationSteps, int level)
        {
            int length = Mathf.Max(1, Length(footprint));
            var start = ToCorner(anchor);
            var raw = ToCorner(point) - start;

            int RoundTo(int value)
            {
                int rounded = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(value) / (float)length)) * length;
                return value >= 0 ? rounded : -rounded;
            }

            var size = new Vector2Int(RoundTo(raw.x), RoundTo(raw.y));
            var c0 = start;
            var c1 = start + new Vector2Int(size.x, 0);
            var c2 = start + size;
            var c3 = start + new Vector2Int(0, size.y);

            if (Length(footprint) == 0)
            {
                foreach (var corner in new[] { c0, c1, c2, c3 })
                    result.Add(new Placement(CornerToWorld(corner, level), Quaternion.Euler(0f, rotationSteps * 90f, 0f)));
                return;
            }

            AddRun(result, c0, Sign(c1 - c0), Mathf.Abs(size.x), footprint, rotationSteps, level, false);
            AddRun(result, c1, Sign(c2 - c1), Mathf.Abs(size.y), footprint, rotationSteps, level, false);
            AddRun(result, c2, Sign(c3 - c2), Mathf.Abs(size.x), footprint, rotationSteps, level, false);
            AddRun(result, c3, Sign(c0 - c3), Mathf.Abs(size.y), footprint, rotationSteps, level, false);
        }

        static Vector2Int Sign(Vector2Int v) => new Vector2Int(System.Math.Sign(v.x), System.Math.Sign(v.y));

        /// <summary>Başlangıç köşesinden 'step' yönünde 'distance' hücrelik bir sıra.</summary>
        static void AddRun(List<Placement> result, Vector2Int start, Vector2Int step, int distance, Vector2Int footprint, int rotationSteps, int level, bool includeEnds)
        {
            bool alongX = step.x != 0;
            var rotation = Quaternion.Euler(0f, AlignedAngle(footprint, alongX, rotationSteps), 0f);
            int length = Length(footprint);

            if (length == 0)
            {
                // Noktasal parça (köşe direği): hattaki her köşeye bir tane.
                int from = includeEnds ? 0 : 1;
                for (int i = from; i <= distance && result.Count < MaxPlacements; i++)
                    result.Add(new Placement(CornerToWorld(start + step * i, level), rotation));
                return;
            }

            int count = Mathf.Max(1, Mathf.RoundToInt(distance / (float)length));
            var origin = CornerToWorld(start, level);
            var direction = new Vector3(step.x, 0f, step.y) * WorldKitGrid.CellSize;
            for (int i = 0; i < count && result.Count < MaxPlacements; i++)
                result.Add(new Placement(origin + direction * (length * (i + 0.5f)), rotation));
        }

        // ---------------- Alan parçaları ----------------

        /// <summary>Dikdörtgeni footprint adımlarıyla doldurur. outlineOnly ise yalnızca kenar sırasını koyar.</summary>
        public static void Area(List<Placement> result, Vector3 anchor, Vector3 point, Vector2Int footprint, int rotationSteps, int level, bool outlineOnly)
        {
            var f = WorldKitGrid.RotatedFootprint(footprint, rotationSteps);
            var rotation = Quaternion.Euler(0f, rotationSteps * 90f, 0f);
            var a = WorldKitGrid.Snap(anchor, footprint, rotationSteps, level);
            var b = WorldKitGrid.Snap(point, footprint, rotationSteps, level);

            float stepX = Mathf.Max(1, f.x) * WorldKitGrid.CellSize;
            float stepZ = Mathf.Max(1, f.y) * WorldKitGrid.CellSize;
            int countX = Mathf.RoundToInt(Mathf.Abs(b.x - a.x) / stepX) + 1;
            int countZ = Mathf.RoundToInt(Mathf.Abs(b.z - a.z) / stepZ) + 1;
            float dirX = b.x >= a.x ? stepX : -stepX;
            float dirZ = b.z >= a.z ? stepZ : -stepZ;

            for (int z = 0; z < countZ; z++)
            {
                for (int x = 0; x < countX; x++)
                {
                    if (outlineOnly && x > 0 && z > 0 && x < countX - 1 && z < countZ - 1)
                        continue;
                    if (result.Count >= MaxPlacements)
                        return;
                    result.Add(new Placement(new Vector3(a.x + x * dirX, a.y, a.z + z * dirZ), rotation));
                }
            }
        }
    }
}
