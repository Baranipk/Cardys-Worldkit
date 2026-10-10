# World Kit: Yapay zeka asistanları için talimatlar

Bu paket üzerinde çalışırken bu kurallara uy. Ayrıntılar: `Documentation~/Gelistirme.md`.

## Ne bu?
Unity 6 için grid tabanlı world building editör aracı (UPM paketi `com.baran.worldkit`).
Hedef: basit, kullanıcıyı kısıtlamayan, eklentiyle genişleyen bir araç. Birden çok projede kullanılır.

## Mimari (kısa)
- `Editor/Core/WorldKitBrush.cs`: Scene girdisi, önizleme, yerleştirme/silme, Undo. Durum `BrushContext`'te.
- `Editor/Tools/`: Fırça modları. `WorldKitTool` alt sınıfı **nereye** konacağını hesaplar, gerisini fırça yapar.
- `Editor/Core/WorldKitLayout.cs` + `WorldKitGrid.cs`: Saf yerleşim ve snap hesapları.
- `Editor/Palette/`: Palet. Sekmeler klasörlerden + `WorldKitPaletteTab`; alt çubuk `WorldKitToolbarItem`.
- `Editor/Core/WorldKitSettings.cs`: Projeye özel tüm ayarlar (`Assets/WorldKit/WorldKitSettings.asset`).
- `Runtime/WorldKitFloor.cs`: Hücre verisinden tek mesh zemin. `Runtime/WorldKitPiece.cs`: Footprint.
- `Runtime/Playtest` + `Editor/Playtest`: İsteğe bağlı modül (Cinemachine + Input System varsa derlenir).

## Kurallar
1. **Yeni özellik = eklenti.** Önce `WorldKitTool`, `WorldKitPaletteTab`, `WorldKitToolbarItem` veya
   `WorldKitEvents` ile çözmeyi dene. Çekirdeği yalnızca gerçekten gerekiyorsa değiştir.
2. **Sabit ölçü veya yol yazma.** `WorldKitGrid.CellSize`, `WorldKitGrid.LevelHeight`, `WorldKitSettings.Instance` kullan.
3. **Her sahne değişikliği Undo'lu** ve bir kullanıcı eylemi = tek Undo grubu.
4. **Çekirdeğe yeni paket bağımlılığı ekleme.** Gerekirse ayrı assembly + `versionDefines`/`defineConstraints`.
5. **Serileştirilmiş alan adlarını değiştirme** (ayarlar, `WorldKitFloor.m_Cells`, `WorldKitPiece.footprint`).
   Değişmesi şartsa `[FormerlySerializedAs]`.
6. **Script dosyalarını taşırken `.meta`'larını da taşı.** GUID değişirse sahnelerdeki referanslar kopar.
7. **Dil:** Arayüz metinleri, yorumlar ve belgeler Türkçe; tip/alan/metot adları İngilizce.
8. **Stil:** Mevcut kodu takip et. `s_` static, `m_` instance private alan; açıklayıcı `<summary>` yorumları.

## Doğrulama
- Unity açıkken derleme sonucu: `%LOCALAPPDATA%/Unity/Editor/Editor.log` içinde `error CS` ara.
- Unity'yi beklemeden derleme kontrolü: Unity'nin Roslyn'i
  (`<Unity>/Editor/Data/DotNetSdkRoslyn/csc.dll`, `NetCoreRuntime/dotnet.exe` ile) +
  `Managed/UnityEngine/*.dll`, `NetStandard/ref/2.1.0/netstandard.dll` ve
  `Library/ScriptAssemblies/Unity.ProBuilder.dll` (Playtest için `Unity.InputSystem.dll`,
  `Unity.Cinemachine.dll`) referanslarıyla her assembly'yi ayrı derle.
- Çekirdeğin Playtest olmadan derlendiğini mutlaka kontrol et.
- Davranış değişikliklerinde `Documentation~/Gelistirme.md` içindeki test listesini uygula
  veya kullanıcıdan uygulamasını iste.

## Belgeler
Kullanıcıya görünen bir davranış değişirse `README.md`'yi, eklenti API'si değişirse
`Documentation~/Gelistirme.md`'yi, her sürümde `CHANGELOG.md`'yi güncelle.
