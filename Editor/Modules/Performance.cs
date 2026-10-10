using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;

namespace WorldKit.Editor
{
    /// <summary>
    /// Performans araçları.
    ///
    /// Sorun: ProBuilder ile yapılmış prefab'lar mesh'i asset olarak saklamaz; sahnedeki her kopya kendi mesh'ini
    /// üretir. 300 ağaç = 300 ayrı mesh (bellek, yükleme süresi, GPU toplu çizemez, her MeshCollider ayrı hazırlanır).
    ///
    /// Çözüm:
    /// - <see cref="OptimizePiecePrefabs"/>: Parçalar klasöründeki prefab'ların ProBuilder mesh'lerini asset'e yazar ve
    ///   ProBuilder bileşenini çıkarır. Bütün kopyalar tek mesh'i paylaşır.
    /// - <see cref="FixSceneInstances"/>: Sahnedeki kopyalarda kalmış mesh override'larını temizler (prefab'ın mesh'ine döner).
    /// - <see cref="Analyze"/>: Benzersiz mesh, collider, gölge sayıları ve render pipeline ayarları.
    /// </summary>
    public static class WorldKitPerformance
    {
        /// <summary>Paylaşılan mesh asset'lerinin klasörü (materyal klasörünün yanında "Meshes").</summary>
        public static string MeshFolder
        {
            get
            {
                var parent = Path.GetDirectoryName(WorldKitSettings.Instance.materialsFolder)?.Replace('\\', '/');
                return (string.IsNullOrEmpty(parent) ? "Assets/WorldKit" : parent) + "/Meshes";
            }
        }

        /// <summary>
        /// Mesh'in bir kopyasını asset olarak kaydeder. Asset zaten varsa içeriği değiştirilir (GUID ve referanslar korunur).
        /// </summary>
        public static Mesh SaveMeshAsset(Mesh source, string path)
        {
            WorldKitSettings.EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var copy = Object.Instantiate(source);
            copy.name = Path.GetFileNameWithoutExtension(path);
            copy.RecalculateBounds();

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(copy, path);
                return copy;
            }

            existing.Clear();
            EditorUtility.CopySerialized(copy, existing);
            existing.name = copy.name;
            Object.DestroyImmediate(copy);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        /// <summary>
        /// Objenin (ve altındakilerin) ProBuilder mesh'lerini asset'e yazar, ProBuilder bileşenlerini kaldırır ve
        /// MeshFilter / MeshCollider'ları asset mesh'ine bağlar. Dönüştürülen mesh sayısını döndürür.
        /// baseName: asset dosya adı (birden fazla mesh varsa sonuna _1, _2... eklenir).
        /// </summary>
        public static int BakeProBuilder(GameObject root, string baseName)
        {
            var meshes = root.GetComponentsInChildren<ProBuilderMesh>(true);
            for (int i = 0; i < meshes.Length; i++)
            {
                var pb = meshes[i];
                var filter = pb.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    pb.ToMesh();
                    pb.Refresh();
                    filter = pb.GetComponent<MeshFilter>();
                }
                if (filter == null || filter.sharedMesh == null)
                    continue;

                var name = meshes.Length == 1 ? baseName : $"{baseName}_{i + 1}";
                var asset = SaveMeshAsset(filter.sharedMesh, $"{MeshFolder}/{name}.asset");
                var collider = pb.GetComponent<MeshCollider>();
                var go = pb.gameObject;

                Object.DestroyImmediate(pb, true);
                filter = go.GetComponent<MeshFilter>();
                filter.sharedMesh = asset;
                if (collider != null)
                    collider.sharedMesh = asset;
            }
            return meshes.Length;
        }

        // ---------------- Prefab'lar ----------------

        [MenuItem("Tools/World Kit/Performans/Parça Prefab'larını Optimize Et", priority = 90)]
        public static void OptimizePiecePrefabsMenu()
        {
            int count = OptimizePiecePrefabs();
            int fixedCount = FixSceneInstances();
            EditorUtility.DisplayDialog("World Kit",
                count == 0 && fixedCount == 0
                    ? "Her şey zaten optimize: ProBuilder kullanan parça prefab'ı yok."
                    : $"{count} prefab paylaşılan mesh'e çevrildi, açık sahnelerde {fixedCount} kopya düzeltildi.\n\n" +
                      "Başka sahnelerin varsa onları açıp Tools → World Kit → Performans → Sahnedeki Parçaları Düzelt'e bas.",
                "Tamam");
        }

        /// <summary>Parçalar klasöründeki ProBuilder'lı prefab'ları paylaşılan mesh'e çevirir. Çevrilen prefab sayısı.</summary>
        public static int OptimizePiecePrefabs()
        {
            var folder = WorldKitSettings.Instance.piecesFolder;
            if (!AssetDatabase.IsValidFolder(folder))
                return 0;

            int converted = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponentInChildren<ProBuilderMesh>(true) == null)
                    continue;

                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var category = Path.GetFileName(Path.GetDirectoryName(path));
                    if (BakeProBuilder(contents, $"{category}_{Path.GetFileNameWithoutExtension(path)}") > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                        converted++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            AssetDatabase.SaveAssets();
            WorldKitThumbnails.Clear();
            return converted;
        }

        // ---------------- Sahne ----------------

        [MenuItem("Tools/World Kit/Performans/Sahnedeki Parçaları Düzelt", priority = 91)]
        static void FixSceneInstancesMenu()
        {
            int count = FixSceneInstances();
            Debug.Log($"[World Kit] Sahnedeki parçalar: {count} kopya prefab'ın paylaşılan mesh'ine döndürüldü.");
        }

        /// <summary>
        /// Açık sahnelerdeki prefab kopyalarında, prefab'dan farklı mesh kullanan MeshFilter / MeshCollider'ları
        /// prefab'a döndürür (eski ProBuilder kopyalarının kendi mesh'leri). Düzeltilen kopya sayısı.
        /// </summary>
        public static int FixSceneInstances()
        {
            var touched = new HashSet<GameObject>();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            foreach (var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(filter);
                if (source != null && source.sharedMesh != null && filter.sharedMesh != source.sharedMesh)
                {
                    PrefabUtility.RevertObjectOverride(filter, InteractionMode.UserAction);
                    touched.Add(filter.gameObject);
                }
            }

            foreach (var collider in Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(collider);
                if (source != null && source.sharedMesh != null && collider.sharedMesh != source.sharedMesh)
                {
                    PrefabUtility.RevertObjectOverride(collider, InteractionMode.UserAction);
                    touched.Add(collider.gameObject);
                }
            }

            // Kopyaya sonradan eklenmiş ProBuilder bileşenleri (prefab'da artık yok)
            foreach (var pb in Object.FindObjectsByType<ProBuilderMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (PrefabUtility.IsAddedComponentOverride(pb))
                {
                    touched.Add(pb.gameObject);
                    Undo.DestroyObjectImmediate(pb);
                }
            }

            Undo.SetCurrentGroupName("World Kit: Parçaları Düzelt");
            Undo.CollapseUndoOperations(group);
            return touched.Count;
        }

        // ---------------- Analiz ----------------

        public sealed class Report
        {
            public int Renderers;
            public int UniqueMeshes;
            /// <summary>Asset olmayan (sahnede üretilmiş) mesh kullanan renderer'lar; zemin hariç.</summary>
            public int SceneMeshes;
            public int ProBuilderComponents;
            public int MeshColliders;
            public int SimpleColliders;
            public int ShadowCasters;
            public int Triangles;
            public string Pipeline;
            public bool? ResidentDrawer;
            public readonly List<string> Tips = new List<string>();
        }

        public static Report Analyze()
        {
            var report = new Report();
            var root = WorldKitScene.FindRoot();
            if (root != null)
            {
                var meshes = new HashSet<Mesh>();
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    var mesh = filter != null ? filter.sharedMesh : null;
                    if (mesh == null)
                        continue;
                    report.Renderers++;
                    meshes.Add(mesh);
                    if (renderer.shadowCastingMode != ShadowCastingMode.Off)
                        report.ShadowCasters++;
                    for (int i = 0; i < mesh.subMeshCount; i++)
                        report.Triangles += (int)(mesh.GetIndexCount(i) / 3);
                    if (!EditorUtility.IsPersistent(mesh) && renderer.GetComponent<WorldKitFloor>() == null)
                        report.SceneMeshes++;
                }
                report.UniqueMeshes = meshes.Count;
                report.ProBuilderComponents = root.GetComponentsInChildren<ProBuilderMesh>(true).Length;

                foreach (var collider in root.GetComponentsInChildren<Collider>())
                {
                    if (collider.isTrigger || collider.GetComponent<WorldKitFloor>() != null)
                        continue;
                    if (collider is MeshCollider)
                        report.MeshColliders++;
                    else
                        report.SimpleColliders++;
                }
            }

            ReadPipeline(report);

            if (report.SceneMeshes > 0)
                report.Tips.Add($"{report.SceneMeshes} parça kendi mesh'ini taşıyor (ProBuilder). \"Prefab'ları Optimize Et\" ile hepsi paylaşılan mesh'e geçer.");
            if (report.MeshColliders > 50)
                report.Tips.Add($"{report.MeshColliders} MeshCollider var. Basit şekilli parçalarda Box/Capsule collider daha ucuzdur.");
            if (report.ResidentDrawer == false)
                report.Tips.Add("GPU Resident Drawer kapalı. Aynı parçadan yüzlerce varsa açmak çizimi çok hızlandırır (aşağıya bak).");
            return report;
        }

        /// <summary>URP asset'inden GPU Resident Drawer ve render yolunu okur (URP'ye derleme bağımlılığı olmadan).</summary>
        static void ReadPipeline(Report report)
        {
            var asset = GraphicsSettings.currentRenderPipeline;
            if (asset == null)
            {
                report.Pipeline = "Built-in";
                return;
            }

            report.Pipeline = asset.GetType().Name;
            var so = new SerializedObject(asset);
            var mode = so.FindProperty("m_GPUResidentDrawerMode");
            if (mode != null)
                report.ResidentDrawer = mode.intValue != 0;
        }

        /// <summary>İstatistik sekmesindeki "Performans" bölümü.</summary>
        public static void DrawReportGUI(ref Report report)
        {
            EditorGUILayout.LabelField("Performans", EditorStyles.boldLabel);
            if (report == null || GUILayout.Button("Performansı Analiz Et"))
                report = Analyze();

            EditorGUILayout.LabelField("Renderer / benzersiz mesh", $"{report.Renderers} / {report.UniqueMeshes}");
            EditorGUILayout.LabelField(new GUIContent("Kendi mesh'ini taşıyan", "Asset olmayan mesh: her kopya ayrı mesh demek."), new GUIContent(report.SceneMeshes.ToString()));
            EditorGUILayout.LabelField("Collider (mesh / basit)", $"{report.MeshColliders} / {report.SimpleColliders}");
            EditorGUILayout.LabelField("Gölge atan", report.ShadowCasters.ToString());
            EditorGUILayout.LabelField("Üçgen", report.Triangles.ToString("N0"));
            EditorGUILayout.LabelField("Render pipeline", report.Pipeline +
                (report.ResidentDrawer.HasValue ? (report.ResidentDrawer.Value ? " · GPU Resident Drawer açık" : " · GPU Resident Drawer kapalı") : ""));

            foreach (var tip in report.Tips)
                EditorGUILayout.HelpBox(tip, MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Prefab'ları Optimize Et"))
                {
                    OptimizePiecePrefabsMenu();
                    report = Analyze();
                }
                if (GUILayout.Button("Sahnedeki Parçaları Düzelt"))
                {
                    FixSceneInstances();
                    report = Analyze();
                }
            }

            if (report.ResidentDrawer == false)
            {
                EditorGUILayout.HelpBox(
                    "GPU Resident Drawer'ı açmak için (Unity 6, URP):\n" +
                    "1. URP Asset → Rendering → GPU Resident Drawer: Instanced Drawing\n" +
                    "2. URP Renderer → Rendering Path: Forward+\n" +
                    "3. Project Settings → Graphics → BatchRendererGroup Variants: Keep All\n" +
                    "World Kit materyallerinde GPU instancing zaten açık.",
                    MessageType.Info);
            }
        }
    }
}
