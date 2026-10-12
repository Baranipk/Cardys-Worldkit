# Değişiklik Günlüğü

Biçim [Keep a Changelog](https://keepachangelog.com/tr/1.1.0/), sürümleme [SemVer](https://semver.org/lang/tr/).

## [0.23.0] - 2026-10-12

### Eklendi
- **Doku seçimi** (karolu ızgara yerine): `surfaceStyle` = Izgara / Düz renk / Yumuşak leke / Kendi dokum
  (`customTexture`, `customTextureTiling`). Zemin sekmesinde **Doku** listesi; değişince bütün `GB_*` materyalleri güncellenir.
- Döşenebilir yumuşak leke dokusu (`GB_Leke.png`, kod ile üretilir).
- Menü: Tools → World Kit → Yüzey Dokusunu Uygula (`WorldKitStarterContent.ApplySurfaceStyle`).

## [0.22.1] - 2026-10-12

### Geri alındı
- 0.22.0'daki köşe rampaları ve rampa çeşitleri geri alındı; rampa sistemi 0.21.0'daki haline döndü
  (dik rampa + "Yumuşak (2 hücre)" seçeneği).
- 0.22.0 ile köşe rampası yapılmış hücreler düz zemin olarak okunur (Rampa ile yeniden eğimlendirilebilir).

## [0.22.0] - 2026-10-12

Köşe rampaları (0.22.1'de geri alındı).

## [0.21.0] - 2026-10-12

Zemin seti.

### Eklendi
- **Zemin seti** (`WorldKitFloorSet` asset, `WorldKitFloorSetBuilder`): Kat kenarlarına otomatik dizilen modüler parçalar:
  duvar / dış köşe / iç köşe (her kat için) ve çimen saçağı / saçak köşeleri. Varyasyonlar konuma göre seçilir.
  Zemin değiştikçe yalnızca değişen parçalar eklenir/silinir (Undo'lu). Ayarlarda `floorSet`; palette Zemin Seti bölümü.
- **Greybox zemin seti** (`WorldKitStarterContent.CreateGreyboxFloorSet`): iki kademeli taş duvar ve dışa taşan çimen saçağı.
- Üst katları gizle, zemin setinin üst kat parçalarını da gizler.

### Düzeltildi
- **`WorldKitScene.GetPiece` her zaman null dönüyordu** (hiyerarşide bir seviye fazla çıkıyordu). Seçime dayanan her şey
  çalışmıyordu: seçimi düzenleme kısayolları, Üste Kopyala, Yere Oturt, Çeşitlendir, Şablon Kaydet, Değiştir → Seçimden,
  komşuya hizalama.
- Zeminin çocukları (yüzey boyası, zemin seti) artık parça sayılmaz (silme, istatistik, seçim).

## [0.20.0] - 2026-10-12

Yüzey boyası.

### Eklendi
- **Yüzey Boya** zemin işlemi: Hücreden bağımsız, yumuşak ve tırtıklı kenarlı patika, koyu çimen, taş yol. Yuvarlak Fırça (8)
  ile sürükleyerek boyanır (darbeler arası doldurulur), Ctrl ile silinir; diğer modlar hücreleri doldurur. Zemine dokunmaz.
- `WorldKitSurfacePaint` (runtime): katman başına hücre × çözünürlük örnek ağırlıkları (16×16 parçalar); marching squares ile
  zemin yüzeyini (kat, rampa, su) takip eden mesh, katmanın materyaliyle. Collider yok; zemin değişince kendiliğinden güncellenir.
- Ayarlar: `paintLayers` (ad, materyal, kenar tırtıklılığı). Hazır katmanlar: Patika, Koyu Çimen, Taş Yol
  (`WorldKitStarterContent.EnsureDefaultPaintLayers`). Palette katman seçimi, yumuşaklık, yarıçap, katmanı temizle.
- API: `WorldKitSurfaceBrush.Dab`, `FillCells`, `PaintPath`; `WorldKitFloor.Rebuilt` olayı.
- Örnek köyde kıvrımlı patikalar ve koyu çimen lekeleri.

## [0.19.0] - 2026-10-12

Yerleştirme deneyimi.

### Eklendi
- **Okunur önizleme:** Işıklı yarı saydam hayalet (`Hidden/WorldKit/Ghost` shader'ı), dış çizgi kutusu, zemin izi ve ok uçlu yön işareti.
  Başka bir objeyle çakışan yerleşim kırmızı gösterilir.
- **Döndürme:** Shift + tekerlek ile döndür. Tek modunda *Sürükle: yön ver* ayarı: bas, fareyi çek, parça o yöne bakarak konur;
  sonraki parçalar aynı yönde gelir (`BrushContext.PlacementFacing`).
- **Yarım hücre snap** (H, fırça kartında ½): grid yerleşiminde yarım hücre adımları (`WorldKitGrid.SnapHalf`).
- **Komşuya hizala** (serbest yerleştirme): yakındaki parçanın yönüne döner ve onun yarım hücre ızgarasına oturur.
- **Otomatik kat** (fırça kartında Oto): prefab fırçası imlecin altındaki zeminin katına kendiliğinden geçer; E/Q kapatır.
- **Rastgeleyi yeniden at** (Boşluk): rastgele dönüş, boyut, varyasyon ve serpme sonucu değişir (`WorldKitFreePlacement.Salt`).

## [0.18.3] - 2026-10-11

### Düzeltildi
- **"Invalid GUILayout state in WorldKitPaletteWindow"** hatası: İstatistik sekmesi ilk açıldığında "Performansı Analiz Et"
  düğmesi Layout geçişinde atlanıyordu (Layout ve Repaint'te farklı sayıda kontrol).
- Diyalog açan veya sekmenin içeriğini değiştiren düğmelerden sonra GUI geçişi güvenle bitirilir (`GUIUtility.ExitGUI`):
  harita PNG, Bake, OBJ, NavMesh (Bake/Kaldır/Göster), Erişim (Hesapla/Gizle), Değiştir, Başlangıç İçeriğini Üret.

## [0.18.2] - 2026-10-10

### Değişti
- Mod çubuğu: Grup yazıları (YERLEŞTİR, SİL, ÖLÇ · İŞARETLE) kaldırıldı; gruplar ince ayırıcıyla ayrılır, adlar ipucunda.
- Mod düğmeleri büyüdü (36×30, ikon 20 px); tıklaması kolay.
- Seçili parçada kullanılamayan modlar belirgin şekilde soluk.
- Fırça (zemin) ikonu boya fırçası, Bölge ikonu konum iğnesi oldu (Halka ve Çerçeve ile karışıyordu).

## [0.18.1] - 2026-10-10

### Düzeltildi
- Palet: Mod çubuğu pencereye sığmayınca alt satıra geçer (önceden taşıp arama satırını ve sağ kenarı kesiyordu).
- Palet: Fırça seçili değilken serbest yerleştirme ve mod ayar panelleri gösterilmez.
- Serbest yerleştirme ayarları dar pencerede taşmaz (ikişer seçenek bir satırda); ayarlar yalnızca değişince kaydedilir.
- Mod ipuçlarında 0 kısayolu gösterilir.

## [0.18.0] - 2026-10-10

### Eklendi
- **Serbest yerleştirme** (`WorldKitFreePlacement`): Grid'siz koyma; parça imlecin altındaki yüzeye (zemin, platform,
  obje) oturur. G ile aç/kapa; ayarlardaki `freePlacementCategories` (varsayılan Dekor) için kendiliğinden açık.
  R ile `freeRotationStep` (15°) döndürme; aralık, rastgele dönüş/boyut, yüzeye otur, eğime yatır ayarları.
- **Yol Boyu** modu (0, `PathTool`): Serbest çizilen yol boyunca aralıklarla dizme; yana kayma, iki yana, yola dönük.
- **Halka** modu (`CircleTool`): Çember üstüne eşit aralıklarla dizme; merkeze / dışarı / rastgele / sabit yön.
- **Yere Oturt** ve **Çeşitlendir** (alt çubuk → Seçim, Tools → World Kit → Seçim).
- **Kullanım kılavuzu** (`Documentation~/Kilavuz.md`): Kavramlar, arayüz, bütün araçlar, tarifler, kısayollar, sorun giderme.
- Eklenti API'si: `WorldKitTool.FreeForm`, `BrushContext.FreePlacement` / `IsFree` / `FreeYaw` / `SurfaceNormal`,
  `WorldKitBrush.FreePlacement`, `WorldKitFreePlacement.Make` / `TryDrop` / `RaycastSurface`. Mod kısayolu 0.

### Değişti
- Serp modu imleci ve parçaları yüzeye oturtur (platformlarda doğru yükseklik); bina içine düşen noktalar atlanır.

## [0.17.0] - 2026-10-10

Sade greybox ve performans.

### Değişti
- **Başlangıç içeriği düz kutular:** Binalarda kapı, çatı, tente, mazgal yok; ağaç = gövde + tepe kutusu; kaya, çalı,
  ot, fener, tabela, çit, varil tek kutu; NPC ve oyuncu referansı karakter boyunda tek kutu. Yalnızca oyunu etkileyen
  açıklıklar (kapılı duvar, geniş geçit) ve yürünen şekiller (merdiven, rampa) korunur.
- `Pencereli_Duvar_2m` ve `Yuvarlak_Sutun` artık üretilmiyor (var olanlara dokunulmaz).
- **Bake bölge bölge:** Varsayılan 16 m'lik bölgeler (Çıktı → Bölge boyutu; 0 = eski davranış). Tek asset dosyası.

### Eklendi
- **Paylaşılan mesh:** Başlangıç parçalarının mesh'leri `Assets/WorldKit/Meshes` altına asset olarak yazılır; ProBuilder
  bileşeni prefab'dan çıkar. Aynı parçanın bütün kopyaları tek mesh'i kullanır.
- **Basit collider'lar:** Kutu parçalarda BoxCollider, ağaçta gövde collider'ı, ot/çalıda collider yok.
  MeshCollider yalnızca kapılı duvar, geçit, merdiven ve rampada.
- **Gölge ve static:** Ot/çalı gölge atmaz; parçalar Occludee (bina/yapı ayrıca Occluder) static. Materyallerde GPU instancing.
- **Performans araçları** (`WorldKitPerformance`, Tools → World Kit → Performans): *Parça Prefab'larını Optimize Et*
  (kendi ProBuilder prefab'ların için), *Sahnedeki Parçaları Düzelt* (eski kopyaların ayrı mesh'lerini temizler).
  İstatistik sekmesinde performans raporu ve GPU Resident Drawer kontrolü.

## [0.16.0] - 2026-10-10

### Eklendi
- **Değiştir** sekmesi (Çıktı, `WorldKitReplace`): Bir prefab'ın sahnedeki (veya seçimdeki) bütün kopyalarını başka bir
  prefab'la değiştirir. Konum/dönüş korunur; ek dönüş, konum kayması, ölçeği koru; footprint kopyalanır. Tek Undo.
- **Bölgeler** (`WorldKitZone` runtime bileşeni, `WorldKitZones`): Spawn, görev, tetikleyici, güvenli, tehlikeli, özel.
  Bölge modu (9) ile çizilir; türüne göre renkli; `onEnter` / `onExit` olayları, `zoneId`, `RandomPoint()`.
  Bölgeler sekmesinde liste. Ignore Raycast katmanında; NavMesh bake bu katmanı dışarıda bırakır.
- Eklenti API'si: `WorldKitTool.OnStrokeFinished(context)` (`ModifiesScene = false` modlar için).

### Değişti
- Mod çubuğundaki "ÖLÇ" grubu "ÖLÇ · İŞARETLE" oldu.

## [0.15.0] - 2026-10-10

### Eklendi
- **MIT lisansı** (`LICENSE.md`, `package.json` → `license`).
- **Örnek Köy Oluştur** (`Tools → World Kit`, `WorldKitDemoVillage`): Yeni sahnede zemin türleri, yumuşak rampalı platform,
  gölet, binalar, kenar süsü, ağaçlar ve NPC'lerle küçük bir köy. Gerekirse başlangıç içeriğini de üretir.
- **Ekip rehberi** (`Documentation~/Rehber.md`): Kurulum, ilk 10 dakika, görev tablosu, teslim kontrol listesi, Git kuralları.

## [0.14.0] - 2026-10-10

### Eklendi
- **Fırça** modu (8, `RoundBrushTool`, yalnızca zemin): Yuvarlak fırçayla sürükleyerek zemin işlemi uygular
  (patika, kıyı, arazi). Bir sürüklemede her hücreye bir kez.
- **Üste Kopyala** (`StackSelection`): Seçimin kopyasını üstüne koyar (kat ekleme, kule yükseltme).
- **Su seviyesi:** Zemin türlerine `surfaceOffset` (m). Su varsayılan olarak -0.3 × kat yüksekliği; kıyı oluşur,
  Erişim'de yürünemez. `WorldKitFloor.SetTypeOffsets`, `TypeOffsets`.
- Palette arama artık bütün kategorilerde arar; temizleme düğmesi.

## [0.13.0] - 2026-10-10

### Eklendi
- **NavMesh** sekmesi (Analiz): Dünyanın collider'larından NavMesh bake eder, asset olarak saklar ve köke
  `WorldKitNavMesh` (runtime) ekler. Unity'nin yerleşik API'si; AI Navigation paketi gerekmez. Sahnede gösterim, alan.
  API: `WorldKitNavMeshBake.Bake()`, `Clear()`, `BuildSettings()`.
- **Erişim:** Dar geçitler (karakter genişliği sığmayan geçişler) mor gösterilir; `Result.Narrow`.

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
