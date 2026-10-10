using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// Cetvel modu: sürükleyerek mesafe ölçer. Sahneyi değiştirmez.
    /// Uzunluk, hücre sayısı ve ayarlardaki hızlara göre yürüme/koşma süresi gösterilir.
    /// </summary>
    public sealed class RulerTool : WorldKitTool
    {
        public override string Name => "Cetvel";
        public override string Tooltip => "Tut-çek: mesafe, hücre sayısı ve yürüme/koşma süresi. Sahneyi değiştirmez. (Sil'den sonra gelir.)";
        public override int Order => 95;
        public override Texture2D Icon => WorldKitIcons.RulerTool;
        public override bool ModifiesScene => false;
        public override string Hint(BrushContext c) => c.Dragging ? Measure(c) : "Tut-çek: ölç";

        public override void CollectPlacements(BrushContext c, List<Placement> result) { }
        public override void CollectCells(BrushContext c, ICollection<Vector2Int> result) { }

        static string Measure(BrushContext c)
        {
            var settings = WorldKitSettings.Instance;
            var delta = c.Hit - c.Anchor;
            delta.y = 0f;
            float distance = delta.magnitude;
            float cell = WorldKitGrid.CellSize;
            return $"<b>{distance:0.0} m</b>  ({Mathf.Abs(delta.x) / cell:0.#} × {Mathf.Abs(delta.z) / cell:0.#} hücre)   " +
                   $"Yürü: {distance / settings.walkSpeed:0.0} sn   Koş: {distance / settings.runSpeed:0.0} sn";
        }

        public override void OnPreviewGUI(BrushContext c)
        {
            if (!c.Dragging)
                return;

            var a = c.Anchor + Vector3.up * 0.05f;
            var b = c.Hit + Vector3.up * 0.05f;
            b.y = a.y;
            using (new Handles.DrawingScope(new Color(1f, 0.85f, 0.2f, 1f)))
            {
                Handles.DrawLine(a, b, 3f);
                Handles.DrawSolidDisc(a, Vector3.up, 0.12f);
                Handles.DrawSolidDisc(b, Vector3.up, 0.12f);

                // Eksen bileşenleri (kesik)
                var corner = new Vector3(b.x, a.y, a.z);
                Handles.DrawDottedLine(a, corner, 3f);
                Handles.DrawDottedLine(corner, b, 3f);

                var style = new GUIStyle(EditorStyles.whiteBoldLabel) { fontSize = 13 };
                Handles.Label((a + b) * 0.5f + Vector3.up * 0.3f, $"{Vector3.Distance(a, b):0.0} m", style);
            }
        }
    }

    /// <summary>İstatistik sekmesi: parça sayıları, zemin alanı, üçgen sayısı, harita boyutu ve harita PNG'si.</summary>
    sealed class StatsPaletteTab : WorldKitPaletteTab
    {
        public override string Title => "İstatistik";
        public override int Order => 2000;
        public override string Section => AnalysisSection;
        public override Texture2D Icon => WorldKitIcons.Stats;

        sealed class Stats
        {
            public int Pieces;
            public readonly List<(string name, int count)> Categories = new List<(string, int)>();
            public int FloorCells;
            public readonly List<(string name, int cells)> FloorTypes = new List<(string, int)>();
            public long Triangles;
            public Bounds? Bounds;
        }

        Stats m_Stats;
        WorldKitPerformance.Report m_Perf;
        const string PpmKey = "WorldKit.Map.PixelsPerMeter";

        public override void OnGUI(WorldKitPaletteWindow window)
        {
            GUILayout.Space(6f);
            if (GUILayout.Button("Hesapla / Yenile", GUILayout.Height(26f)) || m_Stats == null)
                m_Stats = Compute();

            var s = m_Stats;
            float cell = WorldKitGrid.CellSize;

            EditorGUILayout.LabelField("Parçalar", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Toplam", s.Pieces.ToString());
            foreach (var (name, count) in s.Categories)
                EditorGUILayout.LabelField("   " + name, count.ToString());

            GUILayout.Space(4f);
            EditorGUILayout.LabelField("Zemin", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Hücre / alan", $"{s.FloorCells}  ({s.FloorCells * cell * cell:0} m²)");
            foreach (var (name, cells) in s.FloorTypes)
                EditorGUILayout.LabelField("   " + name, $"{cells * cell * cell:0} m²");

            GUILayout.Space(4f);
            EditorGUILayout.LabelField("Genel", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Üçgen", s.Triangles.ToString("N0"));
            if (s.Bounds.HasValue)
            {
                var size = s.Bounds.Value.size;
                EditorGUILayout.LabelField("Dünya boyutu", $"{size.x:0} × {size.z:0} m  (yükseklik {size.y:0.#} m)");
                var settings = WorldKitSettings.Instance;
                float diagonal = new Vector2(size.x, size.z).magnitude;
                EditorGUILayout.LabelField("Köşeden köşeye", $"{diagonal / settings.walkSpeed:0} sn yürü, {diagonal / settings.runSpeed:0} sn koş");
            }

            GUILayout.Space(10f);
            EditorGUILayout.LabelField("Harita görüntüsü", EditorStyles.boldLabel);
            int ppm = EditorPrefs.GetInt(PpmKey, 16);
            int newPpm = EditorGUILayout.IntSlider(new GUIContent("Piksel / metre"), ppm, 2, 64);
            if (newPpm != ppm)
                EditorPrefs.SetInt(PpmKey, newPpm);

            using (new EditorGUI.DisabledScope(!s.Bounds.HasValue))
            {
                if (GUILayout.Button("Üstten Harita PNG Kaydet…"))
                    MapExporter.Export(newPpm);
            }

            GUILayout.Space(10f);
            WorldKitPerformance.DrawReportGUI(ref m_Perf);
        }

        static Stats Compute()
        {
            var stats = new Stats();
            var root = WorldKitScene.FindRoot();
            if (root == null)
                return stats;

            foreach (Transform group in root.transform)
            {
                if (group.childCount > 0 && group.GetComponent<WorldKitFloor>() == null)
                    stats.Categories.Add((group.name, group.childCount));
                stats.Pieces += group.GetComponent<WorldKitFloor>() == null ? group.childCount : 0;
            }
            stats.Categories.Sort((a, b) => b.count.CompareTo(a.count));

            var floor = root.GetComponentInChildren<WorldKitFloor>();
            if (floor != null)
            {
                stats.FloorCells = floor.CellCount;
                var counts = new Dictionary<int, int>();
                var so = new SerializedObject(floor);
                var types = so.FindProperty("m_Types");
                for (int i = 0; i < floor.CellCount; i++)
                {
                    int type = types != null && i < types.arraySize ? types.GetArrayElementAtIndex(i).intValue : 0;
                    counts[type] = counts.TryGetValue(type, out var n) ? n + 1 : 1;
                }
                foreach (var pair in counts.OrderBy(p => p.Key))
                    stats.FloorTypes.Add((WorldKitBrush.FloorTypeName(pair.Key), pair.Value));
            }

            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                if (mesh == null)
                    continue;
                for (int i = 0; i < mesh.subMeshCount; i++)
                    stats.Triangles += mesh.GetIndexCount(i) / 3;
            }

            stats.Bounds = MapExporter.WorldBounds(root);
            return stats;
        }
    }

    /// <summary>Dünyanın üstten, ortografik, kuzey yukarıda bir görüntüsünü PNG olarak kaydeder.</summary>
    static class MapExporter
    {
        const int MaxSize = 8192;

        public static Bounds? WorldBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return null;

            var bounds = renderers[0].bounds;
            foreach (var r in renderers.Skip(1))
                bounds.Encapsulate(r.bounds);
            return bounds;
        }

        public static void Export(int pixelsPerMeter)
        {
            var root = WorldKitScene.FindRoot();
            var maybeBounds = root != null ? WorldBounds(root) : null;
            if (!maybeBounds.HasValue)
                return;

            var path = EditorUtility.SaveFilePanel("Harita PNG Kaydet", "", root.scene.name + "_harita", "png");
            if (string.IsNullOrEmpty(path))
                return;

            var bounds = maybeBounds.Value;
            bounds.Expand(new Vector3(2f, 0f, 2f)); // kenarlarda pay
            int width = Mathf.Clamp(Mathf.CeilToInt(bounds.size.x * pixelsPerMeter), 16, MaxSize);
            int height = Mathf.Clamp(Mathf.CeilToInt(bounds.size.z * pixelsPerMeter), 16, MaxSize);

            var cameraObject = new GameObject("WorldKit Harita Kamerası") { hideFlags = HideFlags.HideAndDontSave };
            var texture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = bounds.extents.z;
                camera.aspect = width / (float)height;
                camera.transform.SetPositionAndRotation(
                    new Vector3(bounds.center.x, bounds.max.y + 50f, bounds.center.z), Quaternion.Euler(90f, 0f, 0f));
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = bounds.size.y + 100f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
                camera.targetTexture = texture;
                camera.Render();

                RenderTexture.active = texture;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);

                Debug.Log($"[World Kit] Harita kaydedildi: {path} ({width}×{height}, {pixelsPerMeter} px/m)");
                EditorUtility.RevealInFinder(path);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(texture);
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
