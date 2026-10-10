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
| | Zemin işlemleri: Boya, Sadece Renk, Yükselt, Alçalt, Duvarla Çevir | ✅ |
| | Şablonlar: seçimi zeminiyle birlikte kaydet, palette yerleştir | ✅ |
| | Serpme fırçası: rastgele konum, dönüş, boyut ve aralık | ✅ |
| **Düzenleme** | Seç, taşı, döndür, çoğalt, sil (grid'e bağlı, klavyeyle) | ✅ |
| | Seviye görünürlüğü (üst katları gizle) | ✅ |
| **Ölçüm** | Cetvel (mesafe, hücre, yürüme/koşma süresi) | ✅ |
| | İstatistik sekmesi (parça, zemin alanı, üçgen, dünya boyutu) | ✅ |
| | Üstten harita PNG | ✅ |
| **Çıktı** | Bake: materyal başına tek mesh asset + collider | ✅ |
| | OBJ + MTL dışa aktarma | ✅ |
| **Test** *(modül)* | Test oyuncusu, Cinemachine top-down kamera, engelden kaçma | ✅ |
| **İçerik** *(isteğe bağlı)* | Duvar, bina, dekor, chibi NPC ve ölçü referansları | ✅ |

---

## Kullanım

### Palet
```
┌──────────────────────────────────────────────┐
│ [İnşa] [Analiz] [Çıktı]          ⟳  ⚙  ⓘ    │  bölümler · yenile · ayarlar · yardım
├───────────┬──────────────────────────────────┤
│ Zemin     │ [önizleme] Duvar 2m              │  seçili fırça: mod · kat · açı
│ ───────── │            Alan · Kat 0 · 0°     │  [− kat +] [↻] [Bırak]
│ Yapı      │ YERLEŞTİR ▪ ━ ▦ ▢ ∴  SİL ✕  ÖLÇ ▭ │  modlar (1-7)
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
- **Mod çubuğu:** Yerleştir / Sil / Ölç grupları. Üzerine gelince adı, kısayolu ve açıklaması görünür.
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

### Kısayollar (fırça seçiliyken, Scene'de)
| Tuş | İşlev |
|---|---|
| Shift + tık | Son noktadan buraya kadar doldurur (önizleme mavi) |
| Ctrl + tık / tut-çek | Her modda siler |
| R / Shift+R | 90° döndürür |
| E / Q | Seviye yukarı / aşağı |
| Esc | Sürüklerken iptal eder, değilse fırçayı bırakır |
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
| **Duvarla Çevir** | Seçilen alanın dış kenarlarına duvar örer; zemini değiştirmez. Duvar sekmede seçilir (boşsa `Duvar_1m`) |

Duvarla Çevir'de bir kenar, komşu hücre seçimin dışındaysa veya farklı yükseklikteyse dış kenar sayılır;
duvar her hücrenin kendi yüksekliğine oturur. Uzun duvarlar (ör. 2 m) kenara sığdığı yerlerde kullanılır,
kalan yerlere 1 m'lik duvar konur. Köşe direkleri ve kapı açma bu duvarlarla da çalışır.

### Şablonlar (stamp)
1. Bir grup parçayı seç (ör. bir kamp, ev + çit + ağaçlar) ve palette **Şablon Kaydet**'e bas.
2. Şablon, altındaki zeminle birlikte `Parçalar/Sablonlar` klasörüne kaydedilir ve palette **Sablonlar** sekmesinde görünür.
3. Her parça gibi yerleştir. R ile döner, Hat ya da Alan moduyla çoğaltılabilir.
4. Yerleşince şablon açılır: parçalar normal parçalar gibi kendi gruplarına dağılır, zemini boyanır.
   Sonradan tek tek düzenlenebilirler.

### Serpme fırçası
Ağaç, kaya, çalı gibi bir parça seç ve **Serp** moduna geç (5). Sürükledikçe imlecin çevresine
rastgele serpiştirir. Palette ayarlanabilenler: yarıçap, adet, aralık (mevcut parçalar dahil),
rastgele dönüş ve boyut aralığı.
- Önizlemede ne görüyorsan o konur.
- Aynı yeri tekrar boyamak alanı aralık değerinden daha fazla sıklaştırmaz.

### Çıktı
Palette **Çıktı** sekmesi veya **Tools → World Kit → Çıktı**:
- **Bake:** Parçalar ve zemin materyal başına birleştirilip tek mesh asset'i olarak `Assets/WorldKit/Baked`
  altına kaydedilir. Sonuç tek bir renderer ve collider olur; orijinal istenirse gizlenir (Ctrl+Z ile geri alınır).
  Sonra düzenlemeye devam edip yeniden bake edebilirsin.
- **OBJ:** `.obj` + `.mtl` (renkler materyallerden alınır). Blender, Maya vb. için.
- İkisi de EditorOnly etiketli objeleri (ölçü referansları) ve gizli objeleri atlar.

### Ölçüm
- **Cetvel (7):** Tut-çek ile ölç. Süreler Project Settings → World Kit → *Walk/Run Speed* değerlerinden hesaplanır.
- **İstatistik sekmesi:** Parça sayıları (kategoriye göre), zemin alanı (türe göre m²), üçgen sayısı, dünya boyutu.
- **Harita PNG:** İstatistik sekmesinde *Üstten Harita PNG Kaydet*. Ortografik, kuzey yukarıda; çözünürlük piksel/metre ile ayarlanır.

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
