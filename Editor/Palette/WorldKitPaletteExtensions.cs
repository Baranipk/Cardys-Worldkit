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
        /// <summary>Paletin üstündeki bölümler. Kendi bölümünüz için herhangi bir ad da verebilirsiniz.</summary>
        public const string BuildSection = "İnşa";
        public const string AnalysisSection = "Analiz";
        public const string OutputSection = "Çıktı";

        public abstract string Title { get; }

        /// <summary>Sekme sırası (bölüm içinde). Zemin: 0, klasör sekmeleri: 100+, varsayılan: 1000.</summary>
        public virtual int Order => 1000;

        /// <summary>Sekmenin bulunduğu bölüm. "İnşa" bölümünde fırça kartı ve mod çubuğu da görünür.</summary>
        public virtual string Section => "Araçlar";

        /// <summary>Kenar çubuğundaki ikon (null = klasör ikonu).</summary>
        public virtual Texture2D Icon => null;

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

        /// <summary>Alt çubukta düğmenin gösterildiği grup (ör. "Görünüm", "Test", "Seçim").</summary>
        public virtual string Group => "Genel";

        /// <summary>Düğme ikonu (null = yalnızca yazı).</summary>
        public virtual Texture2D Icon => null;
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
        public override string Section => BuildSection;
        public override Texture2D Icon => WorldKitIcons.Floor;

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
            (WorldKitBrush.FloorOperation.Ramp, "Hücreyi yanındaki bir kat yüksek zemine doğru rampa yap (yön kendiliğinden). Boya ile düzleşir."),
            (WorldKitBrush.FloorOperation.Wall, "Seçilen alanın dış kenarlarına duvar ör. Zemini değiştirmez."),
        };

        static GUIStyle s_OperationButton;

        static Texture2D OperationIcon(WorldKitBrush.FloorOperation op)
        {
            switch (op)
            {
                case WorldKitBrush.FloorOperation.Recolor: return WorldKitIcons.Recolor;
                case WorldKitBrush.FloorOperation.Raise: return WorldKitIcons.Raise;
                case WorldKitBrush.FloorOperation.Lower: return WorldKitIcons.Lower;
                case WorldKitBrush.FloorOperation.Ramp: return WorldKitIcons.Ramp;
                case WorldKitBrush.FloorOperation.Wall: return WorldKitIcons.Wall;
                default: return WorldKitIcons.Paint;
            }
        }

        /// <summary>Zemin işlemi seçici: Boya / Sadece Renk / Yükselt / Alçalt / Duvarla Çevir.</summary>
        static void DrawOperationPicker()
        {
            GUILayout.Space(6f);
            EditorGUILayout.LabelField("İşlem", EditorStyles.boldLabel);

            s_OperationButton ??= new GUIStyle(EditorStyles.miniButton)
            {
                imagePosition = ImagePosition.ImageAbove,
                fixedHeight = 44f,
                fontSize = 9,
                padding = new RectOffset(2, 2, 4, 2)
            };

            const float buttonWidth = 64f;
            int columns = Mathf.Max(1, Mathf.FloorToInt((EditorGUIUtility.currentViewWidth - 160f) / (buttonWidth + 2f)));
            using (new EditorGUIUtility.IconSizeScope(new Vector2(20f, 20f)))
            {
                for (int i = 0; i < k_Operations.Length; i += columns)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        for (int j = i; j < Mathf.Min(i + columns, k_Operations.Length); j++)
                        {
                            var (op, tip) = k_Operations[j];
                            bool on = WorldKitBrush.FloorOp == op;
                            var content = new GUIContent(WorldKitBrush.FloorOperationName(op), OperationIcon(op), tip);
                            if (GUILayout.Toggle(on, content, s_OperationButton, GUILayout.Width(buttonWidth)) && !on)
                            {
                                WorldKitBrush.FloorOp = op;
                                if (!WorldKitBrush.IsFloorBrush)
                                    WorldKitBrush.BeginFloor();
                            }
                        }
                        GUILayout.FlexibleSpace();
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
        public override string Group => "Görünüm";
        public override Texture2D Icon => WorldKitIcons.Camera;

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

}
