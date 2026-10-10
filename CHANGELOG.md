# Değişiklik Günlüğü

Biçim [Keep a Changelog](https://keepachangelog.com/tr/1.1.0/), sürümleme [SemVer](https://semver.org/lang/tr/).

## [0.12.0] - 2026-10-10

### Eklendi
- **Yumuşak rampa:** Rampa işleminde "Yumuşak (2 hücre)" seçeneği. Bir kat iki hücreye yayılır (yarım + yarım).
  `WorldKitFloor.RampPart`, `AutoSlopeGentle`, `SetSlope(cells, slope, part)`, `TryGetRampPart`.
- Şablonlar yumuşak rampaları da korur (`WorldKitStamp.FloorCell.rampPart`).

### Değişti
- `m_Slopes` artık eğim + 8 × rampa parçası saklar; eski kayıtlar değişmeden okunur.

## [0.11.0] - 2026-10-10

### Eklendi
- **Erişim** sekmesi (Analiz): Başlangıçtan yürüyerek ulaşılan zemini yeşil, ulaşılamayanı kırmızı gösterir.
  Collider'lar engel, katlar arası rampa gerekir; isteğe bağlı aşağı atlama, uzaklık renklendirmesi,
  en uzak / ulaşılamayan noktaya gitme. Gösterim açıkken düzenlemelerden sonra kendiliğinden yenilenir.
- API: `WorldKitReachability.Compute(floor, start, ignore, allowDrop)`, `WorldKitFloor.Cells`, `TryGetCornerLevel`.

## [0.10.0] - 2026-10-10

### Eklendi
- **Kenar Süsle** zemin işlemi (`FloorEdgeDecor`): Seçilen alanın dış kenarlarına kaya, çalı gibi parçaları rastgele
  ama tekrarlanabilir dizer. Parça listesi, yoğunluk ve "uçurum dibine koy" Zemin sekmesinde.
- **Şablonlarda rampa:** Şablona kaydedilen rampalar korunur ve şablonla birlikte döner
  (`WorldKitStamp.FloorCell.localSlopeDirection`, `WorldKitFloor.SlopeFromDirection`).
- **Başlangıç içeriği:** Pencereli duvar, geniş geçit, kale duvarı, köprü (2×4, üstü 1. katta), iskele;
  kale kulesi, ahır; kaya kümesi, ot, fener, tabela. Mevcut projelerde "Başlangıç İçeriğini Üret" ile eklenir.
- `FloorWalls.OuterEdges(floor, cells)`: Bir hücre alanının dış kenarları (modüller için).

## [0.9.0] - 2026-10-10

### Eklendi
- **Rampa** zemin işlemi: Hücreyi yanındaki bir kat yüksek komşuya doğru eğimli yapar (yön kendiliğinden).
  Rampa zeminin bir parçasıdır: tek mesh, tek collider, yan yüzler komşularla dikişsiz.
- `WorldKitFloor.Slope`, `SetSlope`, `AutoSlope`, `TryGetSlope`. Eğimler ayrı listede saklanır; eski zeminler değişmez.

### Değişti
- Zemin yan yüzleri artık köşe yüksekliklerine göre üretiliyor (eğimli komşularla doğru birleşim).
- Boya işlemi rampayı düzleştirir; Yükselt, Alçalt ve Sadece Renk eğimi korur.

## [0.8.0] - 2026-10-10

### Eklendi
- **Varyasyonlar:** Palette Ctrl+tık ile birden fazla parça seçilir; fırça her yerleşimde aralarından birini koyar.
  Seçim konumdan türetilir (önizleme = sonuç). Bütün modlarda ve Shift+tık zincirinde çalışır.
- API: `WorldKitBrush.Variants`, `Begin(IReadOnlyList<GameObject>)`, `ToggleVariant(prefab)`;
  `PlacePiece(..., equivalents)` ile aynı yerde eşdeğer parça varsa koymama.

## [0.7.0] - 2026-10-10

Arayüz yeniden düzenlendi.

### Değişti
- **Palet düzeni:** Üstte bölümler (İnşa / Analiz / Çıktı), İnşa'da solda kategori listesi, sağda fırça kartı,
  gruplu mod çubuğu (Yerleştir / Sil / Ölç) ve içerik. Alt çubuk gruplara ayrıldı (Görünüm / Test / Seçim).
- Ayarlar, yenile ve yardım başlığa taşındı ("Ayarlar" alt çubuk düğmesi kaldırıldı).
- Dar pencerelerde kategori listesi, bölümler ve alt çubuk yalnızca ikonla gösterilir.
- Zemin işlemleri ikonlu düğme ızgarası oldu.

### Eklendi
- **İkonlar** (`WorldKitIcons`): Bütün modlar, kategoriler, bölümler ve düğmeler için kodla çizilen, temaya uyumlu ikonlar.
  Kendi ikonlarınız için `WorldKitIcons.Get(key, canvas => ...)`.
- Eklenti API'si: `WorldKitTool.Icon`, `WorldKitPaletteTab.Section` / `Icon`, `WorldKitToolbarItem.Group` / `Icon`.
- Parça klasörüne `_icon.png` koyarak kategori ikonu verme.

## [0.6.0] - 2026-10-10

Zemin ve arazi.

### Eklendi
- **Zemin işlemleri** (Zemin sekmesi → İşlem): Boya, Sadece Renk, Yükselt, Alçalt, Duvarla Çevir.
  Hepsi seçili modun şekliyle (Tek, Hat, Alan, Çerçeve) uygulanır.
- **Yükselt / Alçalt:** Var olan zemini kat kat yükseltip alçaltır. `WorldKitFloor.AdjustHeight`.
- **Duvarla Çevir:** Seçilen alanın dış kenarlarına duvar örer (`FloorWalls`). Köşe direkleri ve kapı açma bu duvarlarla da çalışır.
- `WorldKitBrush.PlacePiece(prefab, parent, position, rotation, scale)`: Modüllerin parça yerleştirmesi için genel metot
  (aynı yere tekrar koymaz, Undo'ya kaydeder, PiecePlaced tetikler).
- `WorldKitBrush.FloorOp`, `FloorWallPrefab`.

### Değişti
- "Sadece renk" onay kutusu, İşlem seçicisindeki **Sadece Renk** oldu (`FloorKeepHeight` geriye uyumlu kaldı).

## [0.5.0] - 2026-10-10

Çıktı kategorisi.

### Eklendi
- **Bake:** Dünyayı materyal başına birleştirip tek mesh asset + collider olarak kaydeder; orijinali isteğe bağlı gizler.
- **OBJ dışa aktarma:** .obj + .mtl (ek paket gerektirmez).
- Palette "Çıktı" sekmesi, `Tools → World Kit → Çıktı` menüleri.

## [0.4.0] - 2026-10-10

Ölçüm kategorisi.

### Eklendi
- **Cetvel** modu (7, `RulerTool`): mesafe, hücre sayısı, yürüme/koşma süresi (`walkSpeed`, `runSpeed` ayarları).
- **İstatistik** palet sekmesi: parça sayıları, zemin alanı (türe göre), üçgen sayısı, dünya boyutu.
- **Harita PNG** dışa aktarma (ortografik, piksel/metre ayarlı).
- Eklenti API'si: `WorldKitTool.ModifiesScene` (false ise mod sahneyi değiştirmez).

## [0.3.0] - 2026-10-10

İnşa kategorisi.

### Eklendi
- **Zemin türleri:** Çimen, toprak, taş, kum, su, yol... (`WorldKitSettings.floorTypes`). Palette renk kareleri
  ve "Sadece renk (yüksekliği koru)" seçeneği. `WorldKitFloor.SetCells(cells, height, type)`, `PaintType`, `TryGetType`.
- **Şablonlar:** Seçimden şablon oluştur (parçalar + altındaki zemin). Palette "Sablonlar" sekmesi; yerleşince
  parçalara açılır. `WorldKitStamp` bileşeni, `Tools → World Kit → Seçimden Şablon Oluştur`, palette "Şablon Kaydet".
- **Serpme fırçası** (`ScatterTool`, mod 5): Rastgele konum, dönüş, boyut ve en az aralık; ayarları palette.
- Eklenti API'si: `WorldKitTool.HasSettings` / `OnSettingsGUI()` (palette ayar paneli),
  `OnPreviewGUI()` (Scene'de ek çizim), `Placement.Scale`.
- Başlangıç içeriği, liste boşsa 6 hazır zemin türü oluşturur.

### Değişti
- **Sil modu artık 6 numaralı tuşta** (5 = Serp).
- Zemin alt-mesh düzeni türleri de içeriyor; eski zeminler değişmeden çalışır (tür 0).
- Varsayılan kategori sırasına `Sablonlar` eklendi.

## [0.2.0] - 2026-10-10

Düzenleme kategorisi. Hepsi eklenti API'siyle yazılmış modüllerdir (`Editor/Modules`).

### Eklendi
- **Seçimi düzenleme:** Fırça kapalıyken seçili parçalar için oklar (ekrana göre 1 hücre), PgUp/PgDn (kat),
  R / Shift+R (grup halinde döndür), G (grid'e oturt), Delete (sil). Menü: Tools → World Kit → Seçimi Grid'e Oturt.
- **Duvara kapı açma:** Aynı hatta üst üste konan/taşınan çizgi parçası eskisinin yerini alır (`replaceOverlappingLines`).
- **Otomatik köşe direkleri:** L/T/+ birleşimlerine direk konur ve gerektiğinde kaldırılır (`autoCornerPosts`, `cornerPostPrefab`).
- **Üst katları gizle:** Palette düğme. Çalışılan seviyenin üstündeki parçalar ve zemin gizlenir (`WorldKitFloor.MaxVisibleLevel`).
- Olaylar: `PieceMoving`, `PieceMoved`, `EditFinished`. `RaiseXxx` metotları artık public (kendi araçlarınız için).
- `WorldKitScene`: Sahne hiyerarşisi yardımcıları.

### Değişti
- Fırçanın kök/grup/parça bulma kodu `WorldKitScene`'e taşındı (`WorldKitBrush.FindRoot` korunuyor).

## [0.1.0] - 2026-10-10

İlk paket sürümü. "Greybox Kit" proje içi aracından taşındı.

### Eklendi
- Paket yapısı (`com.baran.worldkit`), Git URL ile kurulum.
- Proje ayarları: Edit → Project Settings → World Kit (`WorldKitSettings`).
- Eklenti sistemi: `WorldKitTool` (fırça modu), `WorldKitPaletteTab` (palet sekmesi),
  `WorldKitToolbarItem` (alt çubuk düğmesi), `WorldKitEvents` (olaylar).
- İsteğe bağlı Test modülü (`WorldKit.Playtest`): Cinemachine ve Input System yoksa kendiliğinden kapanır.
- "Seçili Parçaları Zemine Çevir": Herhangi bir blok parçayı zemin hücrelerine dönüştürür.
- Başlangıç içeriği grid boyutuna göre ölçeklenir; duvar ve karakter boyu ayarlardan gelir.
- Belgeler: README, Geliştirme kılavuzu, CLAUDE.md.

### Değişti
- Menüler `Tools/Greybox Kit` → `Tools/World Kit`.
- `GreyboxPiece` → `WorldKitPiece`, `GreyboxFloor` → `WorldKitFloor`, `GreyboxPlayer` → `WorldKitPlayer`,
  `GreyboxCameraRig` → `WorldKitCameraRig`. Script GUID'leri korunduğu için mevcut sahneler ve prefab'lar etkilenmez.
- Fırça modları sabit listeden eklenti sınıflarına taşındı. Kısayollar 1-9.
- `Oyuncu_1.5m` → `Oyuncu`, `Dusman_1.5m` → `Dusman` (boy artık ayarlardan geliyor).

### Kaldırıldı
- Eski `Zemin_*` / `Platform_*` prefab dönüştürücüsü (yerine genel "Seçili Parçaları Zemine Çevir").
