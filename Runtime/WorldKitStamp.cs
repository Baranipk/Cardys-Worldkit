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
        }

        public List<FloorCell> floorCells = new List<FloorCell>();
    }
}
