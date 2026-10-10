using UnityEditor;
using UnityEngine;
using WorldKit.Editor;

namespace WorldKit.Playtest.Editor
{
    /// <summary>
    /// Sahneye gezilebilir test oyuncusu koyar (Scene görünümünün baktığı yere).
    /// Sahnede oyuncu yoksa Play'e basıldığında geçici bir oyuncu kendiliğinden oluşturulur.
    /// </summary>
    [InitializeOnLoad]
    public static class WorldKitPlayerSpawner
    {
        const string PlayerName = "Test_Oyuncu";

        // Play.e basılmadan önceki Scene kamera noktası. Domain reload.dan sağ çıksın diye SessionState.te.
        const string PendingKey = "WorldKit.PendingSpawn";

        static WorldKitPlayerSpawner()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                bool needsPlayer = Object.FindFirstObjectByType<WorldKitPlayer>() == null;
                SessionState.SetBool(PendingKey, needsPlayer);
                if (needsPlayer)
                    SessionState.SetVector3(PendingKey + ".Position", SpawnPoint());
            }
            else if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingKey, false))
            {
                SessionState.SetBool(PendingKey, false);
                if (Object.FindFirstObjectByType<WorldKitPlayer>() == null)
                {
                    var player = Create(SessionState.GetVector3(PendingKey + ".Position", Vector3.zero));
                    player.name = PlayerName + " (geçici)";
                    Debug.Log("[World Kit] Sahnede oyuncu yoktu; Scene kamerasının baktığı yere geçici oyuncu kondu. " +
                              "Kalıcı başlangıç noktası için palette 'Oyuncu Ekle'.");
                }
            }
        }

        [MenuItem("Tools/World Kit/Test Oyuncusu Ekle", priority = 60)]
        public static void Spawn()
        {
            var existing = Object.FindFirstObjectByType<WorldKitPlayer>();
            var position = SpawnPoint();

            if (existing != null)
            {
                // Zaten varsa yenisini ekleme, baktığın yere taşı.
                if (!Application.isPlaying)
                    Undo.RecordObject(existing.transform, "World Kit: Oyuncuyu Taşı");
                existing.transform.position = position;
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            var root = Create(position);
            if (!Application.isPlaying)
                Undo.RegisterCreatedObjectUndo(root, "World Kit: Test Oyuncusu Ekle");
            Selection.activeGameObject = root;
        }

        static GameObject Create(Vector3 position)
        {
            var root = new GameObject(PlayerName) { tag = "Player" };
            root.transform.position = position;

            float height = WorldKitSettings.Instance.characterHeight;
            var controller = root.AddComponent<CharacterController>();
            controller.height = height;
            controller.radius = Mathf.Min(0.3f, height * 0.25f);
            controller.center = new Vector3(0f, height * 0.5f, 0f);
            controller.stepOffset = Mathf.Min(0.35f, height * 0.3f);
            controller.slopeLimit = 50f;
            controller.skinWidth = 0.03f;

            var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldKitStarterContent.PlayerReferencePath);
            if (visualPrefab != null)
            {
                var visual = Application.isPlaying
                    ? Object.Instantiate(visualPrefab, root.transform)
                    : (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, root.transform);
                visual.name = "Gorsel";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                // Referans prefab'ı EditorOnly; oyuncunun görseli build'de de görünsün.
                visual.tag = "Untagged";
            }
            else
            {
                // Başlangıç içeriği yoksa: karakter boyunda basit bir kapsül.
                var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.DestroyImmediate(capsule.GetComponent<Collider>());
                capsule.name = "Gorsel";
                capsule.transform.SetParent(root.transform, false);
                capsule.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
                capsule.transform.localScale = new Vector3(controller.radius * 2f, height * 0.5f, controller.radius * 2f);
            }

            // Görsel hazır olduktan sonra eklensin ki Awake/Start'ta her şey yerinde olsun.
            root.AddComponent<WorldKitPlayer>();
            return root;
        }

        /// <summary>Scene kamerasının baktığı noktanın altındaki zemin.</summary>
        static Vector3 SpawnPoint()
        {
            var sceneView = SceneView.lastActiveSceneView;
            var pivot = sceneView != null ? sceneView.pivot : Vector3.zero;

            // Kameranın baktığı noktanın biraz üstünden: çatıya değil, o seviyedeki zemine konsun.
            if (Physics.Raycast(new Vector3(pivot.x, pivot.y + 1.5f, pivot.z), Vector3.down, out var hit, 100f))
                return hit.point + Vector3.up * 0.05f;

            return new Vector3(pivot.x, WorldKitBrush.Level * WorldKitGrid.LevelHeight + 0.05f, pivot.z);
        }
    }
}
