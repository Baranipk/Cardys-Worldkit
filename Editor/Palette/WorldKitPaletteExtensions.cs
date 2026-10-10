using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// Palete kod ile sekme ekler (ör. "İşaretçiler", "Ölçüm"). Parametresiz yapıcısı olan her alt sınıf
    /// otomatik bulunur. Prefab sekmeleri için kod gerekmez: parçalar klasörüne alt klasör açmak yeterli.
    /// </summary>
    public abstract class WorldKitPaletteTab
    {
        public abstract string Title { get; }

        /// <summary>Sekme sırası. Zemin: 0, klasör sekmeleri: 100+, varsayılan: 1000.</summary>
        public virtual int Order => 1000;

        /// <summary>Sekme içeriği. EditorGUILayout ile çizin.</summary>
        public abstract void OnGUI(WorldKitPaletteWindow window);
    }

    /// <summary>
    /// Paletin alt çubuğuna düğme ekler (ör. "Üstten Kamera", "Oyuncu Ekle").
    /// Parametresiz yapıcısı olan her alt sınıf otomatik bulunur.
    /// </summary>
    public abstract class WorldKitToolbarItem
    {
        public abstract string Label { get; }
        public virtual string Tooltip => Label;
        public virtual int Order => 100;
        public virtual bool IsEnabled => true;
        public abstract void Execute();
    }

    static class WorldKitExtensions
    {
        public static List<T> Create<T>() where T : class
        {
            return TypeCache.GetTypesDerivedFrom<T>()
                .Where(t => !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
                .Select(t => (T)Activator.CreateInstance(t))
                .ToList();
        }
    }

    // ---------------- Yerleşik sekme ve düğmeler ----------------

    /// <summary>Zemin fırçası sekmesi.</summary>
    sealed class FloorPaletteTab : WorldKitPaletteTab
    {
        public override string Title => "Zemin";
        public override int Order => 0;

        public override void OnGUI(WorldKitPaletteWindow window)
        {
            GUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8f);
                bool active = WorldKitBrush.IsFloorBrush;
                var color = GUI.backgroundColor;
                if (active)
                    GUI.backgroundColor = new Color(0.45f, 0.75f, 1f);
                if (GUILayout.Button(active ? "Zemin Fırçası (aktif)" : "Zemin Fırçası", GUILayout.Height(40f)))
                {
                    if (active)
                        WorldKitBrush.End();
                    else
                        WorldKitBrush.BeginFloor();
                }
                GUI.backgroundColor = color;
                GUILayout.Space(8f);
            }

            GUILayout.Space(6f);
            DrawTypePicker();

            GUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "Zemin tek bir obje ve tek bir mesh olarak tutulur; ne kadar büyük boyarsan boya sahne hafif kalır.\n\n" +
                "• Yukarıdaki modlar zeminde de çalışır: Tek = fırça gibi boya, Hat = şerit (yol), Alan = dikdörtgen, Çerçeve = kenar\n" +
                "• Shift + tık: son boyanan hücreden buraya şerit çek\n" +
                "• İşlemler (aşağıda): Boya, Sadece Renk, Yükselt, Alçalt, Duvarla Çevir. Hepsi seçili modun şekliyle uygulanır\n" +
                "• E / Q: seviye. Üst seviyede boyamak platform yapar, kenar duvarları kendiliğinden oluşur\n" +
                "• Aynı alanı başka seviyede boyamak yüksekliği değiştirir\n" +
                "• Ctrl + sürükle veya Sil modu: zemini sil",
                MessageType.None);

            var floor = UnityEngine.Object.FindObjectsByType<WorldKitFloor>(FindObjectsSortMode.None).FirstOrDefault();
            if (floor != null)
                EditorGUILayout.LabelField($"Aktif sahnede {floor.CellCount} zemin hücresi", EditorStyles.centeredGreyMiniLabel);

            if (WorldKitSettings.Instance.floorGroundMaterial == null)
                EditorGUILayout.HelpBox("Zemin materyali atanmamış (Project Settings → World Kit). " +
                                        "Başlangıç içeriğini üretmek bunları otomatik atar.", MessageType.Warning);
        }

        static GUIStyle s_SwatchLabel;

        /// <summary>Zemin türü seçici: renk kareleri + "sadece renk" seçeneği.</summary>
        static void DrawTypePicker()
        {
            var settings = WorldKitSettings.Instance;
            s_SwatchLabel ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperCenter, clipping = TextClipping.Clip };

            EditorGUILayout.LabelField("Zemin türü", EditorStyles.boldLabel);

            const float size = 44f;
            int count = settings.floorTypes.Count + 1;
            int columns = Mathf.Max(1, Mathf.FloorToInt((EditorGUIUtility.currentViewWidth - 24f) / (size + 6f)));
            int rows = Mathf.CeilToInt(count / (float)columns);
            var area = GUILayoutUtility.GetRect(EditorGUIUtility.currentViewWidth - 24f, rows * (size + 18f));

            for (int i = 0; i < count; i++)
            {
                var rect = new Rect(area.x + 8f + (i % columns) * (size + 6f), area.y + (i / columns) * (size + 18f), size, size);
                var material = i == 0 ? settings.floorGroundMaterial : settings.floorTypes[i - 1].material;
                var color = material != null && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor")
                    : material != null && material.HasProperty("_Color") ? material.color : Color.gray;

                bool selected = WorldKitBrush.FloorType == i;
                EditorGUI.DrawRect(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f),
                    selected ? new Color(0.25f, 0.6f, 1f) : new Color(0f, 0f, 0f, 0.25f));
                EditorGUI.DrawRect(rect, color);
                GUI.Label(new Rect(rect.x - 4f, rect.yMax + 1f, size + 8f, 16f), WorldKitBrush.FloorTypeName(i), s_SwatchLabel);

                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                {
                    WorldKitBrush.FloorType = i;
                    if (!WorldKitBrush.IsFloorBrush)
                        WorldKitBrush.BeginFloor();
                }
            }

            if (GUILayout.Button("Tür Ekle…", EditorStyles.miniButton, GUILayout.Width(70f)))
                SettingsService.OpenProjectSettings("Project/World Kit");

            DrawOperationPicker();
        }

        static readonly (WorldKitBrush.FloorOperation op, string tip)[] k_Operations =
        {
            (WorldKitBrush.FloorOperation.Paint, "Seçili seviyede, seçili türle doldur (yeni zemin ekler)."),
            (WorldKitBrush.FloorOperation.Recolor, "Yalnızca var olan zeminin türünü değiştir; yükseklik ve şekil korunur. Patika, nehir, bölge boyamak için."),
            (WorldKitBrush.FloorOperation.Raise, "Var olan zemini bir kat yükselt. Tek moduyla sürükleyerek heykel gibi kullanılabilir."),
            (WorldKitBrush.FloorOperation.Lower, "Var olan zemini bir kat alçalt (çukur, nehir yatağı)."),
            (WorldKitBrush.FloorOperation.Wall, "Seçilen alanın dış kenarlarına duvar ör. Zemini değiştirmez."),
        };

        /// <summary>Zemin işlemi seçici: Boya / Sadece Renk / Yükselt / Alçalt / Duvarla Çevir.</summary>
        static void DrawOperationPicker()
        {
            GUILayout.Space(6f);
            EditorGUILayout.LabelField("İşlem", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var (op, tip) in k_Operations)
                {
                    bool on = WorldKitBrush.FloorOp == op;
                    if (GUILayout.Toggle(on, new GUIContent(WorldKitBrush.FloorOperationName(op), tip), EditorStyles.miniButton) && !on)
                    {
                        WorldKitBrush.FloorOp = op;
                        if (!WorldKitBrush.IsFloorBrush)
                            WorldKitBrush.BeginFloor();
                    }
                }
            }

            if (WorldKitBrush.FloorOp == WorldKitBrush.FloorOperation.Wall)
            {
                var wall = (GameObject)EditorGUILayout.ObjectField(
                    new GUIContent("Duvar", "1 hücrelik bir çizgi parçası önerilir (ör. Duvar_1m). Boşsa Duvar_1m kullanılır."),
                    WorldKitBrush.FloorWallPrefab, typeof(GameObject), false);
                if (wall != WorldKitBrush.FloorWallPrefab)
                    WorldKitBrush.FloorWallPrefab = wall;
            }
        }
    }

    /// <summary>Scene kamerasını top-down açıya getirir.</summary>
    sealed class TopDownCameraItem : WorldKitToolbarItem
    {
        public override string Label => "Üstten Kamera";
        public override string Tooltip => "Scene kamerasını top-down açıya (55°) getirir.";
        public override int Order => 10;

        public override void Execute()
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                return;

            var pivot = sceneView.pivot;
            pivot.y = WorldKitBrush.Level * WorldKitGrid.LevelHeight;
            sceneView.orthographic = false;
            sceneView.LookAt(pivot, Quaternion.Euler(55f, 45f, 0f), 14f);
            sceneView.Focus();
        }
    }

    /// <summary>Proje ayarlarını açar.</summary>
    sealed class SettingsItem : WorldKitToolbarItem
    {
        public override string Label => "Ayarlar";
        public override string Tooltip => "Project Settings → World Kit";
        public override int Order => 900;
        public override void Execute() => SettingsService.OpenProjectSettings("Project/World Kit");
    }
}
