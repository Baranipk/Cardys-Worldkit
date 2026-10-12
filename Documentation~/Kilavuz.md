# World Kit Kullanım Kılavuzu

World Kit ile bir bölümün greybox'ını baştan sona nasıl kuracağını anlatır: kavramlar, arayüz, bütün araçlar,
hazır tarifler ve bölümü teslim etmeden önce yapılacak kontroller.

- Kısa başlangıç: [Ekip rehberi](Rehber.md) · Özellik listesi ve ayrıntılar: [README](../README.md) ·
  Kendi araçlarını yazmak: [Geliştirme kılavuzu](Gelistirme.md)

---

## İçindekiler
1. [Temel kavramlar](#1-temel-kavramlar)
2. [Arayüz](#2-arayüz)
3. [İlk bölüm: adım adım](#3-ilk-bölüm-adım-adım)
4. [Fırça modları](#4-fırça-modları)
5. [Grid ve serbest yerleştirme](#5-grid-ve-serbest-yerleştirme)
6. [Zemin ve arazi](#6-zemin-ve-arazi)
7. [Duvar, çit, kapı](#7-duvar-çit-kapı)
8. [Dekor ve doğa](#8-dekor-ve-doğa)
9. [Şablonlar](#9-şablonlar)
10. [Seçimi düzenlemek](#10-seçimi-düzenlemek)
11. [Bölgeler](#11-bölgeler)
12. [Analiz](#12-analiz)
13. [Çıktı ve gerçek modellere geçiş](#13-çıktı-ve-gerçek-modellere-geçiş)
14. [Oyunda test](#14-oyunda-test)
15. [Performans](#15-performans)
16. [Bütün kısayollar](#16-bütün-kısayollar)
17. [Sorun giderme](#17-sorun-giderme)

---

## 1. Temel kavramlar

| Kavram | Anlamı |
|---|---|
| **Hücre** | Grid'in bir karesi. Varsayılan 1 m (Project Settings → World Kit → *Cell Size*). |
| **Kat / seviye** | Yükseklik basamağı. Varsayılan 1 m (*Level Height*). Fırça hangi kattaysa oraya koyar (E/Q). |
| **Parça** | Palette seçilip sahneye konan prefab (ev, ağaç, duvar...). Sahnede `<Kök>/<Kategori>/<Parça>` altında durur. |
| **Kategori** | Parçalar klasöründeki her alt klasör (Yapi, Bina, Dekor, NPC...). Palette solda listelenir. |
| **Footprint** | Parçanın kapladığı hücre sayısı (X × Z). Tek sayı hücre ortasına, çift sayı grid çizgisine oturur. |
| **Çizgi parçası** | Footprint'inin bir ekseni 0 olan parça (duvar, çit). Hücreye değil, grid **köşelerine** oturur. |
| **Zemin** | Obje değil, hücre verisi. Ne kadar boyarsan boya sahnede tek mesh ve tek collider'dır. |
| **Serbest yerleştirme** | Grid'siz koyma: parça imlecin tam altına, yüzeyin üstüne oturur. Dekor için. |

## 2. Arayüz

**Palet:** Tools → World Kit → Palet (`Ctrl+Shift+G`). Scene penceresinin yanına sabitle.

```
┌ [İnşa] [Analiz] [Çıktı]                     ⟳ ⚙ ⓘ ┐  ← bölümler · yenile · ayarlar · yardım
│ Kategoriler │ Fırça kartı: seçili parça, mod, kat, açı │
│  Zemin      │ Mod çubuğu: yerleştir │ sil │ ölç-işaretle │
│  Yapı       │ Serbest yerleştirme ve mod ayarları        │
│  Bina       │ 🔍 arama (bütün kategorilerde)   boyut    │
│  Dekor ...  │ Parça ızgarası                             │
└ Alt çubuk: Görünüm · Test · Seçim düğmeleri ─────────────┘
```

- **Parçaya tıkla:** fırça o parçayla açılır. **Ctrl+tık:** varyasyon ekle/çıkar.
- **Paletten Scene'e sürükle:** tek parça bırakır.
- **Scene'deki bilgi kutusu (sol alt):** parça, mod, kat, açı ve o anki ipucu.
- **Esc:** fırçayı bırakır (sürüklerken basarsan yalnızca o çizimi iptal eder).

## 3. İlk bölüm: adım adım

1. **Yeni sahne** aç ve kaydet. Palet boşsa *Başlangıç İçeriğini Üret*.
2. **Zemin:** Palet → Zemin → *Çimen* → **Zemin Fırçası** → **Alan (3)** ile büyük bir dikdörtgen çiz.
3. **Yol:** *Yol* türü → işlem **Sadece Renk** → **Fırça (8)** ile sürükleyerek patika çiz.
4. **Tepe:** **E** ile kat 1'e çık → *Taş* → Alan ile platform çiz. Çıkış için işlem **Rampa**, *Yumuşak (2 hücre)*
   açık, platforma doğru 2 hücrelik bir hat çiz.
5. **Binalar:** Bina kategorisinden bir ev → tıkla. **R** ile döndür.
6. **Duvarlar:** Yapı → *Duvar_2m* → **Çerçeve/Oda (4)** ile oda çiz. Kapı için *Kapili_Duvar_3m*'yi aynı hatta üstüne koy.
7. **Doğa:** Dekor → ağaçlar (serbest yerleştirme kendiliğinden açılır) → **Serp (5)** ile kenarlara orman,
   **Yol Boyu (0)** ile yol kenarına fener.
8. **Kontrol:** Analiz → **Erişim** → *Hesapla ve Göster*. Kırmızı yer kalmasın.
9. **Play** ile gez. Kaydet.

## 4. Fırça modları

Mod çubuğunda gruplar halinde durur; sayı tuşlarıyla seçilir. Bir mod seçili parçada kullanılamıyorsa (ör. duvarda
*Alan*) fırça kendiliğinden uygun bir moda düşer.

| Tuş | Mod | Kullanım | En iyi olduğu iş |
|---|---|---|---|
| 1 | **Tek** | Tıkla: bir parça. Sürükle: geçtiğin her hücreye (serbestte her *Aralık* metrede) | Tek tek yerleştirme, boyama |
| 2 | **Hat** | Tut-çek: düz sıra, ilk harekette eksene kilitlenir | Duvar hattı, ağaç sırası |
| 3 | **Alan** | Tut-çek: dikdörtgeni doldurur | Zemin, tarla, düzenli bahçe |
| 4 | **Çerçeve / Oda** | Tut-çek: yalnızca kenarlar; duvarda 4 duvarlı oda | Oda, avlu, çit çevresi |
| 5 | **Serp** | Tıkla/sürükle: imleç çevresine rastgele serpiştirir | Orman, kaya kümeleri, ot |
| 6 | **Sil** | Tıkla: parça siler. Tut-çek: alan siler. (Ctrl her modda siler) | Temizlik |
| 7 | **Cetvel** | Tut-çek: mesafe, hücre, yürüme/koşma süresi | Ölçü, tempo |
| 8 | **Fırça** *(zemin)* | Yuvarlak fırçayla zemin işlemi | Patika, kıyı, arazi |
| 9 | **Bölge** | Tut-çek: spawn/görev/tetikleyici kutusu | İşaretleme |
| 0 | **Yol Boyu** | Serbest çiz: yol boyunca aralıklarla dizer | Fener sırası, yol kenarı ağaçları |
| – | **Halka** | Merkezden dışarı sürükle: çember üstüne dizer | Meydan, taş çemberi, kamp ateşi |

**Shift+tık:** Son konan yerden imlece kadar seçili modla doldurur (önizleme mavi). Uzun duvar ve sıralar için.

**Varyasyonlar:** Palette Ctrl+tık ile birkaç parça seç; fırça her yerleşimde birini koyar. Önizlemede gördüğün aynen konur.

## 5. Grid ve serbest yerleştirme

| | Grid (varsayılan) | Serbest |
|---|---|---|
| Konum | Hücre ortası / grid çizgisi / köşe | İmlecin tam altı |
| Yükseklik | Seçili kat (E/Q) | Altındaki yüzey: zemin, platform, obje üstü |
| Dönüş (R) | 90° | Ayarlardaki adım (varsayılan 15°) |
| Rastgelelik | Yok | İsteğe bağlı dönüş ve boyut |
| Ne için | Yapı, bina, duvar | Dekor, küçük objeler, doğa |

- **Açma/kapama:** Fırça açıkken **G**, ya da palette *Serbest yerleştirme* kutusu. Ayarlardaki
  *Free Placement Categories* listesindeki kategorilerde (varsayılan **Dekor**) kendiliğinden açılır.
- **Çizgi parçaları ve zemin** her zaman grid'dedir.
- **Ayarlar (palette, serbest açıkken):**
  - *Aralık:* Sürükleyerek boyarken iki parça arası en az mesafe.
  - *Rastgele dönüş / Rastgele boyut (min–max):* Her parça farklı görünsün. Konumdan türetilir: önizleme = sonuç.
  - *Yüzeye otur:* Kapalıysa parça seçili katın düzlemine konur.
  - *Eğime yatır:* Rampa ve eğimlerde parça yüzeye paralel durur.
  - *Komşuya hizala:* Yakındaki parçanın yönüne döner ve onun ızgarasına oturur (duvar önüne sandık dizmek).
- **Serp, Yol Boyu, Halka** grid'e bağlı değildir; serbest kapalı olsa da imleç ve parçalar yüzeye oturur.
  Binanın içine düşecek noktalar atlanır.
- **Sonradan düzeltme:** Alt çubuk → *Yere Oturt* (seçimi altındaki yüzeye indirir), *Çeşitlendir* (seçime rastgele
  dönüş ve boyut verir).

## 6. Zemin ve arazi

Palet → **Zemin**. Bir tür seç (Zemin, Çimen, Toprak, Taş, Kum, Su, Yol...) ve bir **işlem**:

| İşlem | Ne yapar |
|---|---|
| **Boya** | Seçili katta, seçili türle doldurur (yeni zemin) |
| **Sadece Renk** | Var olan zeminin türünü değiştirir, yüksekliğe dokunmaz (yol, patika) |
| **Yükselt / Alçalt** | Var olan zemini bir kat kaldırır / indirir |
| **Rampa** | Bir kat yüksek komşuya doğru eğim. *Yumuşak (2 hücre)*: bir kat iki hücreye yayılır |
| **Duvarla Çevir** | Alanın dış kenarlarına duvar örer |
| **Kenar Süsle** | Alanın dış kenarlarına (uçurum, kıyı) kaya/çalı dizer |

İşlem, seçili modun şekliyle uygulanır: **Tek** (hücre hücre), **Hat**, **Alan**, **Çerçeve**, **Fırça** (yuvarlak, yarıçap palette).

- **Su:** Su türü komşularından alçakta durur (ayarlarda *Surface Offset*, ör. -0.3); kıyı kendiliğinden oluşur, yürünemez.
- **Platform:** E ile kat seç ve Boya. Kenar yüzleri otomatik.
- **Uçurum dibi:** Kenar Süsle'de *Uçurum dibine koy* açıksa kayalar aşağıya konur.
- **Üst katları gizle** (alt çubuk): Çalıştığın katın üstündekileri gizler; iç mekan ve alt katlarda çalışmak için.

## 7. Duvar, çit, kapı

- Çizgi parçaları köşeye yapışır (mavi nokta) ve R ile seçilen yöne uzanır. Hat ve oda uzunlukları parça boyunun
  katlarına yuvarlanır; köşeler hep birleşir.
- **Oda:** Duvar seç → Çerçeve/Oda (4) → tut-çek.
- **Kapı açmak:** *Kapili_Duvar_3m* veya *Genis_Gecit_4m*'yi mevcut duvarın üstüne aynı hatta koy; eskisi kalkar.
- **Köşe direkleri:** L, T, + birleşimlerine kendiliğinden konur ve duvar kalkınca kalkar (ayarlardan kapatılabilir).

## 8. Dekor ve doğa

Hazır tarifler:

| İstediğin | Nasıl |
|---|---|
| **Orman** | Ctrl+tık ile 2 ağaç türü → Serp (5), yarıçap 4, adet 6, aralık 1.5 → sürükle |
| **Orman kenarı yumuşak geçiş** | Aynı ağaçlar → Serp, adet 2, aralık 3 → kenarda gezdir |
| **Yol kenarı fenerleri** | Fener → Yol Boyu (0), aralık 6, yana kayma 2, *İki yana* → yolu takip ederek sürükle |
| **Patika boyunca ağaç** | Ağaç → Yol Boyu, aralık 4, yana kayma 2.5, *Yola dönük* kapalı |
| **Meydan** | Ağaç → Halka, aralık 4, yön *Rastgele* → meydanın ortasından dışarı sürükle |
| **Taş çemberi** | Kaya → Halka, aralık 1.5, yön *Merkeze* |
| **Kıyı kayaları** | Zemin → Kenar Süsle → gölü çevreleyen alanı seç |
| **Dağınık ot/çalı** | Ot → serbest, aralık 0.8, rastgele boyut 0.7–1.3 → Tek (1) ile sürükle |
| **Masa üstüne obje** | Serbest + *Yüzeye otur* → objenin üstüne tıkla |

İpucu: Ot ve çalı gibi küçük parçaların collider'ı ve gölgesi yoktur; bol kullanabilirsin.

## 9. Şablonlar

Sık tekrar eden grupları (kamp, çeşme köşesi, ev + bahçe) bir kez kurup çoğalt:
1. Parçaları seç → alt çubuk **Şablon Kaydet**. Altındaki zemin (rampalar dahil) de kaydedilir.
2. Palette **Sablonlar** kategorisinden her parça gibi yerleştir; R ile döner.
3. Yerleşince açılır: parçalar normal parçalara dönüşür, tek tek düzenlenebilir.

## 10. Seçimi düzenlemek

Fırça kapalıyken parçaları seç (Scene'de tıkla veya Hierarchy):

| Tuş / düğme | İşlev |
|---|---|
| Oklar | 1 hücre taşı (ekrana göre) |
| PgUp / PgDn | Bir kat yukarı / aşağı |
| R / Shift+R | 90° döndür (çoklu seçimde grup olarak) |
| G | Grid'e oturt |
| Delete | Sil |
| Ctrl+D | Çoğalt (Unity) |
| **Üste Kopyala** | Kopyasını üstüne koy (kat ekle, kule yükselt) |
| **Yere Oturt** | Altındaki yüzeye indir |
| **Çeşitlendir** | Rastgele dönüş ve boyut |
| **Şablon Kaydet** | Seçimi şablon yap |

## 11. Bölgeler

Spawn alanı, görev noktası, güvenli/tehlikeli alan gibi **tetikleyici kutular**.
1. Palet → **Bölgeler** → tür ve yükseklik seç → *Bölge Çiz* (veya fırça açıkken **9**).
2. Tut-çek. Bölge türüne göre renkli kutu ve adıyla görünür.
3. Oyun tarafı: `WorldKitZone` bileşeninde `onEnter` / `onExit` olaylarını Inspector'dan bağla (varsayılan yalnızca
   `Player` etiketi tetikler) ya da koddan `zoneId` ile bul. Spawn için `RandomPoint()`.

## 12. Analiz

| Sekme / araç | Ne için |
|---|---|
| **Erişim** | Başlangıçtan yürüyerek ulaşılan zemin yeşil, ulaşılamayan kırmızı, dar geçit mor. Kapalı oda ve rampasız platform bulur |
| **NavMesh** | NPC/düşmanlar için NavMesh bake eder ve gösterir. Dünya değişince yeniden bake et |
| **İstatistik** | Parça sayıları, zemin alanı, üçgen, dünya boyutu, köşeden köşeye süre; **Performans** raporu |
| **Harita PNG** | İstatistik sekmesinde; üstten harita görüntüsü (tasarım dokümanı için) |
| **Cetvel (7)** | İki nokta arası mesafe ve yürüme süresi |

## 13. Çıktı ve gerçek modellere geçiş

- **Değiştir** (Çıktı): Bir greybox parçanın bütün kopyalarını gerçek modelle değiştirir. *Bunu* (veya *Seçimden*) →
  *Bununla* → *N Parçayı Değiştir*. Model farklı yöne bakıyorsa *Ek dönüş*, pivotu farklıysa *Konum kayması*.
- **Bake:** Bitmiş bölümü bölge bölge (varsayılan 16 m) birleştirir; orijinal gizlenebilir, Ctrl+Z ile geri döner.
- **OBJ:** Sanatçılar için .obj + .mtl.

## 14. Oyunda test

**Play**'e bas: sahnede oyuncu yoksa Scene kamerasının baktığı yere geçici oyuncu gelir (Cinemachine + Input System
kuruluysa). Kalıcı başlangıç için alt çubuk → *Oyuncu Ekle*.
WASD yürü, Shift koş, Q/E kamera döndür, tekerlek zoom. Kamera binaların içine girmez.

## 15. Performans

- Başlangıç parçaları mesh'i paylaşır, basit collider kullanır; yüzlerce ağaç sorun değildir.
- Kendi ProBuilder parçaların için: Tools → World Kit → Performans → **Parça Prefab'larını Optimize Et**.
- **İstatistik → Performans:** "kendi mesh'ini taşıyan" 0 olmalı; değilse *Sahnedeki Parçaları Düzelt*.
- Çok sayıda aynı parça varsa **GPU Resident Drawer**'ı aç (raporda adımlar yazar).
- Küçük süslerde (ot, çalı) collider ve gölge gereksizdir.

## 16. Bütün kısayollar

**Fırça açıkken (Scene odakta):**

| Tuş | İşlev |
|---|---|
| 1–9, 0 | Mod seç |
| R / Shift+R | Döndür (grid: 90°, serbest: adım) |
| E / Q | Kat yukarı / aşağı |
| G | Serbest yerleştirme aç/kapa |
| H | Yarım hücre snap |
| Shift + tekerlek | Döndür |
| Boşluk | Rastgeleyi yeniden at |
| Ctrl + tık/sürükle | Sil |
| Shift + tık | Son noktadan buraya doldur |
| Esc | Çizimi iptal / fırçayı bırak |
| Alt + sürükle | Kamerayı döndür (Unity) |

**Palette:** `Ctrl+Shift+G` palet · tık: fırça · Ctrl+tık: varyasyon · sürükle: Scene'e bırak.
**Seçimde:** bkz. [Seçimi düzenlemek](#10-seçimi-düzenlemek).
Hepsi **Edit → Shortcuts → World Kit** altından değiştirilebilir.

## 17. Sorun giderme

| Sorun | Çözüm |
|---|---|
| Tıklayınca hiçbir şey olmuyor | Scene penceresine bir kez tıkla (odak). Fırça açık mı (Scene'de bilgi kutusu var mı)? |
| Parça yanlış yükseklikte | Grid'de kat (E/Q) yanlış; serbestte *Yüzeye otur* açık mı? Sonradan: *Yere Oturt* |
| Serbest parçalar üst üste biniyor | *Aralık*'ı büyüt |
| Ağaçlar binanın üstüne çıktı | Serbestte imleç binanın üstündeyken çatıya konur; *Yüzeye otur*'u kapat veya Serp kullan (bina içi atlanır) |
| Duvar yerine kaydı | Duvarlar köşeye yapışır; R ile yönü değiştir |
| Rampa oluşmadı | Rampa, *bir kat yüksek* komşuya doğru olur. Yumuşak rampada 2 hücre aynı katta olmalı |
| Erişim'de kapı geçilmiyor | Kapı açıklığı karakterden dar olabilir (mor gösterilir) ya da kapılı duvar eski duvarın yerini almamış |
| Palette yeni prefab yok | Başlıktaki yenile (⟳) |
| Kısayol çalışmıyor | Scene odakta mı? Edit → Shortcuts'ta çakışma var mı? |

Daha fazlası: [README → Sorun giderme](../README.md#sorun-giderme).
