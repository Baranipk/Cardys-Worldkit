using WorldKit.Editor;

namespace WorldKit.Playtest.Editor
{
    /// <summary>
    /// Palete "Oyuncu Ekle" düğmesi ekler. Test modülü kurulu değilse (Cinemachine / Input System yok)
    /// bu assembly derlenmez ve düğme kendiliğinden kaybolur: çekirdek bu modülü hiç tanımaz.
    /// </summary>
    sealed class PlaytestToolbarItem : WorldKitToolbarItem
    {
        public override string Label => "Oyuncu Ekle";
        public override string Tooltip => "Scene'in baktığı yere gezilebilir test oyuncusu koyar. Play: WASD, Shift, Q/E, tekerlek.";
        public override int Order => 20;
        public override string Group => "Test";
        public override UnityEngine.Texture2D Icon => WorldKitIcons.AddPlayer;
        public override void Execute() => WorldKitPlayerSpawner.Spawn();
    }
}
