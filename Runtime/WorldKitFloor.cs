using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorldKit
{
    /// <summary>
    /// Zemini tek tek objeler yerine hücre verisi olarak tutar ve hepsini tek bir mesh'e çevirir.
    /// Her hücrenin bir yüksekliği (seviye), bir türü (çimen, taş...) ve isteğe bağlı bir eğimi (rampa) vardır;
    /// yalnızca görünen yüzler üretilir.
    ///
    /// Rampa: Eğimli hücrenin alçak kenarı kendi yüksekliğinde, yüksek kenarı bir kat yukarıdadır.
    /// Eğim yönü <see cref="Slope"/> ile belirtilir.
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
        /// <summary>Rampa yönü: hücre bu yöne doğru bir kat yükselir.</summary>
        public enum Slope { None = 0, PositiveX = 1, NegativeX = 2, PositiveZ = 3, NegativeZ = 4 }

        /// <summary>
        /// Rampanın hangi parçası: Full = tek hücrede bir kat, Lower / Upper = iki hücreye yayılan yumuşak rampanın
        /// alt ve üst yarısı (her biri yarım kat yükselir). İki yarıda da Height alt kattır.
        /// </summary>
        public enum RampPart { Full = 0, Lower = 1, Upper = 2 }

        [Tooltip("Bir hücrenin kenar uzunluğu (m).")]
        public float cellSize = 1f;
        [Tooltip("Bir seviyenin yüksekliği (m).")]
        public float levelHeight = 1f;
        [Tooltip("Zemin plakasının kalınlığı (m).")]
        public float thickness = 0.2f;

        [Header("Kenar (World Kit ayarlarından doldurulur)")]
        [Tooltip("Yumuşak kenar: kat sınırları yuvarlatılır (dış köşe yuvarlak, iç köşe kavisli). Kapalıysa köşeli grid.")]
        public bool smoothEdges;
        [Tooltip("Köşe yuvarlaklığı (hücre). 0.5 = yarım hücre; 1 ve üstü çapraz (zikzak) kenarları düz eğriye çevirir.")]
        [Range(0.05f, 1.5f)] public float edgeRadius = 1f;
        [Tooltip("Hücre başına kenar örneği (yüksek = daha pürüzsüz eğri, daha çok üçgen).")]
        [Range(2, 8)] public int edgeResolution = 4;
        [Tooltip("Üst kenardan dışarı taşan dudak / çimen saçağı genişliği (m). 0 = yok.")]
        [Min(0f)] public float edgeLip;

        // x, z = hücre; y = yükseklik (seviye). Hücre (x, z), [x, x+1] x [z, z+1] hücre alanını kaplar.
        [SerializeField, HideInInspector] List<Vector3Int> m_Cells = new List<Vector3Int>();
        // m_Cells ile aynı sırada hücre türleri. Eski kayıtlarda boştur (hepsi tür 0).
        [SerializeField, HideInInspector] List<int> m_Types = new List<int>();
        // m_Cells ile aynı sırada eğimler: Slope + 8 × RampPart. Eski kayıtlarda boştur (hepsi düz).
        [SerializeField, HideInInspector] List<int> m_Slopes = new List<int>();

        // Tür başına yüzey kayması (m), ör. su komşularından biraz alçakta. İndeks = tür. Ayarlardan doldurulur.
        [SerializeField, HideInInspector] List<float> m_TypeOffsets = new List<float>();

        const int PartStride = 8;

        struct Cell
        {
            public int Height;
            public int Type;
            public Slope Slope;
            public RampPart Part;
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

        // ---------------- Sorgular ----------------

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

        public bool TryGetSlope(Vector2Int cell, out Slope slope)
        {
            bool found = m_Map.TryGetValue(cell, out var data);
            slope = data.Slope;
            return found;
        }

        /// <summary>Rampanın parçası (düz hücrelerde Full).</summary>
        public bool TryGetRampPart(Vector2Int cell, out RampPart part)
        {
            bool found = m_Map.TryGetValue(cell, out var data);
            part = data.Part;
            return found;
        }

        /// <summary>Zemin mesh'i yeniden kurulduğunda (hücre, yükseklik, görünür kat değişince). Yüzey boyası bunu dinler.</summary>
        public event System.Action Rebuilt;

        /// <summary>Kenar stilini ayarlar; değiştiyse mesh yeniden kurulur. World Kit bunu ayarlardan çağırır.</summary>
        public void SetEdgeStyle(bool smooth, float radius, int resolution, float lip)
        {
            radius = Mathf.Clamp(radius, 0.05f, 1.5f);
            resolution = Mathf.Clamp(resolution, 2, 8);
            lip = Mathf.Max(0f, lip);
            if (smoothEdges == smooth && Mathf.Approximately(edgeRadius, radius) && edgeResolution == resolution && Mathf.Approximately(edgeLip, lip))
                return;
            smoothEdges = smooth;
            edgeRadius = radius;
            edgeResolution = resolution;
            edgeLip = lip;
            BuildMesh();
        }

        /// <summary>Zemini olan bütün hücreler.</summary>
        public IEnumerable<Vector2Int> Cells => m_Map.Keys;

        /// <summary>
        /// Hücrenin bir köşesinin üst yüksekliği (seviye cinsinden, rampalarda köşeye göre değişir).
        /// Köşe (cx, cz) ∈ {0,1}²: 0 = hücrenin küçük kenarı, 1 = büyük kenarı.
        /// </summary>
        public bool TryGetCornerLevel(Vector2Int cell, int cx, int cz, out float level)
        {
            bool found = m_Map.TryGetValue(cell, out var data);
            level = found ? CornerLevel(data, cx, cz) : 0f;
            return found;
        }

        // ---------------- Düzenleme ----------------

        /// <summary>Hücreleri verilen yükseklikte (düz) doldurur; var olan hücrelerin türü korunur, yenileri tür 0 olur.</summary>
        public void SetCells(IEnumerable<Vector2Int> cells, int height)
        {
            foreach (var cell in cells)
            {
                m_Map.TryGetValue(cell, out var data);
                m_Map[cell] = new Cell { Height = height, Type = data.Type };
            }
            WriteBack();
        }

        /// <summary>Hücreleri verilen yükseklik ve türle (düz) doldurur.</summary>
        public void SetCells(IEnumerable<Vector2Int> cells, int height, int type)
        {
            foreach (var cell in cells)
                m_Map[cell] = new Cell { Height = height, Type = Mathf.Max(0, type) };
            WriteBack();
        }

        /// <summary>Yalnızca var olan hücrelerin türünü değiştirir; yükseklik ve eğim korunur, boş hücreler boş kalır.</summary>
        public void PaintType(IEnumerable<Vector2Int> cells, int type)
        {
            foreach (var cell in cells)
            {
                if (m_Map.TryGetValue(cell, out var data))
                {
                    data.Type = Mathf.Max(0, type);
                    m_Map[cell] = data;
                }
            }
            WriteBack();
        }

        /// <summary>Var olan hücrelerin yüksekliğini delta kadar değiştirir (sınırlar içinde); eğim korunur.</summary>
        public void AdjustHeight(IEnumerable<Vector2Int> cells, int delta, int minHeight, int maxHeight)
        {
            foreach (var cell in cells)
            {
                if (m_Map.TryGetValue(cell, out var data))
                {
                    data.Height = Mathf.Clamp(data.Height + delta, minHeight, maxHeight);
                    m_Map[cell] = data;
                }
            }
            WriteBack();
        }

        /// <summary>Var olan hücrelerin eğimini ayarlar (None = düzleştir).</summary>
        public void SetSlope(IEnumerable<Vector2Int> cells, Slope slope) => SetSlope(cells, slope, RampPart.Full);

        /// <summary>Var olan hücrelerin eğimini ve rampa parçasını ayarlar.</summary>
        public void SetSlope(IEnumerable<Vector2Int> cells, Slope slope, RampPart part)
        {
            foreach (var cell in cells)
            {
                if (m_Map.TryGetValue(cell, out var data))
                {
                    data.Slope = slope;
                    data.Part = slope == Slope.None ? RampPart.Full : part;
                    m_Map[cell] = data;
                }
            }
            WriteBack();
        }

        /// <summary>
        /// Yumuşak rampa: aynı yükseklikteki iki hücreyi, ilerideki bir kat yüksek komşuya doğru yarım kat + yarım kat
        /// eğimli yapar. Hücrelerden biri seçimde olması yeterli (diğeri komşusu). Yön kendiliğinden bulunur;
        /// preferAlongX true ise önce X yönüne bakılır. Rampa olan hücre sayısını döndürür.
        /// </summary>
        public int AutoSlopeGentle(IEnumerable<Vector2Int> cells, bool preferAlongX)
        {
            var order = preferAlongX
                ? new[] { Slope.PositiveX, Slope.NegativeX, Slope.PositiveZ, Slope.NegativeZ }
                : new[] { Slope.PositiveZ, Slope.NegativeZ, Slope.PositiveX, Slope.NegativeX };

            var done = new HashSet<Vector2Int>();
            foreach (var cell in cells)
            {
                if (done.Contains(cell) || !m_Map.TryGetValue(cell, out var data))
                    continue;

                foreach (var slope in order)
                {
                    var dir = Direction(slope);
                    // Bu hücre alt yarı mı (önde aynı yükseklikte bir hücre, onun önünde bir kat yüksek)
                    // yoksa üst yarı mı (önde bir kat yüksek, arkada aynı yükseklikte bir hücre)?
                    Vector2Int lower, upper;
                    if (IsHeight(cell + dir, data.Height) && IsHeight(cell + dir * 2, data.Height + 1))
                    {
                        lower = cell;
                        upper = cell + dir;
                    }
                    else if (IsHeight(cell + dir, data.Height + 1) && IsHeight(cell - dir, data.Height))
                    {
                        lower = cell - dir;
                        upper = cell;
                    }
                    else
                        continue;

                    if (done.Contains(lower) || done.Contains(upper))
                        continue;
                    // Rampanın arkası yüksekse (çukur) olmaz
                    if (m_Map.TryGetValue(lower - dir, out var behind) && behind.Height > data.Height)
                        continue;

                    SetPart(lower, slope, RampPart.Lower);
                    SetPart(upper, slope, RampPart.Upper);
                    done.Add(lower);
                    done.Add(upper);
                    break;
                }
            }
            WriteBack();
            return done.Count;
        }

        bool IsHeight(Vector2Int cell, int height) => m_Map.TryGetValue(cell, out var data) && data.Height == height;

        void SetPart(Vector2Int cell, Slope slope, RampPart part)
        {
            var data = m_Map[cell];
            data.Slope = slope;
            data.Part = part;
            m_Map[cell] = data;
        }

        /// <summary>
        /// Var olan hücreleri, yanlarındaki bir kat yüksek komşuya doğru rampa yapar. Yön kendiliğinden bulunur;
        /// preferAlongX true ise önce X yönüne bakılır. Uygun komşusu olmayan hücreler değişmez.
        /// Kaç hücrenin rampa olduğunu döndürür.
        /// </summary>
        public int AutoSlope(IEnumerable<Vector2Int> cells, bool preferAlongX)
        {
            var order = preferAlongX
                ? new[] { Slope.PositiveX, Slope.NegativeX, Slope.PositiveZ, Slope.NegativeZ }
                : new[] { Slope.PositiveZ, Slope.NegativeZ, Slope.PositiveX, Slope.NegativeX };

            int count = 0;
            foreach (var cell in cells)
            {
                if (!m_Map.TryGetValue(cell, out var data))
                    continue;

                foreach (var slope in order)
                {
                    var dir = Direction(slope);
                    bool higherAhead = m_Map.TryGetValue(cell + dir, out var ahead) && ahead.Height == data.Height + 1;
                    bool notHigherBehind = !m_Map.TryGetValue(cell - dir, out var behind) || behind.Height <= data.Height;
                    if (higherAhead && notHigherBehind)
                    {
                        data.Slope = slope;
                        data.Part = RampPart.Full;
                        m_Map[cell] = data;
                        count++;
                        break;
                    }
                }
            }
            WriteBack();
            return count;
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
            m_Slopes.Clear();
            foreach (var pair in m_Map)
            {
                m_Cells.Add(new Vector3Int(pair.Key.x, pair.Value.Height, pair.Key.y));
                m_Types.Add(pair.Value.Type);
                m_Slopes.Add((int)pair.Value.Slope + PartStride * (int)pair.Value.Part);
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
                m_Map[new Vector2Int(c.x, c.z)] = new Cell
                {
                    Height = c.y,
                    Type = i < m_Types.Count ? m_Types[i] : 0,
                    // 0.22.0'daki (geri alınan) köşe rampası kayıtları (büyük değerler) düz okunur
                    Slope = i < m_Slopes.Count && m_Slopes[i] < PartStride * 3 ? (Slope)(m_Slopes[i] % PartStride) : Slope.None,
                    Part = i < m_Slopes.Count && m_Slopes[i] < PartStride * 3 ? (RampPart)(m_Slopes[i] / PartStride) : RampPart.Full
                };
            }
            BuildMesh();
        }

        // ---------------- Mesh ----------------

        static readonly Vector2Int[] k_Neighbours =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        /// <summary>En yakın eksen yönüne karşılık gelen eğim (sıfır vektör = None).</summary>
        public static Slope SlopeFromDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.01f)
                return Slope.None;
            if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.z))
                return direction.x > 0f ? Slope.PositiveX : Slope.NegativeX;
            return direction.z > 0f ? Slope.PositiveZ : Slope.NegativeZ;
        }

        /// <summary>Eğimin yükseldiği yön (hücre cinsinden).</summary>
        public static Vector2Int Direction(Slope slope)
        {
            switch (slope)
            {
                case Slope.PositiveX: return new Vector2Int(1, 0);
                case Slope.NegativeX: return new Vector2Int(-1, 0);
                case Slope.PositiveZ: return new Vector2Int(0, 1);
                case Slope.NegativeZ: return new Vector2Int(0, -1);
                default: return Vector2Int.zero;
            }
        }

        static int SubMeshOf(Cell cell) => cell.Type > 0 ? 1 + cell.Type : (cell.Height > 0 ? 1 : 0);

        /// <summary>
        /// Hücrenin bir köşesinin üst yüksekliği (seviye cinsinden). Köşe (cx, cz) ∈ {0,1}²: hücre içindeki konum.
        /// </summary>
        float CornerLevel(Cell cell, int cx, int cz) => SlopedLevel(cell, cx, cz) + TypeOffset(cell.Type) / Mathf.Max(0.01f, levelHeight);

        static float SlopedLevel(Cell cell, int cx, int cz)
        {
            // t: köşenin eğim yönündeki konumu (0 = alçak kenar, 1 = yüksek kenar)
            float t;
            switch (cell.Slope)
            {
                case Slope.PositiveX: t = cx; break;
                case Slope.NegativeX: t = 1 - cx; break;
                case Slope.PositiveZ: t = cz; break;
                case Slope.NegativeZ: t = 1 - cz; break;
                default: return cell.Height;
            }

            switch (cell.Part)
            {
                case RampPart.Lower: return cell.Height + 0.5f * t;
                case RampPart.Upper: return cell.Height + 0.5f + 0.5f * t;
                default: return cell.Height + t;
            }
        }

        float TypeOffset(int type) => type >= 0 && type < m_TypeOffsets.Count ? m_TypeOffsets[type] : 0f;

        /// <summary>Tür başına yüzey kayması (m). İndeks = tür (0 = varsayılan).</summary>
        public IReadOnlyList<float> TypeOffsets => m_TypeOffsets;

        /// <summary>
        /// Tür başına yüzey kaymalarını ayarlar (m; ör. su -0.3). Değiştiyse mesh yeniden kurulur.
        /// World Kit bunu proje ayarlarındaki zemin türlerinden kendisi doldurur.
        /// </summary>
        public void SetTypeOffsets(IList<float> offsets)
        {
            bool same = offsets.Count == m_TypeOffsets.Count;
            for (int i = 0; same && i < offsets.Count; i++)
                same = Mathf.Approximately(offsets[i], m_TypeOffsets[i]);
            if (same)
                return;
            m_TypeOffsets.Clear();
            m_TypeOffsets.AddRange(offsets);
            BuildMesh();
        }

        void BuildMesh()
        {
            if (m_Mesh == null)
                m_Mesh = new Mesh { name = "WorldKitFloor", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };

            float size = CellSize;
            float lh = levelHeight;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var subMeshes = new List<List<int>> { new List<int>(), new List<int>() };

            foreach (var pair in m_Map)
            {
                var cell = pair.Key;
                var data = pair.Value;
                if (data.Height > m_MaxVisibleLevel)
                    continue;

                int index = SubMeshOf(data);
                while (subMeshes.Count <= index)
                    subMeshes.Add(new List<int>());
                var triangles = subMeshes[index];

                // Yumuşak kenar: düz hücreler eğriyle üretilir (rampalı hücreler aşağıdaki köşeli yolla)
                if (smoothEdges && data.Slope == Slope.None)
                {
                    BuildSmoothCell(cell, data, vertices, uvs, subMeshes);
                    continue;
                }

                float x0 = cell.x * size, x1 = x0 + size;
                float z0 = cell.y * size, z1 = z0 + size;

                // Üst yüz (eğimliyse köşe yükseklikleri farklı)
                float t00 = CornerLevel(data, 0, 0) * lh, t01 = CornerLevel(data, 0, 1) * lh;
                float t11 = CornerLevel(data, 1, 1) * lh, t10 = CornerLevel(data, 1, 0) * lh;
                AddQuad(vertices, uvs, triangles,
                    new Vector3(x0, t00, z0), new Vector3(x0, t01, z1), new Vector3(x1, t11, z1), new Vector3(x1, t10, z0),
                    Vector3.up, new Vector2(x0, z0), new Vector2(x0, z1), new Vector2(x1, z1), new Vector2(x1, z0));

                AddSideFaces(cell, data, vertices, uvs, triangles, false);
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
            Rebuilt?.Invoke();
        }

        /// <summary>
        /// Hücrenin köşeli yan yüzleri: her kenarda iki uç için "komşunun üstü (veya taban) → benim üstüm".
        /// smoothOnly: yumuşak kenar modunda yalnızca eğriyle üretilmeyen kenarlar (rampalı komşu, aynı kattaki tür farkı).
        /// </summary>
        void AddSideFaces(Vector2Int cell, Cell data, List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, bool smoothOnly)
        {
            float size = CellSize;
            float lh = levelHeight;
            float x0 = cell.x * size, z0 = cell.y * size;
            float bottom = Mathf.Min(data.Height, 0) * lh - thickness;

            foreach (var offset in k_Neighbours)
            {
                bool hasNeighbour = m_Map.TryGetValue(cell + offset, out var neighbour) && neighbour.Height <= m_MaxVisibleLevel;
                if (smoothOnly && (!hasNeighbour || (neighbour.Slope == Slope.None && neighbour.Height != data.Height)))
                    continue;

                // Kenarın iki ucu (hücre içi köşe koordinatları) ve komşudaki karşılıkları
                int ax, az, bx, bz;
                if (offset.x == 1) { ax = 1; az = 0; bx = 1; bz = 1; }
                else if (offset.x == -1) { ax = 0; az = 1; bx = 0; bz = 0; }
                else if (offset.y == 1) { ax = 1; az = 1; bx = 0; bz = 1; }
                else { ax = 0; az = 0; bx = 1; bz = 0; }

                float topA = CornerLevel(data, ax, az) * lh;
                float topB = CornerLevel(data, bx, bz) * lh;
                float lowA, lowB;
                if (hasNeighbour)
                {
                    // Komşunun karşı kenarındaki köşeler: X komşusunda cx ters, Z komşusunda cz ters.
                    lowA = CornerLevel(neighbour, offset.x != 0 ? 1 - ax : ax, offset.y != 0 ? 1 - az : az) * lh;
                    lowB = CornerLevel(neighbour, offset.x != 0 ? 1 - bx : bx, offset.y != 0 ? 1 - bz : bz) * lh;
                }
                else
                {
                    lowA = lowB = bottom;
                }

                lowA = Mathf.Min(lowA, topA);
                lowB = Mathf.Min(lowB, topB);
                if (topA - lowA < 0.001f && topB - lowB < 0.001f)
                    continue;

                var a = new Vector3(x0 + ax * size, 0f, z0 + az * size);
                var b = new Vector3(x0 + bx * size, 0f, z0 + bz * size);
                var normal = new Vector3(offset.x, 0f, offset.y);
                float ua = offset.x != 0 ? a.z : a.x;
                float ub = offset.x != 0 ? b.z : b.x;

                AddQuad(vertices, uvs, triangles,
                    new Vector3(a.x, lowA, a.z), new Vector3(a.x, topA, a.z), new Vector3(b.x, topB, b.z), new Vector3(b.x, lowB, b.z),
                    normal, new Vector2(ua, lowA), new Vector2(ua, topA), new Vector2(ub, topB), new Vector2(ub, lowB));
            }
        }

        // ---------------- Yumuşak kenar ----------------

        // 0.5'in biraz üstü: hücre sınırındaki örnek (tam 0.5) dışarıda sayılır, böylece kenar eğrisi ve duvarı her zaman
        // yüksek tarafın hücresinde üretilir (harita kenarında da).
        const float SmoothThreshold = 0.5001f;

        /// <summary>Duvarların alt-mesh'i: tür alt-mesh'lerinden sonra gelir (World Kit buraya uçurum materyalini koyar).</summary>
        int CliffSubMesh => m_TypeOffsets.Count > 0 ? 1 + m_TypeOffsets.Count : 1;

        /// <summary>Hücre k katında "dolu" mu (yüksekliği k veya üstü)? Gizli üst katlar ve olmayan hücreler boş.</summary>
        bool Occupied(Vector2Int cell, int level) =>
            m_Map.TryGetValue(cell, out var d) && d.Height <= m_MaxVisibleLevel
            // Rampa bir üst kata çıkar: alanda o katı da doldurur (platformla rampa arasında duvar oluşmaz)
            && d.Height + (d.Slope != Slope.None ? 1 : 0) >= level;

        /// <summary>
        /// k katı alanının p noktasındaki kapsaması (0-1): p çevresindeki (2r × 2r) karenin dolu hücrelerle örtüşen oranı.
        /// 0.5 eşiği düz kenarda tam hücre sınırından geçer; dış köşede içeri, iç köşede dışarı kavis yapar.
        /// p hücre birimindedir.
        /// </summary>
        float Coverage(Vector2 p, int level, float r)
        {
            float x0 = p.x - r, x1 = p.x + r, z0 = p.y - r, z1 = p.y + r;
            float covered = 0f;
            for (int cx = Mathf.FloorToInt(x0); cx < Mathf.CeilToInt(x1); cx++)
            for (int cz = Mathf.FloorToInt(z0); cz < Mathf.CeilToInt(z1); cz++)
            {
                if (!Occupied(new Vector2Int(cx, cz), level))
                    continue;
                float w = Mathf.Min(x1, cx + 1) - Mathf.Max(x0, cx);
                float h = Mathf.Min(z1, cz + 1) - Mathf.Max(z0, cz);
                if (w > 0f && h > 0f)
                    covered += w * h;
            }
            return covered / (4f * r * r);
        }

        void BuildSmoothCell(Vector2Int cell, Cell data, List<Vector3> vertices, List<Vector2> uvs, List<List<int>> subMeshes)
        {
            float size = CellSize;
            float lh = levelHeight;
            int own = SubMeshOf(data);

            // Komşuluk: 3×3 pencerede yükseklikler. Hepsi aynı ve rampasızsa hızlı yol (tek dörtgen).
            int minH = data.Height, maxH = data.Height;
            bool uniform = true;
            int window = Mathf.CeilToInt(Mathf.Clamp(edgeRadius, 0.05f, 1.5f));  // yarıçapa göre etkilenen komşuluk
            for (int dx = -window; dx <= window; dx++)
            for (int dz = -window; dz <= window; dz++)
            {
                if (dx == 0 && dz == 0)
                    continue;
                if (m_Map.TryGetValue(new Vector2Int(cell.x + dx, cell.y + dz), out var n) && n.Height <= m_MaxVisibleLevel)
                {
                    minH = Mathf.Min(minH, n.Height);
                    maxH = Mathf.Max(maxH, n.Height);
                    if (n.Height != data.Height || n.Slope != Slope.None || !Mathf.Approximately(TypeOffset(n.Type), TypeOffset(data.Type)))
                        uniform = false;
                }
                else
                    uniform = false;
            }

            float ownOffset = TypeOffset(data.Type);
            if (uniform)
            {
                float y = data.Height * lh + ownOffset;
                float x0 = cell.x * size, z0 = cell.y * size;
                AddQuad(vertices, uvs, subMeshes[own],
                    new Vector3(x0, y, z0), new Vector3(x0, y, z0 + size), new Vector3(x0 + size, y, z0 + size), new Vector3(x0 + size, y, z0),
                    Vector3.up, new Vector2(x0, z0), new Vector2(x0, z0 + size), new Vector2(x0 + size, z0 + size), new Vector2(x0 + size, z0));
                return;
            }

            // Eğriyle üretilmeyen kenarlar (rampalı komşu, aynı katta tür/su farkı) köşeli kalır
            AddSideFaces(cell, data, vertices, uvs, subMeshes[own], true);

            int lowLevel = Mathf.Min(minH, Mathf.Min(data.Height, 0));
            int highLevel = maxH;
            int res = Mathf.Clamp(edgeResolution, 2, 8);
            float r = Mathf.Clamp(edgeRadius, 0.05f, 1.5f);
            int levels = highLevel - lowLevel + 2;   // f[L] için L = lowLevel .. highLevel+1
            int stride = res + 1;

            // Örnek değerleri: f[L][j * stride + i], örnek (cell + i/res, cell + j/res)
            var field = new float[levels][];
            for (int l = 0; l < levels; l++)
            {
                int level = lowLevel + l;
                var values = new float[stride * stride];
                for (int j = 0; j <= res; j++)
                for (int i = 0; i <= res; i++)
                    values[j * stride + i] = level > highLevel ? 0f : Coverage(new Vector2(cell.x + i / (float)res, cell.y + j / (float)res), level, r);
                field[l] = values;
            }

            int cliff = CliffSubMesh;
            while (subMeshes.Count <= cliff)
                subMeshes.Add(new List<int>());

            var poly = new List<Vector2>(8);
            var clipped = new List<Vector2>(8);
            for (int b = 0; b < res; b++)
            for (int a = 0; a < res; a++)
            {
                // Alt karenin köşeleri (hücre içi 0..res): sol-alt, sağ-alt, sağ-üst, sol-üst
                var c0 = new Vector2(a, b); var c1 = new Vector2(a + 1, b); var c2 = new Vector2(a + 1, b + 1); var c3 = new Vector2(a, b + 1);
                int k0 = b * stride + a, k1 = b * stride + a + 1, k2 = (b + 1) * stride + a + 1, k3 = (b + 1) * stride + a;

                for (int l = 0; l < levels - 1; l++)
                {
                    int level = lowLevel + l;
                    var f = field[l];
                    var up = field[l + 1];
                    if (f[k0] < SmoothThreshold && f[k1] < SmoothThreshold && f[k2] < SmoothThreshold && f[k3] < SmoothThreshold)
                        continue;

                    // ---- Üst yüz: f_L ≥ eşik ve f_(L+1) < eşik olan bölge ----
                    poly.Clear();
                    ClipSquare(poly, c0, f[k0], c1, f[k1], c2, f[k2], c3, f[k3]);
                    if (poly.Count >= 3)
                    {
                        clipped.Clear();
                        ClipBelow(poly, clipped, p => Bilinear(up, k0, k1, k2, k3, p.x - a, p.y - b));
                        if (clipped.Count >= 3)
                        {
                            var (sub, offset) = LevelOwner(cell, level, (a + 0.5f) / res, (b + 0.5f) / res, own, ownOffset);
                            float y = level * lh + offset;
                            int start = vertices.Count;
                            foreach (var p in clipped)
                            {
                                float x = (cell.x + p.x / res) * size, z = (cell.y + p.y / res) * size;
                                vertices.Add(new Vector3(x, y, z));
                                uvs.Add(new Vector2(x, z));
                            }
                            while (subMeshes.Count <= sub)
                                subMeshes.Add(new List<int>());
                            var tris = subMeshes[sub];
                            for (int t = 1; t < clipped.Count - 1; t++)
                            {
                                tris.Add(start); tris.Add(start + t + 1); tris.Add(start + t);
                            }
                        }
                    }

                    // ---- Duvar: f_L'nin eşik çizgisi, (L-1) → L arası (en alt katta tabana kadar) ----
                    if (level <= lowLevel - 1)
                        continue;
                    AddContourWalls(cell, level, f, k0, k1, k2, k3, c0, c1, c2, c3, res, lh, vertices, uvs, subMeshes[cliff], subMeshes, own, ownOffset);
                }
            }
        }

        /// <summary>Alt karenin eşik çizgisi parçalarını bulur ve her biri için duvar (ve dudak) ekler.</summary>
        void AddContourWalls(Vector2Int cell, int level, float[] f, int k0, int k1, int k2, int k3,
            Vector2 c0, Vector2 c1, Vector2 c2, Vector2 c3, int res, float lh,
            List<Vector3> vertices, List<Vector2> uvs, List<int> cliff, List<List<int>> subMeshes, int own, float ownOffset)
        {
            float v0 = f[k0] - SmoothThreshold, v1 = f[k1] - SmoothThreshold, v2 = f[k2] - SmoothThreshold, v3 = f[k3] - SmoothThreshold;
            int mask = (v0 >= 0 ? 1 : 0) | (v1 >= 0 ? 2 : 0) | (v2 >= 0 ? 4 : 0) | (v3 >= 0 ? 8 : 0);
            if (mask == 0 || mask == 15)
                return;

            Vector2 E(Vector2 p, float vp, Vector2 q, float vq) => Vector2.Lerp(p, q, vp / (vp - vq));
            var e0 = (v0 >= 0) != (v1 >= 0) ? E(c0, v0, c1, v1) : Vector2.zero; // alt kenar
            var e1 = (v1 >= 0) != (v2 >= 0) ? E(c1, v1, c2, v2) : Vector2.zero; // sağ
            var e2 = (v2 >= 0) != (v3 >= 0) ? E(c2, v2, c3, v3) : Vector2.zero; // üst
            var e3 = (v3 >= 0) != (v0 >= 0) ? E(c3, v3, c0, v0) : Vector2.zero; // sol

            // İçe doğru (f'nin arttığı) yön: dışarısı bunun tersi
            var inward = new Vector2((v1 + v2) - (v0 + v3), (v2 + v3) - (v0 + v1));

            void Segment(Vector2 p, Vector2 q) => AddWall(cell, level, p, q, inward, res, lh, vertices, uvs, cliff, subMeshes, own, ownOffset);

            switch (mask)
            {
                case 1: case 14: Segment(e3, e0); break;
                case 2: case 13: Segment(e0, e1); break;
                case 3: case 12: Segment(e3, e1); break;
                case 4: case 11: Segment(e1, e2); break;
                case 6: case 9: Segment(e0, e2); break;
                case 7: case 8: Segment(e3, e2); break;
                case 5: Segment(e3, e2); Segment(e0, e1); break;   // eyer: iki ayrı köşe
                case 10: Segment(e3, e0); Segment(e1, e2); break;
            }
        }

        void AddWall(Vector2Int cell, int level, Vector2 p, Vector2 q, Vector2 inward, int res, float lh,
            List<Vector3> vertices, List<Vector2> uvs, List<int> cliff, List<List<int>> subMeshes, int own, float ownOffset)
        {
            float size = CellSize;
            var a = new Vector3((cell.x + p.x / res) * size, 0f, (cell.y + p.y / res) * size);
            var b = new Vector3((cell.x + q.x / res) * size, 0f, (cell.y + q.y / res) * size);
            if ((a - b).sqrMagnitude < 1e-8f)
                return;

            // Dışa bakan normal: parçaya dik, içe yönün tersi
            var along = (b - a).normalized;
            var normal = new Vector3(along.z, 0f, -along.x);
            if (normal.x * inward.x + normal.z * inward.y > 0f)
                normal = -normal;

            // Duvarın üstü bu katın yüzeyi; altı bir alt kat (dışarısı boşsa ve en alt kattaysa zemin tabanı)
            var mid = (a + b) * 0.5f;
            var outside = mid + normal * (size * 0.02f);
            var inside = mid - normal * (size * 0.5f / res);
            var insideCell = new Vector2Int(Mathf.FloorToInt(inside.x / size), Mathf.FloorToInt(inside.z / size));
            float topOffset = m_Map.TryGetValue(insideCell, out var insideData) && insideData.Height == level ? TypeOffset(insideData.Type) : 0f;

            float top = level * lh + topOffset;
            // Alt: dışarıda bir alt kat yüzeyi varsa ya da altında başka bir duvar sırası gelecekse bir kat aşağı;
            // en alt sırada (harita kenarı, yuvarlatılmış köşe) zemin tabanına kadar.
            var outsideCells = new Vector2(outside.x / size, outside.z / size);
            float radius = Mathf.Clamp(edgeRadius, 0.05f, 1.5f);
            // Bir alt katın alanı dışarıda bu katınkinden fazlaysa orada gerçek bir alt yüzey var
            bool lowerSurface = Coverage(outsideCells, level - 1, radius) > Coverage(outsideCells, level, radius) + 0.01f;
            bool stacked = level > Mathf.Min(0, insideData.Height);
            float bottom = lowerSurface || stacked ? (level - 1) * lh : level * lh - thickness;
            if (top - bottom < 0.001f)
                return;

            float ua = a.x + a.z, ub = b.x + b.z;
            AddQuad(vertices, uvs, cliff,
                new Vector3(a.x, bottom, a.z), new Vector3(a.x, top, a.z), new Vector3(b.x, top, b.z), new Vector3(b.x, bottom, b.z),
                normal, new Vector2(ua, bottom), new Vector2(ua, top), new Vector2(ub, top), new Vector2(ub, bottom));

            // Dudak / çimen saçağı: üst kenardan dışarı taşan ince şerit (üstteki hücrenin materyaliyle)
            // Yalnızca gerçek üst kenarda (içerideki yüzey tam bu katta); üst üste binen uçurumun ortasında değil
            if (edgeLip > 0f && m_Map.TryGetValue(insideCell, out var lipCell) && lipCell.Height == level)
            {
                int lipSub = SubMeshOf(lipCell);
                while (subMeshes.Count <= lipSub)
                    subMeshes.Add(new List<int>());
                var lipTris = subMeshes[lipSub];
                float y = top + 0.02f;
                var o = normal * edgeLip;
                AddQuad(vertices, uvs, lipTris,
                    new Vector3(a.x, y, a.z), new Vector3(a.x + o.x, y, a.z + o.z), new Vector3(b.x + o.x, y, b.z + o.z), new Vector3(b.x, y, b.z),
                    Vector3.up, new Vector2(a.x, a.z), new Vector2(a.x + o.x, a.z + o.z), new Vector2(b.x + o.x, b.z + o.z), new Vector2(b.x, b.z));
                AddQuad(vertices, uvs, lipTris,
                    new Vector3(a.x + o.x, y - 0.08f, a.z + o.z), new Vector3(a.x + o.x, y, a.z + o.z), new Vector3(b.x + o.x, y, b.z + o.z), new Vector3(b.x + o.x, y - 0.08f, b.z + o.z),
                    normal, new Vector2(ua, y - 0.08f), new Vector2(ua, y), new Vector2(ub, y), new Vector2(ub, y - 0.08f));
            }
        }

        /// <summary>
        /// Bu alt karede L katı yüzeyi hangi hücreye ait (alt-mesh ve tür kayması)? Kendi katıysa kendisi; değilse
        /// penceredeki o kattaki en yakın hücre (iç köşe kavisi komşunun yüzeyidir).
        /// </summary>
        (int subMesh, float offset) LevelOwner(Vector2Int cell, int level, float u, float v, int own, float ownOffset)
        {
            if (m_Map.TryGetValue(cell, out var self) && self.Height == level)
                return (own, ownOffset);
            float best = float.MaxValue;
            Cell found = default;
            bool any = false;
            int window = Mathf.CeilToInt(Mathf.Clamp(edgeRadius, 0.05f, 1.5f));
            for (int dx = -window; dx <= window; dx++)
            for (int dz = -window; dz <= window; dz++)
            {
                if (!m_Map.TryGetValue(new Vector2Int(cell.x + dx, cell.y + dz), out var n) || n.Height != level)
                    continue;
                float d = (new Vector2(dx + 0.5f, dz + 0.5f) - new Vector2(u, v)).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    found = n;
                    any = true;
                }
            }
            return any ? (SubMeshOf(found), TypeOffset(found.Type)) : (own, ownOffset);
        }

        static float Bilinear(float[] f, int k0, int k1, int k2, int k3, float u, float v) =>
            Mathf.Lerp(Mathf.Lerp(f[k0], f[k1], u), Mathf.Lerp(f[k3], f[k2], u), v);

        /// <summary>Kareyi f ≥ eşik bölgesine kırpar (köşe değerleriyle doğrusal kesişim).</summary>
        static void ClipSquare(List<Vector2> poly, Vector2 c0, float f0, Vector2 c1, float f1, Vector2 c2, float f2, Vector2 c3, float f3)
        {
            void Edge(Vector2 p, float fp, Vector2 q, float fq)
            {
                float dp = fp - SmoothThreshold, dq = fq - SmoothThreshold;
                if (dp >= 0f)
                    poly.Add(p);
                if ((dp >= 0f) != (dq >= 0f))
                    poly.Add(Vector2.Lerp(p, q, dp / (dp - dq)));
            }
            Edge(c0, f0, c1, f1);
            Edge(c1, f1, c2, f2);
            Edge(c2, f2, c3, f3);
            Edge(c3, f3, c0, f0);
        }

        /// <summary>Çokgeni g &lt; eşik bölgesine kırpar (Sutherland–Hodgman; g köşelerde çift doğrusal).</summary>
        static void ClipBelow(List<Vector2> input, List<Vector2> output, System.Func<Vector2, float> g)
        {
            for (int i = 0; i < input.Count; i++)
            {
                var p = input[i];
                var q = input[(i + 1) % input.Count];
                float dp = SmoothThreshold - g(p), dq = SmoothThreshold - g(q);
                bool pin = dp > 0f, qin = dq > 0f;
                if (pin)
                    output.Add(p);
                if (pin != qin)
                    output.Add(Vector2.Lerp(p, q, dp / (dp - dq)));
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

            // Sarma yönünü, alanı sıfır olmayan üçgene göre seç (eğimli yan yüzlerde bir üçgen sıfır alanlı olabilir).
            var n1 = Vector3.Cross(b - a, c - a);
            var n2 = Vector3.Cross(c - a, d - a);
            var reference = n1.sqrMagnitude > n2.sqrMagnitude ? n1 : n2;
            if (Vector3.Dot(reference, normal) >= 0f)
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
