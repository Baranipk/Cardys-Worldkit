using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorldKit
{
    /// <summary>
    /// Zemini tek tek objeler yerine hücre verisi olarak tutar ve hepsini tek bir mesh'e çevirir.
    /// Her hücrenin bir yüksekliği (seviye) ve bir türü (çimen, taş...) vardır; yalnızca görünen yüzler üretilir.
    ///
    /// Alt-mesh düzeni (renderer materyalleri de bu sırayla atanır):
    ///   0: tür 0, yer seviyesi (yükseklik ≤ 0)
    ///   1: tür 0, yükseltilmiş (yükseklik > 0)
    ///   1 + t: tür t (t ≥ 1), her yükseklikte
    /// Ölçüler oluşturulurken proje ayarlarından (World Kit Settings) kopyalanır.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class WorldKitFloor : MonoBehaviour
    {
        [Tooltip("Bir hücrenin kenar uzunluğu (m).")]
        public float cellSize = 1f;
        [Tooltip("Bir seviyenin yüksekliği (m).")]
        public float levelHeight = 1f;
        [Tooltip("Zemin plakasının kalınlığı (m).")]
        public float thickness = 0.2f;

        // x, z = hücre; y = yükseklik (seviye). Hücre (x, z), [x, x+1] x [z, z+1] hücre alanını kaplar.
        [SerializeField, HideInInspector] List<Vector3Int> m_Cells = new List<Vector3Int>();
        // m_Cells ile aynı sırada hücre türleri. Eski kayıtlarda boştur (hepsi tür 0).
        [SerializeField, HideInInspector] List<int> m_Types = new List<int>();

        struct Cell
        {
            public int Height;
            public int Type;
        }

        readonly Dictionary<Vector2Int, Cell> m_Map = new Dictionary<Vector2Int, Cell>();
        Mesh m_Mesh;

        float CellSize => Mathf.Max(0.01f, cellSize);

        public int CellCount => m_Cells.Count;

        /// <summary>Mesh'teki alt-mesh sayısı; renderer'da en az bu kadar materyal olmalı.</summary>
        public int SubMeshCount { get; private set; } = 2;

        // Editörde "üst katları gizle" için. Kaydedilmez; domain reload ve Play'de sıfırlanır.
        [System.NonSerialized] int m_MaxVisibleLevel = int.MaxValue;

        /// <summary>Bu seviyeden yüksek hücreler çizilmez (yalnızca görünüm; veri değişmez).</summary>
        public int MaxVisibleLevel
        {
            get => m_MaxVisibleLevel;
            set
            {
                if (m_MaxVisibleLevel == value)
                    return;
                m_MaxVisibleLevel = value;
                BuildMesh();
            }
        }

        void OnValidate()
        {
            if (isActiveAndEnabled)
                Rebuild();
        }

        void OnEnable() => Rebuild();

        void OnDisable()
        {
            if (m_Mesh == null)
                return;

            if (Application.isPlaying)
                Destroy(m_Mesh);
            else
                DestroyImmediate(m_Mesh);
            m_Mesh = null;
        }

        public bool TryGetHeight(Vector2Int cell, out int height)
        {
            bool found = m_Map.TryGetValue(cell, out var data);
            height = data.Height;
            return found;
        }

        public bool TryGetType(Vector2Int cell, out int type)
        {
            bool found = m_Map.TryGetValue(cell, out var data);
            type = data.Type;
            return found;
        }

        /// <summary>Hücreleri verilen yükseklikte doldurur; var olan hücrelerin türü korunur, yenileri tür 0 olur.</summary>
        public void SetCells(IEnumerable<Vector2Int> cells, int height)
        {
            foreach (var cell in cells)
            {
                m_Map.TryGetValue(cell, out var data);
                m_Map[cell] = new Cell { Height = height, Type = data.Type };
            }
            WriteBack();
        }

        /// <summary>Hücreleri verilen yükseklik ve türle doldurur.</summary>
        public void SetCells(IEnumerable<Vector2Int> cells, int height, int type)
        {
            foreach (var cell in cells)
                m_Map[cell] = new Cell { Height = height, Type = Mathf.Max(0, type) };
            WriteBack();
        }

        /// <summary>Yalnızca var olan hücrelerin türünü değiştirir; yükseklik korunur, boş hücreler boş kalır.</summary>
        public void PaintType(IEnumerable<Vector2Int> cells, int type)
        {
            foreach (var cell in cells)
            {
                if (m_Map.TryGetValue(cell, out var data))
                    m_Map[cell] = new Cell { Height = data.Height, Type = Mathf.Max(0, type) };
            }
            WriteBack();
        }

        public void RemoveCells(IEnumerable<Vector2Int> cells)
        {
            foreach (var cell in cells)
                m_Map.Remove(cell);
            WriteBack();
        }

        void WriteBack()
        {
            m_Cells.Clear();
            m_Types.Clear();
            foreach (var pair in m_Map)
            {
                m_Cells.Add(new Vector3Int(pair.Key.x, pair.Value.Height, pair.Key.y));
                m_Types.Add(pair.Value.Type);
            }
            BuildMesh();
        }

        /// <summary>Serileştirilmiş veriden haritayı ve mesh'i yeniden kurar (Undo sonrası da çağrılır).</summary>
        public void Rebuild()
        {
            m_Map.Clear();
            for (int i = 0; i < m_Cells.Count; i++)
            {
                var c = m_Cells[i];
                int type = i < m_Types.Count ? m_Types[i] : 0;
                m_Map[new Vector2Int(c.x, c.z)] = new Cell { Height = c.y, Type = type };
            }
            BuildMesh();
        }

        static readonly Vector2Int[] k_Neighbours =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        static int SubMeshOf(Cell cell) => cell.Type > 0 ? 1 + cell.Type : (cell.Height > 0 ? 1 : 0);

        void BuildMesh()
        {
            if (m_Mesh == null)
                m_Mesh = new Mesh { name = "WorldKitFloor", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };

            float cellSize = CellSize;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var subMeshes = new List<List<int>> { new List<int>(), new List<int>() };

            foreach (var pair in m_Map)
            {
                var cell = pair.Key;
                var data = pair.Value;
                int height = data.Height;
                if (height > m_MaxVisibleLevel)
                    continue;

                int index = SubMeshOf(data);
                while (subMeshes.Count <= index)
                    subMeshes.Add(new List<int>());
                var triangles = subMeshes[index];

                float x0 = cell.x * cellSize, x1 = x0 + cellSize;
                float z0 = cell.y * cellSize, z1 = z0 + cellSize;
                float top = height * levelHeight;
                float bottom = Mathf.Min(height, 0) * levelHeight - thickness;

                AddQuad(vertices, uvs, triangles,
                    new Vector3(x0, top, z0), new Vector3(x0, top, z1), new Vector3(x1, top, z1), new Vector3(x1, top, z0),
                    Vector3.up, new Vector2(x0, z0), new Vector2(x0, z1), new Vector2(x1, z1), new Vector2(x1, z0));

                foreach (var offset in k_Neighbours)
                {
                    float low = m_Map.TryGetValue(cell + offset, out var neighbour) ? neighbour.Height * levelHeight : bottom;
                    if (low >= top)
                        continue;

                    // Komşuya bakan kenar
                    Vector3 a, b;
                    if (offset.x == 1) { a = new Vector3(x1, 0f, z0); b = new Vector3(x1, 0f, z1); }
                    else if (offset.x == -1) { a = new Vector3(x0, 0f, z1); b = new Vector3(x0, 0f, z0); }
                    else if (offset.y == 1) { a = new Vector3(x1, 0f, z1); b = new Vector3(x0, 0f, z1); }
                    else { a = new Vector3(x0, 0f, z0); b = new Vector3(x1, 0f, z0); }

                    var normal = new Vector3(offset.x, 0f, offset.y);
                    float ua = offset.x != 0 ? a.z : a.x;
                    float ub = offset.x != 0 ? b.z : b.x;

                    AddQuad(vertices, uvs, triangles,
                        new Vector3(a.x, low, a.z), new Vector3(a.x, top, a.z), new Vector3(b.x, top, b.z), new Vector3(b.x, low, b.z),
                        normal, new Vector2(ua, low), new Vector2(ua, top), new Vector2(ub, top), new Vector2(ub, low));
                }
            }

            m_Mesh.Clear();
            m_Mesh.SetVertices(vertices);
            m_Mesh.SetUVs(0, uvs);
            m_Mesh.subMeshCount = subMeshes.Count;
            for (int i = 0; i < subMeshes.Count; i++)
                m_Mesh.SetTriangles(subMeshes[i], i);
            m_Mesh.RecalculateNormals();
            m_Mesh.RecalculateTangents();
            m_Mesh.RecalculateBounds();
            SubMeshCount = subMeshes.Count;

            GetComponent<MeshFilter>().sharedMesh = m_Mesh;
            if (TryGetComponent<MeshCollider>(out var meshCollider))
            {
                meshCollider.sharedMesh = null;
                meshCollider.sharedMesh = vertices.Count > 0 ? m_Mesh : null;
            }
        }

        /// <summary>Dörtgen ekler; sarma yönünü verilen normale göre kendisi ayarlar.</summary>
        static void AddQuad(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal,
            Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            uvs.Add(ua); uvs.Add(ub); uvs.Add(uc); uvs.Add(ud);

            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) >= 0f)
            {
                triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
                triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3);
            }
            else
            {
                triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 1);
                triangles.Add(i); triangles.Add(i + 3); triangles.Add(i + 2);
            }
        }
    }
}
