using UnityEngine;
using UnityEngine.InputSystem;

namespace WorldKit.Playtest
{
    /// <summary>
    /// Dünyayı içeriden gezmek için basit top-down test oyuncusu.
    /// WASD: yürü · Shift: koş. Kamera WorldKitCameraRig'de (Q/E döndür, tekerlek zoom).
    /// Hareket, kameranın o anki bakış açısına göre yapılır.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(WorldKitCameraRig))]
    public class WorldKitPlayer : MonoBehaviour
    {
        public float walkSpeed = 4.5f;
        public float runSpeed = 7.5f;
        public float turnSpeed = 900f;
        public float gravity = -25f;

        CharacterController m_Controller;
        float m_VerticalVelocity;
        Vector3 m_SpawnPosition;

        void Awake()
        {
            m_Controller = GetComponent<CharacterController>();
            m_SpawnPosition = transform.position;

            // Kamera rig'inden önce eklenmiş eski oyuncular için.
            if (!TryGetComponent<WorldKitCameraRig>(out _))
                gameObject.AddComponent<WorldKitCameraRig>();
        }

        void Update()
        {
            var keyboard = Keyboard.current;

            Vector2 input = Vector2.zero;
            bool running = false;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
                running = keyboard.leftShiftKey.isPressed;
            }

            // Kameranın yatay açısına göre: W her zaman ekranda "yukarı".
            var cameraTransform = Camera.main != null ? Camera.main.transform : null;
            float cameraYaw = cameraTransform != null ? cameraTransform.eulerAngles.y : 0f;
            var move = Quaternion.Euler(0f, cameraYaw, 0f) * new Vector3(input.x, 0f, input.y);
            if (move.sqrMagnitude > 1f)
                move.Normalize();

            if (move.sqrMagnitude > 0.001f)
            {
                var target = Quaternion.LookRotation(move, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
            }

            // Yerçekimi
            if (m_Controller.isGrounded && m_VerticalVelocity < 0f)
                m_VerticalVelocity = -2f;
            m_VerticalVelocity += gravity * Time.deltaTime;

            var velocity = move * (running ? runSpeed : walkSpeed) + Vector3.up * m_VerticalVelocity;
            m_Controller.Move(velocity * Time.deltaTime);

            // Haritadan düşersen başlangıca dön
            if (transform.position.y < m_SpawnPosition.y - 30f)
            {
                m_Controller.enabled = false;
                transform.position = m_SpawnPosition;
                m_Controller.enabled = true;
                m_VerticalVelocity = 0f;
            }
        }
    }
}
