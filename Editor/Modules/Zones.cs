using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// Bölgeler: spawn alanı, görev noktası, güvenli bölge gibi tetikleyici kutular (<see cref="WorldKitZone"/>).
    /// - "Bölge" modu (9): tut-çek ile çalışılan seviyede bir bölge çizer. Fırça hangi parçada olursa olsun çalışır.
    /// - "Bölgeler" sekmesi: yeni bölgenin türü ve yüksekliği, sahnedeki bölgelerin listesi.
    /// Bölgeler "Bolgeler" grubuna konur, "Ignore Raycast" katmanındadır (Erişim ve NavMesh onları engel saymaz).
    /// </summary>
    public static class WorldKitZones
    {
        public const string GroupName = "Bolgeler";
        const string KindKey = "WorldKit.Zone.Kind";
        const string HeightKey = "WorldKit.Zone.Height";

        /// <summary>Yeni bölgelerin türü.</summary>
        public static WorldKitZone.Kind NewKind
        {
            get => (WorldKitZone.Kind)EditorPrefs.GetInt(KindKey, (int)WorldKitZone.Kind.Trigger);
            set => EditorPrefs.SetInt(KindKey, (int)value);
        }

        /// <summary>Yeni bölgelerin yüksekliği (kat).</summary>
        public static int NewHeightLevels
        {
            get => EditorPrefs.GetInt(HeightKey, 2);
            set => EditorPrefs.SetInt(HeightKey, Mathf.Clamp(value, 1, 10));
        }

        /// <summary>Hücre dikdörtgenini kaplayan bir bölge oluşturur (Undo'lu).</summary>
        public static WorldKitZone Create(RectInt cells, int level, WorldKitZone.Kind kind)
        {
            float cs = WorldKitGrid.CellSize;
            float height = NewHeightLevels * WorldKitGrid.LevelHeight;
            var group = WorldKitScene.GetGroup(GroupName);

            int index = group.GetComponentsInChildren<WorldKitZone>().Count(z => z.kind == kind) + 1;
            var go = new GameObject($"Bolge_{kind}_{index}");
            go.layer = 2; // Ignore Raycast
            go.transform.SetParent(group, false);
            go.transform.position = new Vector3(cells.center.x * cs, level * WorldKitGrid.LevelHeight, cells.center.y * cs);

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(cells.width * cs, height, cells.height * cs);
            box.center = new Vector3(0f, height * 0.5f, 0f);

            var zone = go.AddComponent<WorldKitZone>();
            zone.kind = kind;
            zone.zoneId = go.name.ToLowerInvariant();

            Undo.RegisterCreatedObjectUndo(go, "World Kit: Bölge");
            WorldKitEvents.RaisePiecePlaced(go);
            return zone;
        }

        public static WorldKitZone[] All()
        {
            var root = WorldKitScene.FindRoot();
            return root != null ? root.GetComponentsInChildren<WorldKitZone>(true) : new WorldKitZone[0];
        }
    }

    /// <summary>Tut-çek: çalışılan seviyede dikdörtgen bölge. Sahneye parça koymaz, bölge objesi oluşturur.</summary>
    public sealed class ZoneTool : WorldKitTool
    {
        public override string Name => "Bölge";
        public override string Tooltip => "Tut-çek: spawn, görev, güvenli/tehlikeli alan gibi bir bölge çiz (tür Bölgeler sekmesinde).";
        public override int Order => 98;
        public override Texture2D Icon => WorldKitIcons.Zone;
        public override bool ModifiesScene => false;
        public override string Hint(BrushContext c) => $"Tut-çek: {WorldKitZones.NewKind} bölgesi çiz";

        public override void CollectPlacements(BrushContext c, System.Collections.Generic.List<Placement> result) { }
        public override void CollectCells(BrushContext c, System.Collections.Generic.ICollection<Vector2Int> result) { }

        public override void OnPreviewGUI(BrushContext c)
        {
            var rect = c.DragRect(false);
            float cs = WorldKitGrid.CellSize;
            float y = c.Level * WorldKitGrid.LevelHeight + 0.05f;
            float h = WorldKitZones.NewHeightLevels * WorldKitGrid.LevelHeight;
            var color = WorldKitZone.ColorOf(WorldKitZones.NewKind);

            var corners = new[]
            {
                new Vector3(rect.xMin * cs, y, rect.yMin * cs), new Vector3(rect.xMin * cs, y, rect.yMax * cs),
                new Vector3(rect.xMax * cs, y, rect.yMax * cs), new Vector3(rect.xMax * cs, y, rect.yMin * cs)
            };
            Handles.DrawSolidRectangleWithOutline(corners, new Color(color.r, color.g, color.b, 0.2f), color);
            using (new Handles.DrawingScope(color))
            {
                foreach (var p in corners)
                    Handles.DrawLine(p, p + Vector3.up * h);
            }
            Handles.Label(corners[1] + Vector3.up * (h + 0.2f), $"{WorldKitZones.NewKind}  {rect.width}×{rect.height}", EditorStyles.whiteBoldLabel);
        }

        public override void OnStrokeFinished(BrushContext c)
        {
            var zone = WorldKitZones.Create(c.DragRect(false), c.Level, WorldKitZones.NewKind);
            Selection.activeGameObject = zone.gameObject;
        }
    }

    /// <summary>"Bölgeler" sekmesi: yeni bölge ayarları ve sahnedeki bölgeler.</summary>
    [InitializeOnLoad]
    sealed class ZonesPaletteTab : WorldKitPaletteTab
    {
        static ZonesPaletteTab()
        {
            SceneView.duringSceneGui += DrawLabels;
        }

        public override string Title => "Bölgeler";
        public override int Order => 600;
        public override string Section => BuildSection;
        public override Texture2D Icon => WorldKitIcons.Zone;

        public override void OnGUI(WorldKitPaletteWindow window)
        {
            GUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "Bölge = tetikleyici kutu (spawn alanı, görev noktası, güvenli / tehlikeli alan). Fırça açıkken " +
                "Bölge modunu (9) seçip tut-çek. Oyun kodu WorldKitZone bileşenini türüne ve kimliğine göre bulabilir " +
                "veya onEnter / onExit olaylarını Inspector'dan bağlayabilir.",
                MessageType.None);

            var kind = (WorldKitZone.Kind)EditorGUILayout.EnumPopup("Yeni bölge türü", WorldKitZones.NewKind);
            if (kind != WorldKitZones.NewKind)
                WorldKitZones.NewKind = kind;
            int height = EditorGUILayout.IntSlider("Yükseklik (kat)", WorldKitZones.NewHeightLevels, 1, 10);
            if (height != WorldKitZones.NewHeightLevels)
                WorldKitZones.NewHeightLevels = height;

            var zoneTool = WorldKitTools.All.OfType<ZoneTool>().FirstOrDefault();
            if (GUILayout.Button("Bölge Çiz (Bölge modu)", GUILayout.Height(24f)) && zoneTool != null)
            {
                if (!WorldKitBrush.IsActive)
                    WorldKitBrush.BeginFloor();
                WorldKitBrush.Tool = zoneTool;
            }

            GUILayout.Space(8f);
            var zones = WorldKitZones.All();
            EditorGUILayout.LabelField($"Sahnedeki bölgeler ({zones.Length})", EditorStyles.boldLabel);
            foreach (var zone in zones.OrderBy(z => z.kind).ThenBy(z => z.name))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var color = GUI.color;
                    GUI.color = WorldKitZone.ColorOf(zone.kind);
                    GUILayout.Label("■", GUILayout.Width(14f));
                    GUI.color = color;
                    if (GUILayout.Button(zone.name, EditorStyles.label))
                    {
                        Selection.activeGameObject = zone.gameObject;
                        SceneView.lastActiveSceneView?.FrameSelected();
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(zone.kind.ToString(), EditorStyles.miniLabel);
                }
            }
        }

        static void DrawLabels(SceneView view)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            foreach (var zone in WorldKitZones.All())
            {
                if (!zone.isActiveAndEnabled)
                    continue;
                var box = zone.GetComponent<BoxCollider>();
                var top = zone.transform.TransformPoint(box.center + Vector3.up * box.size.y * 0.5f);
                var style = new GUIStyle(EditorStyles.whiteMiniLabel) { normal = { textColor = WorldKitZone.ColorOf(zone.kind) } };
                Handles.Label(top, string.IsNullOrEmpty(zone.zoneId) ? zone.name : zone.zoneId, style);
            }
        }
    }
}
