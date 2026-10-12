using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorldKit
{
    /// <summary>
    /// Zemin yüzey boyası: patika, koyu çimen, taş yol gibi katmanlar zeminin üstüne hücreden bağımsız, yumuşak ve
    /// hafif tırtıklı kenarlarla boyanır (Link's Awakening tarzı kıvrımlı yollar).
    ///
    /// Veri: her katman için hücre başına <see cref="resolution"/> × resolution örnekten oluşan bir ağırlık ızgarası
    /// (0-255), 16×16'lık parçalar halinde saklanır. Görünüş: her katman ağırlığın 0.5 eşiğinden "marching squares"
    /// ile çıkarılmış bir mesh; zeminin yüzeyini (rampa ve kat dahil) takip eder, katmanın materyaliyle çizilir.
    /// Collider yoktur; yalnızca görünüştür. Zemin değişince kendiliğinden yeniden kurulur.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WorldKitFloor))]
    public class WorldKitSurfacePaint : MonoBehaviour
    {
        public const int ChunkSize = 16;
        const string ChildPrefix = "WorldKitBoya_";

        [Serializable]
        public class Chunk
        {
            public Vector2Int key;
            public byte[] weights = new byte[ChunkSize * ChunkSize];
        }

        [Serializable]
        public class Layer
        {
            public List<Chunk> chunks = new List<Chunk>();
        }

        [Tooltip("Hücre başına örnek sayısı (kenar inceliği). Değiştirmek mevcut boyayı ölçekler; boyamaya başlamadan seçin.")]
        [Range(2, 8)] public int resolution = 4;

        [Tooltip("Katman materyalleri (World Kit ayarlarından doldurulur). Sıra = çizim sırası (sonraki üstte).")]
        public List<Material> materials = new List<Material>();

        [Tooltip("Katman başına kenar tırtıklılığı (0 = pürüzsüz, 1 = çok tırtıklı).")]
        public List<float> edgeNoise = new List<float>();

        [SerializeField, HideInInspector] List<Layer> m_Layers = new List<Layer>();

        readonly List<Dictionary<Vector2Int, Chunk>> m_Index = new List<Dictionary<Vector2Int, Chunk>>();
        readonly List<MeshFilter> m_Filters = new List<MeshFilter>();
        WorldKitFloor m_Floor;

        public int LayerCount => m_Layers.Count;

        bool m_Pending;

        void OnEnable()
        {
            m_Floor = GetComponent<WorldKitFloor>();
            m_Floor.Rebuilt += OnFloorRebuilt;
            // Obje etkinleşirken alt obje eklenemez (SetParent hatası): kurmayı ertele.
            ScheduleRebuild();
        }

        void OnDisable()
        {
            if (m_Floor != null)
                m_Floor.Rebuilt -= OnFloorRebuilt;
            // Çocukların etkinliğini burada değiştirmek yasak; yalnızca çizimi kapat
            foreach (var filter in m_Filters)
                if (filter != null && filter.TryGetComponent<MeshRenderer>(out var renderer))
                    renderer.enabled = false;
        }

        void ScheduleRebuild()
        {
            // Aynı karede birden çok istek tek yeniden kurmaya iner
            if (m_Pending)
                return;
            m_Pending = true;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    m_Pending = false;
                    if (this != null && isActiveAndEnabled)
                        Rebuild();
                };
            }
#endif
        }

        void Update()
        {
            if (m_Pending && Application.isPlaying)
            {
                m_Pending = false;
                Rebuild();
            }
        }

        void OnDestroy()
        {
            foreach (var filter in m_Filters)
            {
                if (filter == null)
                    continue;
                var go = filter.gameObject;
                var mesh = filter.sharedMesh;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.EditorApplication.delayCall += () =>
                    {
                        if (go != null) DestroyImmediate(go);
                        if (mesh != null) DestroyImmediate(mesh);
                    };
                    continue;
                }
#endif
                Destroy(go);
                if (mesh != null)
                    Destroy(mesh);
            }
        }

        void OnValidate()
        {
            // Undo / Inspector değişikliğinden sonra. OnValidate içinde obje oluşturulamaz: bir sonraki editör karesine ertele.
            if (!Application.isPlaying)
                ScheduleRebuild();
        }

        // Zemin OnValidate içinde de yeniden kurulabilir; orada obje oluşturulamaz, o yüzden ertelenir
        void OnFloorRebuilt() => ScheduleRebuild();

        // ---------------- Veri ----------------

        void EnsureLayer(int layer)
        {
            while (m_Layers.Count <= layer)
                m_Layers.Add(new Layer());
        }

        void Reindex()
        {
            m_Index.Clear();
            foreach (var layer in m_Layers)
            {
                var map = new Dictionary<Vector2Int, Chunk>();
                foreach (var chunk in layer.chunks)
                {
                    if (chunk.weights == null || chunk.weights.Length != ChunkSize * ChunkSize)
                        chunk.weights = new byte[ChunkSize * ChunkSize];
                    map[chunk.key] = chunk;
                }
                m_Index.Add(map);
            }
        }

        static Vector2Int ChunkOf(int sx, int sz) =>
            new Vector2Int(Mathf.FloorToInt(sx / (float)ChunkSize), Mathf.FloorToInt(sz / (float)ChunkSize));

        /// <summary>Örnek (sx, sz) noktasındaki ağırlık (0-1). Örnek = hücre köşesinin 1/resolution'ı.</summary>
        public float GetWeight(int layer, int sx, int sz)
        {
            if (layer < 0 || layer >= m_Index.Count)
                return 0f;
            var key = ChunkOf(sx, sz);
            if (!m_Index[layer].TryGetValue(key, out var chunk))
                return 0f;
            return chunk.weights[(sz - key.y * ChunkSize) * ChunkSize + (sx - key.x * ChunkSize)] / 255f;
        }

        /// <summary>Ağırlığı ayarlar (0-1). Mesh'i güncellemek için sonra <see cref="Rebuild"/> çağırın.</summary>
        public void SetWeight(int layer, int sx, int sz, float weight)
        {
            if (layer < 0)
                return;
            EnsureLayer(layer);
            while (m_Index.Count < m_Layers.Count)
                m_Index.Add(new Dictionary<Vector2Int, Chunk>());

            var key = ChunkOf(sx, sz);
            if (!m_Index[layer].TryGetValue(key, out var chunk))
            {
                if (weight <= 0f)
                    return;
                chunk = new Chunk { key = key };
                m_Layers[layer].chunks.Add(chunk);
                m_Index[layer][key] = chunk;
            }
            chunk.weights[(sz - key.y * ChunkSize) * ChunkSize + (sx - key.x * ChunkSize)] = (byte)Mathf.RoundToInt(Mathf.Clamp01(weight) * 255f);
        }

        /// <summary>Bir katmanı tamamen siler.</summary>
        public void ClearLayer(int layer)
        {
            if (layer >= 0 && layer < m_Layers.Count)
                m_Layers[layer].chunks.Clear();
            Rebuild();
        }

        // ---------------- Mesh ----------------

        /// <summary>Bütün katmanların mesh'ini (veya yalnızca birini) yeniden kurar.</summary>
        public void Rebuild(int onlyLayer = -1)
        {
            if (m_Floor == null)
                m_Floor = GetComponent<WorldKitFloor>();
            Reindex();
            CollectChildren();

            int count = Mathf.Max(m_Layers.Count, materials.Count);
            for (int layer = 0; layer < count; layer++)
            {
                if (onlyLayer >= 0 && layer != onlyLayer)
                    continue;
                var filter = ChildFor(layer);
                var renderer = filter.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = layer < materials.Count ? materials[layer] : null;
                BuildLayerMesh(layer, filter.sharedMesh);
                renderer.enabled = isActiveAndEnabled && renderer.sharedMaterial != null && filter.sharedMesh.vertexCount > 0;
            }

            // Fazla çocukları kapat (katman silindiyse)
            for (int i = count; i < m_Filters.Count; i++)
                if (m_Filters[i] != null)
                    m_Filters[i].GetComponent<MeshRenderer>().enabled = false;
        }

        void CollectChildren()
        {
            // Domain reload sonrası liste boştur ama gizli çocuklar sahnede durur: yeniden bul.
            if (m_Filters.Count > 0 && m_Filters.TrueForAll(f => f != null))
                return;
            m_Filters.Clear();
            foreach (Transform child in transform)
            {
                if (!child.name.StartsWith(ChildPrefix) || !int.TryParse(child.name.Substring(ChildPrefix.Length), out var index))
                    continue;
                while (m_Filters.Count <= index)
                    m_Filters.Add(null);
                m_Filters[index] = child.GetComponent<MeshFilter>();
            }
        }

        MeshFilter ChildFor(int layer)
        {
            while (m_Filters.Count <= layer)
                m_Filters.Add(null);
            var filter = m_Filters[layer];
            if (filter == null)
            {
                var go = new GameObject(ChildPrefix + layer, typeof(MeshFilter), typeof(MeshRenderer))
                {
                    hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy | HideFlags.NotEditable
                };
                go.transform.SetParent(transform, false);
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                filter = go.GetComponent<MeshFilter>();
                m_Filters[layer] = filter;
            }
            if (filter.sharedMesh == null)
                filter.sharedMesh = new Mesh { name = ChildPrefix + layer, hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
            return filter;
        }

        void BuildLayerMesh(int layer, Mesh mesh)
        {
            mesh.Clear();
            if (layer >= m_Index.Count || m_Index[layer].Count == 0 || m_Floor == null)
                return;

            int r = Mathf.Max(1, resolution);
            float cell = Mathf.Max(0.01f, m_Floor.cellSize);
            float lh = m_Floor.levelHeight;
            float noise = layer < edgeNoise.Count ? Mathf.Clamp01(edgeNoise[layer]) : 0.5f;
            float lift = 0.012f + layer * 0.004f;

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var values = new float[(r + 1) * (r + 1)];
            var polygon = new List<Vector2>(8);

            foreach (var cellPos in m_Floor.Cells)
            {
                if (!m_Floor.TryGetHeight(cellPos, out var height) || height > m_Floor.MaxVisibleLevel)
                    continue;

                // Hücrenin örnek değerleri (+ kenar gürültüsü); hiçbiri eşiği geçmiyorsa atla
                bool any = false;
                for (int b = 0; b <= r; b++)
                for (int a = 0; a <= r; a++)
                {
                    int sx = cellPos.x * r + a, sz = cellPos.y * r + b;
                    float w = GetWeight(layer, sx, sz);
                    // Gürültü her örneğe: eşik çizgisi iki yönde de oynar, kenar tırtıklı olur (iç 1, dış 0 kalır)
                    w += (Hash01(sx, sz, layer) - 0.5f) * noise * 0.7f;
                    values[b * (r + 1) + a] = w;
                    any |= w > 0.5f;
                }
                if (!any)
                    continue;

                m_Floor.TryGetCornerLevel(cellPos, 0, 0, out var l00);
                m_Floor.TryGetCornerLevel(cellPos, 1, 0, out var l10);
                m_Floor.TryGetCornerLevel(cellPos, 0, 1, out var l01);
                m_Floor.TryGetCornerLevel(cellPos, 1, 1, out var l11);

                for (int b = 0; b < r; b++)
                for (int a = 0; a < r; a++)
                {
                    // Alt kare: sol-alt, sağ-alt, sağ-üst, sol-üst
                    float v0 = values[b * (r + 1) + a] - 0.5f;
                    float v1 = values[b * (r + 1) + a + 1] - 0.5f;
                    float v2 = values[(b + 1) * (r + 1) + a + 1] - 0.5f;
                    float v3 = values[(b + 1) * (r + 1) + a] - 0.5f;
                    if (v0 < 0f && v1 < 0f && v2 < 0f && v3 < 0f)
                        continue;

                    polygon.Clear();
                    Clip(polygon, new Vector2(a, b), v0, new Vector2(a + 1, b), v1);
                    Clip(polygon, new Vector2(a + 1, b), v1, new Vector2(a + 1, b + 1), v2);
                    Clip(polygon, new Vector2(a + 1, b + 1), v2, new Vector2(a, b + 1), v3);
                    Clip(polygon, new Vector2(a, b + 1), v3, new Vector2(a, b), v0);
                    if (polygon.Count < 3)
                        continue;

                    int start = vertices.Count;
                    foreach (var p in polygon)
                    {
                        float u = p.x / r, v = p.y / r;
                        float level = Mathf.Lerp(Mathf.Lerp(l00, l10, u), Mathf.Lerp(l01, l11, u), v);
                        float x = (cellPos.x + u) * cell, z = (cellPos.y + v) * cell;
                        vertices.Add(new Vector3(x, level * lh + lift, z));
                        uvs.Add(new Vector2(x, z) * 0.5f);
                    }
                    // Yukarıdan bakınca saat yönünde (ön yüz)
                    for (int k = 1; k < polygon.Count - 1; k++)
                    {
                        triangles.Add(start);
                        triangles.Add(start + k + 1);
                        triangles.Add(start + k);
                    }
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        /// <summary>Kare kenarını (from→to) eşik çizgisiyle kırpar: içerideki köşeyi ve eşik geçiş noktasını ekler.</summary>
        static void Clip(List<Vector2> polygon, Vector2 from, float dFrom, Vector2 to, float dTo)
        {
            if (dFrom >= 0f)
                polygon.Add(from);
            if ((dFrom >= 0f) != (dTo >= 0f))
            {
                float t = dFrom / (dFrom - dTo);
                polygon.Add(Vector2.Lerp(from, to, t));
            }
        }

        static float Hash01(int x, int z, int layer)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(z * 19349663) ^ (uint)(layer * 83492791);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFF) / 65535f;
            }
        }
    }
}
