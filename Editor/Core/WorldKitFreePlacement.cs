using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// Serbest yerleştirme: dekor ve küçük objeler grid'e oturmadan, imlecin tam altına (zemin, platform veya başka
    /// bir objenin üstüne) konur. Dönüş serbest açıyla (R = ayarlardaki adım), istenirse her parçada rastgele dönüş
    /// ve boyut. Rastgelelik konumdan türetilir: önizleme ile sonuç aynıdır.
    ///
    /// Ayarlardaki <see cref="WorldKitSettings.freePlacementCategories"/> kategorilerinden seçilen parçalarda
    /// kendiliğinden açılır; fırça açıkken G ile açılıp kapanır. Çizgi parçalarında (duvar, çit) ve zeminde kullanılmaz.
    /// </summary>
    public static class WorldKitFreePlacement
    {
        const string Prefix = "WorldKit.Free.";

        /// <summary>Sürükleyerek boyarken iki parça arası en az mesafe (m).</summary>
        public static float Spacing
        {
            get => EditorPrefs.GetFloat(Prefix + "Spacing", 1f);
            set => EditorPrefs.SetFloat(Prefix + "Spacing", Mathf.Max(0.1f, value));
        }

        /// <summary>Her parçaya rastgele Y dönüşü.</summary>
        public static bool RandomYaw
        {
            get => EditorPrefs.GetBool(Prefix + "RandomYaw", true);
            set => EditorPrefs.SetBool(Prefix + "RandomYaw", value);
        }

        /// <summary>Her parçaya rastgele boyut (ScaleMin–ScaleMax).</summary>
        public static bool RandomScale
        {
            get => EditorPrefs.GetBool(Prefix + "RandomScale", true);
            set => EditorPrefs.SetBool(Prefix + "RandomScale", value);
        }

        public static float ScaleMin
        {
            get => EditorPrefs.GetFloat(Prefix + "ScaleMin", 0.85f);
            set => EditorPrefs.SetFloat(Prefix + "ScaleMin", Mathf.Clamp(value, 0.1f, 5f));
        }

        public static float ScaleMax
        {
            get => EditorPrefs.GetFloat(Prefix + "ScaleMax", 1.15f);
            set => EditorPrefs.SetFloat(Prefix + "ScaleMax", Mathf.Clamp(value, 0.1f, 5f));
        }

        /// <summary>İmleç, seviye düzlemi yerine altındaki yüzeye (collider) oturur.</summary>
        public static bool SnapToSurface
        {
            get => EditorPrefs.GetBool(Prefix + "Surface", true);
            set => EditorPrefs.SetBool(Prefix + "Surface", value);
        }

        /// <summary>Parça yüzeyin eğimine yatırılır (rampadaki kaya gibi). Kapalıysa hep dik durur.</summary>
        public static bool AlignToSurface
        {
            get => EditorPrefs.GetBool(Prefix + "Align", false);
            set => EditorPrefs.SetBool(Prefix + "Align", value);
        }

        /// <summary>Bu prefab'ın kategorisinde serbest yerleştirme varsayılan olarak açık mı?</summary>
        public static bool IsDefaultFor(GameObject prefab)
        {
            if (prefab == null)
                return false;
            var category = WorldKitBrush.GetCategory(prefab);
            return WorldKitSettings.Instance.freePlacementCategories.Any(c => string.Equals(c, category, System.StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Imlecin altındaki yüzey (collider; tetikleyiciler hariç). Bulunamazsa false.
        /// </summary>
        public static bool RaycastSurface(Ray ray, out Vector3 point, out Vector3 normal)
        {
            point = normal = Vector3.zero;
            Physics.SyncTransforms();
            var hits = Physics.RaycastAll(ray, 5000f, ~(1 << 2), QueryTriggerInteraction.Ignore);
            if (hits.Length == 0)
                return false;

            var hit = hits.OrderBy(h => h.distance).First();
            point = hit.point;
            normal = hit.normal;
            return true;
        }

        /// <summary>
        /// Noktayı altındaki yüzeye indirir (Serp, Yol Boyu, Halka gibi çok noktalı modlar için). refY: imlecin yüksekliği;
        /// yalnızca ondan en fazla 1.5 kat yukarıdan aşağı bakılır. Nokta bir objenin içindeyse (bina gibi) false.
        /// Yüzeye oturma kapalıysa nokta aynen döner.
        /// </summary>
        public static bool TryDrop(Vector3 point, float refY, out Vector3 result)
        {
            result = point;
            if (!SnapToSurface)
                return true;

            float lh = WorldKitGrid.LevelHeight;
            var origin = new Vector3(point.x, refY + lh * 1.5f, point.z);
            if (Physics.CheckSphere(origin, 0.05f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                return false;
            if (!Physics.Raycast(origin, Vector3.down, out var hit, lh * 4f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                return true; // altında bir şey yok: düzlemde kal
            result = hit.point;
            return true;
        }

        /// <summary>
        /// Serbest yerleşim: konum aynen, dönüş yaw + (isteğe bağlı) rastgele, yüzeye hizalama, rastgele boyut.
        /// Aynı konum her zaman aynı sonucu verir.
        /// </summary>
        public static Placement Make(Vector3 position, float yaw, Vector3 surfaceNormal)
        {
            var random = new System.Random(Hash(position));
            if (RandomYaw)
                yaw += (float)random.NextDouble() * 360f;
            float scale = RandomScale ? Mathf.Lerp(Mathf.Min(ScaleMin, ScaleMax), Mathf.Max(ScaleMin, ScaleMax), (float)random.NextDouble()) : 1f;

            var rotation = Quaternion.Euler(0f, yaw, 0f);
            if (AlignToSurface && surfaceNormal.sqrMagnitude > 0.5f)
                rotation = Quaternion.FromToRotation(Vector3.up, surfaceNormal) * rotation;
            return new Placement(position, rotation, scale);
        }

        static int Hash(Vector3 p) => unchecked(Mathf.RoundToInt(p.x * 100f) * 73856093 ^ Mathf.RoundToInt(p.y * 100f) * 19349663 ^ Mathf.RoundToInt(p.z * 100f) * 83492791);

        /// <summary>Paletteki serbest yerleştirme ayarları.</summary>
        public static void DrawSettingsGUI()
        {
            float spacing = EditorGUILayout.Slider(new GUIContent("Aralık (m)", "Sürükleyerek boyarken iki parça arası en az mesafe."), Spacing, 0.1f, 5f);
            if (!Mathf.Approximately(spacing, Spacing)) Spacing = spacing;

            using (new EditorGUILayout.HorizontalScope())
            {
                bool yaw = EditorGUILayout.ToggleLeft(new GUIContent("Rastgele dönüş"), RandomYaw, GUILayout.Width(120f));
                if (yaw != RandomYaw) RandomYaw = yaw;
                bool surface = EditorGUILayout.ToggleLeft(new GUIContent("Yüzeye otur", "Zemin, platform veya objenin üstüne konur."), SnapToSurface, GUILayout.Width(110f));
                if (surface != SnapToSurface) SnapToSurface = surface;
                bool align = EditorGUILayout.ToggleLeft(new GUIContent("Eğime yatır", "Rampa ve eğimlerde parça yüzeye paralel durur."), AlignToSurface);
                if (align != AlignToSurface) AlignToSurface = align;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                bool scale = EditorGUILayout.ToggleLeft(new GUIContent("Rastgele boyut"), RandomScale, GUILayout.Width(120f));
                if (scale != RandomScale) RandomScale = scale;
                using (new EditorGUI.DisabledScope(!RandomScale))
                {
                    float min = ScaleMin, max = ScaleMax;
                    EditorGUILayout.MinMaxSlider(ref min, ref max, 0.3f, 2f);
                    if (!Mathf.Approximately(min, ScaleMin)) ScaleMin = min;
                    if (!Mathf.Approximately(max, ScaleMax)) ScaleMax = max;
                    GUILayout.Label($"{ScaleMin:0.00}–{ScaleMax:0.00}", EditorStyles.miniLabel, GUILayout.Width(62f));
                }
            }
        }
    }
}
