using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// Parça değiştirme: sahnedeki (veya seçimdeki) bir prefab'ın bütün kopyalarını başka bir prefab'la değiştirir.
    /// Greybox'tan gerçek modele geçiş için: ör. bütün Ev_Kucuk_3x3'ler → yeni ev modeli. Konum, dönüş ve
    /// (istenirse) ölçek korunur; hedefte WorldKitPiece yoksa footprint kopyalanır ki araçlar çalışmaya devam etsin.
    /// </summary>
    public static class WorldKitReplace
    {
        /// <summary>Değişecek parçalar: kaynağın kopyası olan sahne (veya seçim) parçaları.</summary>
        public static List<GameObject> Find(GameObject source, bool selectionOnly)
        {
            if (source == null)
                return new List<GameObject>();
            var pieces = selectionOnly ? WorldKitScene.SelectedPieces() : WorldKitScene.AllPieces().ToList();
            return pieces.Where(p => WorldKitScene.GetSource(p) == source).ToList();
        }

        /// <summary>
        /// Parçaları hedef prefab'la değiştirir (tek Undo). offset: hedefin parçaya göre yerel kayması,
        /// extraYaw: ek dönüş (derece). Yeni parçaları döndürür.
        /// </summary>
        public static List<GameObject> Replace(IEnumerable<GameObject> pieces, GameObject target, bool keepScale,
            Vector3 offset, float extraYaw)
        {
            var created = new List<GameObject>();
            if (target == null)
                return created;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            foreach (var piece in pieces.ToList())
            {
                if (piece == null)
                    continue;

                var parent = piece.transform.parent;
                int sibling = piece.transform.GetSiblingIndex();
                var position = piece.transform.position + piece.transform.rotation * offset;
                var rotation = piece.transform.rotation * Quaternion.Euler(0f, extraYaw, 0f);
                var scale = piece.transform.localScale;
                var footprint = WorldKitScene.GetFootprint(piece);

                // Önce eskisini kaldır: modüller (köşe direkleri, kapı açma) yeni parçayı eskisiyle karıştırmasın.
                WorldKitEvents.RaisePieceRemoving(piece);
                Undo.DestroyObjectImmediate(piece);

                var copy = (GameObject)PrefabUtility.InstantiatePrefab(target, parent);
                copy.transform.SetSiblingIndex(sibling);
                copy.transform.SetPositionAndRotation(position, rotation);
                if (keepScale)
                    copy.transform.localScale = scale;
                Undo.RegisterCreatedObjectUndo(copy, "World Kit: Parça Değiştir");

                if (copy.GetComponent<WorldKitPiece>() == null)
                    Undo.AddComponent<WorldKitPiece>(copy).footprint = footprint;

                WorldKitEvents.RaisePiecePlaced(copy);
                created.Add(copy);
            }

            WorldKitEvents.RaiseEditFinished();
            Undo.SetCurrentGroupName("World Kit: Parça Değiştir");
            Undo.CollapseUndoOperations(group);
            return created;
        }
    }

    /// <summary>"Değiştir" sekmesi (Çıktı): placeholder'ları gerçek modellerle toplu değiştirme.</summary>
    sealed class ReplacePaletteTab : WorldKitPaletteTab
    {
        const string Prefix = "WorldKit.Replace.";

        GameObject m_Source;
        GameObject m_Target;

        public override string Title => "Değiştir";
        public override int Order => 3100;
        public override string Section => OutputSection;
        public override Texture2D Icon => WorldKitIcons.Replace;

        static bool SelectionOnly { get => EditorPrefs.GetBool(Prefix + "SelectionOnly", false); set => EditorPrefs.SetBool(Prefix + "SelectionOnly", value); }
        static bool KeepScale { get => EditorPrefs.GetBool(Prefix + "KeepScale", true); set => EditorPrefs.SetBool(Prefix + "KeepScale", value); }
        static float Yaw { get => EditorPrefs.GetFloat(Prefix + "Yaw", 0f); set => EditorPrefs.SetFloat(Prefix + "Yaw", value); }

        Vector3 m_Offset;

        public override void OnGUI(WorldKitPaletteWindow window)
        {
            GUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "Bir parçanın sahnedeki bütün kopyalarını başka bir prefab'la değiştirir (ör. greybox ev → gerçek ev modeli). " +
                "Konum ve dönüş korunur. Tek Undo ile geri alınır.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                m_Source = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Bunu", "Değişecek prefab (greybox parça)."),
                    m_Source, typeof(GameObject), false);
                using (new EditorGUI.DisabledScope(WorldKitScene.SelectedPieces().Count == 0))
                {
                    if (GUILayout.Button(new GUIContent("Seçimden", "Seçili parçanın prefab'ını al."), GUILayout.Width(70f)))
                        m_Source = WorldKitScene.SelectedPieces().Select(WorldKitScene.GetSource).FirstOrDefault(s => s != null);
                }
            }
            m_Target = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Bununla", "Yeni prefab (gerçek model)."),
                m_Target, typeof(GameObject), false);

            bool selectionOnly = EditorGUILayout.ToggleLeft("Yalnızca seçili parçalar", SelectionOnly);
            if (selectionOnly != SelectionOnly) SelectionOnly = selectionOnly;
            bool keepScale = EditorGUILayout.ToggleLeft(new GUIContent("Ölçeği koru", "Kapalıysa yeni parça kendi ölçeğinde kalır."), KeepScale);
            if (keepScale != KeepScale) KeepScale = keepScale;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent("Ek dönüş", "Yeni modelin önü farklı yöne bakıyorsa."), GUILayout.Width(EditorGUIUtility.labelWidth));
                foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
                {
                    bool on = Mathf.Approximately(Yaw, yaw);
                    if (GUILayout.Toggle(on, $"{yaw:0}°", EditorStyles.miniButton) && !on)
                        Yaw = yaw;
                }
            }
            m_Offset = EditorGUILayout.Vector3Field(new GUIContent("Konum kayması", "Yeni modelin pivotu farklıysa (parçaya göre, m)."), m_Offset);

            var pieces = WorldKitReplace.Find(m_Source, SelectionOnly);
            GUILayout.Space(6f);
            bool valid = m_Source != null && m_Target != null && m_Source != m_Target && pieces.Count > 0;
            using (new EditorGUI.DisabledScope(!valid))
            {
                if (GUILayout.Button($"{pieces.Count} Parçayı Değiştir", GUILayout.Height(28f)))
                {
                    var created = WorldKitReplace.Replace(pieces, m_Target, KeepScale, m_Offset, Yaw);
                    Selection.objects = created.Cast<Object>().ToArray();
                    Debug.Log($"[World Kit] {created.Count} parça değiştirildi: {m_Source.name} → {m_Target.name}");
                }
            }
            if (m_Source != null && pieces.Count == 0)
                EditorGUILayout.HelpBox(SelectionOnly ? "Seçimde bu prefab'ın kopyası yok." : "Sahnede bu prefab'ın kopyası yok.", MessageType.Info);
        }
    }
}
