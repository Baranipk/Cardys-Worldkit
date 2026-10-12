using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldKit
{
    /// <summary>
    /// Şablon (stamp) prefab'ının kökünde durur. Çocuk objeler şablonun parçalarıdır; ayrıca şablonun
    /// altındaki zemin hücrelerini taşır. Fırçayla yerleştirilince şablon açılır: parçalar kendi
    /// kategorilerine dağıtılır, zemin hücreleri boyanır ve bu kök silinir.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldKitStamp : MonoBehaviour
    {
        [Serializable]
        public struct FloorCell
        {
            [Tooltip("Hücre merkezinin şablon köküne göre konumu (y = 0).")]
            public Vector3 localCenter;
            [Tooltip("Şablonun taban seviyesine göre yükseklik (seviye).")]
            public int height;
            [Tooltip("Zemin türü (0 = varsayılan).")]
            public int type;
            [Tooltip("Rampa yönü, şablon köküne göre (0 = düz). Şablon döndürülünce yön de döner.")]
            public Vector3 localSlopeDirection;
            [Tooltip("Rampa parçası: 0 = tam (dik), 1 = yumuşak rampanın alt yarısı, 2 = üst yarısı.")]
            public int rampPart;
            [Tooltip("Köşe rampası kodu (0 = yok): dört köşenin yükselişi, şablon köküne göre. Şablon döndürülünce köşeler de döner.")]
            public int cornerCode;
        }

        public List<FloorCell> floorCells = new List<FloorCell>();
    }
}
