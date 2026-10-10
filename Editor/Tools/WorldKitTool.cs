using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// Bir fırça modu (Tek, Hat, Alan...). Palette "Mod" çubuğunda görünür ve 1-9 tuşlarıyla seçilir.
    ///
    /// Yeni mod eklemek için bu sınıftan türeyin; parametresiz yapıcısı olan her alt sınıf
    /// otomatik bulunur, ayrıca kayıt gerekmez. Mod yalnızca NEREYE konacağını hesaplar;
    /// yerleştirme, önizleme, undo ve silme fırça tarafından yapılır.
    /// </summary>
    public abstract class WorldKitTool
    {
        /// <summary>Paletteki etiket.</summary>
        public abstract string Name { get; }

        /// <summary>Çizgi parçası (duvar, çit) seçiliyken etiket. Varsayılan: Name.</summary>
        public virtual string LinearName => Name;

        public virtual string Tooltip => Name;

        /// <summary>Mod çubuğundaki ikon. null ise adın ilk harfi gösterilir. Bkz. WorldKitIcons.</summary>
        public virtual Texture2D Icon => null;

        /// <summary>Mod çubuğundaki sıra (küçük önce). Yerleşikler: 10, 20, 30, 40, 90.</summary>
        public virtual int Order => 100;

        /// <summary>true ise yerleştirmek yerine siler (Ctrl basılıyken de bu mod kullanılır).</summary>
        public virtual bool Erases => false;

        /// <summary>false ise mod sahneyi hiç değiştirmez (ör. Cetvel): yerleştirme, boyama ve Shift+tık zinciri yapılmaz.</summary>
        public virtual bool ModifiesScene => true;

        /// <summary>true ise sürüklerken imlecin geçtiği her hücreye anında uygular (fırça gibi boyar).</summary>
        public virtual bool PaintsWhileDragging => false;

        /// <summary>
        /// true ise mod grid'e bağlı değildir (Serp, Yol Boyu, Halka): prefab fırçasında imleç, serbest yerleştirme
        /// kapalı olsa bile altındaki yüzeye (zemin, platform, obje) oturur.
        /// </summary>
        public virtual bool FreeForm => false;
        /// <summary>Seçili hedefte (prefab / zemin, çizgi parçası...) bu mod kullanılabilir mi?</summary>
        public virtual bool Supports(BrushContext context) => true;

        /// <summary>Scene'deki bilgi kutusunda gösterilecek kısa ipucu.</summary>
        public virtual string Hint(BrushContext context) => "";

        /// <summary>true ise mod seçiliyken palette OnSettingsGUI ile bir ayar paneli gösterilir.</summary>
        public virtual bool HasSettings => false;

        /// <summary>
        /// Modun ayar paneli (EditorGUILayout ile). Değerleri kalıcı yapmak için EditorPrefs kullanın.
        /// </summary>
        public virtual void OnSettingsGUI() { }

        /// <summary>
        /// ModifiesScene = false olan modlarda tıklama/sürükleme bitince çağrılır (Dragging hâlâ true, Anchor ve Hit dolu).
        /// Fırça bir şey yerleştirmez; mod kendi işini yapabilir (ör. Bölge modu bir bölge objesi oluşturur).
        /// Yapılan değişiklikler Undo'lu olmalı; hepsi kullanıcı eylemiyle aynı Undo grubuna girer.
        /// </summary>
        public virtual void OnStrokeFinished(BrushContext context) { }

        /// <summary>Scene'de ek önizleme çizimi (Handles ile; Repaint sırasında çağrılır). Ör. serpme yarıçapı.</summary>
        public virtual void OnPreviewGUI(BrushContext context) { }

        /// <summary>Prefab fırçası: konacak parçaların konum ve dönüşleri.</summary>
        public virtual void CollectPlacements(BrushContext context, List<Placement> result)
        {
            result.Add(context.SinglePlacement());
        }

        /// <summary>Zemin fırçası: boyanacak (veya silinecek) hücreler.</summary>
        public virtual void CollectCells(BrushContext context, ICollection<Vector2Int> result)
        {
            result.Add(context.HoverCell);
        }

        public string DisplayName(BrushContext context) => context.IsLinear ? LinearName : Name;
    }

    /// <summary>
    /// Fırçanın o anki durumu; modlar bunu okuyarak yerleşimi hesaplar.
    /// </summary>
    public sealed class BrushContext
    {
        /// <summary>Zemin fırçası mı (yoksa prefab fırçası mı)?</summary>
        public bool IsFloor { get; internal set; }

        /// <summary>Seçili prefab (zemin fırçasında null).</summary>
        public GameObject Prefab { get; internal set; }

        /// <summary>Seçili parçanın grid alanı.</summary>
        public Vector2Int Footprint { get; internal set; } = Vector2Int.one;

        /// <summary>Kullanıcının R ile seçtiği dönüş (0-3, 90° adımlar).</summary>
        public int Rotation { get; internal set; }

        /// <summary>Çalışılan seviye (kat).</summary>
        public int Level { get; internal set; }

        /// <summary>Fare basılı ve sürükleniyor mu?</summary>
        public bool Dragging { get; internal set; }

        /// <summary>Sürüklemenin başladığı nokta (seviye düzleminde).</summary>
        public Vector3 Anchor { get; internal set; }

        /// <summary>İmlecin seviye düzlemindeki noktası.</summary>
        public Vector3 Hit { get; internal set; }

        /// <summary>Sürüklemenin kilitlendiği eksen.</summary>
        public DragAxis Axis { get; internal set; }

        /// <summary>Duvar/çit gibi köşeden köşeye yerleşen parça mı?</summary>
        public bool IsLinear => !IsFloor && WorldKitLayout.IsLinear(Footprint);
        /// <summary>Serbest yerleştirme seçeneği açık mı (G)?</summary>
        public bool FreePlacement { get; internal set; }
        /// <summary>Serbest yerleştirme gerçekten uygulanıyor mu (açık, prefab fırçası, çizgi parçası değil)?</summary>
        public bool IsFree => FreePlacement && !IsFloor && !IsLinear;
        /// <summary>Serbest yerleştirmedeki dönüş (derece, R ile ayarlardaki adım kadar değişir).</summary>
        public float FreeYaw { get; internal set; }
        /// <summary>İmlecin altındaki yüzeyin normali (yüzeye oturma kapalıysa yukarı).</summary>
        public Vector3 SurfaceNormal { get; internal set; } = Vector3.up;

        public Quaternion RotationQuaternion => Quaternion.Euler(0f, Rotation * 90f, 0f);

        public Vector2Int HoverCell => WorldKitGrid.ToCell(Hit);

        /// <summary>Kilitli eksene izdüşürülmüş imleç (sürüklenmiyorsa imlecin kendisi).</summary>
        public Vector3 ConstrainedHit => Dragging ? WorldKitLayout.Constrain(Anchor, Hit, Axis) : Hit;

        /// <summary>İmlecin altındaki tek parça (çizgi parçaları köşeden fırça yönüne uzanır).</summary>
        public Placement SinglePlacement()
        {
            if (IsLinear)
                return WorldKitLayout.LinearSingle(Hit, Footprint, Rotation, Level);
            if (IsFree)
                return WorldKitFreePlacement.Make(Hit, FreeYaw, SurfaceNormal);
            return new Placement(WorldKitGrid.Snap(Hit, Footprint, Rotation, Level), RotationQuaternion);
        }

        /// <summary>Başlangıçtan imlece kadar hücre dikdörtgeni.</summary>
        public RectInt DragRect(bool constrainToAxis)
        {
            var from = Dragging ? Anchor : Hit;
            return WorldKitGrid.CellRect(from, constrainToAxis ? ConstrainedHit : Hit);
        }

        public static IEnumerable<Vector2Int> RectCells(RectInt rect, bool outlineOnly)
        {
            for (int z = rect.yMin; z < rect.yMax; z++)
            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                if (outlineOnly && x > rect.xMin && z > rect.yMin && x < rect.xMax - 1 && z < rect.yMax - 1)
                    continue;
                yield return new Vector2Int(x, z);
            }
        }
    }

    /// <summary>Tüm fırça modlarını bulur ve sıralar.</summary>
    public static class WorldKitTools
    {
        static List<WorldKitTool> s_All;

        public static IReadOnlyList<WorldKitTool> All
        {
            get
            {
                if (s_All != null)
                    return s_All;

                s_All = TypeCache.GetTypesDerivedFrom<WorldKitTool>()
                    .Where(t => !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
                    .Select(t => (WorldKitTool)Activator.CreateInstance(t))
                    .OrderBy(t => t.Order)
                    .ThenBy(t => t.Name)
                    .ToList();
                return s_All;
            }
        }

        public static WorldKitTool Find(string typeName) =>
            All.FirstOrDefault(t => t.GetType().FullName == typeName);

        /// <summary>Ctrl basılıyken kullanılan silme modu.</summary>
        public static WorldKitTool Eraser => All.FirstOrDefault(t => t.Erases);
    }
}
