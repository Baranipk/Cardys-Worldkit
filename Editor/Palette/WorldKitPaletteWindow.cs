using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// World Kit paleti. Sekmeler iki kaynaktan gelir:
    /// 1) Parçalar klasörünün her alt klasörü (prefab'lar otomatik listelenir),
    /// 2) Koddan eklenen WorldKitPaletteTab sınıfları (ör. Zemin).
    /// Mod çubuğu WorldKitTool'lardan, alt çubuk WorldKitToolbarItem'lardan oluşur.
    /// </summary>
    public class WorldKitPaletteWindow : EditorWindow
    {
        const string TabPrefKey = "WorldKit.Palette.Tab";
        const string SizePrefKey = "WorldKit.Palette.Size";

        sealed class Tab
        {
            public string Title;
            public int Order;
            public WorldKitPaletteTab Custom;
            public List<GameObject> Prefabs;
        }

        readonly List<Tab> m_Tabs = new List<Tab>();
        List<WorldKitPaletteTab> m_CustomTabs;
        List<WorldKitToolbarItem> m_ToolbarItems;

        int m_Tab;
        float m_TileSize = 84f;
        string m_Search = string.Empty;
        Vector2 m_Scroll;
        GameObject m_DragCandidate;
        Vector2 m_DragStart;

        static GUIStyle s_RichLabel;
        static GUIStyle s_TileLabel;

        [MenuItem("Tools/World Kit/Palet %#g", priority = 0)]
        public static void Open()
        {
            var window = GetWindow<WorldKitPaletteWindow>();
            window.titleContent = new GUIContent("World Kit", EditorGUIUtility.IconContent("Prefab Icon").image);
            window.minSize = new Vector2(280f, 300f);
            window.Show();
        }

        void OnEnable()
        {
            m_Tab = EditorPrefs.GetInt(TabPrefKey, 0);
            m_TileSize = EditorPrefs.GetFloat(SizePrefKey, 84f);
            wantsMouseMove = true;
            m_CustomTabs = WorldKitExtensions.Create<WorldKitPaletteTab>();
            m_ToolbarItems = WorldKitExtensions.Create<WorldKitToolbarItem>().OrderBy(i => i.Order).ToList();
            WorldKitBrush.Changed += Repaint;
            WorldKitEvents.SettingsChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            WorldKitBrush.Changed -= Repaint;
            WorldKitEvents.SettingsChanged -= Refresh;
        }

        void OnProjectChange() => Refresh();

        public void Refresh()
        {
            m_Tabs.Clear();
            var settings = WorldKitSettings.Instance;

            foreach (var custom in m_CustomTabs)
                m_Tabs.Add(new Tab { Title = custom.Title, Order = custom.Order, Custom = custom });

            if (AssetDatabase.IsValidFolder(settings.piecesFolder))
            {
                foreach (var folderPath in AssetDatabase.GetSubFolders(settings.piecesFolder))
                {
                    var folder = Path.GetFileName(folderPath);
                    var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath })
                        .Select(AssetDatabase.GUIDToAssetPath)
                        .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                        .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                        .Where(p => p != null)
                        .ToList();

                    if (prefabs.Count == 0)
                        continue;

                    int index = settings.categoryOrder.IndexOf(folder);
                    m_Tabs.Add(new Tab
                    {
                        Title = folder,
                        Order = index >= 0 ? 100 + index : 500,
                        Prefabs = prefabs
                    });
                }
            }

            // Hiç parça yoksa "Başlangıç içeriği üret" ekranını gösteren boş bir sekme.
            if (m_Tabs.All(t => t.Prefabs == null))
                m_Tabs.Add(new Tab { Title = "Parçalar", Order = 100, Prefabs = new List<GameObject>() });

            m_Tabs.Sort((a, b) => a.Order != b.Order
                ? a.Order.CompareTo(b.Order)
                : string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));

            m_Tab = Mathf.Clamp(m_Tab, 0, Mathf.Max(0, m_Tabs.Count - 1));
            Repaint();
        }

        void OnGUI()
        {
            DrawTabs();
            DrawToolModes();
            DrawBrushStatus();

            if (m_Tab < m_Tabs.Count)
                DrawTabContent(m_Tabs[m_Tab]);
            else
                GUILayout.FlexibleSpace();

            DrawFooter();
        }

        void DrawEmptyState()
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.HelpBox(
                "Parçalar klasöründe (" + WorldKitSettings.Instance.piecesFolder + ") henüz prefab yok.\n\n" +
                "• Kendi prefab'larını bu klasörün alt klasörlerine koy (her alt klasör bir sekme olur), veya\n" +
                "• Başlangıç içeriğini üret.",
                MessageType.Info);
            if (GUILayout.Button("Başlangıç İçeriğini Üret", GUILayout.Height(32f)))
            {
                WorldKitStarterContent.Generate();
                Refresh();
            }
            GUILayout.FlexibleSpace();
        }

        void DrawTabs()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var labels = m_Tabs.Select(t => t.Title).ToArray();
                var tab = GUILayout.Toolbar(m_Tab, labels, EditorStyles.toolbarButton);
                if (tab != m_Tab)
                {
                    m_Tab = tab;
                    EditorPrefs.SetInt(TabPrefKey, m_Tab);
                }
            }
        }

        /// <summary>Mod çubuğu: kayıtlı tüm WorldKitTool'lar.</summary>
        void DrawToolModes()
        {
            var context = WorldKitBrush.Context;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Mod", EditorStyles.miniLabel, GUILayout.Width(28f));
                var tools = WorldKitTools.All;
                for (int i = 0; i < tools.Count; i++)
                {
                    var tool = tools[i];
                    using (new EditorGUI.DisabledScope(!tool.Supports(context)))
                    {
                        var label = i < 9 ? $"{i + 1} {tool.DisplayName(context)}" : tool.DisplayName(context);
                        bool on = WorldKitBrush.Tool == tool;
                        if (GUILayout.Toggle(on, new GUIContent(label, tool.Tooltip), EditorStyles.toolbarButton) && !on)
                            WorldKitBrush.Tool = tool;
                    }
                }
            }

            // Seçili modun ayarları (ör. Serp: yarıçap, yoğunluk).
            var current = WorldKitBrush.Tool;
            if (current != null && current.HasSettings)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUI.BeginChangeCheck();
                    current.OnSettingsGUI();
                    if (EditorGUI.EndChangeCheck())
                        SceneView.RepaintAll();
                }
            }
        }

        void DrawBrushStatus()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                if (!WorldKitBrush.IsActive)
                {
                    GUILayout.Label("Bir parçaya tıkla veya Scene'e sürükle.", EditorStyles.miniLabel);
                    return;
                }

                var brushName = WorldKitBrush.IsFloorBrush ? "Zemin" : Nice(WorldKitBrush.Current.name);
                GUILayout.Label($"Fırça: <b>{brushName}</b>", s_RichLabel ??= new GUIStyle(EditorStyles.label) { richText = true });
                GUILayout.FlexibleSpace();

                GUILayout.Label("Seviye", EditorStyles.miniLabel);
                if (GUILayout.Button("−", EditorStyles.miniButtonLeft, GUILayout.Width(20f)))
                    WorldKitBrush.ChangeLevel(-1);
                GUILayout.Label(WorldKitBrush.Level.ToString(), EditorStyles.miniButtonMid, GUILayout.Width(26f));
                if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(20f)))
                    WorldKitBrush.ChangeLevel(1);

                if (!WorldKitBrush.IsFloorBrush && GUILayout.Button($"↻ {WorldKitBrush.Rotation * 90}°", EditorStyles.miniButton, GUILayout.Width(50f)))
                    WorldKitBrush.Rotate(1);

                if (GUILayout.Button("Bırak", EditorStyles.miniButton, GUILayout.Width(44f)))
                    WorldKitBrush.End();
            }
        }

        void DrawTabContent(Tab tab)
        {
            if (tab.Custom != null)
            {
                m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
                tab.Custom.OnGUI(this);
                EditorGUILayout.EndScrollView();
                return;
            }

            if (tab.Prefabs.Count == 0)
            {
                DrawEmptyState();
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                m_Search = GUILayout.TextField(m_Search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(80f));
                var size = GUILayout.HorizontalSlider(m_TileSize, 56f, 140f, GUILayout.Width(70f));
                if (!Mathf.Approximately(size, m_TileSize))
                {
                    m_TileSize = size;
                    EditorPrefs.SetFloat(SizePrefKey, m_TileSize);
                }
            }

            DrawPrefabGrid(tab.Prefabs);
        }

        void DrawPrefabGrid(List<GameObject> source)
        {
            var pieces = source
                .Where(p => p != null)
                .Where(p => string.IsNullOrEmpty(m_Search) || Nice(p.name).IndexOf(m_Search, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            s_TileLabel ??= new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.UpperCenter,
                wordWrap = true,
                clipping = TextClipping.Clip
            };

            const float padding = 6f;
            const float labelHeight = 28f;
            float tileWidth = m_TileSize;
            float tileHeight = m_TileSize + labelHeight;
            float viewWidth = position.width - 16f;
            int columns = Mathf.Max(1, Mathf.FloorToInt((viewWidth - padding) / (tileWidth + padding)));
            int rows = Mathf.CeilToInt(pieces.Count / (float)columns);

            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
            var area = GUILayoutUtility.GetRect(viewWidth, rows * (tileHeight + padding) + padding);

            var e = Event.current;
            for (int i = 0; i < pieces.Count; i++)
            {
                var prefab = pieces[i];
                int col = i % columns;
                int row = i / columns;
                var rect = new Rect(
                    area.x + padding + col * (tileWidth + padding),
                    area.y + padding + row * (tileHeight + padding),
                    tileWidth, tileHeight);
                var imageRect = new Rect(rect.x, rect.y, tileWidth, tileWidth);
                var labelRect = new Rect(rect.x, imageRect.yMax + 2f, tileWidth, labelHeight);

                bool selected = WorldKitBrush.Current == prefab;
                if (e.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(new Rect(imageRect.x - 2f, imageRect.y - 2f, imageRect.width + 4f, imageRect.height + 4f),
                        selected ? new Color(0.25f, 0.6f, 1f, 1f) : rect.Contains(e.mousePosition) ? new Color(1f, 1f, 1f, 0.15f) : Color.clear);

                    var thumbnail = WorldKitThumbnails.Get(prefab);
                    if (thumbnail != null)
                        GUI.DrawTexture(imageRect, thumbnail, ScaleMode.ScaleToFit);
                    else
                        EditorGUI.DrawRect(imageRect, new Color(0.3f, 0.3f, 0.3f));

                    s_TileLabel.Draw(labelRect, Nice(prefab.name), false, false, selected, false);
                }

                HandleTileInput(e, rect, prefab, selected);
            }

            EditorGUILayout.EndScrollView();

            if (e.type == EventType.MouseMove)
                Repaint();
        }

        void HandleTileInput(Event e, Rect rect, GameObject prefab, bool selected)
        {
            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0 && rect.Contains(e.mousePosition):
                    m_DragCandidate = prefab;
                    m_DragStart = e.mousePosition;
                    e.Use();
                    break;

                case EventType.MouseDrag when m_DragCandidate == prefab && (e.mousePosition - m_DragStart).magnitude > 6f:
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.objectReferences = Array.Empty<UnityEngine.Object>();
                    DragAndDrop.SetGenericData(WorldKitBrush.DragDataKey, prefab);
                    DragAndDrop.StartDrag(Nice(prefab.name));
                    m_DragCandidate = null;
                    e.Use();
                    break;

                case EventType.MouseUp when m_DragCandidate == prefab:
                    m_DragCandidate = null;
                    if (selected)
                        WorldKitBrush.End();
                    else
                        WorldKitBrush.Begin(prefab);
                    e.Use();
                    break;

                case EventType.ContextClick when rect.Contains(e.mousePosition):
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Project'te Göster"), false, () => EditorGUIUtility.PingObject(prefab));
                    menu.AddItem(new GUIContent("Prefab'ı Düzenle"), false, () => AssetDatabase.OpenAsset(prefab));
                    menu.ShowAsContext();
                    e.Use();
                    break;
            }
        }

        void DrawFooter()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                foreach (var item in m_ToolbarItems)
                {
                    using (new EditorGUI.DisabledScope(!item.IsEnabled))
                    {
                        if (GUILayout.Button(new GUIContent(item.Label, item.Tooltip), EditorStyles.toolbarButton))
                            item.Execute();
                    }
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button(EditorGUIUtility.IconContent("Refresh", "|Önizlemeleri ve listeyi yenile"), EditorStyles.toolbarButton))
                {
                    WorldKitThumbnails.Clear();
                    Refresh();
                }

                if (GUILayout.Button(EditorGUIUtility.IconContent("_Help", "|Kısayollar"), EditorStyles.toolbarButton))
                    ShowHelp();
            }
        }

        static void ShowHelp()
        {
            var modes = string.Join("\n", WorldKitTools.All.Select((t, i) => $"  {i + 1} {t.Name}: {t.Tooltip}"));
            EditorUtility.DisplayDialog("World Kit - Kısayollar",
                "Paletten parçaya tıkla → fırça seçilir\n" +
                "Parçayı Scene'e sürükle → tek parça bırakır\n\n" +
                "Modlar:\n" + modes + "\n\n" +
                "Scene'de (fırça seçiliyken):\n" +
                "  Shift + tık: son noktadan buraya kadar doldur\n" +
                "  Ctrl + tık / tut-çek: her modda sil\n" +
                "  R / Shift+R: döndür\n" +
                "  E / Q: seviye yukarı / aşağı\n" +
                "  Esc: sürüklerken iptal, değilse fırçayı bırak\n" +
                "  Alt + sürükle: kamerayı döndür\n\n" +
                "Kısayollar Edit > Shortcuts > World Kit altından değiştirilebilir.",
                "Tamam");
        }

        static string Nice(string name) => name.Replace('_', ' ');
    }
}
