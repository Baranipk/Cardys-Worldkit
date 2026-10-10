using UnityEngine;
using UnityEngine.Events;

namespace WorldKit
{
    /// <summary>
    /// Bölge işareti: spawn alanı, görev noktası, güvenli bölge, tehlike alanı... Bir tetikleyici kutudur
    /// (BoxCollider, isTrigger). Sahnede türüne göre renkli görünür. Oyun kodu bölgeleri türüne ve adına göre bulabilir
    /// ya da onEnter / onExit olaylarını Inspector'dan bağlayabilir.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public class WorldKitZone : MonoBehaviour
    {
        public enum Kind { Spawn, Quest, Trigger, Safe, Danger, Custom }

        [Tooltip("Bölgenin türü (renk ve anlam).")]
        public Kind kind = Kind.Trigger;
        [Tooltip("Oyun kodunun bölgeyi bulması için serbest ad / kimlik (ör. \"koy_girisi\").")]
        public string zoneId = "";
        [Tooltip("Yalnızca bu etiketteki objeler olayları tetikler (boş = hepsi).")]
        public string triggerTag = "Player";

        public UnityEvent<GameObject> onEnter = new UnityEvent<GameObject>();
        public UnityEvent<GameObject> onExit = new UnityEvent<GameObject>();

        /// <summary>Türün sahnedeki rengi.</summary>
        public static Color ColorOf(Kind kind)
        {
            switch (kind)
            {
                case Kind.Spawn: return new Color(0.3f, 0.9f, 0.4f);
                case Kind.Quest: return new Color(1f, 0.82f, 0.2f);
                case Kind.Safe: return new Color(0.35f, 0.7f, 1f);
                case Kind.Danger: return new Color(1f, 0.3f, 0.25f);
                case Kind.Custom: return new Color(0.8f, 0.5f, 1f);
                default: return new Color(0.9f, 0.9f, 0.9f);
            }
        }

        /// <summary>Bölgenin içinde rastgele bir nokta (tabanda). Spawn için.</summary>
        public Vector3 RandomPoint()
        {
            var box = GetComponent<BoxCollider>();
            var local = box.center + Vector3.Scale(box.size, new Vector3(Random.value - 0.5f, -0.5f, Random.value - 0.5f));
            return transform.TransformPoint(local);
        }

        void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        bool Accepts(Collider other) => string.IsNullOrEmpty(triggerTag) || other.CompareTag(triggerTag);

        void OnTriggerEnter(Collider other)
        {
            if (Accepts(other))
                onEnter.Invoke(other.gameObject);
        }

        void OnTriggerExit(Collider other)
        {
            if (Accepts(other))
                onExit.Invoke(other.gameObject);
        }

        void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null)
                return;

            var color = ColorOf(kind);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(color.r, color.g, color.b, 0.12f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = color;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
