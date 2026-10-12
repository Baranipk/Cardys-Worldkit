using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using HandleUtility = UnityEditor.HandleUtility;

namespace WorldKit.Editor
{
    /// <summary>
    /// Scene görünümünde yerleştirme yapan fırça. İki hedefi var: prefab parçaları ve zemin (WorldKitFloor).
    /// NEREYE konacağını seçili WorldKitTool hesaplar; fırça önizleme, undo, yerleştirme ve silmeyi yapar.
    /// </summary>
    [InitializeOnLoad]
    public static class WorldKitBrush
    {
        public const string DragDataKey = "WorldKit.Piece";
        const string ToolPrefKey = "WorldKit.Brush.Tool";

        enum Mode { None, Prefab, Floor }

        struct GhostPart
        {
            public Mesh Mesh;
            public Matrix4x4 Local;
        }

        static Mode s_Mode;
        static WorldKitTool s_Tool;
        static GameObject s_Prefab;
        static readonly List<GhostPart> s_GhostParts = new List<GhostPart>();
        static List<WorldKitStamp.FloorCell> s_StampCells;
        static bool s_Temporary;

        static readonly BrushContext s_Context = new BrushContext();
        static bool s_HasHit;
        static bool s_EraseDrag;
        static bool s_Shift;
        static int s_UndoGroup;
        static Vector2Int s_LastStrokeCell;
        static Vector3 s_LastStrokePoint;

        // Shift+tık zinciri: son yerleştirilen noktadan imlece kadar doldurmak için.
        static bool s_HasLastPoint;
        static Vector3 s_LastPoint;

        static readonly List<Placement> s_Placements = new List<Placement>();
        static readonly HashSet<Vector2Int> s_Cells = new HashSet<Vector2Int>();
        static Material s_GhostMaterial;

        static readonly BrushShortcutContext s_ShortcutContext = new BrushShortcutContext();

        class BrushShortcutContext : IShortcutContext
        {
            public bool active => s_Mode != Mode.None && EditorWindow.focusedWindow is SceneView;
        }

        /// <summary>Fırça, mod, dönüş veya seviye değiştiğinde (palet yenilenir).</summary>
        public static event Action Changed;

        public static GameObject Current => s_Prefab;
        public static bool IsFloorBrush => s_Mode == Mode.Floor;
        public static bool IsActive => s_Mode != Mode.None;
        public static int Rotation => s_Context.Rotation;
        public static int Level => s_Context.Level;

        /// <summary>Modların okuduğu fırça durumu.</summary>
        public static BrushContext Context => s_Context;

        static WorldKitSettings Settings => WorldKitSettings.Instance;

        public static WorldKitTool Tool
        {
            get
            {
                if (s_Tool == null)
                    s_Tool = WorldKitTools.Find(EditorPrefs.GetString(ToolPrefKey, typeof(AreaTool).FullName))
                             ?? WorldKitTools.All.FirstOrDefault();
                return s_Tool;
            }
            set
            {
                if (value == null || s_Tool == value)
                    return;
                s_Tool = value;
                EditorPrefs.SetString(ToolPrefKey, value.GetType().FullName);
                SceneView.RepaintAll();
                Changed?.Invoke();
            }
        }

        static WorldKitBrush()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            WorldKitEvents.SettingsChanged += () =>
            {
                foreach (var floor in UnityEngine.Object.FindObjectsByType<WorldKitFloor>(FindObjectsSortMode.None))
                    SyncFloorMaterials(floor);
            };
            AssemblyReloadEvents.beforeAssemblyReload += End;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode)
                    End();
            };
            Undo.undoRedoPerformed += RebuildFloors;
        }

        // ---------------- Fırça seçimi ----------------

        public static void Begin(GameObject prefab) => Begin(prefab, false);

        static void Begin(GameObject prefab, bool temporary) => Begin(prefab != null ? new[] { prefab } : null, temporary);

        static void Begin(IReadOnlyList<GameObject> prefabs, bool temporary)
        {
            End();
            var list = prefabs?.Where(p => p != null).Distinct().ToList();
            if (list == null || list.Count == 0)
                return;

            s_Mode = Mode.Prefab;
            s_Prefab = list[0];
            s_Variants.AddRange(list);
            s_Temporary = temporary;
            s_Context.FreePlacement = WorldKitFreePlacement.IsDefaultFor(s_Prefab);
            CreateGhost();
            Activate(!temporary);
        }

        public static void BeginFloor()
        {
            End();
            s_Mode = Mode.Floor;
            Activate(true);
        }

        static void Activate(bool focusSceneView)
        {
            s_Context.IsFloor = s_Mode == Mode.Floor;
            s_Context.Prefab = s_Prefab;
            if (s_Mode == Mode.Floor)
                s_Context.Footprint = Vector2Int.one;

            ShortcutManager.RegisterContext(s_ShortcutContext);

            // Seçili objenin gizmo'su tıklamaları çalmasın.
            if (focusSceneView)
            {
                Selection.activeObject = null;
                if (SceneView.lastActiveSceneView != null)
                    SceneView.lastActiveSceneView.Focus();
            }

            SceneView.RepaintAll();
            Changed?.Invoke();
        }

        public static void End()
        {
            if (s_Mode == Mode.None && s_Ghosts.Count == 0)
                return;

            foreach (var ghost in s_Ghosts)
            {
                if (ghost != null)
                    UnityEngine.Object.DestroyImmediate(ghost);
            }
            if (s_Context.Dragging)
                GUIUtility.hotControl = 0;

            s_Ghosts.Clear();
            s_VariantParts.Clear();
            s_VariantBounds.Clear();
            s_Variants.Clear();
            s_GhostParts.Clear();
            s_StampCells = null;
            s_Prefab = null;
            s_Mode = Mode.None;
            s_Temporary = false;
            s_Context.Dragging = false;
            s_Context.Prefab = null;
            s_HasLastPoint = false;
            ShortcutManager.UnregisterContext(s_ShortcutContext);
            SceneView.RepaintAll();
            Changed?.Invoke();
        }

        public static void Rotate(int steps)
        {
            if (s_Context.IsFree)
                s_Context.FreeYaw = Mathf.Repeat(s_Context.FreeYaw + steps * Settings.freeRotationStep, 360f);
            else
                s_Context.Rotation = ((s_Context.Rotation + steps) % 4 + 4) % 4;
            SceneView.RepaintAll();
            Changed?.Invoke();
        }

        /// <summary>Serbest yerleştirme (grid'siz) açık mı? Prefab fırçasında G ile değişir.</summary>
        public static bool FreePlacement
        {
            get => s_Context.FreePlacement;
            set
            {
                if (s_Context.FreePlacement == value)
                    return;
                s_Context.FreePlacement = value;
                SceneView.RepaintAll();
                Changed?.Invoke();
            }
        }

        /// <summary>Ekranda gösterilecek açı: serbestte derece, grid'de 90° adım.</summary>
        public static float DisplayAngle => s_Context.IsFree ? s_Context.FreeYaw : s_Context.Rotation * 90f;

        [Shortcut("World Kit/Serbest Yerleştirme", typeof(BrushShortcutContext), KeyCode.G)]
        static void ToggleFreePlacement()
        {
            if (s_Mode == Mode.Prefab)
                FreePlacement = !FreePlacement;
        }

        public static void ChangeLevel(int delta)
        {
            // Elle kat seçmek otomatik katı kapatır (prefab fırçasında; palette "Oto" ile geri açılır)
            if (s_Mode == Mode.Prefab && AutoLevel)
                AutoLevel = false;
            var range = Settings.levelRange;
            s_Context.Level = Mathf.Clamp(s_Context.Level + delta, range.x, range.y);
            SceneView.RepaintAll();
            Changed?.Invoke();
        }

        /// <summary>Yarım hücre snap (H): grid'de parça yarım hücre adımlarla oturur.</summary>
        public static bool HalfSnap
        {
            get => s_Context.HalfSnap;
            set
            {
                if (s_Context.HalfSnap == value)
                    return;
                s_Context.HalfSnap = value;
                SceneView.RepaintAll();
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// Otomatik kat: prefab fırçasında kat, imlecin altındaki zeminin katına kendiliğinden geçer
        /// (platformun üstüne gelince 1, aşağı inince 0). E/Q ile elle kat seçmek bunu kapatır.
        /// </summary>
        public static bool AutoLevel
        {
            get => EditorPrefs.GetBool("WorldKit.Brush.AutoLevel", true);
            set
            {
                EditorPrefs.SetBool("WorldKit.Brush.AutoLevel", value);
                SceneView.RepaintAll();
                Changed?.Invoke();
            }
        }

        [Shortcut("World Kit/Yarım Hücre Snap", typeof(BrushShortcutContext), KeyCode.H)]
        static void ToggleHalfSnap()
        {
            if (s_Mode == Mode.Prefab)
                HalfSnap = !HalfSnap;
        }

        /// <summary>Rastgele sonuçları (dönüş, boyut, varyasyon, serpme) yeniden atar; önizleme hemen değişir.</summary>
        [Shortcut("World Kit/Rastgeleyi Yeniden At", typeof(BrushShortcutContext), KeyCode.Space)]
        public static void Reroll()
        {
            WorldKitFreePlacement.Salt++;
            SceneView.RepaintAll();
        }

        /// <summary>Sürükleyerek yön verilen son parçanın yönünü fırçaya aktarır (sonraki parçalar da öyle baksın).</summary>
        static void RememberFacing(Quaternion rotation)
        {
            float yaw = rotation.eulerAngles.y;
            if (s_Context.IsFree)
                s_Context.FreeYaw = Mathf.Repeat(yaw, 360f);
            else
                s_Context.Rotation = ((Mathf.RoundToInt(yaw / 90f) % 4) + 4) % 4;
            Changed?.Invoke();
        }

        // ---------------- Kısayollar (yalnızca fırça aktif ve Scene odaktayken) ----------------

        [Shortcut("World Kit/Saat Yönünde Döndür", typeof(BrushShortcutContext), KeyCode.R)]
        static void RotateClockwise() => Rotate(1);

        [Shortcut("World Kit/Saat Yönü Tersine Döndür", typeof(BrushShortcutContext), KeyCode.R, ShortcutModifiers.Shift)]
        static void RotateCounterClockwise() => Rotate(-1);

        [Shortcut("World Kit/Seviye Yukarı", typeof(BrushShortcutContext), KeyCode.E)]
        static void LevelUp() => ChangeLevel(1);

        [Shortcut("World Kit/Seviye Aşağı", typeof(BrushShortcutContext), KeyCode.Q)]
        static void LevelDown() => ChangeLevel(-1);

        [Shortcut("World Kit/Mod 1", typeof(BrushShortcutContext), KeyCode.Alpha1)] static void Tool1() => SelectTool(0);
        [Shortcut("World Kit/Mod 2", typeof(BrushShortcutContext), KeyCode.Alpha2)] static void Tool2() => SelectTool(1);
        [Shortcut("World Kit/Mod 3", typeof(BrushShortcutContext), KeyCode.Alpha3)] static void Tool3() => SelectTool(2);
        [Shortcut("World Kit/Mod 4", typeof(BrushShortcutContext), KeyCode.Alpha4)] static void Tool4() => SelectTool(3);
        [Shortcut("World Kit/Mod 5", typeof(BrushShortcutContext), KeyCode.Alpha5)] static void Tool5() => SelectTool(4);
        [Shortcut("World Kit/Mod 6", typeof(BrushShortcutContext), KeyCode.Alpha6)] static void Tool6() => SelectTool(5);
        [Shortcut("World Kit/Mod 7", typeof(BrushShortcutContext), KeyCode.Alpha7)] static void Tool7() => SelectTool(6);
        [Shortcut("World Kit/Mod 8", typeof(BrushShortcutContext), KeyCode.Alpha8)] static void Tool8() => SelectTool(7);
        [Shortcut("World Kit/Mod 9", typeof(BrushShortcutContext), KeyCode.Alpha9)] static void Tool9() => SelectTool(8);
        [Shortcut("World Kit/Mod 10", typeof(BrushShortcutContext), KeyCode.Alpha0)] static void Tool10() => SelectTool(9);

        static void SelectTool(int index)
        {
            var tools = WorldKitTools.All;
            if (index < tools.Count)
                Tool = tools[index];
        }

        // Esc, ShortcutManager'da atanamadığı için OnSceneGUI içinde yakalanıyor.

        /// <summary>
        /// Gerçekte uygulanacak mod: Ctrl her zaman siler; seçili mod bu hedefte desteklenmiyorsa
        /// (ör. duvarda "Alan") ondan önceki ilk desteklenen moda düşülür.
        /// </summary>
        static WorldKitTool EffectiveTool
        {
            get
            {
                bool erase = s_Context.Dragging ? s_EraseDrag : EditorGUI.actionKey;
                // Yüzey boyasında Ctrl silme modlarına geçmez: aynı fırça boyayı siler (zemine dokunulmaz)
                bool surface = s_Mode == Mode.Floor && s_FloorOperation == FloorOperation.Surface;
                if (erase && !surface && WorldKitTools.Eraser != null)
                    return WorldKitTools.Eraser;

                var tool = Tool;
                if (tool == null || tool.Supports(s_Context))
                    return tool;

                return WorldKitTools.All.Where(t => t.Order <= tool.Order && t.Supports(s_Context) && !t.Erases)
                           .LastOrDefault()
                       ?? WorldKitTools.All.FirstOrDefault(t => t.Supports(s_Context));
            }
        }

        // ---------------- Önizleme (ghost) ----------------

        static void CreateGhost()
        {
            foreach (var variant in s_Variants)
            {
                var ghost = UnityEngine.Object.Instantiate(variant);
                ghost.name = "WorldKitGhost";
                ghost.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var t in ghost.GetComponentsInChildren<Transform>(true))
                    t.gameObject.hideFlags = HideFlags.HideAndDontSave;

                foreach (var pb in ghost.GetComponentsInChildren<ProBuilderMesh>(true))
                {
                    pb.ToMesh();
                    pb.Refresh();
                }

                if (variant == s_Prefab)
                {
                    var piece = s_Prefab.GetComponent<WorldKitPiece>();
                    s_Context.Footprint = piece != null ? piece.footprint : FootprintFromBounds(ghost);
                }

                // Ghost sahnede görünmez; mesh'leri her yerleşim noktası için elle çizilir.
                var parts = new List<GhostPart>();
                var root = ghost.transform.worldToLocalMatrix;
                foreach (var filter in ghost.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null || !filter.TryGetComponent<MeshRenderer>(out _))
                        continue;
                    parts.Add(new GhostPart { Mesh = filter.sharedMesh, Local = root * filter.transform.localToWorldMatrix });
                }

                ghost.SetActive(false);
                s_Ghosts.Add(ghost);
                s_VariantParts[variant] = parts;
                s_VariantBounds[variant] = LocalBounds(parts);
            }

            s_GhostParts.Clear();
            s_GhostParts.AddRange(s_VariantParts[s_Prefab]);

            var stamp = s_Prefab.GetComponent<WorldKitStamp>();
            s_StampCells = stamp != null && stamp.floorCells.Count > 0 ? stamp.floorCells : null;
        }

        // ---------------- Varyasyonlar ----------------

        static readonly List<GameObject> s_Variants = new List<GameObject>();
        static readonly List<GameObject> s_Ghosts = new List<GameObject>();
        static readonly Dictionary<GameObject, List<GhostPart>> s_VariantParts = new Dictionary<GameObject, List<GhostPart>>();
        // Varyasyonun yerel sınırları (önizleme dış çizgisi ve çakışma kontrolü için)
        static readonly Dictionary<GameObject, Bounds> s_VariantBounds = new Dictionary<GameObject, Bounds>();

        /// <summary>Fırçadaki parçalar (ilki ana parça). Birden fazlaysa her yerleşimde aralarından biri seçilir.</summary>
        public static IReadOnlyList<GameObject> Variants => s_Variants;

        /// <summary>Birden fazla parçayla fırça başlatır; her yerleşimde aralarından biri konur.</summary>
        public static void Begin(IReadOnlyList<GameObject> prefabs) => Begin(prefabs, false);

        /// <summary>
        /// Parçayı varyasyon olarak ekler/çıkarır (palette Ctrl+tık). Fırça kapalıysa yalnızca o parçayla başlar.
        /// </summary>
        public static void ToggleVariant(GameObject prefab)
        {
            if (prefab == null)
                return;
            if (s_Mode != Mode.Prefab)
            {
                Begin(prefab);
                return;
            }

            var list = s_Variants.ToList();
            if (!list.Remove(prefab))
                list.Add(prefab);

            if (list.Count == 0)
                End();
            else
                Begin(list);
        }

        /// <summary>Bu noktaya konacak varyasyon. Konumdan türetilir: önizleme ile yerleşim aynı olur.</summary>
        static GameObject PickVariant(Vector3 position)
        {
            if (s_Variants.Count <= 1)
                return s_Prefab;

            var key = Vector3Int.RoundToInt(position * 10f);
            int hash = unchecked(key.x * 73856093 ^ key.y * 19349663 ^ key.z * 83492791 ^ WorldKitFreePlacement.Salt * 486187739);
            return s_Variants[(hash & int.MaxValue) % s_Variants.Count];
        }

        static Bounds LocalBounds(List<GhostPart> parts)
        {
            bool any = false;
            var bounds = new Bounds();
            foreach (var part in parts)
            {
                var b = part.Mesh.bounds;
                // Parçanın 8 köşesini kök uzayına taşı
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = part.Local.MultiplyPoint3x4(corner);
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            return any ? bounds : new Bounds(Vector3.up * 0.5f, Vector3.one);
        }

        static Bounds BoundsFor(Vector3 position) =>
            s_VariantBounds.TryGetValue(PickVariant(position), out var b) ? b : new Bounds(Vector3.up * 0.5f, Vector3.one);

        static GameObject PlaceVariant(Vector3 position, Quaternion rotation, Vector3? scale = null)
        {
            var prefab = PickVariant(position);
            return PlacePiece(prefab, WorldKitScene.GetGroup(GetCategory(prefab)), position, rotation, scale, s_Variants);
        }

        static Vector2Int FootprintFromBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return Vector2Int.one;

            var bounds = renderers[0].bounds;
            foreach (var r in renderers.Skip(1))
                bounds.Encapsulate(r.bounds);

            return new Vector2Int(
                Mathf.Max(1, Mathf.RoundToInt(bounds.size.x / WorldKitGrid.CellSize)),
                Mathf.Max(1, Mathf.RoundToInt(bounds.size.z / WorldKitGrid.CellSize)));
        }

        static float LevelY => s_Context.Level * WorldKitGrid.LevelHeight;

        static void UpdateCursor(Vector2 mousePosition)
        {
            var ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            s_Context.SurfaceNormal = Vector3.up;

            // Serbest yerleştirme ve grid'siz modlar: imleç altındaki yüzeye (zemin, platform, obje) oturur.
            var tool = s_Mode == Mode.Prefab ? EffectiveTool : null;
            if (tool != null && !tool.Erases && WorldKitFreePlacement.SnapToSurface && (s_Context.IsFree || tool.FreeForm)
                && WorldKitFreePlacement.RaycastSurface(ray, out var point, out var normal))
            {
                s_HasHit = true;
                s_Context.Hit = point;
                s_Context.SurfaceNormal = normal;
            }
            else
            {
                // Otomatik kat (prefab fırçası, sürüklemiyorken): imlecin altındaki zeminin katına geç
                if (s_Mode == Mode.Prefab && !s_Context.Dragging && AutoLevel)
                    FollowFloorLevel(ray);

                var plane = new Plane(Vector3.up, new Vector3(0f, LevelY, 0f));
                s_HasHit = plane.Raycast(ray, out var distance) && distance < 5000f;
                if (!s_HasHit)
                    return;
                s_Context.Hit = ray.GetPoint(distance);
            }

            if (s_Context.Dragging)
                s_Context.Axis = WorldKitLayout.UpdateAxisLock(s_Context.Axis, s_Context.Anchor, s_Context.Hit);
        }

        /// <summary>Işın zemine nerede değiyorsa o hücrenin katını fırçanın katı yapar (değiştiyse palet yenilenir).</summary>
        static void FollowFloorLevel(Ray ray)
        {
            var root = WorldKitScene.FindRoot();
            var floor = root != null ? root.GetComponentInChildren<WorldKitFloor>() : null;
            if (floor == null || !floor.TryGetComponent<MeshCollider>(out var collider) || collider.sharedMesh == null)
                return;
            if (!collider.Raycast(ray, out var hit, 5000f))
                return;

            // Yan yüze değdiyse (uçurum duvarı) hücrenin içine doğru biraz kaydır
            var point = hit.point - new Vector3(hit.normal.x, 0f, hit.normal.z) * 0.05f;
            if (!floor.TryGetHeight(WorldKitGrid.ToCell(point), out var level) || level == s_Context.Level)
                return;
            var range = Settings.levelRange;
            s_Context.Level = Mathf.Clamp(level, range.x, range.y);
            Changed?.Invoke();
        }

        // ---------------- Shift+tık zinciri ----------------

        static bool IsChaining
        {
            get
            {
                var tool = EffectiveTool;
                return s_Shift && s_HasLastPoint && !s_Context.Dragging && tool != null && tool.ModifiesScene && !tool.Erases;
            }
        }

        static DragAxis ChainAxis =>
            Mathf.Abs(s_Context.Hit.x - s_LastPoint.x) >= Mathf.Abs(s_Context.Hit.z - s_LastPoint.z) ? DragAxis.X : DragAxis.Z;

        static Vector3 ChainEnd => WorldKitLayout.Constrain(s_LastPoint, s_Context.Hit, ChainAxis);

        static void ComputeChain()
        {
            s_Placements.Clear();
            s_Cells.Clear();

            if (s_Mode == Mode.Floor)
            {
                foreach (var cell in BrushContext.RectCells(WorldKitGrid.CellRect(s_LastPoint, ChainEnd), false))
                    s_Cells.Add(cell);
            }
            else if (s_Context.IsLinear)
            {
                WorldKitLayout.Line(s_Placements, s_LastPoint, s_Context.Hit, s_Context.Footprint, s_Context.Rotation, s_Context.Level, ChainAxis);
            }
            else
            {
                WorldKitLayout.Area(s_Placements, s_LastPoint, ChainEnd, s_Context.Footprint, s_Context.Rotation, s_Context.Level, false);
            }
        }

        static void ApplyChain()
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            var from = s_LastPoint;
            ComputeChain();
            if (s_Mode == Mode.Floor)
            {
                PaintFloor(s_Cells, false);
                RememberCell(WorldKitGrid.ToCell(ChainEnd));
            }
            else
            {
                PlaceAll();
                RememberPlacements(from);
            }

            WorldKitEvents.RaiseEditFinished();
            Undo.SetCurrentGroupName("World Kit: Arayı Doldur");
            Undo.CollapseUndoOperations(group);
        }

        static void RememberCell(Vector2Int cell)
        {
            s_HasLastPoint = true;
            s_LastPoint = WorldKitGrid.CellCenter(cell, s_Context.Level);
        }

        /// <summary>Çizgi parçalarında son parçanın başlangıca uzak ucu, diğerlerinde son parçanın merkezi.</summary>
        static void RememberPlacements(Vector3 from)
        {
            if (s_Placements.Count == 0)
                return;

            s_HasLastPoint = true;
            var last = s_Placements[s_Placements.Count - 1];
            s_LastPoint = s_Context.IsLinear ? WorldKitLayout.FarEnd(last, s_Context.Footprint, from) : last.Position;
        }

        // ---------------- Sahne girdisi ----------------

        static void OnSceneGUI(SceneView sceneView)
        {
            var e = Event.current;

            if (HandleExternalDrag(sceneView, e))
                return;

            if (s_Mode == Mode.None)
                return;

            if (s_Mode == Mode.Prefab && s_Prefab == null)
            {
                End();
                return;
            }

            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            s_Shift = e.shift;

            switch (e.type)
            {
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(controlId);
                    break;

                case EventType.MouseMove:
                    UpdateCursor(e.mousePosition);
                    sceneView.Repaint();
                    break;

                // Shift + tekerlek: döndür (grid'de 90°, serbestte ayarlardaki adım). Kamera zoom'u tekerleğin kendisinde kalır.
                case EventType.ScrollWheel when e.shift && s_Mode == Mode.Prefab:
                {
                    float delta = Mathf.Abs(e.delta.y) >= Mathf.Abs(e.delta.x) ? e.delta.y : e.delta.x;
                    if (Mathf.Abs(delta) > 0.01f)
                        Rotate(delta > 0f ? 1 : -1);
                    e.Use();
                    break;
                }

                case EventType.MouseDown when e.button == 0 && !e.alt:
                    UpdateCursor(e.mousePosition);
                    if (!s_HasHit)
                        break;
                    if (IsChaining)
                    {
                        ApplyChain();
                        e.Use();
                        break;
                    }
                    GUIUtility.hotControl = controlId;
                    BeginStroke();
                    e.Use();
                    break;

                case EventType.MouseDrag when s_Context.Dragging && GUIUtility.hotControl == controlId:
                    UpdateCursor(e.mousePosition);
                    ContinueStroke();
                    sceneView.Repaint();
                    e.Use();
                    break;

                case EventType.MouseUp when s_Context.Dragging && GUIUtility.hotControl == controlId:
                    GUIUtility.hotControl = 0;
                    UpdateCursor(e.mousePosition);
                    FinishStroke(e.mousePosition);
                    e.Use();
                    break;

                case EventType.KeyDown when e.keyCode == KeyCode.Escape:
                    if (s_Context.Dragging)
                    {
                        // Yalnızca bu çizimi iptal et; boyama modunda o ana kadar konanlar da geri alınır.
                        s_Context.Dragging = false;
                        GUIUtility.hotControl = 0;
                        Undo.RevertAllDownToGroup(s_UndoGroup);
                        sceneView.Repaint();
                    }
                    else
                    {
                        End();
                    }
                    e.Use();
                    return;

                // Shift/Ctrl basılınca önizleme hemen güncellensin.
                case EventType.KeyDown when e.keyCode is KeyCode.LeftShift or KeyCode.RightShift or KeyCode.LeftControl or KeyCode.RightControl:
                case EventType.KeyUp when e.keyCode is KeyCode.LeftShift or KeyCode.RightShift or KeyCode.LeftControl or KeyCode.RightControl:
                    sceneView.Repaint();
                    break;

                case EventType.Repaint:
                    DrawPreview(sceneView);
                    DrawHud(sceneView);
                    break;
            }
        }

        /// <summary>Paletten Scene'e sürükle-bırak: tek parça bırakır.</summary>
        static bool HandleExternalDrag(SceneView sceneView, Event e)
        {
            if (e.type == EventType.DragExited && s_Temporary)
            {
                End();
                return true;
            }

            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform)
                return false;

            if (!(DragAndDrop.GetGenericData(DragDataKey) is GameObject dragged))
                return false;

            if (s_Prefab != dragged)
                Begin(dragged, true);

            UpdateCursor(e.mousePosition);
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                if (s_HasHit)
                {
                    var single = s_Context.SinglePlacement();
                    Undo.IncrementCurrentGroup();
                    int group = Undo.GetCurrentGroup();
                    var go = PlaceVariant(single.Position, single.Rotation);
                    WorldKitEvents.RaiseEditFinished();
                    Undo.CollapseUndoOperations(group);
                    if (go != null)
                        Selection.activeGameObject = go;
                }

                if (s_Temporary)
                    End();
            }

            e.Use();
            sceneView.Repaint();
            return true;
        }

        static void BeginStroke()
        {
            s_Context.Dragging = true;
            s_Context.Anchor = s_Context.Hit;
            s_Context.Axis = DragAxis.None;
            s_EraseDrag = EditorGUI.actionKey || (Tool != null && Tool.Erases);

            Undo.IncrementCurrentGroup();
            s_UndoGroup = Undo.GetCurrentGroup();

            var tool = EffectiveTool;
            if (tool != null && tool.ModifiesScene && tool.PaintsWhileDragging && !tool.Erases)
            {
                s_LastStrokeCell = s_Context.HoverCell;
                s_LastStrokePoint = s_Context.Hit;
                ApplyPaintStep(tool);
            }
        }

        static void ContinueStroke()
        {
            var tool = EffectiveTool;
            if (tool == null || !tool.ModifiesScene || !tool.PaintsWhileDragging || tool.Erases || !s_HasHit)
                return;

            // Serbest yerleştirmede hücre yerine mesafe: son parçadan "Aralık" kadar uzaklaşınca yenisi.
            if (s_Mode == Mode.Prefab && s_Context.IsFree)
            {
                var flat = s_Context.Hit - s_LastStrokePoint;
                flat.y = 0f;
                if (flat.magnitude < WorldKitFreePlacement.Spacing)
                    return;
                s_LastStrokePoint = s_Context.Hit;
                ApplyPaintStep(tool);
                return;
            }

            var cell = s_Context.HoverCell;
            if (cell == s_LastStrokeCell)
                return;

            s_LastStrokeCell = cell;
            ApplyPaintStep(tool);
        }

        /// <summary>Boyama modlarında sürüklerken her yeni hücrede çağrılır (sürükleme yokmuş gibi).</summary>
        static void ApplyPaintStep(WorldKitTool tool)
        {
            s_Context.Dragging = false;
            try
            {
                if (s_Mode == Mode.Floor)
                {
                    s_Cells.Clear();
                    tool.CollectCells(s_Context, s_Cells);
                    PaintFloor(s_Cells, false);
                    RememberCell(s_Context.HoverCell);
                }
                else
                {
                    var from = WorldKitGrid.CornerToWorld(WorldKitGrid.ToCorner(s_Context.Hit), s_Context.Level);
                    s_Placements.Clear();
                    tool.CollectPlacements(s_Context, s_Placements);
                    PlaceAll();
                    RememberPlacements(from);
                }
            }
            finally
            {
                s_Context.Dragging = true;
            }
        }

        static void FinishStroke(Vector2 mousePosition)
        {
            var tool = EffectiveTool;
            bool dragged = s_Context.HoverCell != WorldKitGrid.ToCell(s_Context.Anchor);

            if (s_HasHit && tool != null && tool.ModifiesScene && !(tool.PaintsWhileDragging && !tool.Erases))
            {
                if (s_Mode == Mode.Floor)
                {
                    s_Cells.Clear();
                    tool.CollectCells(s_Context, s_Cells);
                    PaintFloor(s_Cells, tool.Erases);
                    if (!tool.Erases)
                        RememberCell(WorldKitGrid.ToCell(s_Context.ConstrainedHit));
                }
                else if (tool.Erases)
                {
                    if (dragged)
                        DeleteInRect(s_Context.DragRect(false));
                    else
                        DeleteAt(mousePosition);
                }
                else
                {
                    var anchorCorner = WorldKitGrid.CornerToWorld(WorldKitGrid.ToCorner(s_Context.Anchor), s_Context.Level);
                    s_Placements.Clear();
                    tool.CollectPlacements(s_Context, s_Placements);
                    PlaceAll();
                    RememberPlacements(anchorCorner);

                    // Sürükleyerek yön verildiyse sonraki parçalar da o yöne baksın
                    if (tool is SingleTool && SingleTool.RotatesOnDrag && s_Placements.Count == 1 && !s_Context.IsLinear)
                        RememberFacing(s_Placements[0].Rotation);
                }
            }
            else if (s_HasHit && tool != null && !tool.ModifiesScene)
                tool.OnStrokeFinished(s_Context);

            s_Context.Dragging = false;
            WorldKitEvents.RaiseEditFinished();
            Undo.CollapseUndoOperations(s_UndoGroup);
        }

        // ---------------- Çizim ----------------

        static readonly Color k_Accent = new Color(0.2f, 0.8f, 1f, 1f);
        static readonly Color k_Danger = new Color(1f, 0.3f, 0.3f, 1f);

        static void DrawPreview(SceneView sceneView)
        {
            if (!s_HasHit)
                return;

            DrawLocalGrid();
            var tool = EffectiveTool;
            if (tool == null)
                return;

            bool chaining = IsChaining;
            if (chaining)
            {
                DrawChainGuide();
                ComputeChain();
            }

            var color = tool.Erases ? k_Danger : k_Accent;
            if (!chaining)
                tool.OnPreviewGUI(s_Context);
            if (!tool.ModifiesScene)
                return;

            if (s_Mode == Mode.Floor)
            {
                if (!chaining)
                {
                    s_Cells.Clear();
                    tool.CollectCells(s_Context, s_Cells);
                }
                // Yuvarlak fırçayla yüzey boyasında hücre kareleri yanıltır; fırçanın kendi dairesi yeterli
                if (!(s_FloorOperation == FloorOperation.Surface && tool is RoundBrushTool))
                    DrawCells(s_Cells, s_FloorOperation == FloorOperation.Surface && EditorGUI.actionKey ? k_Danger : color);
                return;
            }

            if (tool.Erases)
            {
                if (s_Context.Dragging && s_Context.HoverCell != WorldKitGrid.ToCell(s_Context.Anchor))
                    DrawCellRect(s_Context.DragRect(false), k_Danger, true);
                else
                    using (new Handles.DrawingScope(k_Danger))
                        Handles.DrawWireDisc(new Vector3(s_Context.Hit.x, LevelY + 0.01f, s_Context.Hit.z), Vector3.up, 0.4f, 3f);
                return;
            }

            if (!chaining)
            {
                s_Placements.Clear();
                tool.CollectPlacements(s_Context, s_Placements);
            }

            // Önizleme: ışıklı yarı saydam hayalet + dış çizgi + zemin izi + yön oku.
            // Az sayıda yerleşimde başka bir objeyle çakışan hayalet kırmızı olur (yine de konabilir).
            var camera = sceneView.camera;
            bool detailed = s_Placements.Count <= 30;
            bool checkOverlap = detailed && !s_Context.IsLinear;
            if (checkOverlap)
                Physics.SyncTransforms();
            foreach (var placement in s_Placements)
            {
                var bounds = BoundsFor(placement.Position);
                bool blocked = checkOverlap && Overlaps(placement, bounds);
                var material = blocked ? BlockedMaterial : GhostMaterial;
                var matrix = Matrix4x4.TRS(placement.Position, placement.Rotation, placement.ScaleVector);
                foreach (var part in PartsFor(placement.Position))
                {
                    var world = matrix * part.Local;
                    for (int i = 0; i < part.Mesh.subMeshCount; i++)
                        Graphics.DrawMesh(part.Mesh, world, material, 0, camera, i);
                }

                if (detailed)
                {
                    using (new Handles.DrawingScope(blocked ? k_Danger : k_Accent, matrix))
                        Handles.DrawWireCube(bounds.center, bounds.size);
                }
                DrawFootprint(placement, blocked ? k_Danger : k_Accent);
                DrawStampCells(placement);
            }

            // Serbestte komşuya hizalanınca hangi parçaya hizalandığını göster
            var neighbor = WorldKitFreePlacement.AlignedNeighbor;
            if (s_Context.IsFree && neighbor != null && s_Placements.Count == 1)
            {
                using (new Handles.DrawingScope(new Color(1f, 0.85f, 0.3f, 0.9f)))
                {
                    Handles.DrawDottedLine(s_Placements[0].Position + Vector3.up * 0.1f, neighbor.position + Vector3.up * 0.1f, 3f);
                    Handles.Label(s_Placements[0].Position + Vector3.up * 0.4f, "hizalı", EditorStyles.whiteMiniLabel);
                }
            }

            // Çizgi parçalarında başlangıç köşesini işaretle.
            if (s_Context.IsLinear && !chaining)
            {
                var corner = WorldKitGrid.CornerToWorld(WorldKitGrid.ToCorner(s_Context.Dragging ? s_Context.Anchor : s_Context.Hit), s_Context.Level);
                using (new Handles.DrawingScope(k_Accent))
                    Handles.DrawSolidDisc(corner + Vector3.up * 0.03f, Vector3.up, 0.12f);
            }
        }

        static void DrawChainGuide()
        {
            var from = new Vector3(s_LastPoint.x, LevelY + 0.04f, s_LastPoint.z);
            var to = ChainEnd;
            to.y = from.y;
            using (new Handles.DrawingScope(k_Accent))
            {
                Handles.DrawSolidDisc(from, Vector3.up, 0.15f);
                Handles.DrawDottedLine(from, to, 4f);
            }
        }

        static Material s_BlockedMaterial;

        static Material GhostMaterial => s_GhostMaterial != null ? s_GhostMaterial : s_GhostMaterial = CreateGhostMaterial(new Color(0.25f, 0.65f, 1f, 0.55f));

        /// <summary>Başka bir objeyle çakışan yerleşimin hayaleti.</summary>
        static Material BlockedMaterial => s_BlockedMaterial != null ? s_BlockedMaterial : s_BlockedMaterial = CreateGhostMaterial(new Color(1f, 0.3f, 0.25f, 0.55f));

        static Material CreateGhostMaterial(Color color)
        {
            // Işıklı hayalet shader'ı (Editor/Shaders); bulunamazsa Unity'nin düz renkli shader'ı.
            var shader = Shader.Find("Hidden/WorldKit/Ghost");
            if (shader != null)
            {
                var lit = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                lit.SetColor("_Color", color);
                lit.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                return lit;
            }

            var material = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
            material.SetColor("_Color", new Color(color.r, color.g, color.b, 0.45f));
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Back);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return material;
        }

        /// <summary>
        /// Yerleşim başka bir objeyle çakışıyor mu? Zemin, tetikleyiciler ve parçanın oturduğu yüzey (alt kısım biraz
        /// yukarıdan başlar) sayılmaz.
        /// </summary>
        static bool Overlaps(Placement placement, Bounds local)
        {
            var scale = placement.ScaleVector;
            var size = Vector3.Scale(local.size, scale) * 0.85f;
            if (size.x < 0.05f || size.z < 0.05f)
                return false;
            var center = placement.Position + placement.Rotation * Vector3.Scale(local.center, scale) + Vector3.up * 0.05f;
            foreach (var collider in Physics.OverlapBox(center, size * 0.5f, placement.Rotation, ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                if (collider.GetComponentInParent<WorldKitFloor>() == null)
                    return true;
            }
            return false;
        }

        static void DrawLocalGrid()
        {
            const int radius = 6;
            float c = WorldKitGrid.CellSize;
            var y = LevelY + 0.01f;
            var cx = Mathf.Round(s_Context.Hit.x / c) * c;
            var cz = Mathf.Round(s_Context.Hit.z / c) * c;
            using (new Handles.DrawingScope(new Color(1f, 1f, 1f, 0.15f)))
            {
                for (int i = -radius; i <= radius; i++)
                {
                    Handles.DrawLine(new Vector3(cx + i * c, y, cz - radius * c), new Vector3(cx + i * c, y, cz + radius * c));
                    Handles.DrawLine(new Vector3(cx - radius * c, y, cz + i * c), new Vector3(cx + radius * c, y, cz + i * c));
                }
            }
        }

        /// <summary>Hücreler: azsa tek tek, çoksa sınır dikdörtgeni olarak çizilir.</summary>
        static void DrawCells(HashSet<Vector2Int> cells, Color color)
        {
            if (cells.Count == 0)
                return;

            if (cells.Count <= 400)
            {
                foreach (var cell in cells)
                    DrawCellRect(new RectInt(cell, Vector2Int.one), color, true);
                return;
            }

            var min = new Vector2Int(int.MaxValue, int.MaxValue);
            var max = new Vector2Int(int.MinValue, int.MinValue);
            foreach (var cell in cells)
            {
                min = Vector2Int.Min(min, cell);
                max = Vector2Int.Max(max, cell);
            }
            DrawCellRect(new RectInt(min, max - min + Vector2Int.one), color, true);
        }

        static void DrawCellRect(RectInt rect, Color color, bool filled) => DrawCellRect(rect, color, filled, LevelY);

        /// <summary>Şablonun zemin hücrelerini yerleşimin dönüşüyle birlikte önizler.</summary>
        static void DrawStampCells(Placement placement)
        {
            if (s_StampCells == null || s_Placements.Count > 20)
                return;

            var matrix = Matrix4x4.TRS(placement.Position, placement.Rotation, Vector3.one);
            foreach (var cell in s_StampCells)
            {
                var world = matrix.MultiplyPoint3x4(cell.localCenter);
                float y = placement.Position.y + cell.height * WorldKitGrid.LevelHeight;
                DrawCellRect(new RectInt(WorldKitGrid.ToCell(world), Vector2Int.one), new Color(0.2f, 0.8f, 1f, 0.6f), true, y);
            }
        }

        static void DrawCellRect(RectInt rect, Color color, bool filled, float baseY)
        {
            var y = baseY + 0.02f;
            float c = WorldKitGrid.CellSize;
            var corners = new[]
            {
                new Vector3(rect.xMin * c, y, rect.yMin * c),
                new Vector3(rect.xMin * c, y, rect.yMax * c),
                new Vector3(rect.xMax * c, y, rect.yMax * c),
                new Vector3(rect.xMax * c, y, rect.yMin * c),
            };
            Handles.DrawSolidRectangleWithOutline(corners, filled ? new Color(color.r, color.g, color.b, 0.2f) : Color.clear, color);
        }

        static void DrawFootprint(Placement placement, Color color)
        {
            var footprint = s_Context.Footprint;
            var f = new Vector3(Mathf.Max(footprint.x, 0.1f), 0f, Mathf.Max(footprint.y, 0.1f)) * (WorldKitGrid.CellSize * 0.5f);
            if (s_Context.IsFree)
                f *= placement.Scale > 0f ? placement.Scale : 1f;
            var p = placement.Position + Vector3.up * 0.02f;
            // Zemin izi yatay kalsın (eğime yatırılmış parçalarda bile)
            var r = Quaternion.Euler(0f, placement.Rotation.eulerAngles.y, 0f);
            var corners = new[]
            {
                p + r * new Vector3(-f.x, 0f, -f.z),
                p + r * new Vector3(-f.x, 0f, f.z),
                p + r * new Vector3(f.x, 0f, f.z),
                p + r * new Vector3(f.x, 0f, -f.z),
            };
            Handles.DrawSolidRectangleWithOutline(corners, new Color(color.r, color.g, color.b, 0.12f), color);

            // Ön yön oku: kapı, NPC gibi yönü önemli parçalar için hangi tarafa baktığını gösterir.
            if (s_Placements.Count <= 40)
            {
                float length = Mathf.Max(f.z, 0.1f) + 0.45f;
                var tip = p + r * Vector3.forward * length;
                var back = p + r * Vector3.forward * (length - 0.28f);
                var side = r * Vector3.right * 0.16f;
                using (new Handles.DrawingScope(color))
                {
                    Handles.DrawLine(p, back, 3f);
                    Handles.DrawAAConvexPolygon(tip, back + side, back - side);
                }
            }
        }

        static GUIStyle s_HudStyle;

        static void DrawHud(SceneView sceneView)
        {
            var tool = EffectiveTool;
            if (tool == null)
                return;

            s_HudStyle ??= new GUIStyle(EditorStyles.helpBox) { fontSize = 11, richText = true, padding = new RectOffset(8, 8, 6, 6) };

            var name = s_Mode == Mode.Floor
                ? (s_FloorOperation == FloorOperation.Paint || s_FloorOperation == FloorOperation.Recolor
                    ? $"Zemin: {FloorTypeName(s_FloorType)}  [{FloorOperationName(s_FloorOperation)}]"
                    : $"Zemin  [{FloorOperationName(s_FloorOperation)}]")
                : s_Prefab.name.Replace('_', ' ');
            string status = $"<b>{name}</b>   Mod: <b>{tool.DisplayName(s_Context)}</b>   Seviye: {s_Context.Level}";
            if (s_Mode == Mode.Prefab)
                status += s_Context.IsFree ? $"   Açı: {s_Context.FreeYaw:0}°   <b>Serbest</b> (G)" : $"   Açı: {s_Context.Rotation * 90}°";
            if (s_Mode == Mode.Prefab && !s_Context.IsFree && AutoLevel)
                status += "   <color=#9ad>kat oto</color>";
            if (s_Mode == Mode.Prefab && s_Context.HalfSnap && !s_Context.IsFree)
                status += "   <b>½ snap</b> (H)";

            bool chaining = IsChaining;
            if (chaining || (s_Context.Dragging && !tool.PaintsWhileDragging))
            {
                var rect = chaining ? WorldKitGrid.CellRect(s_LastPoint, ChainEnd) : s_Context.DragRect(false);
                float c = WorldKitGrid.CellSize;
                status += $"   {rect.width * c:0.#} × {rect.height * c:0.#} m";
                if (s_Mode == Mode.Prefab && !tool.Erases)
                    status += $"   <b>{s_Placements.Count} parça</b>";
            }

            string hint = chaining ? "<b>Shift+tık: son noktadan buraya kadar doldur</b>" : tool.Hint(s_Context);
            if (!chaining && !tool.Erases && s_HasLastPoint)
                hint += "   Shift+tık: arayı doldur";

            var text = $"{status}\n{hint}   1-9: mod   R: döndür   Q/E: seviye   Ctrl: sil   Esc: {(s_Context.Dragging ? "iptal" : "bırak")}";

            Handles.BeginGUI();
            var height = sceneView.camera.pixelRect.height / EditorGUIUtility.pixelsPerPoint;
            var content = new GUIContent(text);
            var size = s_HudStyle.CalcSize(content);
            GUI.Label(new Rect(10f, height - size.y - 10f, size.x, size.y), content, s_HudStyle);
            Handles.EndGUI();
        }

        // ---------------- Sahne düzenleme ----------------

        static void PlaceAll()
        {
            if (s_Placements.Count == 0)
                return;

            foreach (var placement in s_Placements)
                PlaceVariant(placement.Position, placement.Rotation, placement.ScaleVector);
        }

        static List<GhostPart> PartsFor(Vector3 position) =>
            s_VariantParts.TryGetValue(PickVariant(position), out var parts) ? parts : s_GhostParts;

        /// <summary>
        /// Bir prefab'ı parça olarak yerleştirir: aynı yerde aynısı varsa koymaz, Undo'ya kaydeder ve
        /// PiecePlaced olayını tetikler. Modüller kendi yerleştirmeleri için bunu kullanmalı
        /// (sonunda WorldKitEvents.RaiseEditFinished çağırarak). parent null ise prefab'ın kategorisi kullanılır.
        /// equivalents: aynı yerde bunlardan biri varsa da konmaz (varyasyonların üst üste binmemesi için).
        /// </summary>
        public static GameObject PlacePiece(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation, Vector3? scale = null,
            IReadOnlyCollection<GameObject> equivalents = null)
        {
            if (prefab == null)
                return null;
            if (parent == null)
                parent = WorldKitScene.GetGroup(GetCategory(prefab));

            // Aynı yere aynı parçayı ikinci kez koyma. Simetrik çizgi parçalarında (duvar, çit)
            // 180° çevrilmiş kopya da aynı sayılır, üst üste binmez.
            var info = prefab.GetComponent<WorldKitPiece>();
            bool symmetric = info != null && WorldKitLayout.IsLinear(info.footprint);
            foreach (Transform child in parent)
            {
                if ((child.position - position).sqrMagnitude > 0.0001f)
                    continue;
                var source = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                if (source != prefab && (equivalents == null || !equivalents.Contains(source)))
                    continue;

                float angle = Quaternion.Angle(child.rotation, rotation);
                if (angle < 1f || (symmetric && angle > 179f))
                    return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(position, rotation);
            // Ölçek, prefab'ın kendi ölçeğiyle çarpılır (kendi ölçeği 1 olmayan prefab'lar bozulmasın).
            if (scale.HasValue && scale.Value != Vector3.one)
                go.transform.localScale = Vector3.Scale(go.transform.localScale, scale.Value);
            Undo.RegisterCreatedObjectUndo(go, "World Kit: Yerleştir");
            WorldKitEvents.RaisePiecePlaced(go);
            return go;
        }

        static void PaintFloor(ICollection<Vector2Int> cells, bool erase)
        {
            // Yuvarlak fırçayla yüzey boyası hücre listesine bakmaz (aynı hücreye tekrar boyanabilir)
            bool surfaceDab = s_FloorOperation == FloorOperation.Surface && EffectiveTool is RoundBrushTool;
            if (cells.Count == 0 && !surfaceDab)
                return;

            var floor = GetFloor();
            if (s_FloorOperation == FloorOperation.Surface)
            {
                // Ctrl (sürüklemenin başında basılıysa) boyayı siler; zemin hücrelerine hiç dokunulmaz.
                bool eraseSurface = erase || (s_Context.Dragging ? s_EraseDrag : EditorGUI.actionKey);
                if (EffectiveTool is RoundBrushTool)
                    WorldKitSurfaceBrush.Dab(floor, s_Context.Hit, RoundBrushTool.Radius * WorldKitGrid.CellSize, eraseSurface);
                else
                    WorldKitSurfaceBrush.FillCells(floor, cells, eraseSurface);
                return;
            }
            if (!erase && s_FloorOperation == FloorOperation.Wall)
            {
                FloorWalls.Build(floor, cells, FloorWallPrefab);
                return;
            }
            if (!erase && s_FloorOperation == FloorOperation.EdgeDecor)
            {
                FloorEdgeDecor.Build(floor, cells);
                return;
            }

            var range = Settings.levelRange;
            Undo.RecordObject(floor, erase ? "World Kit: Zemin Sil" : "World Kit: Zemin Boya");
            if (erase)
                floor.RemoveCells(cells);
            else switch (s_FloorOperation)
            {
                case FloorOperation.Recolor: floor.PaintType(cells, s_FloorType); break;
                case FloorOperation.Raise: floor.AdjustHeight(cells, 1, range.x, range.y); break;
                case FloorOperation.Lower: floor.AdjustHeight(cells, -1, range.x, range.y); break;
                case FloorOperation.Ramp:
                    if (FloorGentleRamp)
                        floor.AutoSlopeGentle(cells, s_Context.Axis != DragAxis.Z);
                    else
                        floor.AutoSlope(cells, s_Context.Axis != DragAxis.Z);
                    break;
                default: floor.SetCells(cells, s_Context.Level, s_FloorType); break;
            }
            SyncFloorMaterials(floor);
            EditorUtility.SetDirty(floor);
            WorldKitEvents.RaiseFloorChanged(floor);
        }

        /// <summary>Zemin fırçasının yaptığı iş. Hangi hücrelere uygulanacağını seçili mod belirler.</summary>
        public enum FloorOperation
        {
            /// <summary>Seçili seviyede, seçili türle doldur.</summary>
            Paint,
            /// <summary>Yalnızca var olan hücrelerin türünü değiştir.</summary>
            Recolor,
            /// <summary>Var olan hücreleri bir kat yükselt.</summary>
            Raise,
            /// <summary>Var olan hücreleri bir kat alçalt.</summary>
            Lower,
            /// <summary>Hücreleri yanlarındaki bir kat yüksek komşuya doğru rampa yap (yön kendiliğinden).</summary>
            Ramp,
            /// <summary>Hücre alanının kenarlarına duvar ör (zemini değiştirmez).</summary>
            Wall,
            /// <summary>Hücre alanının dış kenarlarına kaya, çalı gibi süs parçaları diz (zemini değiştirmez).</summary>
            EdgeDecor,
            /// <summary>Yüzey boyası: hücreden bağımsız, yumuşak kenarlı patika ve lekeler (zemini değiştirmez; Ctrl siler).</summary>
            Surface
        }

        static int s_FloorType;
        static FloorOperation s_FloorOperation;
        static GameObject s_FloorWallPrefab;

        public static FloorOperation FloorOp
        {
            get => s_FloorOperation;
            set
            {
                s_FloorOperation = value;
                SceneView.RepaintAll();
                Changed?.Invoke();
            }
        }

        /// <summary>Rampa işlemi yumuşak mı (bir kat iki hücreye yayılır) yoksa dik mi (tek hücre).</summary>
        public static bool FloorGentleRamp
        {
            get => EditorPrefs.GetBool("WorldKit.Floor.GentleRamp", false);
            set
            {
                EditorPrefs.SetBool("WorldKit.Floor.GentleRamp", value);
                Changed?.Invoke();
            }
        }

        /// <summary>"Duvarla Çevir" işleminin kullandığı duvar (boşsa başlangıç içeriğindeki Duvar_1m).</summary>
        public static GameObject FloorWallPrefab
        {
            get => s_FloorWallPrefab != null
                ? s_FloorWallPrefab
                : AssetDatabase.LoadAssetAtPath<GameObject>(WorldKitStarterContent.DefaultWallPath);
            set
            {
                s_FloorWallPrefab = value;
                Changed?.Invoke();
            }
        }

        public static string FloorOperationName(FloorOperation op)
        {
            switch (op)
            {
                case FloorOperation.Recolor: return "Sadece Renk";
                case FloorOperation.Raise: return "Yükselt";
                case FloorOperation.Lower: return "Alçalt";
                case FloorOperation.Ramp: return "Rampa";
                case FloorOperation.Wall: return "Duvarla Çevir";
                case FloorOperation.EdgeDecor: return "Kenar Süsle";
                case FloorOperation.Surface: return "Yüzey Boya";
                default: return "Boya";
            }
        }

        /// <summary>Zemin fırçasının boyadığı tür (0 = varsayılan Zemin, n = ayarlardaki n. ek tür).</summary>
        public static int FloorType
        {
            get => s_FloorType;
            set
            {
                s_FloorType = Mathf.Clamp(value, 0, Settings.floorTypes.Count);
                SceneView.RepaintAll();
                Changed?.Invoke();
            }
        }

        /// <summary>Eski API: FloorOp == Recolor ile aynı.</summary>
        public static bool FloorKeepHeight
        {
            get => s_FloorOperation == FloorOperation.Recolor;
            set => FloorOp = value ? FloorOperation.Recolor : FloorOperation.Paint;
        }

        /// <summary>Tür adı (0 = "Zemin").</summary>
        public static string FloorTypeName(int type)
        {
            var types = Settings.floorTypes;
            return type <= 0 || type > types.Count ? "Zemin" : types[type - 1].name;
        }

        /// <summary>Zemin renderer'ının materyallerini ayarlardaki türlerle eşitler (alt-mesh sırasıyla).</summary>
        public static void SyncFloorMaterials(WorldKitFloor floor)
        {
            var settings = Settings;

            // Tür başına yüzey kayması (su vb.); 0 = varsayılan tür
            var offsets = new List<float> { 0f };
            offsets.AddRange(settings.floorTypes.Select(t => t.surfaceOffset));
            if (!floor.TypeOffsets.SequenceEqual(offsets))
            {
                Undo.RecordObject(floor, "World Kit: Zemin Türleri");
                floor.SetTypeOffsets(offsets);
                EditorUtility.SetDirty(floor);
            }

            // Kenar stili (köşeli / yumuşak)
            if (floor.smoothEdges != settings.floorSmoothEdges || !Mathf.Approximately(floor.edgeRadius, settings.floorEdgeRadius)
                || floor.edgeResolution != settings.floorEdgeResolution || !Mathf.Approximately(floor.edgeLip, settings.floorEdgeLip))
            {
                Undo.RecordObject(floor, "World Kit: Zemin Kenarı");
                floor.SetEdgeStyle(settings.floorSmoothEdges, settings.floorEdgeRadius, settings.floorEdgeResolution, settings.floorEdgeLip);
                EditorUtility.SetDirty(floor);
            }

            // Alt-mesh sırası: 0 zemin, 1 platform, 1+t türler, sonra (yumuşak kenarda) uçurum duvarı
            var materials = new List<Material> { settings.floorGroundMaterial, settings.floorRaisedMaterial };
            materials.AddRange(settings.floorTypes.Select(t => t.material != null ? t.material : settings.floorGroundMaterial));
            materials.Add(settings.floorCliffMaterial != null ? settings.floorCliffMaterial : settings.floorRaisedMaterial);
            while (materials.Count < floor.SubMeshCount)
                materials.Add(settings.floorGroundMaterial);
            // Alt-mesh'ten fazla materyal olursa Unity fazlasını son alt-mesh'e tekrar çizer: kırp
            if (materials.Count > floor.SubMeshCount)
                materials.RemoveRange(floor.SubMeshCount, materials.Count - floor.SubMeshCount);

            var renderer = floor.GetComponent<MeshRenderer>();
            if (!renderer.sharedMaterials.SequenceEqual(materials))
            {
                Undo.RecordObject(renderer, "World Kit: Zemin Materyalleri");
                renderer.sharedMaterials = materials.ToArray();
            }
        }

        static void Remove(GameObject go)
        {
            WorldKitEvents.RaisePieceRemoving(go);
            Undo.DestroyObjectImmediate(go);
        }

        static void DeleteAt(Vector2 mousePosition)
        {
            var piece = WorldKitScene.GetPiece(HandleUtility.PickGameObject(mousePosition, false));
            if (piece != null)
                Remove(piece);
        }

        /// <summary>Alandaki ve mevcut seviyedeki tüm parçaları siler (zemin hariç).</summary>
        static void DeleteInRect(RectInt cells)
        {
            float c = WorldKitGrid.CellSize;
            const float epsilon = 0.01f;
            var doomed = WorldKitScene.AllPieces().Where(piece =>
            {
                var p = piece.transform.position;
                return p.x >= cells.xMin * c - epsilon && p.x <= cells.xMax * c + epsilon
                    && p.z >= cells.yMin * c - epsilon && p.z <= cells.yMax * c + epsilon
                    && Mathf.Abs(p.y - LevelY) < WorldKitGrid.LevelHeight * 0.5f;
            }).ToList();

            foreach (var go in doomed)
                Remove(go);
        }

        /// <summary>Prefab'ın kategorisi: parçalar klasöründeki alt klasörünün adı.</summary>
        public static string GetCategory(GameObject prefab)
        {
            var path = AssetDatabase.GetAssetPath(prefab);
            var folder = Path.GetFileName(Path.GetDirectoryName(path));
            return string.IsNullOrEmpty(folder) ? "Diger" : folder;
        }

        /// <summary>Aktif sahnenin kök objesi. (WorldKitScene.FindRoot ile aynı.)</summary>
        public static GameObject FindRoot() => WorldKitScene.FindRoot();

        /// <summary>Aktif sahnenin zemin objesini bulur, yoksa proje ayarlarıyla oluşturur.</summary>
        public static WorldKitFloor GetFloor()
        {
            var root = WorldKitScene.GetRoot();
            var floor = root.GetComponentInChildren<WorldKitFloor>();
            if (floor != null)
                return floor;

            var settings = Settings;
            var go = new GameObject(settings.floorName, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            go.transform.SetParent(root, false);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);

            floor = go.AddComponent<WorldKitFloor>();
            floor.cellSize = settings.cellSize;
            floor.levelHeight = settings.levelHeight;
            floor.thickness = settings.floorThickness;
            floor.Rebuild();
            go.GetComponent<MeshRenderer>().sharedMaterials = new[] { settings.floorGroundMaterial, settings.floorRaisedMaterial };
            SyncFloorMaterials(floor);

            Undo.RegisterCreatedObjectUndo(go, "World Kit: Zemin Oluştur");
            return floor;
        }

        static void RebuildFloors()
        {
            foreach (var floor in UnityEngine.Object.FindObjectsByType<WorldKitFloor>(FindObjectsSortMode.None))
                floor.Rebuild();
        }
    }
}
