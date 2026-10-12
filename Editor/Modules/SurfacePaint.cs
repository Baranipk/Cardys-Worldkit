using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// Yüzey boyası fırçası (Zemin → Yüzey Boya). Katmanlar ayarlardan gelir (<see cref="WorldKitSettings.paintLayers"/>);
    /// veri ve görünüş <see cref="WorldKitSurfacePaint"/> bileşenindedir.
    /// - Yuvarlak Fırça (8): imlecin çevresine yumuşak kenarlı daire; sürükledikçe arası doldurulur.
    /// - Diğer modlar (Tek, Hat, Alan, Çerçeve): seçilen hücreleri tamamen boyar.
    /// - Ctrl: siler (zemine dokunmaz).
    /// </summary>
    public static class WorldKitSurfaceBrush
    {
        const string Prefix = "WorldKit.Surface.";

        static Vector3? s_LastDab;

        /// <summary>Boyanan katman (ayarlardaki sıra).</summary>
        public static int ActiveLayer
        {
            get => EditorPrefs.GetInt(Prefix + "Layer", 0);
            set => EditorPrefs.SetInt(Prefix + "Layer", Mathf.Max(0, value));
        }

        /// <summary>Kenar yumuşaklığı: 0 = sert kenar, 1 = çok yumuşak geçiş.</summary>
        public static float Softness
        {
            get => EditorPrefs.GetFloat(Prefix + "Softness", 0.4f);
            set => EditorPrefs.SetFloat(Prefix + "Softness", Mathf.Clamp01(value));
        }

        /// <summary>Zemindeki boya bileşeni; yoksa (create true ise) Undo'lu ekler.</summary>
        public static WorldKitSurfacePaint GetPaint(WorldKitFloor floor, bool create)
        {
            if (floor == null)
                return null;
            var paint = floor.GetComponent<WorldKitSurfacePaint>();
            if (paint == null && create)
                paint = Undo.AddComponent<WorldKitSurfacePaint>(floor.gameObject);
            return paint;
        }

        /// <summary>Bileşenin materyal ve tırtık listelerini ayarlardan doldurur.</summary>
        public static void Sync(WorldKitSurfacePaint paint)
        {
            if (paint == null)
                return;
            var layers = WorldKitSettings.Instance.paintLayers;
            var materials = layers.Select(l => l.material).ToList();
            var noise = layers.Select(l => l.edgeNoise).ToList();
            if (paint.materials.SequenceEqual(materials) && paint.edgeNoise.SequenceEqual(noise))
                return;
            Undo.RecordObject(paint, "World Kit: Yüzey Boyası");
            paint.materials = materials;
            paint.edgeNoise = noise;
            EditorUtility.SetDirty(paint);
            paint.Rebuild();
        }

        /// <summary>Yuvarlak fırça darbesi: center çevresinde radius (m) yarıçaplı yumuşak daire; önceki darbeyle arası doldurulur.</summary>
        public static void Dab(WorldKitFloor floor, Vector3 center, float radius, bool erase)
        {
            int layer = ActiveLayer;
            if (layer >= WorldKitSettings.Instance.paintLayers.Count)
                return;

            var e = Event.current;
            if (e != null && e.type == EventType.MouseDown)
                s_LastDab = null;

            var paint = GetPaint(floor, true);
            Sync(paint);
            Undo.RecordObject(paint, erase ? "World Kit: Yüzey Boyası Sil" : "World Kit: Yüzey Boya");

            float cell = WorldKitGrid.CellSize;
            int r = Mathf.Max(1, paint.resolution);
            float step = cell / r;
            var from = s_LastDab ?? center;
            float inner = radius * (1f - Softness);

            var min = Vector3.Min(from, center) - Vector3.one * radius;
            var max = Vector3.Max(from, center) + Vector3.one * radius;
            int x0 = Mathf.FloorToInt(min.x / step), x1 = Mathf.CeilToInt(max.x / step);
            int z0 = Mathf.FloorToInt(min.z / step), z1 = Mathf.CeilToInt(max.z / step);
            var a = new Vector2(from.x, from.z);
            var b = new Vector2(center.x, center.z);

            for (int sz = z0; sz <= z1; sz++)
            for (int sx = x0; sx <= x1; sx++)
            {
                var p = new Vector2(sx * step, sz * step);
                float d = DistanceToSegment(p, a, b);
                if (d > radius)
                    continue;
                float strength = d <= inner ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (d - inner) / Mathf.Max(0.0001f, radius - inner));
                float current = paint.GetWeight(layer, sx, sz);
                float value = erase ? Mathf.Min(current, 1f - strength) : Mathf.Max(current, strength);
                if (!Mathf.Approximately(value, current))
                    paint.SetWeight(layer, sx, sz, value);
            }

            s_LastDab = center;
            paint.Rebuild(layer);
            EditorUtility.SetDirty(paint);
        }

        /// <summary>
        /// Noktalardan geçen bir yolu boyar (koddan patika çizmek için; örnek köy bunu kullanır).
        /// layer: ayarlardaki katman sırası, radius: metre.
        /// </summary>
        public static void PaintPath(WorldKitFloor floor, IList<Vector3> points, float radius, int layer)
        {
            if (points.Count == 0)
                return;
            int previous = ActiveLayer;
            ActiveLayer = layer;
            s_LastDab = null;
            foreach (var p in points)
                Dab(floor, p, radius, false);
            s_LastDab = null;
            ActiveLayer = previous;
        }

        /// <summary>Hücreleri tamamen boyar (veya siler). Hat, Alan, Çerçeve modları için.</summary>
        public static void FillCells(WorldKitFloor floor, ICollection<Vector2Int> cells, bool erase)
        {
            int layer = ActiveLayer;
            if (layer >= WorldKitSettings.Instance.paintLayers.Count || cells.Count == 0)
                return;

            var paint = GetPaint(floor, true);
            Sync(paint);
            Undo.RecordObject(paint, erase ? "World Kit: Yüzey Boyası Sil" : "World Kit: Yüzey Boya");
            int r = Mathf.Max(1, paint.resolution);
            foreach (var cell in cells)
            {
                for (int b = 0; b <= r; b++)
                for (int a = 0; a <= r; a++)
                    paint.SetWeight(layer, cell.x * r + a, cell.y * r + b, erase ? 0f : 1f);
            }
            paint.Rebuild(layer);
            EditorUtility.SetDirty(paint);
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude < 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Zemin sekmesindeki Yüzey Boya ayarları.</summary>
        public static void DrawSettingsGUI()
        {
            var settings = WorldKitSettings.Instance;
            if (settings.paintLayers.Count == 0)
            {
                EditorGUILayout.HelpBox("Henüz yüzey boyası katmanı yok.", MessageType.Info);
                if (GUILayout.Button("Hazır Katmanları Ekle (Patika, Koyu Çimen, Taş Yol)"))
                {
                    WorldKitStarterContent.EnsureDefaultPaintLayers();
                    GUIUtility.ExitGUI();
                }
                return;
            }

            EditorGUILayout.LabelField("Katman", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < settings.paintLayers.Count; i++)
                {
                    var layer = settings.paintLayers[i];
                    var color = layer.material != null && layer.material.HasProperty("_BaseColor") ? layer.material.GetColor("_BaseColor")
                        : layer.material != null ? layer.material.color : Color.gray;
                    var rect = GUILayoutUtility.GetRect(64f, 34f, GUILayout.Width(64f));
                    bool on = ActiveLayer == i;
                    EditorGUI.DrawRect(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, 16f), color);
                    if (on)
                        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 3f, rect.width, 3f), new Color(0.3f, 0.65f, 1f));
                    GUI.Label(new Rect(rect.x, rect.y + 18f, rect.width, 14f), layer.name, EditorStyles.centeredGreyMiniLabel);
                    if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                        ActiveLayer = i;
                }
                GUILayout.FlexibleSpace();
            }

            float softness = EditorGUILayout.Slider(new GUIContent("Yumuşaklık", "Fırça kenarının yumuşaklığı (0 = sert)."), Softness, 0f, 1f);
            if (!Mathf.Approximately(softness, Softness))
                Softness = softness;
            float radius = EditorGUILayout.Slider(new GUIContent("Fırça yarıçapı (hücre)", "Yuvarlak Fırça (8) yarıçapı."), RoundBrushTool.Radius, 0.5f, 10f);
            if (!Mathf.Approximately(radius, RoundBrushTool.Radius))
                RoundBrushTool.Radius = Mathf.Round(radius * 4f) / 4f;

            EditorGUILayout.HelpBox("Fırça (8) ile sürükle: kıvrımlı patika. Ctrl: sil. Alan/Hat: hücreleri doldurur.\n" +
                                    "Katman ekle / materyalini değiştir: Project Settings → World Kit → Paint Layers.", MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Katmanları Ayarla…", EditorStyles.miniButton))
                    SettingsService.OpenProjectSettings("Project/World Kit");
                var floor = FindFloor();
                var paint = GetPaint(floor, false);
                using (new EditorGUI.DisabledScope(paint == null))
                {
                    if (GUILayout.Button("Bu Katmanı Temizle", EditorStyles.miniButton) &&
                        EditorUtility.DisplayDialog("World Kit", $"'{settings.paintLayers[Mathf.Min(ActiveLayer, settings.paintLayers.Count - 1)].name}' katmanının bütün boyası silinsin mi? (Ctrl+Z ile geri alınır)", "Sil", "Vazgeç"))
                    {
                        Undo.RecordObject(paint, "World Kit: Katmanı Temizle");
                        paint.ClearLayer(ActiveLayer);
                        EditorUtility.SetDirty(paint);
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }

        static WorldKitFloor FindFloor()
        {
            var root = WorldKitScene.FindRoot();
            return root != null ? root.GetComponentInChildren<WorldKitFloor>() : null;
        }

        [InitializeOnLoadMethod]
        static void Init()
        {
            // Ayarlar değişince (katman materyali, tırtık) sahnedeki boyaları güncelle
            WorldKitEvents.SettingsChanged += () =>
            {
                foreach (var paint in Object.FindObjectsByType<WorldKitSurfacePaint>(FindObjectsSortMode.None))
                    Sync(paint);
            };
        }
    }
}
