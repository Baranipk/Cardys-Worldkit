using System;
using UnityEngine;

namespace WorldKit.Editor
{
    /// <summary>
    /// Modüllerin çekirdeğe dokunmadan araya girebilmesi için olaylar.
    /// Örnek: otomatik köşe direği modülü PiecePlaced'i dinleyip komşu duvarlara bakabilir.
    ///
    /// Not: Olaylar bir Undo grubu içinde tetiklenir; sahneyi değiştiren dinleyiciler
    /// Undo.RegisterCreatedObjectUndo / Undo.RecordObject kullanmalıdır ki tek Ctrl+Z ile geri alınsın.
    /// </summary>
    public static class WorldKitEvents
    {
        /// <summary>Fırça bir prefab parçası yerleştirdikten sonra.</summary>
        public static event Action<GameObject> PiecePlaced;

        /// <summary>Fırça bir parçayı silmeden hemen önce.</summary>
        public static event Action<GameObject> PieceRemoving;

        /// <summary>Bir parça taşınmadan/döndürülmeden hemen önce (eski konumu hâlâ geçerli).</summary>
        public static event Action<GameObject> PieceMoving;

        /// <summary>Bir parça taşındıktan/döndürüldükten sonra.</summary>
        public static event Action<GameObject> PieceMoved;

        /// <summary>Zemin hücreleri değiştikten sonra.</summary>
        public static event Action<WorldKitFloor> FloorChanged;

        /// <summary>
        /// Bir kullanıcı eylemi (tık, sürükleme, Shift+tık, seçim taşıma...) bittiğinde, Undo grubu kapanmadan hemen önce.
        /// Toplu iş yapan modüller (ör. köşe direkleri) PiecePlaced/PieceRemoving'de toplayıp burada bir kez çalışır.
        /// </summary>
        public static event Action EditFinished;

        /// <summary>Proje ayarları değiştiğinde.</summary>
        public static event Action SettingsChanged;

        // Parçaları kendisi taşıyan/oluşturan modüller de bu olayları tetiklemeli ki diğer modüller haberdar olsun.
        public static void RaisePiecePlaced(GameObject go) => PiecePlaced?.Invoke(go);
        public static void RaisePieceRemoving(GameObject go) => PieceRemoving?.Invoke(go);
        public static void RaisePieceMoving(GameObject go) => PieceMoving?.Invoke(go);
        public static void RaisePieceMoved(GameObject go) => PieceMoved?.Invoke(go);
        public static void RaiseFloorChanged(WorldKitFloor floor) => FloorChanged?.Invoke(floor);
        public static void RaiseEditFinished() => EditFinished?.Invoke();
        internal static void RaiseSettingsChanged() => SettingsChanged?.Invoke();
    }
}
