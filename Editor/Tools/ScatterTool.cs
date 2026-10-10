using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// Serpme fırçası: imlecin çevresine rastgele konum, dönüş ve boyutla parçalar serpiştirir (orman, kaya alanı...).
    /// Grid'e bağlı değildir. Rastgelelik imlecin hücresinden türetilir, yani önizlemede görünen aynen konur.
    /// Mevcut parçalara ve birbirlerine en az "Aralık" kadar uzak durur; aynı yeri tekrar boyamak sıklaştırmaz.
    /// </summary>
    public sealed class ScatterTool : WorldKitTool
    {
        const string Prefix = "WorldKit.Scatter.";

        static float Radius { get => EditorPrefs.GetFloat(Prefix + "Radius", 3f); set => EditorPrefs.SetFloat(Prefix + "Radius", value); }
        static int Count { get => EditorPrefs.GetInt(Prefix + "Count", 4); set => EditorPrefs.SetInt(Prefix + "Count", value); }
        static float Spacing { get => EditorPrefs.GetFloat(Prefix + "Spacing", 1.2f); set => EditorPrefs.SetFloat(Prefix + "Spacing", value); }
        static bool RandomRotation { get => EditorPrefs.GetBool(Prefix + "RandomRotation", true); set => EditorPrefs.SetBool(Prefix + "RandomRotation", value); }
        static float MinScale { get => EditorPrefs.GetFloat(Prefix + "MinScale", 0.8f); set => EditorPrefs.SetFloat(Prefix + "MinScale", value); }
        static float MaxScale { get => EditorPrefs.GetFloat(Prefix + "MaxScale", 1.25f); set => EditorPrefs.SetFloat(Prefix + "MaxScale", value); }

        public override string Name => "Serp";
        public override string Tooltip => "Sürükle: imlecin çevresine rastgele serpiştir (ağaç, kaya, çalı). Ayarlar palette.";
        public override int Order => 50;
        public override bool PaintsWhileDragging => true;
        public override bool Supports(BrushContext c) => !c.IsFloor && !c.IsLinear;
        public override string Hint(BrushContext c) => $"Tıkla/Sürükle: serp (yarıçap {Radius:0.#} m, {Count} adet)";
        public override bool HasSettings => true;

        public override void OnSettingsGUI()
        {
            EditorGUILayout.LabelField("Serpme", EditorStyles.boldLabel);
            Radius = EditorGUILayout.Slider(new GUIContent("Yarıçap (m)", "Her vuruşta serpilecek alanın yarıçapı."), Radius, 0.5f, 15f);
            Count = EditorGUILayout.IntSlider(new GUIContent("Adet", "Her vuruşta en fazla kaç parça."), Count, 1, 30);
            Spacing = EditorGUILayout.Slider(new GUIContent("Aralık (m)", "Parçalar arası en az mesafe (mevcut parçalar dahil)."), Spacing, 0.2f, 10f);
            RandomRotation = EditorGUILayout.Toggle(new GUIContent("Rastgele dönüş", "Kapalıysa R ile seçilen açı kullanılır."), RandomRotation);

            float min = MinScale, max = MaxScale;
            EditorGUILayout.MinMaxSlider(new GUIContent($"Boyut ({min:0.00} – {max:0.00})"), ref min, ref max, 0.3f, 3f);
            if (!Mathf.Approximately(min, MinScale)) MinScale = min;
            if (!Mathf.Approximately(max, MaxScale)) MaxScale = max;
        }

        public override void CollectPlacements(BrushContext c, List<Placement> result)
        {
            var cell = c.HoverCell;
            var center = WorldKitGrid.CellCenter(cell, c.Level);
            float radius = Radius, spacing = Spacing, minScale = MinScale, maxScale = MaxScale;
            bool randomRotation = RandomRotation;
            int count = Count;

            // Aynı hücre → aynı tohum → önizleme ile yerleşim aynı.
            int seed = unchecked(cell.x * 73856093 ^ cell.y * 19349663 ^ c.Level * 83492791 ^ (c.Prefab != null ? c.Prefab.GetInstanceID() : 0));
            var random = new System.Random(seed);

            // Yakındaki mevcut parçalar (aynı kattaki) aralık kontrolüne girer.
            float reach = radius + spacing;
            var occupied = WorldKitScene.AllPieces()
                .Select(p => p.transform.position)
                .Where(p => Mathf.Abs(p.y - center.y) < WorldKitGrid.LevelHeight * 0.5f && Flat(p - center).sqrMagnitude <= reach * reach)
                .ToList();

            float minDistanceSq = spacing * spacing;
            for (int attempt = 0; attempt < count * 8 && result.Count < count; attempt++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float distance = Mathf.Sqrt((float)random.NextDouble()) * radius;
                var position = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                float yaw = randomRotation ? (float)random.NextDouble() * 360f : c.Rotation * 90f;
                float scale = Mathf.Lerp(minScale, maxScale, (float)random.NextDouble());

                if (occupied.Any(o => Flat(o - position).sqrMagnitude < minDistanceSq))
                    continue;

                occupied.Add(position);
                result.Add(new Placement(position, Quaternion.Euler(0f, yaw, 0f), scale));
            }
        }

        public override void OnPreviewGUI(BrushContext c)
        {
            var center = WorldKitGrid.CellCenter(c.HoverCell, c.Level) + Vector3.up * 0.03f;
            using (new Handles.DrawingScope(new Color(0.2f, 0.8f, 1f, 0.8f)))
                Handles.DrawWireDisc(center, Vector3.up, Radius, 2f);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
