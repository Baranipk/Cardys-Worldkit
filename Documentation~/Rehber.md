# World Kit: Ekip Rehberi

Level design'a yardım edecek ekip arkadaşları için adım adım rehber. Her özelliğin ayrıntısı [README](../README.md)'de.
Kod yazmak gerekmez.

---

## 1. Kurulum (bir kez, 5 dk)

1. Projeyi Git'ten çek ve Unity 6 ile aç. World Kit, projenin `Packages/manifest.json` dosyasında tanımlıysa
   kendiliğinden kurulur. Değilse: **Window → Package Manager → + → Add package from git URL** →
   `https://github.com/Baranipk/Cardys-Worldkit.git`
2. **Tools → World Kit → Palet** (`Ctrl+Shift+G`) ile paleti aç. Scene penceresinin yanına sabitle.
3. Palet boşsa **Başlangıç İçeriğini Üret**'e bas.

## 2. İlk 10 dakika: örnek köyü gez

1. **Tools → World Kit → Örnek Köy Oluştur**. Yeni bir sahnede küçük bir köy kurulur.
2. **Play**'e bas. Test oyuncusu kameranın baktığı yerde doğar.
   - **WASD** yürü, **Shift** koş, **Q/E** kamerayı döndür, **tekerlek** yakınlaş.
3. Play'den çık. Palette **Analiz → Erişim → Hesapla ve Göster**: yeşil yerlere yürünebiliyor,
   kırmızılara ulaşılamıyor.

Bu sahneyle istediğin gibi oyna; kaydetmezsen hiçbir şey kalıcı olmaz.

## 3. Kendi bölümünü kurmak

Her adım tek Undo'dur: yanlış bir şey yaparsan **Ctrl+Z**.

| Ne yapmak istiyorsun? | Nasıl? |
|---|---|
| Yeni sahne | **File → New Scene**, sonra `Assets/Scenes/` altına kendi adınla kaydet |
| Zemin dök | Palet → **Zemin** sekmesi → bir tür seç (Çimen, Toprak...) → **Alan** modunda (3) tut-çek |
| Yükselti / platform | **E / Q** ile seviye seç, sonra boya. Ya da var olan zemini **Yükselt** işlemiyle kaldır |
| Platforma çıkış | **Rampa** işlemi; yumuşak eğim için **Yumuşak (2 hücre)** aç ve platforma doğru 2 hücrelik hat çiz |
| Yol, patika, kıyı | **Sadece Renk** işlemi + **Fırça** modu (8): sürükleyerek boya |
| Gölet | Zemini **Su** türüyle boya (su biraz alçakta durur) |
| Duvar, oda | **Yapı** kategorisinden bir duvar → **Oda** modu (4) ile tut-çek |
| Kapı | Duvarın üstüne aynı hatta **Kapili_Duvar** çiz (eskisinin yerini alır) |
| Bina, ağaç, NPC | Kategoriden seç → tıkla. **R** ile döndür |
| Orman, kaya serpme | Birkaç ağacı **Ctrl+tık** ile seç (varyasyon) → **Serp** modu (5) |
| Uçurum kenarını süsle | Zemin → **Kenar Süsle** işlemi → kenarı olan alanı seç |
| Sil | **Ctrl** basılıyken tıkla ya da tut-çek |
| Fırçayı bırak | **Esc** |

Seçili parçaları düzenlemek (fırça kapalıyken): **oklar** taşı, **PgUp/PgDn** kat, **R** döndür, **Delete** sil,
**Üste Kopyala** (alt çubuk) kat ekle.

## 4. Kontrol listesi (bölümü teslim etmeden önce)

- [ ] **Play**'de baştan sona yürüdün mü? Takıldığın, düştüğün yer var mı?
- [ ] **Erişim** analizinde kırmızı (ulaşılamayan) yer kalmadı mı? Mor (dar geçit) yerler bilerek mi öyle?
- [ ] **İstatistik** sekmesinde en uzak nokta süresi hedefine uygun mu?
- [ ] NPC/düşman kullanılacaksa **NavMesh → Bake Et** yaptın mı?
- [ ] Sahneyi kaydettin mi?

## 5. Git ile çalışma kuralları

1. **Herkes kendi sahnesinde çalışır.** Aynı sahneyi iki kişi düzenlerse birleştirmek zordur.
2. Başlamadan önce **pull**, bitirince **commit + push**. Commit mesajında ne yaptığını yaz
   (ör. "Köy: batı yolu ve gölet").
3. Ortak dosyalara (`Assets/WorldKit/WorldKitSettings.asset`, `Assets/WorldKit/Pieces/`) dokunmadan önce ekibe haber ver.
4. Yeni parça eklediysen prefab'ı ve `.meta` dosyasını birlikte commit'le.
5. `Library/`, `Temp/` gibi klasörleri asla commit'leme (`.gitignore` bunu zaten yapar).

## 6. Sık sorulanlar

**Parça yerleşmiyor / önizleme görünmüyor.** Scene penceresine tıkla (odak orada olmalı). Seviye (E/Q) doğru mu?

**Yanlış katta.** Seçip **PgUp/PgDn** ile taşı.

**Palette yeni prefab'ım yok.** Paletin başlığındaki **yenile** düğmesine bas. Prefab, parçalar klasörünün bir alt
klasöründe olmalı (her alt klasör bir kategori).

**Bir şey bozuldu.** Ctrl+Z. Olmadıysa sahneyi kaydetmeden kapat ve Git'teki son hâline dön.

Daha fazlası: [README → Sorun giderme](../README.md#sorun-giderme).
