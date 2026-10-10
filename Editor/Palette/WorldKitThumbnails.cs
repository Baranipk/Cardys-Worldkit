using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace WorldKit.Editor
{
    /// <summary>
    /// Parçaların palet önizlemelerini çizer. ProBuilder mesh'leri prefab içinde saklanmadığı için
    /// Unity'nin hazır AssetPreview'u boş kalabilir; bu yüzden kendi önizlememizi üretiyoruz.
    /// </summary>
    public static class WorldKitThumbnails
    {
        const int Size = 128;

        static readonly Dictionary<GameObject, Texture2D> s_Cache = new Dictionary<GameObject, Texture2D>();

        public static Texture2D Get(GameObject prefab)
        {
            if (prefab == null)
                return null;

            if (s_Cache.TryGetValue(prefab, out var texture) && texture != null)
                return texture;

            texture = Render(prefab);
            s_Cache[prefab] = texture;
            return texture;
        }

        public static void Clear()
        {
            foreach (var texture in s_Cache.Values)
            {
                if (texture != null)
                    Object.DestroyImmediate(texture);
            }

            s_Cache.Clear();
        }

        static Texture2D Render(GameObject prefab)
        {
            var preview = new PreviewRenderUtility();
            try
            {
                var instance = preview.InstantiatePrefabInScene(prefab);
                foreach (var pb in instance.GetComponentsInChildren<ProBuilderMesh>(true))
                {
                    pb.ToMesh();
                    pb.Refresh();
                }

                var renderers = instance.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0)
                    return null;

                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);

                // Top-down ARPG kamerasına benzer bir açı
                var camera = preview.camera;
                var rotation = Quaternion.Euler(35f, 45f, 0f);
                var radius = Mathf.Max(bounds.extents.magnitude, 0.1f);
                camera.fieldOfView = 30f;
                var distance = radius / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                camera.transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * distance, rotation);
                camera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 1.5f);
                camera.farClipPlane = distance + radius * 1.5f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.22f, 0.22f, 0.24f, 1f);

                preview.lights[0].intensity = 1.2f;
                preview.lights[0].transform.rotation = Quaternion.Euler(50f, 20f, 0f);
                preview.lights[1].intensity = 0.6f;
                preview.lights[1].transform.rotation = Quaternion.Euler(-20f, 200f, 0f);
                preview.ambientColor = new Color(0.35f, 0.35f, 0.4f, 1f);

                preview.BeginStaticPreview(new Rect(0f, 0f, Size, Size));
                preview.Render(true, false);
                return preview.EndStaticPreview();
            }
            finally
            {
                preview.Cleanup();
            }
        }
    }
}
