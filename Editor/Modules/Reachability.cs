using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// Erişilebilirlik analizi: bir başlangıç noktasından yürüyerek ulaşılabilen zemin hücrelerini bulur.
    ///
    /// Kurallar:
    /// - İki komşu hücre arasında yürünür, ortak kenarları aynı yükseklikteyse (düz zemin veya rampa ile bağlı).
    ///   "Aşağı atlama" açıksa daha alçak komşuya inilebilir (tek yönlü).
    /// - Collider'lar engeldir: hücrede karakter boyunda bir kapsül sığmıyorsa hücre doludur; iki hücre arasındaki
    ///   duvar, çit vb. geçişi keser. Kapılardan geçilir. Zemin, tetikleyiciler ve başlangıç objesi sayılmaz.
    /// - Karakter boyu ve yürüme hızı ayarlardan (characterHeight, walkSpeed) gelir.
    /// Sahneyi değiştirmez.
    /// </summary>
    public static class WorldKitReachability
    {
        public sealed class Result
        {
            /// <summary>Ulaşılan hücreler ve başlangıçtan adım sayıları.</summary>
            public readonly Dictionary<Vector2Int, int> Distance = new Dictionary<Vector2Int, int>();
            /// <summary>Ulaşılamayan (boş) hücreler.</summary>
            public readonly List<Vector2Int> Unreachable = new List<Vector2Int>();
            /// <summary>Bir objenin kapladığı, karakterin duramayacağı hücreler.</summary>
            public readonly HashSet<Vector2Int> Occupied = new HashSet<Vector2Int>();
            /// <summary>Dar geçidin iki yanındaki hücreler: ışınla geçiliyor ama karakter genişliği sığmıyor olabilir.</summary>
            public readonly HashSet<Vector2Int> Narrow = new HashSet<Vector2Int>();
            public Vector2Int Start;
            public bool HasStart;
            public Vector2Int Farthest;
            public int MaxDistance;
        }

        static readonly Vector2Int[] k_Directions = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        static readonly Collider[] s_Overlap = new Collider[32];
        static readonly RaycastHit[] s_Hits = new RaycastHit[32];

        /// <summary>start noktasından ulaşılabilen hücreleri hesaplar. ignore: engel sayılmayacak obje (ör. başlangıç objesi).</summary>
        public static Result Compute(WorldKitFloor floor, Vector3 start, GameObject ignore, bool allowDrop)
        {
            var result = new Result();
            if (floor == null || floor.CellCount == 0)
                return result;

            Physics.SyncTransforms();
            var context = new Context(floor, ignore);

            foreach (var cell in floor.Cells)
                if (context.IsOccupied(cell))
                    result.Occupied.Add(cell);

            // Başlangıç: noktanın altındaki hücre, yoksa en yakın boş hücre
            var startCell = WorldKitGrid.ToCell(start);
            if (!floor.TryGetHeight(startCell, out _) || result.Occupied.Contains(startCell))
            {
                float best = float.MaxValue;
                bool found = false;
                foreach (var cell in floor.Cells)
                {
                    if (result.Occupied.Contains(cell))
                        continue;
                    float d = (cell - startCell).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        startCell = cell;
                        found = true;
                    }
                }
                if (!found)
                    return result;
            }

            result.Start = startCell;
            result.HasStart = true;
            result.Farthest = startCell;

            var queue = new Queue<Vector2Int>();
            result.Distance[startCell] = 0;
            queue.Enqueue(startCell);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                int distance = result.Distance[cell];
                if (distance > result.MaxDistance)
                {
                    result.MaxDistance = distance;
                    result.Farthest = cell;
                }

                foreach (var dir in k_Directions)
                {
                    var next = cell + dir;
                    if (result.Distance.ContainsKey(next) || !floor.TryGetHeight(next, out _) || result.Occupied.Contains(next))
                        continue;
                    if (!context.CanStep(cell, next, dir, allowDrop))
                        continue;
                    if (context.IsNarrow(cell, next))
                    {
                        result.Narrow.Add(cell);
                        result.Narrow.Add(next);
                    }

                    result.Distance[next] = distance + 1;
                    queue.Enqueue(next);
                }
            }

            foreach (var cell in floor.Cells)
                if (!result.Distance.ContainsKey(cell) && !result.Occupied.Contains(cell))
                    result.Unreachable.Add(cell);

            return result;
        }

        /// <summary>Hücrenin ortasındaki yüzey noktası (dünya).</summary>
        public static Vector3 SurfaceCenter(WorldKitFloor floor, Vector2Int cell)
        {
            float level = 0f;
            for (int cx = 0; cx < 2; cx++)
            for (int cz = 0; cz < 2; cz++)
            {
                floor.TryGetCornerLevel(cell, cx, cz, out var l);
                level += l * 0.25f;
            }
            var center = WorldKitGrid.CellCenter(cell, 0);
            center.y = level * WorldKitGrid.LevelHeight;
            return center;
        }

        /// <summary>Hücrenin dir yönündeki kenarının iki köşe yüksekliği (kenar boyunca küçükten büyüğe).</summary>
        static (float, float) EdgeLevels(WorldKitFloor floor, Vector2Int cell, Vector2Int dir)
        {
            float a, b;
            if (dir.x != 0)
            {
                int cx = dir.x > 0 ? 1 : 0;
                floor.TryGetCornerLevel(cell, cx, 0, out a);
                floor.TryGetCornerLevel(cell, cx, 1, out b);
            }
            else
            {
                int cz = dir.y > 0 ? 1 : 0;
                floor.TryGetCornerLevel(cell, 0, cz, out a);
                floor.TryGetCornerLevel(cell, 1, cz, out b);
            }
            return (a, b);
        }

        sealed class Context
        {
            readonly WorldKitFloor m_Floor;
            readonly HashSet<Collider> m_Ignored = new HashSet<Collider>();
            readonly float m_Height;
            readonly float m_Radius;

            public Context(WorldKitFloor floor, GameObject ignore)
            {
                m_Floor = floor;
                m_Height = WorldKitSettings.Instance.characterHeight;
                m_Radius = Mathf.Min(0.3f, m_Height * 0.25f, WorldKitGrid.CellSize * 0.4f);
                foreach (var c in floor.GetComponentsInChildren<Collider>())
                    m_Ignored.Add(c);
                if (ignore != null)
                    foreach (var c in ignore.GetComponentsInChildren<Collider>())
                        m_Ignored.Add(c);
            }

            bool Blocks(Collider c) => c != null && !c.isTrigger && !m_Ignored.Contains(c) && !(c is CharacterController);

            public bool IsOccupied(Vector2Int cell)
            {
                var p = SurfaceCenter(m_Floor, cell);
                // Rampalarda kapsülün altı yüzeye değmesin diye biraz yukarıdan başla.
                var bottom = p + Vector3.up * (m_Radius + WorldKitGrid.LevelHeight * 0.35f);
                var top = p + Vector3.up * Mathf.Max(m_Height - m_Radius, m_Radius + WorldKitGrid.LevelHeight * 0.35f);
                int count = Physics.OverlapCapsuleNonAlloc(bottom, top, m_Radius * 0.9f, s_Overlap, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                    if (Blocks(s_Overlap[i]))
                        return true;
                return false;
            }

            public bool CanStep(Vector2Int from, Vector2Int to, Vector2Int dir, bool allowDrop)
            {
                var (a0, a1) = EdgeLevels(m_Floor, from, dir);
                var (b0, b1) = EdgeLevels(m_Floor, to, -dir);
                bool level = Mathf.Abs(a0 - b0) < 0.01f && Mathf.Abs(a1 - b1) < 0.01f;
                bool drop = allowDrop && b0 <= a0 + 0.01f && b1 <= a1 + 0.01f;
                if (!level && !drop)
                    return false;

                // Aradaki engeller: diz ve göğüs hizasında iki yönlü ışın (collider'ın içinden çıkan ışın çarpmaz).
                var a = SurfaceCenter(m_Floor, from);
                var b = SurfaceCenter(m_Floor, to);
                foreach (float t in new[] { 0.35f, 0.75f })
                {
                    var offset = Vector3.up * Mathf.Max(m_Height * t, 0.2f);
                    if (Hit(a + offset, b + offset) || Hit(b + offset, a + offset))
                        return false;
                }
                return true;
            }

            /// <summary>
            /// Geçiş ışınlarla açık ama karakter genişliğinde bir kapsül sığmıyor mu (dar kapı, sıkışık kolonlar)?
            /// </summary>
            public bool IsNarrow(Vector2Int from, Vector2Int to)
            {
                var a = SurfaceCenter(m_Floor, from);
                var b = SurfaceCenter(m_Floor, to);
                return Sweep(a, b) || Sweep(b, a);
            }

            bool Sweep(Vector3 from, Vector3 to)
            {
                var delta = to - from;
                var bottom = from + Vector3.up * (m_Radius + WorldKitGrid.LevelHeight * 0.35f);
                var top = from + Vector3.up * Mathf.Max(m_Height - m_Radius, m_Radius + WorldKitGrid.LevelHeight * 0.35f);
                int count = Physics.CapsuleCastNonAlloc(bottom, top, m_Radius, delta.normalized, s_Hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                    if (Blocks(s_Hits[i].collider))
                        return true;
                return false;
            }

            bool Hit(Vector3 from, Vector3 to)
            {
                var delta = to - from;
                int count = Physics.RaycastNonAlloc(from, delta.normalized, s_Hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                    if (Blocks(s_Hits[i].collider))
                        return true;
                return false;
            }
        }
    }

    /// <summary>
    /// "Erişim" sekmesi (Analiz): başlangıçtan yürüyerek ulaşılan hücreleri sahnede yeşil, ulaşılamayanları kırmızı
    /// gösterir. Gösterim açıkken her düzenlemeden sonra kendiliğinden yenilenir.
    /// </summary>
    [InitializeOnLoad]
    sealed class ReachabilityPaletteTab : WorldKitPaletteTab
    {
        const string DropKey = "WorldKit.Reach.AllowDrop";
        const string GradientKey = "WorldKit.Reach.Gradient";

        static WorldKitReachability.Result s_Result;
        static GameObject s_StartObject;
        static Vector3 s_StartPoint;
        static string s_StartName;
        static bool s_Show;
        static bool s_Dirty;
        static Mesh s_Mesh;
        static Material s_Material;

        static readonly Color k_Reachable = new Color(0.25f, 0.85f, 0.35f, 0.45f);
        static readonly Color k_Far = new Color(1f, 0.75f, 0.15f, 0.5f);
        static readonly Color k_Unreachable = new Color(1f, 0.25f, 0.2f, 0.55f);
        static readonly Color k_Narrow = new Color(0.75f, 0.3f, 1f, 0.65f);

        static ReachabilityPaletteTab()
        {
            SceneView.duringSceneGui += Draw;
            WorldKitEvents.EditFinished += MarkDirty;
            WorldKitEvents.FloorChanged += _ => MarkDirty();
            Undo.undoRedoPerformed += MarkDirty;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode)
                    Hide();
            };
        }

        public override string Title => "Erişim";
        public override int Order => 2010;
        public override string Section => AnalysisSection;
        public override Texture2D Icon => WorldKitIcons.Reach;

        static bool AllowDrop
        {
            get => EditorPrefs.GetBool(DropKey, false);
            set => EditorPrefs.SetBool(DropKey, value);
        }

        static bool Gradient
        {
            get => EditorPrefs.GetBool(GradientKey, true);
            set => EditorPrefs.SetBool(GradientKey, value);
        }

        public override void OnGUI(WorldKitPaletteWindow window)
        {
            GUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "Başlangıçtan yürüyerek ulaşılan zemin yeşil, ulaşılamayan kırmızı, dar geçitler mor gösterilir. Duvarlar, binalar ve " +
                "diğer collider'lar engeldir; katlar arasında rampa gerekir.\n" +
                "Başlangıç: seçili obje, yoksa sahnedeki Oyuncu referansı, o da yoksa Scene kamerasının baktığı yer.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(s_Show ? "Yeniden Hesapla" : "Hesapla ve Göster", GUILayout.Height(26f)))
                    Calculate();
                using (new EditorGUI.DisabledScope(!s_Show))
                {
                    if (GUILayout.Button("Gizle", GUILayout.Height(26f), GUILayout.Width(60f)))
                        Hide();
                }
            }

            bool drop = EditorGUILayout.ToggleLeft(new GUIContent("Aşağı atlamaya izin ver",
                "Açıksa karakter daha alçak bir komşu hücreye inebilir (geri çıkamaz)."), AllowDrop);
            if (drop != AllowDrop)
            {
                AllowDrop = drop;
                MarkDirty();
            }

            bool gradient = EditorGUILayout.ToggleLeft(new GUIContent("Uzaklığı renklendir",
                "Ulaşılan hücreler başlangıca yakınken yeşil, uzaklaştıkça turuncu."), Gradient);
            if (gradient != Gradient)
            {
                Gradient = gradient;
                RebuildMesh();
                SceneView.RepaintAll();
            }

            var result = s_Result;
            if (!s_Show || result == null)
                return;

            var floor = FindFloor();
            float cell = WorldKitGrid.CellSize;
            float area = cell * cell;
            var settings = WorldKitSettings.Instance;

            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Sonuç", EditorStyles.boldLabel);
            if (!result.HasStart)
            {
                EditorGUILayout.HelpBox("Başlangıç için boş bir zemin hücresi bulunamadı.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Başlangıç", s_StartName);
            EditorGUILayout.LabelField("Ulaşılan", $"{result.Distance.Count} hücre  ({result.Distance.Count * area:0} m²)");
            EditorGUILayout.LabelField("Ulaşılamayan", $"{result.Unreachable.Count} hücre  ({result.Unreachable.Count * area:0} m²)");
            EditorGUILayout.LabelField("Dolu (obje altında)", $"{result.Occupied.Count} hücre");
            EditorGUILayout.LabelField(new GUIContent("Dar geçit", "Mor: geçiliyor ama karakter genişliği sığmayabilir (dar kapı, sıkışık engeller)."),
                new GUIContent($"{result.Narrow.Count} hücre"));

            float far = result.MaxDistance * cell;
            EditorGUILayout.LabelField(new GUIContent("En uzak nokta", "Hücreden hücreye yol uzunluğu; çapraz kısayollar sayılmaz."),
                new GUIContent($"~{far:0} m  (yürü {far / settings.walkSpeed:0} sn, koş {far / settings.runSpeed:0} sn)"));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("En Uzağa Git") && floor != null)
                    Frame(WorldKitReachability.SurfaceCenter(floor, result.Farthest));
                using (new EditorGUI.DisabledScope(result.Unreachable.Count == 0))
                {
                    if (GUILayout.Button("Ulaşılamayana Git") && floor != null)
                        Frame(WorldKitReachability.SurfaceCenter(floor, NearestUnreachable(result)));
                }
            }
        }

        static void Calculate()
        {
            s_StartObject = Selection.activeGameObject;
            if (s_StartObject == null)
            {
                var playerPath = WorldKitStarterContent.PlayerReferencePath;
                s_StartObject = WorldKitScene.AllPieces().FirstOrDefault(p =>
                {
                    var source = WorldKitScene.GetSource(p);
                    return source != null && AssetDatabase.GetAssetPath(source) == playerPath;
                });
            }

            if (s_StartObject == null)
            {
                var view = SceneView.lastActiveSceneView;
                s_StartPoint = view != null ? view.pivot : Vector3.zero;
                s_StartName = "Scene kamerasının baktığı yer";
            }

            s_Show = true;
            Recompute();
        }

        static void Recompute()
        {
            s_Dirty = false;
            if (!s_Show)
                return;

            var floor = FindFloor();
            var start = s_StartObject != null ? s_StartObject.transform.position : s_StartPoint;
            if (s_StartObject != null)
                s_StartName = s_StartObject.name;

            s_Result = WorldKitReachability.Compute(floor, start, s_StartObject, AllowDrop);
            RebuildMesh();
            SceneView.RepaintAll();
            RepaintPalette();
        }

        static void MarkDirty()
        {
            if (!s_Show || s_Dirty)
                return;

            // Bir düzenleme birden çok olay tetikleyebilir; hepsi bitince bir kez hesapla.
            s_Dirty = true;
            EditorApplication.delayCall += Recompute;
        }

        static void Hide()
        {
            s_Show = false;
            s_Result = null;
            s_StartObject = null;
            if (s_Mesh != null)
                Object.DestroyImmediate(s_Mesh);
            s_Mesh = null;
            SceneView.RepaintAll();
            RepaintPalette();
        }

        static WorldKitFloor FindFloor()
        {
            var root = WorldKitScene.FindRoot();
            return root != null ? root.GetComponentInChildren<WorldKitFloor>() : null;
        }

        static void RepaintPalette()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<WorldKitPaletteWindow>())
                window.Repaint();
        }

        static Vector2Int NearestUnreachable(WorldKitReachability.Result result)
        {
            var view = SceneView.lastActiveSceneView;
            var from = view != null ? WorldKitGrid.ToCell(view.pivot) : result.Start;
            return result.Unreachable.OrderBy(c => (c - from).sqrMagnitude).First();
        }

        static void Frame(Vector3 point)
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null)
                return;
            view.LookAt(point, view.rotation, Mathf.Max(6f, WorldKitGrid.CellSize * 6f));
        }

        // ---------------- Sahne gösterimi ----------------

        static void RebuildMesh()
        {
            var result = s_Result;
            var floor = FindFloor();
            if (result == null || floor == null)
            {
                if (s_Mesh != null)
                    s_Mesh.Clear();
                return;
            }

            if (s_Mesh == null)
                s_Mesh = new Mesh { name = "WorldKit Erişim", hideFlags = HideFlags.HideAndDontSave, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            s_Mesh.Clear();

            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var indices = new List<int>();
            bool gradient = Gradient;
            float max = Mathf.Max(1, result.MaxDistance);

            foreach (var pair in result.Distance)
                AddCell(floor, pair.Key, result.Narrow.Contains(pair.Key) ? k_Narrow
                    : gradient ? Color.Lerp(k_Reachable, k_Far, pair.Value / max) : k_Reachable, vertices, colors, indices);
            foreach (var cell in result.Unreachable)
                AddCell(floor, cell, k_Unreachable, vertices, colors, indices);

            s_Mesh.SetVertices(vertices);
            s_Mesh.SetColors(colors);
            s_Mesh.SetTriangles(indices, 0);
        }

        static void AddCell(WorldKitFloor floor, Vector2Int cell, Color color, List<Vector3> vertices, List<Color> colors, List<int> indices)
        {
            float size = WorldKitGrid.CellSize;
            float lh = WorldKitGrid.LevelHeight;
            float inset = size * 0.06f;
            const float lift = 0.04f;

            int start = vertices.Count;
            foreach (var (cx, cz) in new[] { (0, 0), (0, 1), (1, 1), (1, 0) })
            {
                floor.TryGetCornerLevel(cell, cx, cz, out var level);
                vertices.Add(new Vector3(
                    cell.x * size + (cx == 0 ? inset : size - inset),
                    level * lh + lift,
                    cell.y * size + (cz == 0 ? inset : size - inset)));
                colors.Add(color);
            }
            indices.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }

        static Material Material
        {
            get
            {
                if (s_Material != null)
                    return s_Material;

                s_Material = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                s_Material.SetColor("_Color", Color.white);
                s_Material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                s_Material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                s_Material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Back);
                s_Material.SetInt("_ZWrite", 0);
                s_Material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
                return s_Material;
            }
        }

        static void Draw(SceneView view)
        {
            if (!s_Show || s_Mesh == null || Event.current.type != EventType.Repaint)
                return;

            Material.SetPass(0);
            Graphics.DrawMeshNow(s_Mesh, Matrix4x4.identity);

            var result = s_Result;
            var floor = FindFloor();
            if (result == null || !result.HasStart || floor == null)
                return;

            var start = WorldKitReachability.SurfaceCenter(floor, result.Start) + Vector3.up * 0.06f;
            using (new Handles.DrawingScope(Color.white))
            {
                Handles.DrawSolidDisc(start, Vector3.up, WorldKitGrid.CellSize * 0.25f);
                Handles.Label(start + Vector3.up * 0.4f, "Başlangıç", EditorStyles.whiteBoldLabel);
            }
        }
    }
}
