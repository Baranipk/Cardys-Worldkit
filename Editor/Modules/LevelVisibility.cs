using UnityEditor;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// Palete "Üst Katları Gizle" düğmesi ekler: çalışılan seviyenin üstündeki parçaları ve zemin hücrelerini
    /// gizler. Seviye (E/Q) değiştikçe güncellenir.
    /// Gizleme yalnızca görünümdür: parçalar Unity'nin Scene Visibility sistemiyle gizlenir (sahneye kaydedilmez),
    /// zemin verisi değişmez. Play'e girerken her şey yeniden görünür olur.
    /// </summary>
    [InitializeOnLoad]
    sealed class LevelVisibilityItem : WorldKitToolbarItem
    {
        static bool s_Active;

        static LevelVisibilityItem()
        {
            WorldKitBrush.Changed += () => { if (s_Active) Apply(); };
            WorldKitEvents.EditFinished += () => { if (s_Active) Apply(); };
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode && s_Active)
                {
                    s_Active = false;
                    Apply();
                }
            };
        }

        public override string Label => s_Active ? $"Üst Katlar Gizli ({WorldKitBrush.Level})" : "Üst Katları Gizle";
        public override string Tooltip => "Çalışılan seviyenin (E/Q) üstündeki parçaları ve zemini gizler / gösterir.";
        public override int Order => 15;
        public override string Group => "Görünüm";
        public override Texture2D Icon => WorldKitIcons.Layers;

        public override void Execute()
        {
            s_Active = !s_Active;
            Apply();
        }

        static void Apply()
        {
            int level = WorldKitBrush.Level;
            float limit = level * WorldKitGrid.LevelHeight + 0.01f;
            var visibility = SceneVisibilityManager.instance;

            foreach (var piece in WorldKitScene.AllPieces())
            {
                if (s_Active && piece.transform.position.y > limit)
                    visibility.Hide(piece, true);
                else
                    visibility.Show(piece, true);
            }

            foreach (var floor in Object.FindObjectsByType<WorldKitFloor>(FindObjectsSortMode.None))
                floor.MaxVisibleLevel = s_Active ? level : int.MaxValue;

            SceneView.RepaintAll();
        }
    }
}
