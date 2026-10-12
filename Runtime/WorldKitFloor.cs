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

        // x, z = hücre; y = yükseklik (seviye). Hücre (x, z), [x, x+1] x [z, z+1] hücre alanını kaplar.
        [SerializeField, HideInInspector] List<Vector3Int> m_Cells = new List<Vector3Int>();
        // m_Cells ile aynı sırada hücre türleri. Eski kayıtlarda boştur (hepsi tür 0).
        [SerializeField, HideInInspector] List<int> m_Types = new List<int>();
        // m_Cells ile aynı sırada eğimler: Slope + 8 × RampPart. Eski kayıtlarda boştur (hepsi düz).
        [SerializeField, HideInInspector] List<int> m_Slopes = new List<int>();

        // Tür başına yüzey kayması (m), ör. su komşularından biraz alçakta. İndeks = tür. Ayarlardan doldurulur.
        [SerializeField, HideInInspector] List<float> m_TypeOffsets = new List<float>();

        const int PartStride = 8;
        // m_Slopes içinde köşe modu: bu bit varsa alt 16 bit dört köşenin yükselişidir (her biri 0-12 = 0-1 kat).
        const int CornerFlag = 1 << 20;
        /// <summary>Köşe yükselişinin adım sayısı: 12 = bir kat (1, 2, 3, 4 ve 6 hücrelik rampalar tam bölünür).</summary>
        public const int CornerSteps = 12;

        struct Cell
        {
            public int Height;
            public int Type;
            public Slope Slope;
            public RampPart Part;
            // Köşe modu (0 = yok): 4 bitlik dört alan, sırasıyla köşe 00, 10, 01, 11 için 0-12 adım yükseliş.
            public int Corners;
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
                    data.Corners = 0;
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

        // ---------------- Köşe rampaları ----------------

        /// <summary>Köşe kodundan (cx, cz) köşesinin yükselişi (0-12 adım).</summary>
        public static int CornerNibble(int code, int cx, int cz) => (code >> (4 * (cx + 2 * cz))) & 0xF;

        /// <summary>Dört köşenin yükselişinden (0-12 adım; 00, 10, 01, 11 sırasıyla) köşe kodu.</summary>
        public static int EncodeCorners(int c00, int c10, int c01, int c11) =>
            Mathf.Clamp(c00, 0, CornerSteps) | Mathf.Clamp(c10, 0, CornerSteps) << 4 | Mathf.Clamp(c01, 0, CornerSteps) << 8 | Mathf.Clamp(c11, 0, CornerSteps) << 12;

        /// <summary>Hücrenin köşe kodu (köşe modunda değilse 0).</summary>
        public bool TryGetCornerCode(Vector2Int cell, out int code)
        {
            bool found = m_Map.TryGetValue(cell, out var data);
            code = data.Corners;
            return found;
        }

        /// <summary>Hücreyi köşe moduna alır (code 0 = düz). Eski yönlü eğimi siler.</summary>
        public void SetCornerCode(Vector2Int cell, int code)
        {
            if (!m_Map.TryGetValue(cell, out var data))
                return;
            data.Corners = code & 0xFFFF;
            data.Slope = Slope.None;
            data.Part = RampPart.Full;
            m_Map[cell] = data;
            WriteBack();
        }

        /// <summary>Hücrenin üst yüzü eğimli mi (yönlü rampa veya köşe modu)?</summary>
        public bool IsSloped(Vector2Int cell) =>
            m_Map.TryGetValue(cell, out var data) && (data.Slope != Slope.None || data.Corners != 0);

        /// <summary>
        /// Akıllı rampa: seçili hücreleri, bir kat yüksek komşu zemine doğru köşe köşe eğimlendirir. Her köşenin yüksekliği
        /// yüksek zemine uzaklığından hesaplanır; böylece rampa platformun köşelerinde döner (dış köşede tek köşe,
        /// iç köşede üç köşe yükselir) ve birden çok hücreye yayılabilir.
        /// width: rampanın kaç hücrede bir kat çıkacağı (1 = dik, 2-4 = yumuşak). rounded: dış köşeler yuvarlak
        /// (Öklid uzaklığı) ya da köşeli (Chebyshev). eased: yumuşak (S) profil. Eğimlenen hücre sayısını döndürür.
        /// </summary>
        public int AutoSlopeCorners(IEnumerable<Vector2Int> cells, int width, bool rounded, bool eased)
        {
            width = Mathf.Clamp(width, 1, 6);
            var selected = new HashSet<Vector2Int>();
            foreach (var c in cells)
                if (m_Map.ContainsKey(c))
                    selected.Add(c);

            // Köşe noktası p, h+1 yüksekliğindeki (seçim dışı) bir hücrenin köşesi mi?
            bool IsSource(Vector2Int p, int top)
            {
                for (int dx = -1; dx <= 0; dx++)
                for (int dz = -1; dz <= 0; dz++)
                {
                    var c = new Vector2Int(p.x + dx, p.y + dz);
                    if (!selected.Contains(c) && m_Map.TryGetValue(c, out var d) && d.Height == top && d.Slope == Slope.None && d.Corners == 0)
                        return true;
                }
                return false;
            }

            int count = 0;
            var codes = new Dictionary<Vector2Int, int>();
            foreach (var cell in selected)
            {
                int h = m_Map[cell].Height;
                var nibbles = new int[4];
                bool any = false;
                for (int i = 0; i < 4; i++)
                {
                    int cx = i & 1, cz = i >> 1;
                    var corner = new Vector2Int(cell.x + cx, cell.y + cz);
                    float best = float.MaxValue;
                    for (int qx = -width; qx <= width; qx++)
                    for (int qz = -width; qz <= width; qz++)
                    {
                        float d = rounded ? Mathf.Sqrt(qx * qx + qz * qz) : Mathf.Max(Mathf.Abs(qx), Mathf.Abs(qz));
                        if (d < best && d <= width && IsSource(new Vector2Int(corner.x + qx, corner.y + qz), h + 1))
                            best = d;
                    }
                    float t = best <= width ? 1f - best / width : 0f;
                    if (eased)
                        t = t * t * (3f - 2f * t);
                    nibbles[i] = Mathf.RoundToInt(t * CornerSteps);
                    any |= nibbles[i] > 0;
                }
                codes[cell] = any ? EncodeCorners(nibbles[0], nibbles[1], nibbles[2], nibbles[3]) : 0;
                if (any)
                    count++;
            }

            foreach (var pair in codes)
            {
                var data = m_Map[pair.Key];
                data.Corners = pair.Value;
                data.Slope = Slope.None;
                data.Part = RampPart.Full;
                m_Map[pair.Key] = data;
            }
            WriteBack();
            return count;
        }

        void SetPart(Vector2Int cell, Slope slope, RampPart part)
        {
            var data = m_Map[cell];
            data.Slope = slope;
            data.Part = part;
            data.Corners = 0;
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
                        data.Corners = 0;
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
                m_Slopes.Add(pair.Value.Corners != 0 ? CornerFlag | pair.Value.Corners : (int)pair.Value.Slope + PartStride * (int)pair.Value.Part);
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
                int raw = i < m_Slopes.Count ? m_Slopes[i] : 0;
                bool corners = (raw & CornerFlag) != 0;
                m_Map[new Vector2Int(c.x, c.z)] = new Cell
                {
                    Height = c.y,
                    Type = i < m_Types.Count ? m_Types[i] : 0,
                    Slope = corners ? Slope.None : (Slope)(raw % PartStride),
                    Part = corners ? RampPart.Full : (RampPart)(raw / PartStride),
                    Corners = corners ? raw & 0xFFFF : 0
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
            // Köşe modu: her köşenin kendi yükselişi (köşe rampaları, geniş rampalar)
            if (cell.Corners != 0)
                return cell.Height + CornerNibble(cell.Corners, cx, cz) / (float)CornerSteps;

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

                float x0 = cell.x * size, x1 = x0 + size;
                float z0 = cell.y * size, z1 = z0 + size;
                float bottom = Mathf.Min(data.Height, 0) * lh - thickness;

                // Üst yüz (eğimliyse köşe yükseklikleri farklı)
                float t00 = CornerLevel(data, 0, 0) * lh, t01 = CornerLevel(data, 0, 1) * lh;
                float t11 = CornerLevel(data, 1, 1) * lh, t10 = CornerLevel(data, 1, 0) * lh;
                // Köşe rampalarında dört köşe farklı olabilir: yüzeyi, uçları birbirine en yakın köşegenden böl
                // (dış köşede tek yüksek köşe, iç köşede tek alçak köşe düzgün üçgen olur).
                if (Mathf.Abs(t00 - t11) > Mathf.Abs(t01 - t10) + 0.0001f)
                    AddQuad(vertices, uvs, triangles,
                        new Vector3(x0, t01, z1), new Vector3(x1, t11, z1), new Vector3(x1, t10, z0), new Vector3(x0, t00, z0),
                        Vector3.up, new Vector2(x0, z1), new Vector2(x1, z1), new Vector2(x1, z0), new Vector2(x0, z0));
                else
                    AddQuad(vertices, uvs, triangles,
                        new Vector3(x0, t00, z0), new Vector3(x0, t01, z1), new Vector3(x1, t11, z1), new Vector3(x1, t10, z0),
                        Vector3.up, new Vector2(x0, z0), new Vector2(x0, z1), new Vector2(x1, z1), new Vector2(x1, z0));

                // Yan yüzler: her kenarda iki uç için ayrı ayrı "komşunun üstü (veya taban) → benim üstüm".
                foreach (var offset in k_Neighbours)
                {
                    // Kenarın iki ucu (hücre içi köşe koordinatları) ve komşudaki karşılıkları
                    int ax, az, bx, bz;
                    if (offset.x == 1) { ax = 1; az = 0; bx = 1; bz = 1; }
                    else if (offset.x == -1) { ax = 0; az = 1; bx = 0; bz = 0; }
                    else if (offset.y == 1) { ax = 1; az = 1; bx = 0; bz = 1; }
                    else { ax = 0; az = 0; bx = 1; bz = 0; }

                    float topA = CornerLevel(data, ax, az) * lh;
                    float topB = CornerLevel(data, bx, bz) * lh;
                    float lowA, lowB;
                    if (m_Map.TryGetValue(cell + offset, out var neighbour) && neighbour.Height <= m_MaxVisibleLevel)
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
