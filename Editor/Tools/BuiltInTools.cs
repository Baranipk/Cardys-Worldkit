using System.Collections.Generic;
using UnityEngine;

namespace WorldKit.Editor
{
    // Yerleşik fırça modları. Kendi modunu yazmak için bunlar örnek olarak kullanılabilir.

    /// <summary>Tıkla: tek parça. Sürükle: imlecin geçtiği yerlere boya.</summary>
    public sealed class SingleTool : WorldKitTool
    {
        public override string Name => "Tek";
        public override string Tooltip => "Tıkla: tek parça. Sürükle: imlecin geçtiği yerlere boya.";
        public override int Order => 10;
        public override bool PaintsWhileDragging => true;
        public override string Hint(BrushContext c) => "Tıkla: koy   Sürükle: boya";
    }

    /// <summary>Tut-çek: düz hat. İlk harekette eksene kilitlenir.</summary>
    public sealed class LineTool : WorldKitTool
    {
        public override string Name => "Hat";
        public override string Tooltip => "Tut-çek: düz hat/sıra. Duvar ve çit köşeden başlar, boylarının katlarına yuvarlanır.";
        public override int Order => 20;
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
