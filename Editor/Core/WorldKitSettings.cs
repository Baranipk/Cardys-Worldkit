using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// Projeye özel tüm World Kit ayarları. Assets içinde bir asset olarak durur, böylece Git ile
    /// takımla paylaşılır. Edit → Project Settings → World Kit altından düzenlenir.
    ///
    /// Kod içinde ölçü veya klasör yolu sabitlemek yerine her zaman buradan okuyun.
    /// </summary>
    public class WorldKitSettings : ScriptableObject
    {
        /// <summary>Ek zemin türü. Tür numarası = listedeki sıra + 1 (0 = varsayılan Zemin).</summary>
        [System.Serializable]
        public class FloorType
        {
            public string name = "Yeni Tür";
            public Material material;
        }

        public const string DefaultAssetPath = "Assets/WorldKit/WorldKitSettings.asset";

        [Header("Grid")]
        [Tooltip("Bir grid hücresinin kenar uzunluğu (m).")]
        [Min(0.1f)] public float cellSize = 1f;
        [Tooltip("Bir seviyenin (kat) yüksekliği (m). E/Q bu kadar yukarı/aşağı çıkar.")]
        [Min(0.1f)] public float levelHeight = 1f;
        [Tooltip("Seviye sınırları.")]
        public Vector2Int levelRange = new Vector2Int(-10, 30);

        [Header("Sahne")]
        [Tooltip("Yerleştirilen her şeyin toplandığı kök objenin adı.")]
        public string rootName = "World";
        [Tooltip("Zemin objesinin adı (kökün altında).")]
        public string floorName = "Zemin";

        [Header("Parçalar")]
        [Tooltip("Palet bu klasörün alt klasörlerini sekme olarak gösterir. Her alt klasör bir kategori.")]
        public string piecesFolder = "Assets/WorldKit/Pieces";
        [Tooltip("Sekmelerin sırası (klasör adları). Listede olmayanlar alfabetik olarak sona eklenir.")]
        public List<string> categoryOrder = new List<string> { "Yapi", "Bina", "Dekor", "Sablonlar", "NPC", "Referans" };

        [Header("Zemin")]
        [Min(0.01f)] public float floorThickness = 0.2f;
        [Tooltip("Yer seviyesi (≤ 0) hücrelerin materyali.")]
        public Material floorGroundMaterial;
        [Tooltip("Yükseltilmiş (> 0) hücrelerin materyali.")]
        public Material floorRaisedMaterial;
        [Tooltip("Ek zemin türleri (çimen, taş, su...). Palette Zemin sekmesinden seçilir. " +
                 "Sırayı değiştirmek boyanmış hücrelerin türünü de değiştirir; yeni türleri sona ekleyin.")]
        public List<FloorType> floorTypes = new List<FloorType>();

        [Header("Düzenleme")]
        [Tooltip("Bir çizgi parçası (duvar, çit) aynı hatta başka bir çizgi parçasının üstüne konunca eskisi kaldırılır. " +
                 "Duvara kapı açmak için: kapılı duvarı mevcut duvarın üstüne koy.")]
        public bool replaceOverlappingLines = true;
        [Tooltip("Dik açıyla birleşen duvarların köşesine otomatik direk koyar, duvar kalkınca direği de kaldırır.")]
        public bool autoCornerPosts = true;
        [Tooltip("Otomatik köşe direği prefab'ı. Boşsa başlangıç içeriğindeki Kose_Diregi kullanılır.")]
        public GameObject cornerPostPrefab;

        [Header("Ölçüm")]
        [Tooltip("Cetvelde süre hesabı için yürüme hızı (m/sn).")]
        [Min(0.1f)] public float walkSpeed = 4.5f;
        [Tooltip("Cetvelde süre hesabı için koşma hızı (m/sn).")]
        [Min(0.1f)] public float runSpeed = 7.5f;

        [Header("Başlangıç içeriği")]
        [Tooltip("Başlangıç içeriğinin materyallerinin yazılacağı klasör.")]
        public string materialsFolder = "Assets/WorldKit/Materials";
        [Min(0.5f)] public float wallHeight = 3f;
        [Min(0.05f)] public float wallThickness = 0.2f;
        [Tooltip("Karakter (oyuncu, NPC) boyu. 1.5 m civarı chibi oranlar verir.")]
        [Min(0.5f)] public float characterHeight = 1.5f;

        static WorldKitSettings s_Instance;

        /// <summary>Projenin ayar asset'i. Yoksa varsayılan değerlerle oluşturulur.</summary>
        public static WorldKitSettings Instance
        {
            get
            {
                if (s_Instance != null)
                    return s_Instance;

                var guids = AssetDatabase.FindAssets("t:" + nameof(WorldKitSettings));
                if (guids.Length > 0)
                {
                    s_Instance = AssetDatabase.LoadAssetAtPath<WorldKitSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
                    if (s_Instance != null)
                        return s_Instance;
                }

                s_Instance = CreateInstance<WorldKitSettings>();
                EnsureFolder(Path.GetDirectoryName(DefaultAssetPath)?.Replace('\\', '/'));
                AssetDatabase.CreateAsset(s_Instance, DefaultAssetPath);
                AssetDatabase.SaveAssets();
                return s_Instance;
            }
        }

        public static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
                return;

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        [SettingsProvider]
        static SettingsProvider CreateProvider()
        {
            UnityEditor.Editor editor = null;
            return new SettingsProvider("Project/World Kit", SettingsScope.Project)
            {
                label = "World Kit",
                keywords = new HashSet<string> { "grid", "greybox", "level", "world", "zemin", "palet" },
                guiHandler = _ =>
                {
                    var settings = Instance;
                    if (editor == null || editor.target != settings)
                        editor = UnityEditor.Editor.CreateEditor(settings);

                    EditorGUILayout.HelpBox(
                        "Bu ayarlar projeye özeldir ve " + AssetDatabase.GetAssetPath(settings) +
                        " dosyasında saklanır (Git ile takımla paylaşılır).", MessageType.None);
                    EditorGUILayout.Space();
                    EditorGUI.BeginChangeCheck();
                    editor.OnInspectorGUI();
                    if (EditorGUI.EndChangeCheck())
                        WorldKitEvents.RaiseSettingsChanged();
                }
            };
        }
    }
}
