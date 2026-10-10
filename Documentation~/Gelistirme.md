# World Kit: Geliştirme Kılavuzu

Bu belge World Kit'i genişletmek veya değiştirmek isteyenler içindir.
Kullanım için [README](../README.md) dosyasına bakın.

## İlkeler

1. **Çekirdek küçük kalır.** Yeni bir özellik mümkünse bir *eklenti* (mod, sekme, düğme, olay dinleyici)
   olarak yazılır, çekirdek dosyalar değiştirilmez.
2. **Sabit ölçü yok.** Grid, kat yüksekliği ve klasör yolları her zaman `WorldKitSettings`'ten okunur.
   Yeni bir proje ayarı gerekiyorsa oraya alan eklenir.
3. **Her sahne değişikliği geri alınabilir.** `Undo.RegisterCreatedObjectUndo`, `Undo.RecordObject`,
   `Undo.DestroyObjectImmediate` kullanılır. Bir kullanıcı eylemi tek bir Undo grubudur.
4. **Kullanıcıyı kısıtlama.** Paket, kendi prefab'larıyla da hiçbir hazır içerik olmadan çalışmalıdır.
5. **Bağımlılık eklemeden önce düşün.** Çekirdek yalnızca ProBuilder'a bağlıdır. Başka bir pakete ihtiyaç
   duyan özellik, kendi assembly'sinde `versionDefines` + `defineConstraints` ile isteğe bağlı olur
   (örnek: `WorldKit.Playtest`).

## Klasör yapısı

```
com.baran.worldkit/
├── Runtime/                     WorldKit.Runtime (build'e girer, bağımlılık yok)
│   ├── WorldKitPiece.cs         Parçanın footprint bilgisi
│   ├── WorldKitFloor.cs         Hücre verisinden (yükseklik + tür) tek mesh zemin
│   ├── WorldKitStamp.cs         Şablon prefab'ının kökü (parçalar + zemin hücreleri)
│   └── Playtest/                WorldKit.Playtest (Input System + Cinemachine varsa)
├── Editor/
│   ├── Core/                    Fırça, grid, yerleşim hesapları, ayarlar, olaylar
│   ├── Tools/                   Fırça modları (WorldKitTool) — eklenti noktası
│   ├── Palette/                 Palet penceresi, sekme ve düğme eklenti noktaları
│   ├── Modules/                 Yerleşik modüller (eklenti API'siyle yazılmış, örnek olarak okuyun):
│   │                            LineReplacement, AutoCornerPosts, SelectionEditing, LevelVisibility, Stamps, FloorWalls, Measurement, Output
│   ├── Content/                 Başlangıç içeriği, dönüştürücüler (isteğe bağlı)
│   └── Playtest/                WorldKit.Playtest.Editor (isteğe bağlı)
├── Documentation~/              Bu belgeler (Unity içe aktarmaz)
├── CLAUDE.md                    Yapay zeka asistanları için kurallar
└── CHANGELOG.md
```

## Akış

```
Palet (parça/zemin seçimi) ──► WorldKitBrush ──► BrushContext (imleç, seviye, dönüş, sürükleme)
                                     │                    │
                                     │                    ▼
                                     │            WorldKitTool.CollectPlacements / CollectCells
                                     │                    │  "nereye?"
                                     ▼                    ▼
                         önizleme (mavi) ◄──── Placement listesi / hücre listesi
                                     │
                         fare bırakılınca ──► yerleştir / zemini boya / sil  (Undo grubu)
                                     │
                                     └──► WorldKitEvents (PiecePlaced, PieceRemoving, FloorChanged)
```

---

## Eklenti noktaları

Aşağıdaki sınıflardan türeyen ve **parametresiz yapıcısı olan** her sınıf `TypeCache` ile otomatik
bulunur; kayıt gerekmez. Kodunuzu herhangi bir **Editor** assembly'sine koyabilirsiniz (paketin içine
veya projenin `Assets/.../Editor` klasörüne). O assembly'nin `WorldKit.Editor`'e referans vermesi yeterli.

### 1. Yeni fırça modu: `WorldKitTool`

Mod yalnızca **nereye** konacağını hesaplar. Önizleme, Undo, yerleştirme ve silme fırçanındır.

```csharp
using System.Collections.Generic;
using UnityEngine;
using WorldKit.Editor;

/// Tut-çek: dikdörtgeni satranç tahtası deseniyle doldurur.
public sealed class CheckerTool : WorldKitTool
{
    public override string Name => "Dama";
    public override string Tooltip => "Tut-çek: alanı dama deseniyle doldur.";
    public override int Order => 35;                       // Alan (30) ile Çerçeve (40) arası
    public override bool Supports(BrushContext c) => !c.IsLinear;
    public override string Hint(BrushContext c) => "Tut-çek: dama deseni";

    public override void CollectPlacements(BrushContext c, List<Placement> result)
    {
        if (!c.Dragging) { result.Add(c.SinglePlacement()); return; }

        var all = new List<Placement>();
        WorldKitLayout.Area(all, c.Anchor, c.Hit, c.Footprint, c.Rotation, c.Level, false);
        foreach (var p in all)
        {
            var cell = WorldKitGrid.ToCell(p.Position);
            if (((cell.x + cell.y) & 1) == 0)
                result.Add(p);
        }
    }

    public override void CollectCells(BrushContext c, ICollection<Vector2Int> result)
    {
        foreach (var cell in BrushContext.RectCells(c.DragRect(false), false))
            if (((cell.x + cell.y) & 1) == 0)
                result.Add(cell);
    }
}
```

| Üye | Ne zaman değiştirilir |
|---|---|
| `Order` | Mod çubuğundaki sıra. İlk 9 mod 1-9 tuşlarıyla seçilir |
| `Icon` | Mod çubuğundaki ikon (`WorldKitIcons` veya kendi çiziminiz); yoksa adın ilk harfi |
| `Supports` | Mod bazı hedeflerde anlamsızsa (ör. duvarda "Alan"). Desteklenmeyince fırça bir önceki desteklenen moda düşer |
| `PaintsWhileDragging` | `true`: sürüklerken her yeni hücrede anında uygular (Tek modu gibi) |
| `Erases` | `true`: silme modu. Ctrl basılıyken ilk silme modu kullanılır |
| `LinearName` | Duvar/çit seçiliyken farklı bir etiket (ör. Çerçeve → Oda) |
| `ModifiesScene` | `false`: sahneyi değiştirmeyen mod (ör. Cetvel); yerleştirme, boyama ve Shift+tık yapılmaz |
| `HasSettings` + `OnSettingsGUI()` | Mod seçiliyken palette ayar paneli gösterir. Değerleri `EditorPrefs`'te tutun |
| `OnPreviewGUI(context)` | Scene'de ek önizleme çizimi (Handles), ör. serpme yarıçapı çemberi |

`Placement` içinde `Position`, `Rotation` ve isteğe bağlı `Scale` (0 = 1) var. Grid dışı yerleşim
(rastgele konum, açı, boyut) için örnek: `Editor/Tools/ScatterTool.cs`.

`BrushContext` içinde: `IsFloor`, `IsLinear`, `Prefab`, `Footprint`, `Rotation`, `Level`, `Dragging`,
`Anchor`, `Hit`, `Axis`, `ConstrainedHit`, `HoverCell`, `SinglePlacement()`, `DragRect()`, `RectCells()`.

Yardımcı hesaplar (`WorldKitLayout`): `Area`, `Line`, `Room`, `LinearSingle`, `Constrain`, `FarEnd`.
Grid (`WorldKitGrid`): `Snap`, `ToCell`, `ToCorner`, `CellCenter`, `CornerToWorld`, `CellRect`.

### 2. Yeni palet sekmesi: `WorldKitPaletteTab`

Prefab'lar için **kod gerekmez**: parçalar klasörüne alt klasör açmak yeni sekme yaratır.
Kod ile sekme, özel arayüz gerektiğinde kullanılır.

```csharp
using UnityEditor;
using UnityEngine;
using WorldKit.Editor;

public sealed class StatsTab : WorldKitPaletteTab
{
    public override string Title => "İstatistik";
    public override int Order => 2000;   // Zemin: 0, klasörler: 100+, varsayılan: 1000
    public override string Section => AnalysisSection;     // İnşa, Analiz, Çıktı veya kendi bölümünüz
    public override Texture2D Icon => WorldKitIcons.Stats;

    public override void OnGUI(WorldKitPaletteWindow window)
    {
        var root = WorldKitBrush.FindRoot();
        EditorGUILayout.LabelField("Parça sayısı",
            root != null ? (root.GetComponentsInChildren<UnityEngine.Transform>().Length - 1).ToString() : "0");
    }
}
```

### 3. Alt çubuk düğmesi: `WorldKitToolbarItem`

```csharp
using UnityEditor;
using UnityEngine;
using WorldKit.Editor;

public sealed class FrameWorldItem : WorldKitToolbarItem
{
    public override string Label => "Dünyayı Göster";
    public override int Order => 30;
    public override string Group => "Görünüm";              // alt çubukta grup
    public override Texture2D Icon => WorldKitIcons.Camera; // isteğe bağlı
    public override bool IsEnabled => WorldKitBrush.FindRoot() != null;
    public override void Execute() => Selection.activeGameObject = WorldKitBrush.FindRoot();
}
```

Gerçek örnek: `Editor/Playtest/PlaytestToolbarItem.cs`. Bu düğme, modül kurulu değilse kendiliğinden kaybolur.

### 4. Olaylar: `WorldKitEvents`

```csharp
using UnityEditor;
using UnityEngine;
using WorldKit.Editor;

[InitializeOnLoad]
static class AutoStaticFlags
{
    static AutoStaticFlags()
    {
        WorldKitEvents.PiecePlaced += go =>
        {
            // Olay fırçanın Undo grubunun içinde gelir; değişiklikleri Undo'ya kaydedin.
            Undo.RecordObject(go, "Static");
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        };
    }
}
```

| Olay | Ne zaman |
|---|---|
| `PiecePlaced(GameObject)` | Bir parça yerleştirildikten sonra |
| `PieceRemoving(GameObject)` | Bir parça silinmeden hemen önce |
| `PieceMoving(GameObject)` | Bir parça taşınmadan/döndürülmeden önce (eski konum geçerli) |
| `PieceMoved(GameObject)` | Bir parça taşındıktan/döndürüldükten sonra |
| `FloorChanged(WorldKitFloor)` | Zemin hücreleri değiştikten sonra |
| `EditFinished()` | Bir kullanıcı eylemi bittiğinde, Undo grubu kapanmadan hemen önce |
| `SettingsChanged()` | Proje ayarları değiştiğinde |

**Parça yerleştirmek:** Modülünüz parça koyacaksa `WorldKitBrush.PlacePiece(prefab, parent, position, rotation)` kullanın;
aynı yere tekrar koymaz, Undo'ya kaydeder ve `PiecePlaced`'i tetikler. Örnek: `Editor/Modules/FloorWalls.cs`.

**Toplu iş deseni:** Her parça için ağır iş yapmak yerine, `PiecePlaced`/`PieceRemoving`'de etkilenen
yerleri bir kümeye toplayın, `EditFinished`'te bir kez işleyin. Örnek: `Editor/Modules/AutoCornerPosts.cs`.

**Kendi kodunuz parça oluşturuyor, taşıyor veya siliyorsa** aynı olayları `WorldKitEvents.RaiseXxx(...)` ile
tetikleyin ve sonunda `RaiseEditFinished()` çağırın; böylece köşe direkleri gibi modüller haberdar olur.
Örnek: `Editor/Modules/SelectionEditing.cs`.

**Sahne yardımcıları (`WorldKitScene`):** `FindRoot`, `GetRoot`, `GetGroup`, `GetPiece` (alt objeden parçaya),
`AllPieces`, `SelectedPieces`, `GetFootprint`, `GetSource`, `TryGetSegment` (çizgi parçasının uçları).

### 5. Yeni proje ayarı

`Editor/Core/WorldKitSettings.cs`'e `[Tooltip]`'li bir public alan ekleyin. Project Settings sayfası
otomatik olarak gösterir. Kodda `WorldKitSettings.Instance.yeniAlan` ile okuyun.
Mevcut alanların **adını değiştirmeyin**: serileştirilmiş değerler kaybolur. Gerekirse `[FormerlySerializedAs]` kullanın.

### 6. İsteğe bağlı modül (başka pakete bağımlı)

`WorldKit.Playtest`'i örnek alın:

```json
"versionDefines": [
  { "name": "com.unity.cinemachine", "expression": "3.0.0", "define": "WORLDKIT_CINEMACHINE" }
],
"defineConstraints": [ "WORLDKIT_CINEMACHINE" ]
```

Paket kurulu değilse assembly hiç derlenmez; çekirdek bu modülü hiç referans etmez.
Modül palete düğme, sekme veya mod ekleyerek kendini gösterir.

---

## Kurallar ve tuzaklar

- **Undo:** Çoklu yerleştirmeler tek grup olmalı (`Undo.IncrementCurrentGroup` → `CollapseUndoOperations`).
- **ProBuilder mesh'leri** prefab içinde saklanmaz, `Awake`'te yeniden kurulur. Önizleme veya hesap için
  bir kopya oluşturduğunuzda `pb.ToMesh(); pb.Refresh();` çağırın.
- **`HandleUtility`** adı hem `UnityEditor` hem `UnityEngine.ProBuilder`'da var. Gerekirse
  `using HandleUtility = UnityEditor.HandleUtility;` yazın.
- **Kısayollar** `[Shortcut(..., typeof(BrushShortcutContext), ...)]` ile yalnızca fırça aktifken çalışır.
  `KeyCode.Escape` kısayol olarak atanamaz.
- **Sahne hiyerarşisi** `<Kök>/<Kategori>/<Parça>` varsayılır. Silme ve alan seçimi buna dayanır.
- **Önizleme** `Graphics.DrawMesh` + `Hidden/Internal-Colored` ile çizilir; her render pipeline'da çalışır.
- **Arayüz metinleri Türkçe**, kod (sınıf, alan, metot adları) İngilizcedir.

## Test kontrol listesi

Bir değişiklikten sonra en azından şunları deneyin:

- [ ] Paket, Test modülünün bağımlılıkları olmadan derleniyor (Cinemachine'i geçici olarak kaldırın)
- [ ] Her modda: tıklama, sürükleme, Shift+tık, Ctrl ile silme, Esc ile iptal, tek Ctrl+Z ile geri alma
- [ ] Hem prefab hem zemin fırçasında
- [ ] Duvar (`2×0`), köşe direği (`0×0`), alan parçası (`1×1`, `2×2`) ile
- [ ] Grid boyutu 1 dışında bir değerle (ör. 2)
- [ ] Domain reload ve Play'e girip çıkmadan sonra fırça ve palet düzgün

## Sürümleme

- `package.json` içindeki `version` [SemVer](https://semver.org) kuralına uyar: hata düzeltme `0.1.x`,
  yeni özellik `0.x.0`, uyumsuz değişiklik `x.0.0`.
- Her sürümde `CHANGELOG.md`'yi güncelleyin ve Git'te `v0.1.0` gibi bir etiket oluşturun.

## Zemin API'si (`WorldKitFloor`)

| Üye | Açıklama |
|---|---|
| `SetCells(cells, height)` | Hücreleri doldurur; var olanın türü korunur |
| `SetCells(cells, height, type)` | Hücreleri yükseklik ve türle doldurur |
| `PaintType(cells, type)` | Yalnızca var olan hücrelerin türünü değiştirir |
| `AutoSlope(cells, preferAlongX)` / `SetSlope(cells, slope)` | Rampa (eğim). `Slope.None` düzleştirir |
| `AdjustHeight(cells, delta, min, max)` | Yüksekliği değiştirir, eğimi korur |
| `RemoveCells(cells)` | Hücreleri siler |
| `TryGetHeight` / `TryGetType` | Hücre sorgusu |
| `MaxVisibleLevel` | Görünüm sınırı (kaydedilmez) |
| `SubMeshCount` | Renderer'da gereken materyal sayısı |

Alt-mesh düzeni: `0` tür 0 yer seviyesi, `1` tür 0 yükseltilmiş, `1 + t` tür t. Zemini koddan değiştirdikten
sonra `WorldKitBrush.SyncFloorMaterials(floor)` çağırın; materyaller ayarlardaki türlerle eşitlenir.
Undo için değişiklikten önce `Undo.RecordObject(floor, ...)` kullanın.

## Arayüz ve ikonlar

Palet üç katmandan oluşur: **bölümler** (`WorldKitPaletteTab.Section`), **kategoriler/sekmeler** ve
**alt çubuk grupları** (`WorldKitToolbarItem.Group`). İnşa bölümündeki sekmeler sol listede, fırça kartı ve
mod çubuğunun altında görünür; diğer bölümlerde sekmeler üstte sıralanır.

Mod çubuğu modları kendiliğinden gruplar: `ModifiesScene && !Erases` → YERLEŞTİR, `Erases` → SİL,
`!ModifiesScene` → ÖLÇ.

**İkonlar (`WorldKitIcons`)** kodla, 64×64 kenar yumuşatmalı bir tuvalde çizilir ve temaya göre renklenir.
Kendi ikonunuz için:

```cs
public override Texture2D Icon => WorldKitIcons.Get("benim-ikon", c =>
{
    c.Circle(32, 32, 20);          // dolu daire
    c.ClearCircle(32, 32, 10);     // ortasını boşalt
    c.Line(8, 56, 56, 8, 6);       // kalın çizgi
});
```

Tuval metotları: `Rect`, `RoundRect`, `RectStroke`, `Circle`, `Ring`, `Line`, `Triangle`, `Clear`, `ClearCircle`
(koordinatlar sol üstten). Parça klasörüne ikon vermek için kod gerekmez: klasöre `_icon.png` koyun.
Bütün ikonlar: `Documentation~/images/ikonlar.png`.
