using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace WorldKit.Editor
{
    /// <summary>
    /// NavMesh bake: dünyanın collider'larından NavMesh üretir, asset olarak kaydeder ve köke
    /// <see cref="WorldKitNavMesh"/> ekler (NavMeshAgent'lar Play'de ve build'de kullanır).
    /// Unity'nin yerleşik NavMeshBuilder API'sini kullanır; AI Navigation paketi gerekmez.
    /// Ajan boyu ve yarıçapı ayarlardaki karakter boyundan gelir.
    /// </summary>
    public static class WorldKitNavMeshBake
    {
        /// <summary>Ajanın çıkabileceği basamak (m).</summary>
        public static float StepHeight => Mathf.Min(0.35f, WorldKitSettings.Instance.characterHeight * 0.3f);

        /// <summary>Yürünebilir en dik eğim (derece). Tam rampa (45°) ve yumuşak rampa geçilir.</summary>
        public const float MaxSlope = 50f;

        public static NavMeshBuildSettings BuildSettings()
        {
            var settings = NavMesh.GetSettingsByID(0);
            float height = WorldKitSettings.Instance.characterHeight;
            settings.agentHeight = height;
            settings.agentRadius = Mathf.Min(0.3f, height * 0.25f);
            settings.agentClimb = StepHeight;
            settings.agentSlope = MaxSlope;
            return settings;
        }

        /// <summary>Aktif sahnenin dünyasını bake eder. Başarılıysa oluşan veriyi döndürür.</summary>
        public static NavMeshData Bake()
        {
            var root = WorldKitScene.FindRoot();
            if (root == null)
            {
                Debug.LogWarning("[World Kit] NavMesh: sahnede dünya kökü yok.");
                return null;
            }

            Physics.SyncTransforms();
            var sources = new List<NavMeshBuildSource>();
            // Ignore Raycast katmanı (bölgeler vb. tetikleyiciler) hariç
            NavMeshBuilder.CollectSources(root.transform, ~(1 << 2), NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), sources);
            if (sources.Count == 0)
            {
                Debug.LogWarning("[World Kit] NavMesh: collider bulunamadı.");
                return null;
            }

            var bounds = WorldBounds(root);
            bounds.Expand(2f);
            var data = NavMeshBuilder.BuildNavMeshData(BuildSettings(), sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null)
                return null;

            data.name = root.scene.name + "_NavMesh";
            var path = AssetPath(root.scene);
            WorldKitSettings.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));

            // Var olan asset'in içeriğini değiştir ki referanslar kopmasın.
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(data, existing);
                existing.name = data.name;
                Object.DestroyImmediate(data);
                data = existing;
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
            }
            else
                AssetDatabase.CreateAsset(data, path);

            var component = root.GetComponent<WorldKitNavMesh>();
            if (component == null)
                component = Undo.AddComponent<WorldKitNavMesh>(root);
            Undo.RecordObject(component, "World Kit: NavMesh");
            component.data = data;
            component.Load();
            EditorUtility.SetDirty(component);
            return data;
        }

        /// <summary>Kökteki NavMesh'i kaldırır (asset silinmez).</summary>
        public static void Clear()
        {
            var root = WorldKitScene.FindRoot();
            var component = root != null ? root.GetComponent<WorldKitNavMesh>() : null;
            if (component != null)
                Undo.DestroyObjectImmediate(component);
        }

        public static bool HasNavMesh
        {
            get
            {
                var root = WorldKitScene.FindRoot();
                var component = root != null ? root.GetComponent<WorldKitNavMesh>() : null;
                return component != null && component.data != null;
            }
        }

        static string AssetPath(Scene scene)
        {
            var folder = System.IO.Path.GetDirectoryName(WorldKitSettings.Instance.piecesFolder)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder))
                folder = "Assets/WorldKit";
            var name = string.IsNullOrEmpty(scene.name) ? "Untitled" : scene.name;
            return $"{folder}/NavMesh/{name}_NavMesh.asset";
        }

        static Bounds WorldBounds(GameObject root)
        {
            var colliders = root.GetComponentsInChildren<Collider>();
            var bounds = colliders.Length > 0 ? colliders[0].bounds : new Bounds(root.transform.position, Vector3.one);
            foreach (var c in colliders)
                bounds.Encapsulate(c.bounds);
            return bounds;
        }
    }

    /// <summary>"NavMesh" sekmesi (Analiz): bake, temizle, sahnede göster.</summary>
    [InitializeOnLoad]
    sealed class NavMeshPaletteTab : WorldKitPaletteTab
    {
        static bool s_Show;
        static Mesh s_Mesh;
        static Material s_Material;
        static float s_Area;

        static NavMeshPaletteTab()
        {
            SceneView.duringSceneGui += Draw;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode)
                    s_Show = false;
            };
        }

        public override string Title => "NavMesh";
        public override int Order => 2020;
        public override string Section => AnalysisSection;
        public override Texture2D Icon => WorldKitIcons.NavMesh;

        public override void OnGUI(WorldKitPaletteWindow window)
        {
            GUILayout.Space(6f);
            var settings = WorldKitNavMeshBake.BuildSettings();
            EditorGUILayout.HelpBox(
                "Dünyanın collider'larından NavMesh üretir; NPC ve düşmanlardaki NavMeshAgent'lar bunu kullanır. " +
                "Veri Assets altında saklanır, köke WorldKitNavMesh eklenir (build'e de girer).\n" +
                $"Ajan: boy {settings.agentHeight:0.##} m, yarıçap {settings.agentRadius:0.##} m, basamak {settings.agentClimb:0.##} m, " +
                $"eğim {settings.agentSlope:0}°. Boy: Project Settings → World Kit → Character Height.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(WorldKitNavMeshBake.HasNavMesh ? "Yeniden Bake Et" : "Bake Et", GUILayout.Height(26f)))
                {
                    if (WorldKitNavMeshBake.Bake() != null)
                    {
                        s_Show = true;
                        RebuildMesh();
                    }
                    GUIUtility.ExitGUI();
                }

                using (new EditorGUI.DisabledScope(!WorldKitNavMeshBake.HasNavMesh))
                {
                    if (GUILayout.Button("Kaldır", GUILayout.Height(26f), GUILayout.Width(70f)))
                    {
                        WorldKitNavMeshBake.Clear();
                        s_Show = false;
                        GUIUtility.ExitGUI();
                    }
                }
            }

            bool show = EditorGUILayout.ToggleLeft("Sahnede göster", s_Show);
            if (show != s_Show)
            {
                s_Show = show;
                if (s_Show)
                    RebuildMesh();
                SceneView.RepaintAll();
                GUIUtility.ExitGUI();
            }

            if (s_Show && s_Mesh != null)
                EditorGUILayout.LabelField("Yürünebilir alan", $"{s_Area:0} m²  ({s_Mesh.triangles.Length / 3} üçgen)");

            EditorGUILayout.HelpBox("Dünya değişince yeniden bake etmeyi unutma; NavMesh kendiliğinden güncellenmez.", MessageType.Info);
        }

        static void RebuildMesh()
        {
            var triangulation = NavMesh.CalculateTriangulation();
            if (s_Mesh == null)
                s_Mesh = new Mesh { name = "WorldKit NavMesh", hideFlags = HideFlags.HideAndDontSave, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            s_Mesh.Clear();

            // Zeminle çakışmasın diye biraz yukarı
            var vertices = new Vector3[triangulation.vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = triangulation.vertices[i] + Vector3.up * 0.05f;
            s_Mesh.vertices = vertices;
            s_Mesh.triangles = triangulation.indices;

            s_Area = 0f;
            var indices = triangulation.indices;
            for (int i = 0; i + 2 < indices.Length; i += 3)
                s_Area += Vector3.Cross(vertices[indices[i + 1]] - vertices[indices[i]], vertices[indices[i + 2]] - vertices[indices[i]]).magnitude * 0.5f;

            SceneView.RepaintAll();
        }

        static Material Material
        {
            get
            {
                if (s_Material != null)
                    return s_Material;

                s_Material = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                s_Material.SetColor("_Color", new Color(0.2f, 0.75f, 1f, 0.4f));
                s_Material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                s_Material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                s_Material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
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
        }
    }
}
