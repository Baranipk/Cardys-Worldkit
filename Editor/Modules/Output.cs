using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorldKit.Editor
{
    /// <summary>
    /// Çıktı: dünyayı tek mesh'e bake etme ve OBJ olarak dışa aktarma.
    /// İkisi de kökün altındaki aktif renderer'ları kullanır; EditorOnly etiketli objeler (ölçü referansları) atlanır.
    /// </summary>
    static class WorldKitOutput
    {
        struct Part
        {
            public Mesh Mesh;
            public int SubMesh;
            public Matrix4x4 Matrix;
            public Material Material;
            public string Name;
        }

        static List<Part> CollectParts(GameObject root)
        {
            var parts = new List<Part>();
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled || IsEditorOnly(renderer.transform, root.transform))
                    continue;
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null)
                    continue;

                var materials = renderer.sharedMaterials;
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    if (mesh.GetIndexCount(i) == 0)
                        continue;
                    parts.Add(new Part
                    {
                        Mesh = mesh,
                        SubMesh = i,
                        Matrix = renderer.transform.localToWorldMatrix,
                        Material = materials.Length > 0 ? materials[Mathf.Min(i, materials.Length - 1)] : null,
                        Name = renderer.name
                    });
                }
            }
            return parts;
        }

        static bool IsEditorOnly(Transform t, Transform root)
        {
            for (; t != null && t != root.parent; t = t.parent)
                if (t.CompareTag("EditorOnly"))
                    return true;
            return false;
        }

        /// <summary>Bake bölge boyutu (m). 0 = tek mesh.</summary>
        public static float ChunkSize
        {
            get => EditorPrefs.GetFloat("WorldKit.Bake.ChunkSize", 16f);
            set => EditorPrefs.SetFloat("WorldKit.Bake.ChunkSize", Mathf.Max(0f, value));
        }

        static GameObject RootOrWarn()
        {
            var root = WorldKitScene.FindRoot();
            if (root == null)
                EditorUtility.DisplayDialog("World Kit", "Aktif sahnede World Kit kökü yok.", "Tamam");
            return root;
        }

        // ---------------- Bake ----------------

        [MenuItem("Tools/World Kit/Çıktı/Dünyayı Bake Et…", priority = 80)]
        public static void Bake()
        {
            var root = RootOrWarn();
            if (root == null)
                return;

            int choice = EditorUtility.DisplayDialogComplex("World Kit: Bake",
                "Bütün parçalar ve zemin, bölge bölge (Çıktı sekmesindeki bölge boyutu) ve materyal başına birleştirilip mesh asset'ine kaydedilecek.\n\n" +
                "Orijinal (düzenlenebilir) dünya ne olsun?",
                "Bake et, orijinali gizle", "Vazgeç", "Bake et, orijinal kalsın");
            if (choice == 1)
                return;

            var folder = (Path.GetDirectoryName(WorldKitSettings.Instance.piecesFolder) ?? "Assets").Replace('\\', '/') + "/Baked";
            WorldKitSettings.EnsureFolder(folder);
            var path = EditorUtility.SaveFilePanelInProject("Bake Mesh'ini Kaydet", root.scene.name + "_Baked", "asset",
                "Birleştirilmiş mesh'in kaydedileceği yer.", folder);
            if (string.IsNullOrEmpty(path))
                return;

            var parts = CollectParts(root);
            if (parts.Count == 0)
                return;

            // Bölgelere ayır: tek dev mesh, kameranın görmediği yerleri de çizdirir. Her bölge ayrı objedir.
            float chunk = ChunkSize;
            var chunks = parts.GroupBy(p =>
            {
                if (chunk <= 0f)
                    return Vector2Int.zero;
                var center = p.Matrix.MultiplyPoint3x4(p.Mesh.bounds.center);
                return new Vector2Int(Mathf.FloorToInt(center.x / chunk), Mathf.FloorToInt(center.z / chunk));
            }).ToList();

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            var parent = new GameObject(root.name + "_Baked");
            Undo.RegisterCreatedObjectUndo(parent, "World Kit: Bake");

            var baseName = Path.GetFileNameWithoutExtension(path);
            Mesh mainAsset = null;
            int vertices = 0;
            var allMaterials = new HashSet<Material>();
            foreach (var chunkParts in chunks)
            {
                // 1) Materyal başına birleştir, 2) hepsini alt-mesh olarak tek mesh'e koy.
                var groupMeshes = new List<Mesh>();
                var materials = new List<Material>();
                foreach (var group in chunkParts.GroupBy(p => p.Material))
                {
                    var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(group.Select(p => new CombineInstance { mesh = p.Mesh, subMeshIndex = p.SubMesh, transform = p.Matrix }).ToArray(), true, true);
                    groupMeshes.Add(mesh);
                    materials.Add(group.Key);
                    allMaterials.Add(group.Key);
                }

                var key = chunkParts.Key;
                var baked = new Mesh { name = chunk > 0f ? $"{baseName}_{key.x}_{key.y}" : baseName, indexFormat = IndexFormat.UInt32 };
                baked.CombineMeshes(groupMeshes.Select(m => new CombineInstance { mesh = m, transform = Matrix4x4.identity }).ToArray(), false, false);
                baked.RecalculateBounds();
                foreach (var m in groupMeshes)
                    Object.DestroyImmediate(m);
                vertices += baked.vertexCount;

                // Bütün bölgeler tek asset dosyasında
                if (mainAsset == null)
                {
                    AssetDatabase.CreateAsset(baked, path);
                    mainAsset = baked;
                }
                else
                    AssetDatabase.AddObjectToAsset(baked, mainAsset);

                var go = new GameObject(baked.name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
                go.transform.SetParent(parent.transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = baked;
                go.GetComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
                go.GetComponent<MeshCollider>().sharedMesh = baked;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }
            AssetDatabase.SaveAssets();

            if (choice == 0)
            {
                Undo.RecordObject(root, "World Kit: Bake");
                root.SetActive(false);
            }

            Undo.SetCurrentGroupName("World Kit: Bake");
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = parent;
            Debug.Log($"[World Kit] Bake: {parts.Count} parça → {chunks.Count} bölge, {allMaterials.Count} materyal, {vertices:N0} vertex → {path}");
        }

        // ---------------- OBJ ----------------

        [MenuItem("Tools/World Kit/Çıktı/OBJ Olarak Dışa Aktar…", priority = 81)]
        public static void ExportObj()
        {
            var root = RootOrWarn();
            if (root == null)
                return;

            var path = EditorUtility.SaveFilePanel("OBJ Olarak Dışa Aktar", "", root.scene.name, "obj");
            if (string.IsNullOrEmpty(path))
                return;

            var parts = CollectParts(root);
            var mtlName = Path.GetFileNameWithoutExtension(path) + ".mtl";
            var ci = CultureInfo.InvariantCulture;
            var obj = new StringBuilder();
            var mtl = new StringBuilder();
            obj.AppendLine("# World Kit OBJ").AppendLine($"mtllib {mtlName}");

            var materialNames = new Dictionary<Material, string>();
            string MaterialName(Material m)
            {
                if (m == null)
                    return "Varsayilan";
                if (!materialNames.TryGetValue(m, out var name))
                {
                    name = m.name.Replace(' ', '_');
                    materialNames[m] = name;
                    var c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.color : Color.gray;
                    mtl.AppendLine($"newmtl {name}").AppendLine(string.Format(ci, "Kd {0:0.###} {1:0.###} {2:0.###}", c.r, c.g, c.b)).AppendLine();
                }
                return name;
            }

            // Unity sol el, OBJ sağ el: X eksenini ters çevir ve üçgen sırasını değiştir.
            int offset = 1;
            var cache = new Dictionary<Mesh, (Vector3[] v, Vector3[] n, Vector2[] uv)>();
            foreach (var part in parts)
            {
                if (!cache.TryGetValue(part.Mesh, out var data))
                    cache[part.Mesh] = data = (part.Mesh.vertices, part.Mesh.normals, part.Mesh.uv);

                var normalMatrix = part.Matrix.inverse.transpose;
                obj.AppendLine($"g {part.Name.Replace(' ', '_')}_{part.SubMesh}").AppendLine($"usemtl {MaterialName(part.Material)}");
                for (int i = 0; i < data.v.Length; i++)
                {
                    var p = part.Matrix.MultiplyPoint3x4(data.v[i]);
                    obj.AppendLine(string.Format(ci, "v {0:0.####} {1:0.####} {2:0.####}", -p.x, p.y, p.z));
                }
                for (int i = 0; i < data.v.Length; i++)
                {
                    var n = i < data.n.Length ? normalMatrix.MultiplyVector(data.n[i]).normalized : Vector3.up;
                    obj.AppendLine(string.Format(ci, "vn {0:0.####} {1:0.####} {2:0.####}", -n.x, n.y, n.z));
                }
                for (int i = 0; i < data.v.Length; i++)
                {
                    var uv = i < data.uv.Length ? data.uv[i] : Vector2.zero;
                    obj.AppendLine(string.Format(ci, "vt {0:0.####} {1:0.####}", uv.x, uv.y));
                }

                var tris = part.Mesh.GetTriangles(part.SubMesh);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    int a = tris[i] + offset, b = tris[i + 2] + offset, c = tris[i + 1] + offset;
                    obj.AppendLine($"f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}");
                }
                offset += data.v.Length;
            }

            File.WriteAllText(path, obj.ToString());
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(path) ?? "", mtlName), mtl.ToString());
            Debug.Log($"[World Kit] OBJ: {parts.Count} parça → {path}");
            EditorUtility.RevealInFinder(path);
        }
    }

    /// <summary>Palette "Çıktı" sekmesi.</summary>
    sealed class OutputPaletteTab : WorldKitPaletteTab
    {
        public override string Title => "Çıktı";
        public override int Order => 2100;
        public override string Section => OutputSection;
        public override Texture2D Icon => WorldKitIcons.Output;

        public override void OnGUI(WorldKitPaletteWindow window)
        {
            GUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "Bake: Parçalar ve zemin, bölge bölge ve materyal başına birleştirilip mesh asset'ine kaydedilir. " +
                "Her bölge ayrı obje (renderer + collider) olduğundan kameranın görmediği bölgeler çizilmez. " +
                "Orijinal düzenlenebilir dünya istersen gizlenir (Ctrl+Z ile geri alınır).\n\n" +
                "OBJ: Sanatçılar için .obj + .mtl (Blender, Maya vb.). Renkler materyallerden alınır.\n\n" +
                "İkisinde de EditorOnly etiketli objeler (ölçü referansları) ve gizli objeler atlanır.",
                MessageType.None);

            float chunk = EditorGUILayout.Slider(new GUIContent("Bölge boyutu (m)", "Bake edilen dünya bu boyutta karelere bölünür. 0 = tek mesh."),
                WorldKitOutput.ChunkSize, 0f, 64f);
            if (!Mathf.Approximately(chunk, WorldKitOutput.ChunkSize))
                WorldKitOutput.ChunkSize = Mathf.Round(chunk);

            if (GUILayout.Button("Dünyayı Bake Et…", GUILayout.Height(30f)))
            {
                WorldKitOutput.Bake();
                GUIUtility.ExitGUI();
            }
            if (GUILayout.Button("OBJ Olarak Dışa Aktar…", GUILayout.Height(30f)))
            {
                WorldKitOutput.ExportObj();
                GUIUtility.ExitGUI();
            }
        }
    }
}
