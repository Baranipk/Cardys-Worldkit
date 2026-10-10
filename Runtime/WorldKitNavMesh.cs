using UnityEngine;
using UnityEngine.AI;

namespace WorldKit
{
    /// <summary>
    /// World Kit'in bake ettiği NavMesh verisini sahneye yükler (editörde, Play'de ve build'de).
    /// NavMeshAgent'lar bu veriyi kullanır. Ek paket gerektirmez; AI Navigation paketinin NavMeshSurface'i
    /// ile birlikte kullanmayın (iki NavMesh üst üste biner).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class WorldKitNavMesh : MonoBehaviour
    {
        [Tooltip("Bake edilmiş NavMesh (World Kit → Analiz → NavMesh).")]
        public NavMeshData data;

        NavMeshDataInstance m_Instance;

        void OnEnable() => Load();

        void OnDisable() => Unload();

        void OnValidate()
        {
            if (isActiveAndEnabled)
                Load();
        }

        /// <summary>Veriyi (yeniden) yükler.</summary>
        public void Load()
        {
            Unload();
            if (data != null)
                m_Instance = NavMesh.AddNavMeshData(data, transform.position, transform.rotation);
        }

        public void Unload()
        {
            if (m_Instance.valid)
                m_Instance.Remove();
            m_Instance = default;
        }
    }
}
