# World Kit

Grid tabanlı, sürükle-bırak **world building** aracı (Unity 6, URP/HDRP/Built-in).
Amaç: olabildiğince basit kalmak, kullanıcıyı kısıtlamamak ve kolayca genişletilebilmek.

- Kendi prefab'larınla çalışır. Hazır içerik isteğe bağlıdır.
- Ölçüler (grid, kat yüksekliği, karakter boyu...) projeye göre ayarlanır.
- Yeni fırça modu, palet sekmesi veya düğme eklemek için çekirdek koda dokunmak gerekmez.
  Bkz. [Geliştirme kılavuzu](Documentation~/Gelistirme.md).

---

## Kurulum

**Git ile (önerilen):** Package Manager → **+** → *Add package from git URL*:

```
https://github.com/Baranipk/Cardys-Worldkit.git
```

Belirli bir sürüm için sona etiket ekle, ör. `https://github.com/Baranipk/Cardys-Worldkit.git#v0.5.0`.

**Yerel geliştirme:** Paket klasörünü projenin `Packages/` klasörüne koy (embedded package).

**Gereksinimler:** Unity 6000.0+, ProBuilder (otomatik gelir).
**Test modülü için (isteğe bağlı):** Cinemachine 3.x ve Input System. Bu paketler yoksa test modülü
kendiliğinden devre dışı kalır, geri kalan her şey çalışır.

## Hızlı başlangıç

1. **Tools → World Kit → Palet** (`Ctrl+Shift+G`).
2. Palet boşsa **Başlangıç İçeriğini Üret**'e bas veya kendi prefab'larını parçalar klasörüne koy.
3. **Zemin** sekmesinde *Zemin Fırçası* ile alan boya. Bir duvar seçip **Oda** modunda oda çiz.
4. **Play**: Test modülü kuruluysa oyuncu otomatik olarak gelir. WASD, Shift, Q/E ve tekerlek ile gez.

Hazır bir örnek görmek için: **Tools → World Kit → Örnek Köy Oluştur** (yeni sahnede küçük bir köy kurar).
Ekip arkadaşları için adım adım anlatım: [Ekip rehberi](Documentation~/Rehber.md).
Bütün araçlar, tarifler ve kısayollar: **[Kullanım kılavuzu](Documentation~/Kilavuz.md)**.

---

## Özellikler (kategorilere göre)

| Kategori | Özellik | Durum |
|---|---|---|
| **Çekirdek** | Grid ve snap, proje ayarları, palet, Undo, kısayollar | ✅ |
| | Fırça modları: Tek, Hat, Alan, Çerçeve/Oda, Serp, Sil (genişletilebilir) | ✅ |
| | Shift + tık ile arayı doldurma (zincir) | ✅ |
| | Eklenti sistemi: mod, palet sekmesi, alt çubuk düğmesi, olaylar | ✅ |
| **İnşa** | Zemin boyama: tek mesh, seviyeler, otomatik kenar duvarları | ✅ |
| | Duvar/çit: köşe tabanlı, taşmayan hat ve oda | ✅ |
| | Klasörden otomatik palet (her alt klasör bir sekme) | ✅ |
| | Seçili parçaları zemine çevirme | ✅ |
| | Duvara kapı açma (aynı hatta üst üste konan çizgi parçası eskisinin yerini alır) | ✅ |
| | Otomatik köşe direği | ✅ |
| | Zemin türleri (çimen, toprak, taş, kum, su, yol...) | ✅ |
| | Zemin işlemleri: Boya, Sadece Renk, Yükselt, Alçalt, Rampa, Duvarla Çevir, Kenar Süsle | ✅ |
| | Şablonlar: seçimi zeminiyle birlikte kaydet, palette yerleştir | ✅ |
| | Serpme fırçası: rastgele konum, dönüş, boyut ve aralık | ✅ |
| | Yuvarlak fırça: patika, kıyı, arazi şekillendirme | ✅ |
| | Serbest yerleştirme (grid'siz, yüzeye oturan, rastgele dönüş/boyut), Yol Boyu ve Halka modları | ✅ |
| | Su seviyesi: tür başına yüzey kayması | ✅ |
| | Varyasyonlar: Ctrl+tık ile birden fazla parça, her yerleşimde rastgele biri | ✅ |
| **Düzenleme** | Seç, taşı, döndür, çoğalt, sil (grid'e bağlı, klavyeyle) | ✅ |
| | Seviye görünürlüğü (üst katları gizle) | ✅ |
| | Üste kopyala: kat ekle, kule yükselt | ✅ |
| **Ölçüm** | Cetvel (mesafe, hücre, yürüme/koşma süresi) | ✅ |
| | İstatistik sekmesi (parça, zemin alanı, üçgen, dünya boyutu) | ✅ |
| | Üstten harita PNG | ✅ |
| | Erişim analizi: ulaşılan / ulaşılamayan zemin, dar geçitler | ✅ |
| | NavMesh bake (ek paket gerekmez), sahnede gösterim | ✅ |
| **Çıktı** | Bake: materyal başına tek mesh asset + collider | ✅ |
| | OBJ + MTL dışa aktarma | ✅ |
| | Parça değiştirme: placeholder → gerçek model, toplu | ✅ |
| **İşaretleme** | Bölgeler: spawn, görev, güvenli / tehlikeli alan (tetikleyici kutu, olaylar) | ✅ |
| **Test** *(modül)* | Test oyuncusu, Cinemachine top-down kamera, engelden kaçma | ✅ |
| **İçerik** *(isteğe bağlı)* | Düz kutu greybox parçalar: duvar, kapılı duvar, geçit, kale duvarı, köprü, iskele, merdiven, rampa; ev, han, dükkan, ahır, kule, kale kulesi; ağaç, kaya, ot, fener, tabela; NPC ve ölçü referansları. Paylaşılan mesh, basit collider | ✅ |

---

## Kullanım

### Palet
```
┌──────────────────────────────────────────────┐
│ [İnşa] [Analiz] [Çıktı]          ⟳  ⚙  ⓘ    │  bölümler · yenile · ayarlar · yardım
├───────────┬──────────────────────────────────┤
│ Zemin     │ [önizleme] Duvar 2m              │  seçili fırça: mod · kat · açı
│ ───────── │            Alan · Kat 0 · 0°     │  [− kat +] [↻] [Bırak]
│ Yapı      │ ▪ ━ ▦ ▢ ∴ 🖌 ⋰ ◯ │ ✕ │ ▭ 📍      │  modlar (1-0)
│ Bina      │ (seçili modun ayarları)          │
│ Dekor     │ 🔍 ara                    boyut   │
│ Şablonlar │ [parça] [parça] [parça] ...       │
│ NPC       │                                  │
│ Referans  │                                  │
├───────────┴──────────────────────────────────┤
│ GÖRÜNÜM Üstten Kamera · Üst Katları Gizle   TEST Oyuncu Ekle   SEÇİM Şablon Kaydet │
└──────────────────────────────────────────────┘
```
- **Bölümler:** *İnşa* (yerleştirme), *Analiz* (istatistik, harita), *Çıktı* (bake, OBJ). Eklentiler kendi bölümlerini ekleyebilir.
- **Kategoriler (sol):** *Zemin* ve parçalar klasörünün her alt klasörü. Kendi klasörüne ikon vermek için içine
  `_icon.png` koy.
- **Fırça kartı:** Seçili parçanın önizlemesi, modu, katı ve açısı; kat, döndür ve bırak düğmeleri.
- **Mod çubuğu:** Yerleştirme, silme ve ölç/işaretle modları; gruplar ince çizgiyle ayrılır. Üzerine gelince adı, kısayolu ve açıklaması görünür. Seçili parçada kullanılamayan modlar soluktur.
- **Parçaya tıkla:** Fırça seçilir. **Scene'e sürükle:** Tek bir parça bırakılır. **Sağ tık:** Project'te göster veya düzenle.
- **Alt çubuk:** Görünüm, Test ve Seçim gruplarında düğmeler. Dar pencerede yalnızca ikonlar görünür.
- Pencere daraldığında (440 px altı) kategori listesi ikon moduna geçer.

![Palet ikonları (koyu ve açık tema)](Documentation~/images/ikonlar.png)

### Fırça modları
| Tuş | Mod | Ne yapar |
|---|---|---|
| 1 | **Tek** | Tıkla: tek parça. Sürükle: imlecin geçtiği yerlere boyar |
| 2 | **Hat** | Tut-çek: düz hat. İlk harekette eksene kilitlenir |
| 3 | **Alan** | Tut-çek: dikdörtgeni doldurur (duvar/çitte yok) |
| 4 | **Çerçeve / Oda** | Tut-çek: kenarlar. Duvar/çitte 4 kenarlı oda |
| 5 | **Serp** | Sürükle: imlecin çevresine rastgele serpiştirir (ayarları palette) |
| 6 | **Sil** | Tıkla: parça siler. Tut-çek: alanı siler |
| 7 | **Cetvel** | Tut-çek: mesafe, hücre sayısı, yürüme/koşma süresi. Sahneyi değiştirmez |
| 8 | **Fırça** *(zemin)* | Sürükle: yuvarlak fırçayla seçili zemin işlemini uygular. Patika (Sadece Renk + Yol), kıyı, arazi (Yükselt/Alçalt). Yarıçap palette. Bir sürüklemede her hücreye bir kez uygulanır |
| 9 | **Bölge** | Tut-çek: bölge (spawn, görev...) çizer. Bkz. *Bölgeler* |
| 0 | **Yol Boyu** | Serbest çiz: yol boyunca aralıklarla dizer; yana kayma, iki yana, yola dönük |
| – | **Halka** | Merkezden dışarı sürükle: çember üstüne eşit aralıklarla dizer; merkeze/dışarı/rastgele bakar |

### Serbest yerleştirme (dekor ve küçük objeler)
Grid'siz koyma: parça imlecin tam altına, zeminin, platformun veya bir objenin **üstüne** oturur.
- Fırça açıkken **G** ile açılır/kapanır; *Free Placement Categories* ayarındaki kategorilerde (varsayılan **Dekor**) kendiliğinden açık.
- **R** serbest açıyla döndürür (*Free Rotation Step*, varsayılan 15°).
- Palette: *Aralık* (sürüklerken parça arası), *Rastgele dönüş*, *Rastgele boyut* (min–max), *Yüzeye otur*, *Eğime yatır*.
  Rastgelelik konumdan türetilir: önizleme = sonuç.
- Çizgi parçaları (duvar, çit) ve zemin her zaman grid'dedir. Serp, Yol Boyu ve Halka da yüzeye oturur; bina içine düşen noktalar atlanır.
- Sonradan: alt çubuk → **Yere Oturt** (seçimi altındaki yüzeye indirir), **Çeşitlendir** (rastgele dönüş ve boyut).
- **Komşuya hizala** (varsayılan açık): yakında bir parça varsa yeni parça onun yönüne döner ve onun yarım hücre ızgarasına oturur (önizlemede sarı kesik çizgi, "hizalı").

Palette arama kutusu bütün kategorilerde arar.

### Kısayollar (fırça seçiliyken, Scene'de)
| Tuş | İşlev |
|---|---|
| Shift + tık | Son noktadan buraya kadar doldurur (önizleme mavi) |
| Ctrl + tık / tut-çek | Her modda siler |
| R / Shift+R | 90° döndürür (serbest yerleştirmede ayarlardaki adım kadar) |
| G | Serbest yerleştirme aç / kapa |
| E / Q | Seviye yukarı / aşağı (prefab fırçasında otomatik katı kapatır) |
| Shift + tekerlek | Döndür (grid 90°, serbest adım) |
| H | Yarım hücre snap aç / kapa |
| Boşluk | Rastgeleyi yeniden at (dönüş, boyut, varyasyon, serpme) |
| Esc | Sürüklerken iptal eder, değilse fırçayı bırakır |

**Önizleme:** Hayalet ışıklı ve yarı saydamdır; dış çizgisi, zemindeki izi ve ön yön oku görünür. Başka bir objeyle
çakışırsa **kırmızı** olur (yine de konabilir).
**Otomatik kat** (fırça kartında *Oto*, varsayılan açık): Prefab fırçası imlecin altındaki zeminin katına kendiliğinden geçer;
platformun üstüne gelince 1, aşağıda 0. E/Q ile elle kat seçmek bunu kapatır.
**Tek modunda yön verme:** Mod ayarlarından *Sürükle: yön ver* seçilirse basılan yere tek parça konur ve fareyi çektiğin yöne
bakar; sonraki parçalar da o yönde gelir.
| Alt + sürükle | Kamerayı döndürür |

Hepsi **Edit → Shortcuts → World Kit** altından değiştirilebilir.

### Zemin
Zemin obje obje değil, **veri olarak** tutulur ve tek bir mesh'e çevrilir. Ne kadar büyük boyarsan boya
sahnede 1 obje, 1 mesh ve 1 collider kalır; görünmeyen yüzler üretilmez.
Üst seviyede boyamak platform yapar ve kenar duvarları otomatik oluşur.

### Duvar, çit, direk ("çizgi parçaları")
Footprint'inin bir ekseni 0 olan parçalar grid **köşelerine** oturur:
- İmleç en yakın köşeye yapışır (mavi nokta). Parça o köşeden R ile seçilen yöne uzanır.
- Hat ve oda uzunlukları parça boyunun katlarına yuvarlanır: hiçbir zaman taşmaz, köşeler birleşir.
- Yönü önemli parçalarda (kapı) R ile seçilen yöne en yakın açı korunur. Mavi ok ön yüzü gösterir.
- Aynı yere aynı parça, 180° çevrilmiş hali dahil, ikinci kez konmaz.

### Zemin türleri
Zemin sekmesindeki renk karelerinden bir tür seç (Zemin, Çimen, Toprak, Taş, Kum, Su, Yol...) ve boya.
- Türler **Project Settings → World Kit → Floor Types** listesindedir: ad + materyal. Yeni türleri listenin
  **sonuna** ekle; sırayı değiştirmek boyanmış hücrelerin türünü de değiştirir.
- **Surface Offset:** Türün yüzeyini kat düzleminden kaydırır (m). Su için ör. `-0.3`: su komşularından alçakta durur,
  kıyı kendiliğinden oluşur ve Erişim analizinde yürünemez sayılır. Yeni projelerde *Su* bununla gelir;
  mevcut projede ayarlardan elle gir.
- Başlangıç içeriği, liste boşsa 6 hazır tür oluşturur.

### Zemin işlemleri
Zemin sekmesinin altındaki **İşlem** seçicisi, zemin fırçasının ne yapacağını belirler. Hangi hücrelere
uygulanacağını ise üstteki mod belirler (Tek = fırça, Hat = şerit, Alan = dikdörtgen, Çerçeve = kenar).

| İşlem | Ne yapar |
|---|---|
| **Boya** | Seçili seviyede, seçili türle doldurur (yeni zemin ekler) |
| **Sadece Renk** | Yalnızca var olan zeminin türünü değiştirir; yükseklik ve şekil korunur. Patika, nehir, bölge için |
| **Yükselt** | Var olan zemini bir kat yükseltir. Tek moduyla sürükleyerek tepe veya teras "heykel"lenir |
| **Alçalt** | Var olan zemini bir kat alçaltır (çukur, nehir yatağı) |
| **Rampa** | Hücreyi yanındaki bir kat yüksek zemine doğru eğimli rampa yapar; yön kendiliğinden bulunur. Zeminin parçasıdır (tek mesh, yürünebilir). **Boya** ile düzleşir. **Yumuşak (2 hücre)** açıksa bir kat iki hücreye yayılır: platforma doğru 2 hücrelik bir hat çiz |
| **Duvarla Çevir** | Seçilen alanın dış kenarlarına duvar örer; zemini değiştirmez. Duvar sekmede seçilir (boşsa `Duvar_1m`) |
| **Kenar Süsle** | Seçilen alanın dış kenarlarına (uçurum, platform kenarı, kıyı) kaya, çalı gibi parçaları rastgele dizer. Parçalar, yoğunluk ve "uçurum dibine koy" sekmede ayarlanır. Aynı alanı tekrar süslemek üst üste koymaz |
| **Yüzey Boya** | Hücreden bağımsız, yumuşak ve tırtıklı kenarlı **patika, koyu çimen, taş yol** (kıvrımlı yollar). **Fırça (8)** ile sürükle, **Ctrl** ile sil; Alan/Hat hücreleri doldurur. Katmanlar ve materyalleri: Project Settings → World Kit → *Paint Layers* (kendi dokunu kullanabilirsin). Zemine dokunmaz, collider'ı yoktur; kat ve rampaları takip eder |

Duvarla Çevir'de bir kenar, komşu hücre seçimin dışındaysa veya farklı yükseklikteyse dış kenar sayılır;
duvar her hücrenin kendi yüksekliğine oturur. Uzun duvarlar (ör. 2 m) kenara sığdığı yerlerde kullanılır,
kalan yerlere 1 m'lik duvar konur. Köşe direkleri ve kapı açma bu duvarlarla da çalışır.

### Şablonlar (stamp)
1. Bir grup parçayı seç (ör. bir kamp, ev + çit + ağaçlar) ve palette **Şablon Kaydet**'e bas.
2. Şablon, altındaki zeminle birlikte `Parçalar/Sablonlar` klasörüne kaydedilir ve palette **Sablonlar** sekmesinde görünür.
3. Her parça gibi yerleştir. R ile döner, Hat ya da Alan moduyla çoğaltılabilir.
4. Yerleşince şablon açılır: parçalar normal parçalar gibi kendi gruplarına dağılır, zemini boyanır.
   Sonradan tek tek düzenlenebilirler.

### Varyasyonlar
Palette **Ctrl+tık** ile birden fazla parça seç (ör. 3 farklı ağaç, 2 kaya, farklı çitler). Fırça her yerleşimde
aralarından birini koyar; bütün modlarda (Tek, Hat, Alan, Çerçeve, Serp, Shift+tık) çalışır.
- Hangi noktaya hangisinin geleceği konumdan türetilir: önizlemede gördüğün aynen konur.
- Fırça kartında "+N varyasyon" yazar; Ctrl+tık seçili bir parçayı varyasyonlardan çıkarır.
- Grid hizası ilk seçilen parçanın footprint'ine göre yapılır; varyasyonların aynı boyutta olması önerilir.
- Aynı yerde varyasyonlardan biri zaten varsa yenisi konmaz.

### Serpme fırçası
Ağaç, kaya, çalı gibi bir parça seç ve **Serp** moduna geç (5). Sürükledikçe imlecin çevresine
rastgele serpiştirir. Palette ayarlanabilenler: yarıçap, adet, aralık (mevcut parçalar dahil),
rastgele dönüş ve boyut aralığı.
- Önizlemede ne görüyorsan o konur.
- Aynı yeri tekrar boyamak alanı aralık değerinden daha fazla sıklaştırmaz.

### Çıktı
Palette **Çıktı** sekmesi veya **Tools → World Kit → Çıktı**:
- **Bake:** Parçalar ve zemin, bölge bölge (*Bölge boyutu*, varsayılan 16 m; 0 = tek mesh) ve materyal başına
  birleştirilip `Assets/WorldKit/Baked` altına tek asset dosyasında kaydedilir. Her bölge ayrı renderer + collider olur,
  böylece kameranın görmediği bölgeler çizilmez. Orijinal istenirse gizlenir (Ctrl+Z ile geri alınır).
  Sonra düzenlemeye devam edip yeniden bake edebilirsin.
- **OBJ:** `.obj` + `.mtl` (renkler materyallerden alınır). Blender, Maya vb. için.
- İkisi de EditorOnly etiketli objeleri (ölçü referansları) ve gizli objeleri atlar.
- **Değiştir sekmesi:** Greybox'tan gerçek modele geçiş. *Bunu* (ör. `Ev_Kucuk_3x3`, ya da *Seçimden*) ve *Bununla*
  (yeni model) seç → *N Parçayı Değiştir*. Konum ve dönüş korunur; model farklı yöne bakıyorsa *Ek dönüş*,
  pivotu farklıysa *Konum kayması* kullan. *Yalnızca seçili parçalar* ile bir kısmını değiştir. Tek Ctrl+Z ile geri alınır.
  Yeni modelde `WorldKitPiece` yoksa footprint eskisinden kopyalanır.

### Bölgeler
Spawn alanı, görev noktası, güvenli / tehlikeli alan gibi **tetikleyici kutular** (`WorldKitZone`).
- Palette **Bölgeler** sekmesinde tür ve yükseklik seç → *Bölge Çiz* (veya fırça açıkken **9**) → tut-çek.
- Sahnede türüne göre renkli kutu ve adıyla görünür. *Ignore Raycast* katmanındadır: Erişim ve NavMesh engel saymaz.
- Oyunda: `onEnter` / `onExit` olaylarını Inspector'dan bağla (varsayılan yalnızca `Player` etiketi), ya da koddan
  `FindObjectsByType<WorldKitZone>` ile türüne / `zoneId`'sine göre bul. Spawn için `RandomPoint()`.

### Ölçüm
- **Cetvel (7):** Tut-çek ile ölç. Süreler Project Settings → World Kit → *Walk/Run Speed* değerlerinden hesaplanır.
- **İstatistik sekmesi:** Parça sayıları (kategoriye göre), zemin alanı (türe göre m²), üçgen sayısı, dünya boyutu.
- **Harita PNG:** İstatistik sekmesinde *Üstten Harita PNG Kaydet*. Ortografik, kuzey yukarıda; çözünürlük piksel/metre ile ayarlanır.
- **Erişim sekmesi (Analiz):** *Hesapla ve Göster* ile başlangıçtan yürüyerek ulaşılan zemin sahnede **yeşil**
  (uzaklaştıkça turuncu), ulaşılamayan **kırmızı** boyanır. Kapalı kalmış odaları, rampasız platformları, duvarla
  kesilmiş yolları bulmak için.
  - Başlangıç: seçili obje; seçim yoksa sahnedeki *Oyuncu* referansı; o da yoksa Scene kamerasının baktığı yer.
  - Engel = collider'ı olan her şey (duvar, bina, kaya...). Kapılardan geçilir. NPC'ler ve referanslar engel değildir.
  - Katlar arasında yalnızca rampadan geçilir. *Aşağı atlamaya izin ver* açıksa alçak komşuya inilebilir.
  - **Mor** hücreler dar geçit: ışınla geçiliyor ama karakter genişliği sığmayabilir (dar kapı, sıkışık kolonlar).
  - Gösterim açıkken her düzenlemeden sonra kendiliğinden yenilenir. *En Uzağa Git* / *Ulaşılamayana Git* kamerayı oraya götürür.
- **NavMesh sekmesi (Analiz):** *Bake Et* dünyanın collider'larından NavMesh üretir, `Assets/WorldKit/NavMesh` altına kaydeder
  ve köke `WorldKitNavMesh` ekler; NPC ve düşmanlardaki `NavMeshAgent`'lar Play'de ve build'de bunu kullanır. Ajan boyu ve
  yarıçapı *Character Height*'tan gelir; tam ve yumuşak rampalar yürünebilir. Ek paket gerekmez.
  - Dünya değişince yeniden bake et (kendiliğinden güncellenmez).
  - AI Navigation paketinin `NavMeshSurface`'i ile aynı sahnede kullanma (iki NavMesh üst üste biner).

### Duvara kapı açma
Bir çizgi parçasını, aynı hattaki başka bir çizgi parçasının üstüne koy (ya da taşı): eskisi kaldırılır.
Örneğin kapılı duvarı mevcut bir duvarın üstüne koymak kapıyı açar. Kısmen örtüşen eski parçalar da
kaldırılır; kalan boşluğu kısa duvarla doldur. Ayar: *Replace Overlapping Lines*.

### Otomatik köşe direkleri
Dik açıyla birleşen duvarların köşesine (L, T, + birleşimleri) direk kendiliğinden konur, köşe bozulunca kalkar.
Yalnızca direkle aynı kategorideki ve duvar yüksekliğinin en az %75'i olan parçalar sayılır; çitler ve
alçak duvarlar direk almaz. Direkler `Otomatik Köşeler` grubunda durur.
Ayar: *Auto Corner Posts*, *Corner Post Prefab* (boşsa başlangıç içeriğindeki `Kose_Diregi`).

### Seçimi düzenleme
Fırça kapalıyken World Kit parçalarını seç (Hierarchy veya Scene'de tıkla):

| Tuş | İşlev |
|---|---|
| Oklar | 1 hücre taşı (ekrana göre: ↑ her zaman ekranda yukarı) |
| PgUp / PgDn | Bir kat yukarı / aşağı |
| R / Shift+R | 90° döndür (çoklu seçimde grup olarak, merkezdeki köşe etrafında) |
| G | Grid'e oturt (serbestçe taşınmış parçaları düzeltir) |
| Ctrl+D | Çoğalt (Unity), ardından oklarla kaydır |
| Delete | Sil (köşe direkleri de güncellenir) |

**Üste Kopyala** (palet alt çubuğu → Seçim, veya Tools → World Kit): Seçili parçaların bir kopyasını tam üstlerine,
kat yüksekliğine yuvarlayarak koyar. Binaya kat eklemek, kule ve duvar yükseltmek için; her basışta bir kat daha.

Bu tuşlar yalnızca seçimde parça varken ve Scene odaktayken geçerlidir; diğer zamanlarda Unity'nin
kendi kısayolları (ör. R = ölçekle) aynen çalışır.

### Üst katları gizle
Palette **Üst Katları Gizle**: Çalışılan seviyenin (E/Q) üstündeki parçalar ve zemin gizlenir, seviye
değiştikçe güncellenir. Yalnızca görünümdür: sahneye kaydedilmez, Play'e girince her şey görünür.

### Sahne düzeni
```
<Kök> (ayarlardaki ad)
├── Zemin             WorldKitFloor (tek mesh)
├── <Kategori>        parçalar klasörünün alt klasör adı
├── Otomatik Köşeler  otomatik köşe direkleri
└── ...
```

---

## Kendi parçalarını eklemek

1. Prefab'ı `Parçalar klasörü/<Kategori>/` içine koy. Palete otomatik eklenir, yeni klasör yeni sekme olur.
2. **Pivot alt-ortada** olmalı.
3. İsteğe bağlı: `WorldKitPiece` bileşeni ekle ve **Footprint**'i (X, Z hücre) gir:
   - `1×1`, `3×3` gibi **tek** sayı → hücre ortasına oturur
   - `2×2`, `4×4` gibi **çift** sayı → grid çizgisine oturur
   - Bir ekseni **0** (ör. `2×0`) → çizgi parçası (duvar/çit gibi köşeden köşeye)
   - Bileşen yoksa boyut render sınırlarından tahmin edilir.
4. Parçanın **ön yüzü +Z** yönüne bakmalı (kapılar, NPC'ler).
5. **Performans:** ProBuilder ile yaptıysan **Tools → World Kit → Performans → Parça Prefab'larını Optimize Et**'e bas
   (mesh paylaşılır). Basit şekillerde MeshCollider yerine Box/Capsule collider kullan; küçük süslerde gölgeyi kapat.

## Performans

Yüzlerce ağaç, kaya ve bina olduğunda maliyeti belirleyen şeyler ve World Kit'in yaptıkları:

| Konu | Başlangıç içeriği | Kendi parçaların |
|---|---|---|
| **Paylaşılan mesh:** aynı parçanın bütün kopyaları tek mesh | Mesh'ler `Assets/WorldKit/Meshes` altında asset | ProBuilder'lı prefab'lar için *Parça Prefab'larını Optimize Et* |
| **Basit collider** | Kutu parçalar BoxCollider, ağaçta yalnızca gövde, ot/çalıda yok; MeshCollider sadece kapı, geçit, merdiven, rampa | Elle seç |
| **Gölge** | Ot ve çalı gölge atmaz | Küçük süslerde *Cast Shadows: Off* |
| **Static** | Bina/yapı: Occluder + Occludee; dekor: Occludee | Hareket etmeyenlerde aynısı |
| **GPU instancing** | Materyallerde açık | Materyalde *Enable GPU Instancing* |

- **İstatistik sekmesi → Performans:** Benzersiz mesh, "kendi mesh'ini taşıyan" parça, collider ve gölge sayıları ve
  öneriler. *Sahnedeki Parçaları Düzelt*: eski kopyalarda kalmış ayrı mesh'leri prefab'ın paylaşılan mesh'ine döndürür
  (başka sahnelerin varsa her birini açıp bir kez bas).
- **GPU Resident Drawer (Unity 6, URP):** Aynı parçadan yüzlerce olduğunda en büyük kazanç. URP Asset → *GPU Resident
  Drawer: Instanced Drawing*, Renderer → *Forward+*, Graphics → *BatchRendererGroup Variants: Keep All*.
  Performans raporu açık olup olmadığını gösterir.
- **Bitmiş bölümler için Bake:** Bölge bölge birleştirir (varsayılan 16 m); kameranın görmediği bölgeler çizilmez.

## Proje ayarları

**Edit → Project Settings → World Kit** (`Assets/WorldKit/WorldKitSettings.asset`, Git ile paylaşılır).

| Ayar | Açıklama |
|---|---|
| Cell Size / Level Height | Grid hücresi ve kat yüksekliği (m) |
| Root Name / Floor Name | Sahnedeki kök ve zemin objelerinin adı |
| Pieces Folder / Category Order | Paletin okuduğu klasör ve sekme sırası |
| Floor Materials / Thickness | Zemin görünümü |
| Wall Height, Character Height... | Yalnızca başlangıç içeriği için |

## Test modülü

Sahnede oyuncu yoksa Play'e basınca Scene kamerasının baktığı yere **geçici** bir oyuncu konur.
Kalıcı başlangıç noktası için palette **Oyuncu Ekle**'ye bas.

| Tuş | İşlev |
|---|---|
| WASD / oklar | Yürü (ekrana göre) |
| Shift | Koş |
| Q / E | Kamerayı 45° döndür |
| Tekerlek | Zoom |

Kamera (`WorldKitCameraRig`) Cinemachine ile çalışır:
- Engelin önüne gelir, binanın içine girmez. *Avoid Obstacles Up To Distance*'tan uzak zoom'da bu kapanır.
- Yürüme yönüne doğru önden bakar, koşarken hafifçe uzaklaşır.
- Bütün değerler Play'de Inspector'dan canlı ayarlanabilir.

## Git ile ekip çalışması

- `Assets/WorldKit/WorldKitSettings.asset`'i commit'le. Böylece herkes aynı ayarları kullanır.
- Ekip arkadaşları için adım adım kurallar: [Ekip rehberi → Git](Documentation~/Rehber.md#5-git-ile-çalışma-kuralları).
- Sahne çakışmalarını önlemek için herkes kendi sahnesinde çalışsın.
- Unity YAML birleştirme için `.gitattributes`'a `*.unity merge=unityyamlmerge` ekle
  ve `git config merge.unityyamlmerge.driver` ayarını yap.

## Sorun giderme

| Belirti | Çözüm |
|---|---|
| Palet boş | Ayarlardaki *Pieces Folder* doğru mu? İçinde alt klasör ve prefab var mı? |
| Zemin pembe veya görünmez | Ayarlarda zemin materyallerini ata ya da başlangıç içeriğini üret |
| Önizleme resmi gri | Palette yenile (⟳) düğmesine bas |
| Kısayol çalışmıyor | Scene penceresi odakta mı? Edit → Shortcuts → World Kit'te çakışma var mı? |
| "Oyuncu Ekle" yok | Cinemachine 3.x ve Input System kurulu mu? |
| Silinen duvarın direği kaldı | Unity menüsünden (Edit → Delete) silindiyse modüller haberdar olmaz. Direği elle sil veya parçaları Ctrl+tık / Delete (Scene odakta) ile sil |
| Oklar parçayı değil kamerayı oynatıyor | Scene penceresine bir kez tıklayıp odakla; seçimde World Kit parçası olmalı |

## Lisans

[MIT](LICENSE.md). Ticari projeler dahil özgürce kullanılabilir, değiştirilebilir ve dağıtılabilir; telif satırı korunmalı.
