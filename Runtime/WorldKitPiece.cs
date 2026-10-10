using UnityEngine;

namespace WorldKit
{
    /// <summary>
    /// Paletteki bir parçanın grid bilgisini taşır. Pivot her zaman kaplanan alanın alt-ortasındadır.
    /// Bu bileşen isteğe bağlıdır: olmayan prefab'ların boyutu render sınırlarından tahmin edilir.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldKitPiece : MonoBehaviour
    {
        [Tooltip("Parçanın grid hücresi cinsinden kapladığı alan (X, Z).\n" +
                 "Tek sayı: hücre ortasına oturur. Çift sayı veya 0: grid çizgisine oturur.\n" +
                 "Bir ekseni 0 olan parçalar (duvar, çit) 'çizgi parçası'dır ve köşeden köşeye yerleşir.")]
        public Vector2Int footprint = Vector2Int.one;
    }
}
