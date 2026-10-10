using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// World Kit paleti.
    ///
    /// Düzen:
    ///   Başlık    — bölümler (İnşa / Analiz / Çıktı / eklentilerin bölümleri) + yenile, ayarlar, yardım
    ///   İnşa      — solda kategori listesi (Zemin + parçalar klasörünün alt klasörleri + İnşa bölümündeki eklenti
    ///               sekmeleri), sağda seçili fırça kartı, mod çubuğu (Yerleştir / Sil / Ölç) ve içerik
    ///   Diğerleri — bölümdeki sekmeler
    ///   Alt çubuk — WorldKitToolbarItem'lar, gruplarına göre (Görünüm, Test, Seçim...)
    /// </summary>
    public class WorldKitPaletteWindow : EditorWindow
    {
        const string SectionPrefKey = "WorldKit.Palette.Section";
        const string TabPrefKey = "WorldKit.Palette.Tab.";
        const string SizePrefKey = "WorldKit.Palette.Size";

        /// <summary>Bilinen klasörlerin görünen adı ve ikonu. Diğer klasörler için klasöre "_icon.png" koyun.</summary>
        static readonly Dictionary<string, (string title, Func<Texture2D> icon)> k_KnownFolders =
            new Dictionary<string, (string, Func<Texture2D>)>
            {
                { "Yapi", ("Yapı", () => WorldKitIcons.Wall) },
                { "Bina", ("Bina", () => WorldKitIcons.House) },
                { "Dekor", ("Dekor", () => WorldKitIcons.Tree) },
                { "Sablonlar", ("Şablonlar", () => WorldKitIcons.Stamp) },
                { "NPC", ("NPC", () => WorldKitIcons.Person) },
                { "Referans", ("Referans", () => WorldKitIcons.Reference) },
            };

        sealed class Tab
        {
            public string Title;
            public string Section;
            public int Order;
            public Func<Texture2D> Icon;
            public WorldKitPaletteTab Custom;
            public List<GameObject> Prefabs;
        }

        readonly List<Tab> m_Tabs = new List<Tab>();
        readonly List<string> m_Sections = new List<string>();
        List<WorldKitPaletteTab> m_CustomTabs;
        List<WorldKitToolbarItem> m_ToolbarItems;

        string m_Section;
        float m_TileSize = 84f;
        string m_Search = string.Empty;
        Vector2 m_Scroll;
        Vector2 m_SidebarScroll;
        GameObject m_DragCandidate;
        Vector2 m_DragStart;

        static GUIStyle s_TileLabel;
        static GUIStyle s_SidebarItem;
        static GUIStyle s_CardTitle;
        static GUIStyle s_GroupLabel;

        bool Narrow => position.width < 440f;

        [MenuItem("Tools/World Kit/Palet %#g", priority = 0)]
        public static void Open()
        {
            var window = GetWindow<WorldKitPaletteWindow>();
            window.titleContent = new GUIContent("World Kit", WorldKitIcons.Build);
            window.minSize = new Vector2(300f, 360f);
            window.Show();
        }

        void OnEnable()
        {
            titleContent = new GUIContent("World Kit", WorldKitIcons.Build);
            m_Section = EditorPrefs.GetString(SectionPrefKey, WorldKitPaletteTab.BuildSection);
            m_TileSize = EditorPrefs.GetFloat(SizePrefKey, 84f);
            wantsMouseMove = true;
            m_CustomTabs = WorldKitExtensions.Create<WorldKitPaletteTab>();
            m_ToolbarItems = WorldKitExtensions.Create<WorldKitToolbarItem>().OrderBy(i => i.Order).ToList();
            WorldKitBrush.Changed += Repaint;
            WorldKitEvents.SettingsChanged += Refresh;
            Selection.selectionChanged += Repaint;
            Refresh();
        }

        void OnDisable()
        {
            WorldKitBrush.Changed -= Repaint;
            WorldKitEvents.SettingsChanged -= Refresh;
            Selection.selectionChanged -= Repaint;
        }

        void OnProjectChange() => Refresh();

        // ---------------- Veri ----------------

        public void Refresh()
        {
            m_Tabs.Clear();
            var settings = WorldKitSettings.Instance;

            foreach (var custom in m_CustomTabs)
            {
                var tab = custom;
                m_Tabs.Add(new Tab { Title = tab.Title, Section = tab.Section, Order = tab.Order, Custom = tab, Icon = () => tab.Icon ?? WorldKitIcons.Folder });
            }

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
                    var customIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(folderPath + "/_icon.png");
                    k_KnownFolders.TryGetValue(folder, out var known);

                    m_Tabs.Add(new Tab
                    {
                        Title = known.title ?? folder.Replace('_', ' '),
                        Section = WorldKitPaletteTab.BuildSection,
                        Order = index >= 0 ? 100 + index : 500,
                        Icon = () => customIcon != null ? customIcon : known.icon != null ? known.icon() : WorldKitIcons.Folder,
                        Prefabs = prefabs
                    });
                }
            }

            // Hiç parça yoksa "Başlangıç içeriği üret" ekranını gösteren boş bir kategori.
            if (m_Tabs.All(t => t.Prefabs == null))
                m_Tabs.Add(new Tab { Title = "Parçalar", Section = WorldKitPaletteTab.BuildSection, Order = 100, Icon = () => WorldKitIcons.Folder, Prefabs = new List<GameObject>() });

            m_Tabs.Sort((a, b) => a.Order != b.Order
                ? a.Order.CompareTo(b.Order)
                : string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));

            // Bölümler: İnşa her zaman ilk, diğerleri içlerindeki ilk sekmenin sırasına göre.
            m_Sections.Clear();
            m_Sections.AddRange(m_Tabs
                .GroupBy(t => t.Section)
                .OrderBy(g => g.Key == WorldKitPaletteTab.BuildSection ? int.MinValue : g.Min(t => t.Order))
                .Select(g => g.Key));

            if (!m_Sections.Contains(m_Section))
                m_Section = m_Sections.FirstOrDefault() ?? WorldKitPaletteTab.BuildSection;

            Repaint();
        }

        IEnumerable<Tab> TabsIn(string section) => m_Tabs.Where(t => t.Section == section);

        Tab SelectedTab(string section)
        {
            var title = EditorPrefs.GetString(TabPrefKey + section, "");
            return TabsIn(section).FirstOrDefault(t => t.Title == title) ?? TabsIn(section).FirstOrDefault();
        }

        void SelectTab(Tab tab)
        {
            EditorPrefs.SetString(TabPrefKey + tab.Section, tab.Title);
            m_Scroll = Vector2.zero;
        }

        // ---------------- Çizim ----------------

        void OnGUI()
        {
            InitStyles();
            DrawHeader();

            var tab = SelectedTab(m_Section);
            if (m_Section == WorldKitPaletteTab.BuildSection)
            {
                using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
                {
                    DrawSidebar(tab);
                    using (new EditorGUILayout.VerticalScope())
                    {
                        DrawBrushCard();
                        DrawToolModes();
                        if (tab != null)
                            DrawTabContent(tab);
                        else
                            GUILayout.FlexibleSpace();
                    }
                }
            }
            else
            {
                var tabs = TabsIn(m_Section).ToList();
                if (tabs.Count > 1)
                {
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                    {
                        foreach (var t in tabs)
                        {
                            if (GUILayout.Toggle(t == tab, t.Title, EditorStyles.toolbarButton) && t != tab)
                                SelectTab(t);
                        }
                        GUILayout.FlexibleSpace();
                    }
                }

                if (tab != null)
                    DrawTabContent(tab);
                else
                    GUILayout.FlexibleSpace();
            }

            DrawFooter();
        }

        static void InitStyles()
        {
            s_TileLabel ??= new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.UpperCenter,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            s_SidebarItem ??= new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(6, 4, 2, 2),
                fixedHeight = 26f
            };
            s_CardTitle ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, clipping = TextClipping.Clip };
            s_GroupLabel ??= new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.4f, 0.4f, 0.4f) }
            };
        }

        static Texture2D SectionIcon(string section)
        {
            switch (section)
            {
                case WorldKitPaletteTab.BuildSection: return WorldKitIcons.Build;
                case WorldKitPaletteTab.AnalysisSection: return WorldKitIcons.Stats;
                case WorldKitPaletteTab.OutputSection: return WorldKitIcons.Output;
                default: return WorldKitIcons.Folder;
            }
        }

        /// <summary>Başlık: bölümler + yenile / ayarlar / yardım.</summary>
        void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            using (new EditorGUIUtility.IconSizeScope(new Vector2(14f, 14f)))
            {
                foreach (var section in m_Sections)
                {
                    bool on = section == m_Section;
                    var content = Narrow ? new GUIContent(SectionIcon(section), section) : new GUIContent(" " + section, SectionIcon(section));
                    if (GUILayout.Toggle(on, content, EditorStyles.toolbarButton, GUILayout.MinWidth(Narrow ? 30f : 70f)) && !on)
                    {
                        m_Section = section;
                        EditorPrefs.SetString(SectionPrefKey, section);
                        m_Scroll = Vector2.zero;
                    }
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent(WorldKitIcons.Refresh, "Listeyi ve önizlemeleri yenile"), EditorStyles.toolbarButton, GUILayout.Width(26f)))
                {
                    WorldKitThumbnails.Clear();
                    Refresh();
                }
                if (GUILayout.Button(new GUIContent(WorldKitIcons.Settings, "Proje ayarları (Project Settings → World Kit)"), EditorStyles.toolbarButton, GUILayout.Width(26f)))
                    SettingsService.OpenProjectSettings("Project/World Kit");
                if (GUILayout.Button(new GUIContent(WorldKitIcons.Help, "Kısayollar"), EditorStyles.toolbarButton, GUILayout.Width(26f)))
                    ShowHelp();
            }
        }

        /// <summary>İnşa bölümünün sol kategori listesi.</summary>
        void DrawSidebar(Tab selected)
        {
            float width = Narrow ? 34f : 124f;
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(width)))
            {
                m_SidebarScroll = EditorGUILayout.BeginScrollView(m_SidebarScroll, GUIStyle.none, GUI.skin.verticalScrollbar);
                GUILayout.Space(4f);

                string lastGroup = null;
                foreach (var tab in TabsIn(WorldKitPaletteTab.BuildSection))
                {
                    // Zemin ile parçalar arasında ince bir ayraç
                    string group = tab.Prefabs != null ? "parts" : "custom";
                    if (lastGroup != null && group != lastGroup)
                        DrawSeparator();
                    lastGroup = group;

                    var rect = GUILayoutUtility.GetRect(width, 26f);
                    bool isSelected = tab == selected;
                    if (Event.current.type == EventType.Repaint)
                    {
                        if (isSelected)
                            EditorGUI.DrawRect(rect, new Color(0.24f, 0.48f, 0.9f, EditorGUIUtility.isProSkin ? 0.55f : 0.35f));
                        else if (rect.Contains(Event.current.mousePosition))
                            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.15f));

                        GUI.DrawTexture(new Rect(rect.x + 7f, rect.y + 4f, 18f, 18f), tab.Icon(), ScaleMode.ScaleToFit);
                        if (!Narrow)
                            GUI.Label(new Rect(rect.x + 28f, rect.y, rect.width - 30f, rect.height), tab.Title, s_SidebarItem);
                    }

                    if (Narrow)
                        GUI.Label(rect, new GUIContent("", tab.Title));

                    if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                    {
                        SelectTab(tab);
                        Event.current.Use();
                    }
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndScrollView();
            }

            // Kenar çubuğu ile içerik arasında dikey çizgi
            var line = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(line, new Color(0f, 0f, 0f, 0.25f));

            if (Event.current.type == EventType.MouseMove)
                Repaint();
        }

        static void DrawSeparator()
        {
            GUILayout.Space(3f);
            var rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(new Rect(rect.x + 6f, rect.y, rect.width - 12f, 1f), new Color(0.5f, 0.5f, 0.5f, 0.3f));
            GUILayout.Space(3f);
        }

        /// <summary>Seçili fırçanın kartı: önizleme, ad, mod/kat/açı ve kontroller.</summary>
        void DrawBrushCard()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox, GUILayout.Height(46f)))
            {
                bool active = WorldKitBrush.IsActive;
                var thumbRect = GUILayoutUtility.GetRect(40f, 40f, GUILayout.Width(40f), GUILayout.Height(40f));
                Texture image = !active ? WorldKitIcons.Build
                    : WorldKitBrush.IsFloorBrush ? WorldKitIcons.Floor
                    : WorldKitThumbnails.Get(WorldKitBrush.Current);
                if (image != null)
                {
                    var color = GUI.color;
                    if (!active)
                        GUI.color = new Color(1f, 1f, 1f, 0.35f);
                    GUI.DrawTexture(active ? thumbRect : new Rect(thumbRect.x + 8f, thumbRect.y + 8f, 24f, 24f), image, ScaleMode.ScaleToFit);
                    GUI.color = color;
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (!active)
                    {
                        GUILayout.Label("Fırça seçili değil", s_CardTitle);
                        GUILayout.Label("Parçaya tıkla · Ctrl+tık: varyasyon ekle", EditorStyles.miniLabel);
                    }
                    else
                    {
                        var name = WorldKitBrush.IsFloorBrush
                            ? $"Zemin · {WorldKitBrush.FloorOperationName(WorldKitBrush.FloorOp)}"
                            : Nice(WorldKitBrush.Current.name) + (WorldKitBrush.Variants.Count > 1 ? $"  +{WorldKitBrush.Variants.Count - 1} varyasyon" : "");
                        GUILayout.Label(name, s_CardTitle);
                        var tool = WorldKitBrush.Tool;
                        var details = $"{tool?.DisplayName(WorldKitBrush.Context)} · Kat {WorldKitBrush.Level}";
                        if (!WorldKitBrush.IsFloorBrush)
                            details += $" · {WorldKitBrush.Rotation * 90}°";
                        GUILayout.Label(details, EditorStyles.miniLabel);
                    }
                    GUILayout.FlexibleSpace();
                }

                if (!active)
                    return;

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(Narrow ? 70f : 120f)))
                {
                    GUILayout.FlexibleSpace();
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (!Narrow)
                            GUILayout.Label("Kat", EditorStyles.miniLabel, GUILayout.Width(22f));
                        if (GUILayout.Button(new GUIContent("−", "Kat aşağı (Q)"), EditorStyles.miniButtonLeft, GUILayout.Width(20f)))
                            WorldKitBrush.ChangeLevel(-1);
                        GUILayout.Label(WorldKitBrush.Level.ToString(), EditorStyles.miniButtonMid, GUILayout.Width(24f));
                        if (GUILayout.Button(new GUIContent("+", "Kat yukarı (E)"), EditorStyles.miniButtonRight, GUILayout.Width(20f)))
                            WorldKitBrush.ChangeLevel(1);
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (!WorldKitBrush.IsFloorBrush && GUILayout.Button(new GUIContent("↻", "Döndür (R)"), EditorStyles.miniButtonLeft, GUILayout.Width(Narrow ? 30f : 42f)))
                            WorldKitBrush.Rotate(1);
                        if (GUILayout.Button(new GUIContent("Bırak", "Fırçayı bırak (Esc)"), WorldKitBrush.IsFloorBrush ? EditorStyles.miniButton : EditorStyles.miniButtonRight))
                            WorldKitBrush.End();
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        /// <summary>Mod çubuğu: Yerleştir / Sil / Ölç grupları, ikonlu düğmeler.</summary>
        void DrawToolModes()
        {
            var context = WorldKitBrush.Context;
            var tools = WorldKitTools.All.ToList();
            var groups = new[]
            {
                ("YERLEŞTİR", tools.Where(t => t.ModifiesScene && !t.Erases).ToList()),
                ("SİL", tools.Where(t => t.Erases).ToList()),
                ("ÖLÇ", tools.Where(t => !t.ModifiesScene).ToList()),
            };

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(26f)))
            using (new EditorGUIUtility.IconSizeScope(new Vector2(16f, 16f)))
            {
                foreach (var (label, list) in groups)
                {
                    if (list.Count == 0)
                        continue;
                    if (!Narrow)
                        GUILayout.Label(label, s_GroupLabel, GUILayout.ExpandWidth(false));

                    foreach (var tool in list)
                    {
                        int index = tools.IndexOf(tool);
                        var key = index < 9 ? $" ({index + 1})" : "";
                        var content = tool.Icon != null
                            ? new GUIContent(tool.Icon, $"{tool.DisplayName(context)}{key}\n{tool.Tooltip}")
                            : new GUIContent(tool.DisplayName(context).Substring(0, 1), $"{tool.DisplayName(context)}{key}\n{tool.Tooltip}");

                        using (new EditorGUI.DisabledScope(!tool.Supports(context)))
                        {
                            bool on = WorldKitBrush.Tool == tool;
                            if (GUILayout.Toggle(on, content, EditorStyles.toolbarButton, GUILayout.Width(28f), GUILayout.Height(22f)) && !on)
                                WorldKitBrush.Tool = tool;
                        }
                    }
                    GUILayout.Space(6f);
                }

                GUILayout.FlexibleSpace();
                var current = WorldKitBrush.Tool;
                if (current != null)
                    GUILayout.Label(current.DisplayName(context), EditorStyles.miniBoldLabel);
            }

            // Seçili modun ayarları (ör. Serp: yarıçap, yoğunluk).
            var selected = WorldKitBrush.Tool;
            if (selected != null && selected.HasSettings && selected.Supports(context))
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUI.BeginChangeCheck();
                    selected.OnSettingsGUI();
                    if (EditorGUI.EndChangeCheck())
                        SceneView.RepaintAll();
                }
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
                m_Search = GUILayout.TextField(m_Search, EditorStyles.toolbarSearchField,
                    GUILayout.MinWidth(60f)) ?? string.Empty;
                if (m_Search.Length > 0 && GUILayout.Button(new GUIContent("×", "Aramayı temizle"), EditorStyles.toolbarButton, GUILayout.Width(20f)))
                {
                    m_Search = string.Empty;
                    GUI.FocusControl(null);
                }
                var size = GUILayout.HorizontalSlider(m_TileSize, 56f, 140f, GUILayout.Width(60f));
                if (!Mathf.Approximately(size, m_TileSize))
                {
                    m_TileSize = size;
                    EditorPrefs.SetFloat(SizePrefKey, m_TileSize);
                }
            }

            // Arama bütün kategorilerde yapılır
            if (m_Search.Length > 0)
            {
                var all = m_Tabs.Where(t => t.Prefabs != null).SelectMany(t => t.Prefabs).Distinct().ToList();
                int hits = all.Count(p => p != null && Nice(p.name).IndexOf(m_Search, StringComparison.OrdinalIgnoreCase) >= 0);
                EditorGUILayout.LabelField($"{hits} sonuç (tüm kategoriler)", EditorStyles.miniLabel);
                DrawPrefabGrid(all);
                return;
            }

            DrawPrefabGrid(tab.Prefabs);
        }

        void DrawEmptyState()
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.HelpBox(
                "Parçalar klasöründe (" + WorldKitSettings.Instance.piecesFolder + ") henüz prefab yok.\n\n" +
                "• Kendi prefab'larını bu klasörün alt klasörlerine koy (her alt klasör bir kategori olur), veya\n" +
                "• Başlangıç içeriğini üret.",
                MessageType.Info);
            if (GUILayout.Button("Başlangıç İçeriğini Üret", GUILayout.Height(32f)))
            {
                WorldKitStarterContent.Generate();
                Refresh();
            }
            GUILayout.FlexibleSpace();
        }

        void DrawPrefabGrid(List<GameObject> source)
        {
            var pieces = source
                .Where(p => p != null)
                .Where(p => string.IsNullOrEmpty(m_Search) || Nice(p.name).IndexOf(m_Search, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            const float padding = 6f;
            const float labelHeight = 28f;
            float tileWidth = m_TileSize;
            float tileHeight = m_TileSize + labelHeight;
            float viewWidth = EditorGUIUtility.currentViewWidth - (Narrow ? 34f : 124f) - 20f;
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

                bool selected = WorldKitBrush.Variants.Contains(prefab);
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
                    if (EditorGUI.actionKey)
                        WorldKitBrush.ToggleVariant(prefab);        // Ctrl+tık: varyasyon ekle/çıkar
                    else if (selected && WorldKitBrush.Variants.Count == 1)
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

        /// <summary>Alt çubuk: eklenti düğmeleri, gruplarına göre.</summary>
        void DrawFooter()
        {
            if (m_ToolbarItems.Count == 0)
                return;

            var groups = m_ToolbarItems.GroupBy(i => i.Group).OrderBy(g => g.Min(i => i.Order));
            bool iconsOnly = position.width < 560f;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(24f)))
            using (new EditorGUIUtility.IconSizeScope(new Vector2(14f, 14f)))
            {
                foreach (var group in groups)
                {
                    if (!iconsOnly)
                        GUILayout.Label(group.Key.ToUpperInvariant(), s_GroupLabel, GUILayout.ExpandWidth(false));

                    foreach (var item in group)
                    {
                        var content = item.Icon == null ? new GUIContent(item.Label, item.Tooltip)
                            : iconsOnly ? new GUIContent(item.Icon, $"{item.Label}\n{item.Tooltip}")
                            : new GUIContent(" " + item.Label, item.Icon, item.Tooltip);

                        using (new EditorGUI.DisabledScope(!item.IsEnabled))
                        {
                            if (GUILayout.Button(content, EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
                                item.Execute();
                        }
                    }

                    GUILayout.Space(8f);
                }
                GUILayout.FlexibleSpace();
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
                "Fırça kapalıyken seçili parçalar:\n" +
                "  Oklar: taşı · PgUp/PgDn: kat · R: döndür · G: grid'e oturt · Delete: sil\n\n" +
                "Kısayollar Edit > Shortcuts > World Kit altından değiştirilebilir.",
                "Tamam");
        }

        static string Nice(string name) => name.Replace('_', ' ');
    }
}
