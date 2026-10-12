using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WorldKit.Editor
{
    // Yerleşik fırça modları. Kendi modunu yazmak için bunlar örnek olarak kullanılabilir.

    /// <summary>Tıkla: tek parça. Sürükle: imlecin geçtiği yerlere boya.</summary>
    public sealed class SingleTool : WorldKitTool
    {
        const string DragKey = "WorldKit.Single.DragRotates";

        public override string Name => "Tek";
        public override string Tooltip => "Tıkla: tek parça. Sürükle: imlecin geçtiği yerlere boya, ya da (ayar) basılı tutup çekerek yön ver.";
        public override int Order => 10;
        public override Texture2D Icon => WorldKitIcons.Single;
        public override bool PaintsWhileDragging => !RotatesOnDrag;
        public override bool HasSettings => true;

        /// <summary>true: sürüklemek boyamaz; basılan yere tek parça konur ve fareye doğru bakar (yön verme).</summary>
        public static bool RotatesOnDrag
        {
            get => EditorPrefs.GetBool(DragKey, false);
            set => EditorPrefs.SetBool(DragKey, value);
        }

        public override string Hint(BrushContext c) =>
            RotatesOnDrag && !c.IsFloor && !c.IsLinear
                ? (c.Dragging ? "Bırak: koy (fareye doğru bakar)" : "Bas ve çek: yön ver   Shift+tekerlek: döndür")
                : "Tıkla: koy   Sürükle: boya   Shift+tekerlek: döndür";

        public override void OnSettingsGUI()
        {
            int mode = GUILayout.Toolbar(RotatesOnDrag ? 1 : 0, new[]
            {
                new GUIContent("Sürükle: boya", "Sürükledikçe geçtiğin yerlere parça koyar."),
                new GUIContent("Sürükle: yön ver", "Basılan yere tek parça koyar; fareyi çektiğin yöne bakar. Sonraki parçalar da o yönde gelir.")
            });
            if ((mode == 1) != RotatesOnDrag)
                RotatesOnDrag = mode == 1;
        }

        public override void CollectPlacements(BrushContext c, List<Placement> result)
        {
            if (RotatesOnDrag && c.Dragging && !c.IsFloor && !c.IsLinear)
                result.Add(c.PlacementFacing(c.Anchor, c.Hit));
            else
                result.Add(c.SinglePlacement());
        }

        public override void OnPreviewGUI(BrushContext c)
        {
            if (!RotatesOnDrag || !c.Dragging || c.IsFloor || c.IsLinear)
                return;
            using (new Handles.DrawingScope(new Color(1f, 0.85f, 0.3f, 0.9f)))
                Handles.DrawDottedLine(c.Anchor + Vector3.up * 0.05f, c.Hit + Vector3.up * 0.05f, 3f);
        }
    }

    /// <summary>Tut-çek: düz hat. İlk harekette eksene kilitlenir.</summary>
    public sealed class LineTool : WorldKitTool
    {
        public override string Name => "Hat";
        public override string Tooltip => "Tut-çek: düz hat/sıra. Duvar ve çit köşeden başlar, boylarının katlarına yuvarlanır.";
        public override int Order => 20;
        public override Texture2D Icon => WorldKitIcons.Line;
        public override string Hint(BrushContext c) => c.IsLinear ? "Tut-çek: düz hat (köşeden başlar)" : "Tut-çek: düz sıra";

        public override void CollectPlacements(BrushContext c, List<Placement> result)
        {
            if (!c.Dragging)
            {
                result.Add(c.SinglePlacement());
                return;
            }

            if (c.IsLinear)
                WorldKitLayout.Line(result, c.Anchor, c.Hit, c.Footprint, c.Rotation, c.Level, c.Axis);
            else
                WorldKitLayout.Area(result, c.Anchor, c.ConstrainedHit, c.Footprint, c.Rotation, c.Level, false);
        }

        public override void CollectCells(BrushContext c, ICollection<Vector2Int> result)
        {
            foreach (var cell in BrushContext.RectCells(c.DragRect(true), false))
                result.Add(cell);
        }
    }

    /// <summary>Tut-çek: dikdörtgeni doldur. Çizgi parçalarında kullanılmaz.</summary>
    public sealed class AreaTool : WorldKitTool
    {
        public override string Name => "Alan";
        public override string Tooltip => "Tut-çek: dikdörtgeni doldur. Duvar ve çitte kullanılamaz.";
        public override int Order => 30;
        public override Texture2D Icon => WorldKitIcons.Area;
        public override bool Supports(BrushContext c) => !c.IsLinear;
        public override string Hint(BrushContext c) => "Tut-çek: alanı doldur";

        public override void CollectPlacements(BrushContext c, List<Placement> result)
        {
            if (!c.Dragging)
            {
                result.Add(c.SinglePlacement());
                return;
            }

            WorldKitLayout.Area(result, c.Anchor, c.Hit, c.Footprint, c.Rotation, c.Level, false);
        }

        public override void CollectCells(BrushContext c, ICollection<Vector2Int> result)
        {
            foreach (var cell in BrushContext.RectCells(c.DragRect(false), false))
                result.Add(cell);
        }
    }

    /// <summary>Tut-çek: yalnızca kenarlar. Çizgi parçalarında dört kenarlı oda.</summary>
    public sealed class OutlineTool : WorldKitTool
    {
        public override string Name => "Çerçeve";
        public override string LinearName => "Oda";
        public override string Tooltip => "Tut-çek: çerçeve. Duvar ve çitte 4 kenarlı oda çizer.";
        public override int Order => 40;
        public override Texture2D Icon => WorldKitIcons.Outline;
        public override string Hint(BrushContext c) => c.IsLinear ? "Tut-çek: oda (4 kenar)" : "Tut-çek: çerçeve";

        public override void CollectPlacements(BrushContext c, List<Placement> result)
        {
            if (!c.Dragging)
            {
                result.Add(c.SinglePlacement());
                return;
            }

            if (c.IsLinear)
                WorldKitLayout.Room(result, c.Anchor, c.Hit, c.Footprint, c.Rotation, c.Level);
            else
                WorldKitLayout.Area(result, c.Anchor, c.Hit, c.Footprint, c.Rotation, c.Level, true);
        }

        public override void CollectCells(BrushContext c, ICollection<Vector2Int> result)
        {
            foreach (var cell in BrushContext.RectCells(c.DragRect(false), true))
                result.Add(cell);
        }
    }

    /// <summary>Tıkla: parça sil. Tut-çek: alanı sil. Ctrl basılıyken her modda bu kullanılır.</summary>
    public sealed class EraseTool : WorldKitTool
    {
        public override string Name => "Sil";
        public override string Tooltip => "Tıkla: parça sil. Tut-çek: alanı sil. (Ctrl her modda geçici olarak siler.)";
        public override int Order => 90;
        public override Texture2D Icon => WorldKitIcons.Erase;
        public override bool Erases => true;
        public override string Hint(BrushContext c) => c.IsFloor ? "Tut-çek: zemini sil" : "Tıkla: parça sil   Tut-çek: alanı sil";

        public override void CollectPlacements(BrushContext c, List<Placement> result) { }

        public override void CollectCells(BrushContext c, ICollection<Vector2Int> result)
        {
            foreach (var cell in BrushContext.RectCells(c.DragRect(false), false))
                result.Add(cell);
        }
    }
}
