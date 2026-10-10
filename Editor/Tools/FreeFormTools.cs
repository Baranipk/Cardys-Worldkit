using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// Yol Boyu: serbestçe çizilen bir yol boyunca belirli aralıklarla parça dizer (yol kenarına fener, patikaya
    /// ağaç, dere boyunca kaya). İsteğe bağlı iki yana, yola dönük. Grid'e bağlı değildir; parçalar yüzeye oturur.
    /// </summary>
    public sealed class PathTool : WorldKitTool
    {
        const string Prefix = "WorldKit.Path.";

        readonly List<Vector3> m_Points = new List<Vector3>();
        Vector3 m_Anchor;
        bool m_Tracking;

        public override string Name => "Yol Boyu";
        public override string Tooltip => "Sürükle: çizdiğin yol boyunca aralıklarla diz (fener, ağaç, kaya). İki yana ve yola dönük seçenekleri palette. (0)";
        public override int Order => 99;
        public override Texture2D Icon => WorldKitIcons.PathTool;
        public override bool FreeForm => true;
        public override bool Supports(BrushContext c) => !c.IsFloor && !c.IsLinear;
        public override bool HasSettings => true;
        public override string Hint(BrushContext c) => c.Dragging ? $"Yol: {Length():0.#} m" : "Sürükle: yol boyunca diz";

        static float Spacing { get => EditorPrefs.GetFloat(Prefix + "Spacing", 3f); set => EditorPrefs.SetFloat(Prefix + "Spacing", Mathf.Max(0.3f, value)); }
        static float SideOffset { get => EditorPrefs.GetFloat(Prefix + "Offset", 0f); set => EditorPrefs.SetFloat(Prefix + "Offset", value); }
        static bool BothSides { get => EditorPrefs.GetBool(Prefix + "Both", false); set => EditorPrefs.SetBool(Prefix + "Both", value); }
        static bool FaceAlong { get => EditorPrefs.GetBool(Prefix + "Face", true); set => EditorPrefs.SetBool(Prefix + "Face", value); }

        public override void OnSettingsGUI()
        {
            EditorGUILayout.LabelField("Yol Boyu", EditorStyles.boldLabel);
            Spacing = EditorGUILayout.Slider(new GUIContent("Aralık (m)", "Yol boyunca iki parça arası."), Spacing, 0.3f, 20f);
            SideOffset = EditorGUILayout.Slider(new GUIContent("Yana kayma (m)", "Parçalar yolun ne kadar yanına konsun (0 = tam üstüne)."), SideOffset, 0f, 10f);
            using (new EditorGUI.DisabledScope(SideOffset <= 0f))
                BothSides = EditorGUILayout.Toggle(new GUIContent("İki yana", "Yolun iki kenarına da koy (fener sırası gibi)."), BothSides);
            FaceAlong = EditorGUILayout.Toggle(new GUIContent("Yola dönük", "Parçalar yolun yönüne bakar (R ile ek açı). Kapalıysa serbest yerleştirme ayarları (rastgele dönüş) geçerli."), FaceAlong);
        }

        void Track(BrushContext c)
        {
            if (!c.Dragging)
            {
                m_Tracking = false;
                return;
            }

            if (!m_Tracking || c.Anchor != m_Anchor)
            {
                m_Points.Clear();
                m_Points.Add(c.Anchor);
                m_Anchor = c.Anchor;
                m_Tracking = true;
            }

            var last = m_Points[m_Points.Count - 1];
            var delta = c.Hit - last;
            delta.y = 0f;
            if (delta.magnitude > 0.25f)
                m_Points.Add(c.Hit);
        }

        float Length()
        {
            float length = 0f;
            for (int i = 1; i < m_Points.Count; i++)
                length += Vector3.Distance(m_Points[i - 1], m_Points[i]);
            return length;
        }

        public override void CollectPlacements(BrushContext c, List<Placement> result)
        {
            Track(c);
            if (!c.Dragging || m_Points.Count < 2)
            {
                result.Add(FreeSingle(c));
                return;
            }

            float spacing = Spacing, offset = SideOffset;
            bool both = BothSides && offset > 0f, face = FaceAlong;
            float extraYaw = c.IsFree ? c.FreeYaw : c.Rotation * 90f;

            float next = 0f, walked = 0f;
            for (int i = 1; i < m_Points.Count; i++)
            {
                var a = m_Points[i - 1];
                var b = m_Points[i];
                var segment = b - a;
                segment.y = 0f;
                float length = segment.magnitude;
                if (length < 0.0001f)
                    continue;
                var dir = segment / length;
                var right = new Vector3(dir.z, 0f, -dir.x);

                while (next <= walked + length)
                {
                    float t = (next - walked) / length;
                    var p = Vector3.Lerp(a, b, t);
                    Add(p + right * offset, dir);
                    if (both)
                        Add(p - right * offset, -dir);
                    next += spacing;
                }
                walked += length;
            }

            void Add(Vector3 position, Vector3 forward)
            {
                if (!WorldKitFreePlacement.TryDrop(position, position.y, out position))
                    return;
                var free = WorldKitFreePlacement.Make(position, extraYaw, c.SurfaceNormal);
                if (face)
                    free.Rotation = Quaternion.LookRotation(forward) * Quaternion.Euler(0f, extraYaw, 0f);
                result.Add(free);
            }
        }

        /// <summary>Sürüklenmezken imlecin altındaki tek parça (serbest).</summary>
        internal static Placement FreeSingle(BrushContext c) =>
            WorldKitFreePlacement.Make(c.Hit, c.IsFree ? c.FreeYaw : c.Rotation * 90f, c.SurfaceNormal);

        public override void OnPreviewGUI(BrushContext c)
        {
            if (!c.Dragging || m_Points.Count < 2)
                return;
            var points = new Vector3[m_Points.Count];
            for (int i = 0; i < points.Length; i++)
                points[i] = m_Points[i] + Vector3.up * 0.05f;
            using (new Handles.DrawingScope(new Color(1f, 0.85f, 0.3f, 0.9f)))
                Handles.DrawAAPolyLine(3f, points);
        }
    }

    /// <summary>
    /// Halka: merkezden dışarı sürükleyerek bir çemberin üstüne eşit aralıklarla parça dizer (meydan çevresine ağaç,
    /// taş çemberi, kamp ateşi etrafına kütük). Parçalar merkeze, dışarı veya rastgele bakar.
    /// </summary>
    public sealed class CircleTool : WorldKitTool
    {
        const string Prefix = "WorldKit.Circle.";

        public enum Facing { Merkeze, Disari, Rastgele, Sabit }

        public override string Name => "Halka";
        public override string Tooltip => "Merkezden dışarı sürükle: çemberin üstüne eşit aralıklarla diz (meydan, taş çemberi).";
        public override int Order => 100;
        public override Texture2D Icon => WorldKitIcons.CircleTool;
        public override bool FreeForm => true;
        public override bool Supports(BrushContext c) => !c.IsFloor && !c.IsLinear;
        public override bool HasSettings => true;
        public override string Hint(BrushContext c) => c.Dragging ? $"Yarıçap {Radius(c):0.#} m" : "Merkezden dışarı sürükle";

        static float Spacing { get => EditorPrefs.GetFloat(Prefix + "Spacing", 2f); set => EditorPrefs.SetFloat(Prefix + "Spacing", Mathf.Max(0.3f, value)); }
        static Facing Face { get => (Facing)EditorPrefs.GetInt(Prefix + "Face", 0); set => EditorPrefs.SetInt(Prefix + "Face", (int)value); }

        public override void OnSettingsGUI()
        {
            EditorGUILayout.LabelField("Halka", EditorStyles.boldLabel);
            Spacing = EditorGUILayout.Slider(new GUIContent("Aralık (m)", "Çember üstünde iki parça arası (yaklaşık)."), Spacing, 0.3f, 20f);
            Face = (Facing)EditorGUILayout.Popup(new GUIContent("Yön", "Parçalar nereye baksın. Sabit: R ile seçilen açı."), (int)Face,
                new[] { "Merkeze", "Dışarı", "Rastgele", "Sabit" });
        }

        static float Radius(BrushContext c)
        {
            var d = c.Hit - c.Anchor;
            d.y = 0f;
            return d.magnitude;
        }

        public override void CollectPlacements(BrushContext c, List<Placement> result)
        {
            float radius = c.Dragging ? Radius(c) : 0f;
            if (radius < 0.3f)
            {
                result.Add(PathTool.FreeSingle(c));
                return;
            }

            var center = c.Anchor;
            int count = Mathf.Max(3, Mathf.RoundToInt(2f * Mathf.PI * radius / Spacing));
            float baseYaw = c.IsFree ? c.FreeYaw : c.Rotation * 90f;
            var facing = Face;
            for (int i = 0; i < count; i++)
            {
                float angle = 2f * Mathf.PI * i / count;
                var p = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (!WorldKitFreePlacement.TryDrop(p, center.y, out p))
                    continue;

                var placement = WorldKitFreePlacement.Make(p, baseYaw, c.SurfaceNormal);
                var outward = new Vector3(p.x - center.x, 0f, p.z - center.z);
                switch (facing)
                {
                    case Facing.Merkeze: placement.Rotation = Quaternion.LookRotation(-outward) * Quaternion.Euler(0f, baseYaw, 0f); break;
                    case Facing.Disari: placement.Rotation = Quaternion.LookRotation(outward) * Quaternion.Euler(0f, baseYaw, 0f); break;
                    case Facing.Sabit: placement.Rotation = Quaternion.Euler(0f, baseYaw, 0f); break;
                }
                result.Add(placement);
            }
        }

        public override void OnPreviewGUI(BrushContext c)
        {
            if (!c.Dragging)
                return;
            float radius = Radius(c);
            using (new Handles.DrawingScope(new Color(1f, 0.85f, 0.3f, 0.9f)))
            {
                Handles.DrawWireDisc(c.Anchor + Vector3.up * 0.05f, Vector3.up, radius, 2f);
                Handles.DrawDottedLine(c.Anchor, c.Anchor + (c.Hit - c.Anchor).normalized * radius, 3f);
            }
        }
    }
}
