using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// Yuvarlak fırça (yalnızca zemin): imlecin çevresindeki daire içindeki hücrelere, sürükledikçe seçili zemin
    /// işlemini uygular. Patika, kıyı, doğal kenarlı çimen alanı boyamak ya da araziyi (Yükselt / Alçalt) şekillendirmek için.
    /// Bir sürüklemede her hücreye işlem bir kez uygulanır (Yükselt üst üste binmez).
    /// </summary>
    public sealed class RoundBrushTool : WorldKitTool
    {
        const string RadiusKey = "WorldKit.RoundBrush.Radius";

        readonly HashSet<Vector2Int> m_Stroke = new HashSet<Vector2Int>();

        public override string Name => "Fırça";
        public override string Tooltip => "Yuvarlak fırça (zemin): sürükledikçe dairenin içindeki hücrelere seçili zemin işlemini uygular. Patika, kıyı, arazi şekillendirme.";
        public override int Order => 97;
        public override Texture2D Icon => WorldKitIcons.RoundBrush;
        public override bool PaintsWhileDragging => true;
        // Duvar ve kenar süsü alanın tamamına bakar; parça parça boyamada iç kenarlara da konurdu.
        public override bool Supports(BrushContext c) => c.IsFloor
            && WorldKitBrush.FloorOp != WorldKitBrush.FloorOperation.Wall
            && WorldKitBrush.FloorOp != WorldKitBrush.FloorOperation.EdgeDecor;
        public override bool HasSettings => true;
        public override string Hint(BrushContext c) => $"Sürükle: boya (yarıçap {Radius:0.#} hücre)";

        /// <summary>Fırça yarıçapı (hücre).</summary>
        public static float Radius
        {
            get => EditorPrefs.GetFloat(RadiusKey, 1.5f);
            set => EditorPrefs.SetFloat(RadiusKey, Mathf.Clamp(value, 0.5f, 10f));
        }

        public override void OnSettingsGUI()
        {
            float radius = EditorGUILayout.Slider(new GUIContent("Yarıçap (hücre)", "Dairenin yarıçapı. 0.5 = tek hücre."), Radius, 0.5f, 10f);
            if (!Mathf.Approximately(radius, Radius))
                Radius = Mathf.Round(radius * 2f) * 0.5f;
        }

        public override void CollectCells(BrushContext c, ICollection<Vector2Int> result)
        {
            var e = Event.current;
            bool painting = e != null && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag);
            if (painting && e.type == EventType.MouseDown)
                m_Stroke.Clear();

            float cell = WorldKitGrid.CellSize;
            float radius = Radius;
            var center = new Vector2(c.Hit.x / cell, c.Hit.z / cell);
            int r = Mathf.CeilToInt(radius);
            var origin = c.HoverCell;
            for (int z = -r; z <= r; z++)
            for (int x = -r; x <= r; x++)
            {
                var cellPos = new Vector2Int(origin.x + x, origin.y + z);
                var cellCenter = new Vector2(cellPos.x + 0.5f, cellPos.y + 0.5f);
                // 0.5 yarıçapta en azından imlecin altındaki hücre
                if ((cellCenter - center).magnitude > radius && cellPos != origin)
                    continue;
                if (painting && !m_Stroke.Add(cellPos))
                    continue;
                result.Add(cellPos);
            }
        }

        public override void OnPreviewGUI(BrushContext c)
        {
            using (new Handles.DrawingScope(new Color(0.3f, 0.65f, 1f, 0.9f)))
                Handles.DrawWireDisc(c.Hit + Vector3.up * 0.05f, Vector3.up, Radius * WorldKitGrid.CellSize, 2f);
        }
    }
}
