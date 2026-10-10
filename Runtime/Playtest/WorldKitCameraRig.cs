using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WorldKit.Playtest
{
    /// <summary>
    /// Cinemachine tabanlı dinamik top-down kamera. Oyuncunun üzerine eklenir; tüm rig'i Play'de kendisi kurar.
    ///
    /// - Orbital Follow: oyuncunun etrafında sabit eğimli yörünge (Q/E ile 45° döner, tekerlekle zoom).
    /// - Deoccluder: oyuncuyla kamera arasına bina/duvar girerse kamerayı engelin önüne çeker.
    /// - Decollider: kamera bir objenin içine girerse onu dışarı iter.
    /// - Dinamik: yürüme yönüne doğru önden bakar (look-ahead), koşarken hafifçe uzaklaşır.
    /// </summary>
    [DefaultExecutionOrder(100)] // Oyuncu hareket ettikten sonra hedefi güncelle.
    [DisallowMultipleComponent]
    public class WorldKitCameraRig : MonoBehaviour
    {
        [Header("Açı ve Mesafe")]
        [Range(20f, 85f)] public float pitch = 55f;
        public float yaw = 45f;
        public float distance = 13f;
        public float minDistance = 5f;
        public float maxDistance = 30f;
        [Range(20f, 70f)] public float fieldOfView = 40f;

        [Header("Dinamik")]
        [Tooltip("Hareket yönünde kaç saniye önüne bakılsın.")]
        public float lookAheadTime = 0.35f;
        [Tooltip("Önden bakma en fazla kaç metre olsun.")]
        public float maxLookAhead = 2.5f;
        [Tooltip("Koşarken mesafe bu oranda artar.")]
        public float runZoomOut = 0.15f;
        [Tooltip("Koşu sayılacak hız (m/s).")]
        public float runSpeedThreshold = 5.5f;

        [Header("Engeller")]
        [Tooltip("Kameranın çarpacağı katmanlar (bina, duvar, zemin).")]
        public LayerMask obstacleLayers = 1; // Default
        [Tooltip("Ağaç/çit gibi ince objelerin önünden geçerken titrememesi için gecikme.")]
        public float minimumOcclusionTime = 0.15f;
        [Tooltip("Zoom bu mesafeden uzaktaysa kamera engel yüzünden öne çekilmez (engelden kaçma kapanır). " +
                 "Yakın zoom'da her zamanki gibi çalışır.")]
        public float avoidObstaclesUpToDistance = 15f;
        public float cameraRadius = 0.35f;

        CharacterController m_Controller;
        Transform m_Target;
        CinemachineCamera m_Camera;
        CinemachineOrbitalFollow m_Orbit;
        CinemachineDeoccluder m_Deoccluder;
        Vector3 m_LookAhead;
        Vector3 m_LookAheadVelocity;
        float m_TargetYaw;
        float m_TargetDistance;
        float m_RunBlend;

        public Transform CameraTransform => Camera.main != null ? Camera.main.transform : null;

        void Awake()
        {
            m_Controller = GetComponent<CharacterController>();
            m_TargetYaw = yaw;
            m_TargetDistance = distance;
        }

        void Start()
        {
            BuildRig();
        }

        void OnDestroy()
        {
            if (m_Target != null)
                Destroy(m_Target.gameObject);
            if (m_Camera != null)
                Destroy(m_Camera.gameObject);
        }

        void BuildRig()
        {
            // Ana kamera + Brain
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                mainCamera = go.AddComponent<Camera>();
            }

            if (!mainCamera.TryGetComponent<CinemachineBrain>(out _))
                mainCamera.gameObject.AddComponent<CinemachineBrain>();

            // Kameranın takip ettiği nokta: oyuncunun göğüs hizası + önden bakma.
            m_Target = new GameObject("World Kit Kamera Hedefi").transform;
            m_Target.position = TargetBase();

            var rig = new GameObject("World Kit Kamera (Cinemachine)");
            m_Camera = rig.AddComponent<CinemachineCamera>();
            m_Camera.Target.TrackingTarget = m_Target;
            m_Camera.Lens = LensSettings.Default;
            m_Camera.Lens.FieldOfView = fieldOfView;
            m_Camera.Lens.NearClipPlane = 0.2f;
            m_Camera.Lens.FarClipPlane = 500f;

            // Konum: hedefin etrafında küresel yörünge, dünya eksenine bağlı.
            m_Orbit = rig.AddComponent<CinemachineOrbitalFollow>();
            m_Orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            m_Orbit.Radius = distance;
            m_Orbit.TrackerSettings = new TrackerSettings
            {
                BindingMode = BindingMode.WorldSpace,
                PositionDamping = new Vector3(0.15f, 0.15f, 0.15f),
                AngularDampingMode = AngularDampingMode.Euler,
                RotationDamping = Vector3.zero,
            };
            m_Orbit.HorizontalAxis = new InputAxis
            {
                Value = yaw, Center = yaw, Range = new Vector2(-180f, 180f), Wrap = true,
                Recentering = new InputAxis.RecenteringSettings { Enabled = false, Wait = 1f, Time = 2f }
            };
            m_Orbit.VerticalAxis = new InputAxis
            {
                Value = pitch, Center = pitch, Range = new Vector2(5f, 89f), Wrap = false,
                Recentering = new InputAxis.RecenteringSettings { Enabled = false, Wait = 1f, Time = 2f }
            };
            m_Orbit.RadialAxis = new InputAxis
            {
                Value = 1f, Center = 1f, Range = new Vector2(1f, 1f), Wrap = false,
                Recentering = new InputAxis.RecenteringSettings { Enabled = false, Wait = 1f, Time = 2f }
            };

            // Bakış: her zaman hedefe.
            rig.AddComponent<CinemachineHardLookAt>();

            // Engel: oyuncuyla kamera arasına bir şey girerse kamerayı öne çek.
            var deoccluder = m_Deoccluder = rig.AddComponent<CinemachineDeoccluder>();
            deoccluder.CollideAgainst = obstacleLayers;
            deoccluder.IgnoreTag = "Player";
            deoccluder.TransparentLayers = 0;
            deoccluder.MinimumDistanceFromTarget = 1.5f;
            deoccluder.AvoidObstacles = new CinemachineDeoccluder.ObstacleAvoidance
            {
                Enabled = true,
                DistanceLimit = 0f,
                MinimumOcclusionTime = minimumOcclusionTime,
                CameraRadius = cameraRadius,
                Strategy = CinemachineDeoccluder.ObstacleAvoidance.ResolutionStrategy.PullCameraForward,
                MaximumEffort = 4,
                SmoothingTime = 0.25f,
                Damping = 0.5f,
                DampingWhenOccluded = 0.1f,
            };
            deoccluder.ShotQualityEvaluation = new CinemachineDeoccluder.QualityEvaluation { Enabled = false };

            // İçine girme: kamera bir objenin içindeyse dışarı it.
            var decollider = rig.AddComponent<CinemachineDecollider>();
            decollider.CameraRadius = cameraRadius;
            decollider.Decollision = new CinemachineDecollider.DecollisionSettings
            {
                Enabled = true,
                ObstacleLayers = obstacleLayers,
                Damping = 0.3f,
                SmoothingTime = 0.2f,
            };
            decollider.TerrainResolution = new CinemachineDecollider.TerrainSettings { Enabled = false };

            m_Camera.ForceCameraPosition(OrbitPosition(m_Target.position), Quaternion.Euler(pitch, yaw, 0f));
        }

        Vector3 TargetBase() => transform.position + Vector3.up * 0.9f;

        Vector3 OrbitPosition(Vector3 target) =>
            target - Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward * distance;

        void Update()
        {
            if (m_Orbit == null)
                return;

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            // Q/E: 45° döndür, tekerlek: zoom
            if (keyboard != null)
            {
                if (keyboard.qKey.wasPressedThisFrame) m_TargetYaw -= 45f;
                if (keyboard.eKey.wasPressedThisFrame) m_TargetYaw += 45f;
            }

            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    m_TargetDistance = Mathf.Clamp(m_TargetDistance * (scroll > 0f ? 0.9f : 1.1f), minDistance, maxDistance);
            }

            float dt = Time.deltaTime;
            float t = 1f - Mathf.Exp(-10f * dt);
            yaw = Mathf.LerpAngle(yaw, m_TargetYaw, t);

            // Dinamik: hız
            var velocity = m_Controller != null ? m_Controller.velocity : Vector3.zero;
            velocity.y = 0f;
            bool running = velocity.magnitude > runSpeedThreshold;
            m_RunBlend = Mathf.MoveTowards(m_RunBlend, running ? 1f : 0f, dt * 1.5f);
            distance = Mathf.Lerp(distance, m_TargetDistance * (1f + runZoomOut * m_RunBlend), t);

            // Dinamik: yürüme yönünde önden bak
            var desiredLookAhead = Vector3.ClampMagnitude(velocity * lookAheadTime, maxLookAhead);
            m_LookAhead = Vector3.SmoothDamp(m_LookAhead, desiredLookAhead, ref m_LookAheadVelocity, 0.35f);
            m_Target.position = TargetBase() + m_LookAhead;

            m_Orbit.HorizontalAxis.Value = Mathf.Repeat(yaw + 180f, 360f) - 180f;
            m_Orbit.VerticalAxis.Value = pitch;
            m_Orbit.Radius = distance;
            m_Camera.Lens.FieldOfView = fieldOfView;

            // Uzak zoom'da kamera engel yüzünden öne çekilmesin. Koşu uzaklaşması sayılmaz,
            // yalnızca oyuncunun seçtiği zoom belirler; böylece koşarken açılıp kapanmaz.
            if (m_Deoccluder != null)
            {
                bool avoid = m_TargetDistance <= avoidObstaclesUpToDistance;
                if (m_Deoccluder.AvoidObstacles.Enabled != avoid)
                    m_Deoccluder.AvoidObstacles.Enabled = avoid;
            }
        }
    }
}
