using System.Collections.Generic;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// Zemin seti: zeminin kat kenarlarına (uçurumlara) otomatik dizilen modüler parçalar. Kendi modellediğin taş duvar ve
    /// çimen saçağı parçalarını buraya bağlarsın; World Kit her kenara ve köşeye doğru parçayı koyar.
    /// Her yuvaya birden fazla parça (varyasyon) konabilir; konumdan seçilir.
    ///
    /// Parça kuralları (hepsi 1 hücre / 1 kat ölçüsünde, ölçek 1):
    /// - Duvar: pivot hücre kenarının ortası, tabanda. Yerel +Z dışarı (alçak tarafa) bakar. Genişlik 1 hücre (X),
    ///   yükseklik 1 kat (Y). Ön yüzü kenardan biraz dışarı taşmalı (zeminin yan yüzüyle çakışmasın).
    /// - Köşeler: pivot köşe noktası. Yerel +X ve +Z, köşede birleşen iki duvarın baktığı yönlerdir
    ///   (dış köşede dışarı, iç köşede boşluğa doğru).
    /// - Üst kenar (çimen saçağı): duvarla aynı kurallar; pivot kenarın üstünde (zemin yüzeyi hizasında).
    /// </summary>
    [CreateAssetMenu(menuName = "World Kit/Zemin Seti", fileName = "ZeminSeti")]
    public class WorldKitFloorSet : ScriptableObject
    {
        [Header("Uçurum duvarı (her kat için bir sıra)")]
        [Tooltip("Düz duvar parçaları (1 hücre geniş, 1 kat yüksek). Birden fazlaysa konuma göre biri seçilir.")]
        public List<GameObject> wall = new List<GameObject>();
        [Tooltip("Dış (dışbükey) köşe dolgusu.")]
        public List<GameObject> wallOuterCorner = new List<GameObject>();
        [Tooltip("İç (içbükey) köşe dolgusu. Boş bırakılabilir.")]
        public List<GameObject> wallInnerCorner = new List<GameObject>();

        [Header("Üst kenar (çimen saçağı)")]
        public List<GameObject> trim = new List<GameObject>();
        public List<GameObject> trimOuterCorner = new List<GameObject>();
        public List<GameObject> trimInnerCorner = new List<GameObject>();

        [Header("Kurallar")]
        [Tooltip("Haritanın kenarında (zemin olmayan yere bakan) yükseltiler için 0. kata kadar duvar konsun mu?")]
        public bool wallsAtMapEdge = true;
        [Tooltip("Rampalı hücrelerin üst kenarına çimen saçağı konmaz (rampa düz parçalarla uyuşmaz).")]
        public bool skipRampTops = true;
    }
}
