# Değişiklik Günlüğü

Biçim [Keep a Changelog](https://keepachangelog.com/tr/1.1.0/), sürümleme [SemVer](https://semver.org/lang/tr/).

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
