using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Windows;
using HouseDesigner.Models;

namespace HouseDesigner.Services;

/// <summary>
/// 화면의 빈 캔버스가 아닌 실제 도면 경계로 viewBox를 계산해 SVG를 생성합니다.
/// </summary>
public static class SvgExportService
{
    private const double Margin = 45;

    public static void Export(string path, FloorPlan plan, double gridSize = 10,
        string selectedElementName = "선택된 요소 없음", string selectedElementDetails = "")
    {
        var drawingBounds = GetDrawingBounds(plan);
        drawingBounds.Inflate(Margin, Margin);
        var detailLines = selectedElementDetails.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var levelCount = Math.Max(1, plan.RoomAreas.Select(room => room.Level).Distinct().Count());
        var panelWidth = Math.Clamp(drawingBounds.Width * .3, 280, 480);
        var panelHeight = Math.Max(330, 245 + detailLines.Length * 17 + levelCount * 20);
        var bounds = new Rect(drawingBounds.X, drawingBounds.Y,
            drawingBounds.Width + 24 + panelWidth, Math.Max(drawingBounds.Height, panelHeight));
        var panelX = drawingBounds.Right + 24;
        var panelY = drawingBounds.Top;
        var svg = new StringBuilder();
        svg.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        svg.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{F(bounds.X)} {F(bounds.Y)} {F(bounds.Width)} {F(bounds.Height)}\" width=\"{F(bounds.Width)}\" height=\"{F(bounds.Height)}\">");
        svg.AppendLine("  <defs><pattern id=\"roomPattern\" width=\"18\" height=\"18\" patternUnits=\"userSpaceOnUse\"><path d=\"M-4 18 L18 -4 M5 23 L23 5\" stroke=\"#dededb\" stroke-width=\"1\"/></pattern></defs>");
        svg.AppendLine($"  <rect x=\"{F(bounds.X)}\" y=\"{F(bounds.Y)}\" width=\"{F(bounds.Width)}\" height=\"{F(bounds.Height)}\" fill=\"white\"/>");

        DrawSiteElements(svg, plan);
        DrawRoomFills(svg, plan);
        DrawFurniture(svg, plan);
        DrawWalls(svg, plan);
        DrawOpenings(svg, plan);
        DrawRoomAreaLabels(svg, plan);
        DrawLabels(svg, plan);
        DrawDimensions(svg, plan);
        DrawInformationPanel(svg, plan, gridSize, selectedElementName, detailLines,
            new Rect(panelX, panelY, panelWidth, panelHeight));
        svg.AppendLine("</svg>");
        File.WriteAllText(path, svg.ToString(), new UTF8Encoding(false));
    }

    private static void DrawSiteElements(StringBuilder svg, FloorPlan plan)
    {
        foreach (var element in plan.SiteElements)
        {
            var x = element.Location.X - element.Width / 2;
            var y = element.Location.Y - element.Height / 2;
            svg.AppendLine($"  <g transform=\"rotate({F(element.RotationDegrees)} {F(element.Location.X)} {F(element.Location.Y)})\">");
            switch (element.Kind)
            {
                case SiteElementKind.Road:
                    svg.AppendLine($"  <rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(element.Width)}\" height=\"{F(element.Height)}\" rx=\"8\" fill=\"#969692\" fill-opacity=\"0.5\" stroke=\"#696965\"/>");
                    svg.AppendLine($"  <line x1=\"{F(x + 12)}\" y1=\"{F(element.Location.Y)}\" x2=\"{F(x + element.Width - 12)}\" y2=\"{F(element.Location.Y)}\" stroke=\"#ffffff\" stroke-opacity=\"0.75\" stroke-width=\"2\" stroke-dasharray=\"10 8\"/>");
                    break;
                case SiteElementKind.Tree:
                    svg.AppendLine($"  <ellipse cx=\"{F(element.Location.X)}\" cy=\"{F(element.Location.Y)}\" rx=\"{F(element.Width / 2)}\" ry=\"{F(element.Height / 2)}\" fill=\"#849d74\" fill-opacity=\"0.48\" stroke=\"#696965\"/>");
                    AppendLine(svg, new Point(element.Location.X, y + 8), new Point(element.Location.X, y + element.Height - 8), "#696965", 1);
                    AppendLine(svg, new Point(x + 8, element.Location.Y), new Point(x + element.Width - 8, element.Location.Y), "#696965", 1);
                    break;
                case SiteElementKind.Bench:
                    svg.AppendLine($"  <rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(element.Width)}\" height=\"{F(element.Height)}\" rx=\"5\" fill=\"#96846f\" fill-opacity=\"0.5\" stroke=\"#696965\"/>");
                    break;
            }
            AppendText(svg, element.Location.X, element.Location.Y + 4, GetSiteElementName(element.Kind), 12, "#3e3e3b", "600");
            AppendMeasurement(svg, new Point(x, y), new Point(x + element.Width, y), $"{element.Width:0.#} cm", "#dc2626", -15);
            AppendMeasurement(svg, new Point(x + element.Width, y), new Point(x + element.Width, y + element.Height), $"{element.Height:0.#} cm", "#dc2626", -15);
            svg.AppendLine("  </g>");
        }
    }

    private static void DrawRoomFills(StringBuilder svg, FloorPlan plan)
    {
        foreach (var room in plan.RoomAreas)
        {
            svg.AppendLine($"  <rect x=\"{F(room.Bounds.X)}\" y=\"{F(room.Bounds.Y)}\" width=\"{F(room.Bounds.Width)}\" height=\"{F(room.Bounds.Height)}\" fill=\"#f7f7f5\" stroke=\"#d2d2cf\"/>");
            svg.AppendLine($"  <rect x=\"{F(room.Bounds.X)}\" y=\"{F(room.Bounds.Y)}\" width=\"{F(room.Bounds.Width)}\" height=\"{F(room.Bounds.Height)}\" fill=\"url(#roomPattern)\"/>");
        }
    }

    private static void DrawRoomAreaLabels(StringBuilder svg, FloorPlan plan)
    {
        foreach (var room in plan.RoomAreas)
        {
            var centerX = room.Bounds.X + room.Bounds.Width / 2;
            var centerY = room.Bounds.Y + room.Bounds.Height / 2;
            var labelWidth = Math.Max(130, room.Name.Length * 16 + 60);
            svg.AppendLine($"  <rect x=\"{F(centerX - labelWidth / 2)}\" y=\"{F(centerY - 22)}\" width=\"{F(labelWidth)}\" height=\"44\" rx=\"4\" fill=\"#ffffff\" fill-opacity=\"0.94\"/>");
            AppendText(svg, centerX, centerY - 4, $"{room.Name} · L{room.Level}", 15, "#20201f", "600");
            AppendText(svg, centerX, centerY + 14,
                $"{room.AreaSquareMeters:0.##} m² (약 {Math.Round(room.AreaPyeong):0}평)", 11, "#666663", "400");
        }
    }

    private static void DrawWalls(StringBuilder svg, FloorPlan plan)
    {
        foreach (var wall in plan.Walls)
        {
            AppendLine(svg, wall.StartPoint, wall.EndPoint, "#000000", wall.Thickness);
            AppendMeasurement(svg, wall.StartPoint, wall.EndPoint, $"{wall.Length:0.#} cm", "#dc2626", 18);
        }
    }

    private static void DrawOpenings(StringBuilder svg, FloorPlan plan)
    {
        foreach (var door in plan.Doors)
        {
            var (center, tangent, normal) = GetOpeningGeometry(door);
            var half = tangent * (door.Width / 2);
            var openingStart = center - half;
            var openingEnd = center + half;
            var hingeAtStart = door.RotationQuarterTurns is 0 or 3;
            var hinge = hingeAtStart ? openingStart : openingEnd;
            var jamb = hingeAtStart ? openingEnd : openingStart;
            if (door.RotationQuarterTurns >= 2)
            {
                normal *= -1;
            }
            var leafEnd = hinge + normal * door.Width;
            AppendLine(svg, hinge, jamb, "#ffffff", door.ParentWall.Thickness + 3);
            AppendLine(svg, hinge, leafEnd, "#8b5e3c", 2);
            svg.AppendLine($"  <path d=\"M {F(jamb.X)} {F(jamb.Y)} A {F(door.Width)} {F(door.Width)} 0 0 0 {F(leafEnd.X)} {F(leafEnd.Y)}\" fill=\"none\" stroke=\"#8b5e3c\" stroke-width=\"1\"/>");
            AppendMeasurement(svg, openingStart, openingEnd, $"{door.Width:0.#} cm", "#dc2626", -18);
        }
        foreach (var window in plan.Windows)
        {
            var (center, tangent, normal) = GetOpeningGeometry(window);
            var half = tangent * (window.Width / 2);
            var start = center - half;
            var end = center + half;
            AppendLine(svg, start, end, "#ffffff", window.ParentWall.Thickness + 3);
            AppendLine(svg, start + normal * 3, end + normal * 3, "#2563eb", 2);
            AppendLine(svg, start - normal * 3, end - normal * 3, "#2563eb", 2);
            AppendLine(svg, start - normal * 6, start + normal * 6, "#2563eb", 2);
            AppendLine(svg, end - normal * 6, end + normal * 6, "#2563eb", 2);
            var sashNormal = window.RotationQuarterTurns >= 2 ? normal * -1 : normal;
            var diagonalStart = window.RotationQuarterTurns % 2 == 0 ? start : end;
            var diagonalEnd = window.RotationQuarterTurns % 2 == 0 ? end : start;
            AppendLine(svg, diagonalStart + sashNormal * 3, diagonalEnd - sashNormal * 3, "#2563eb", 1);
            AppendMeasurement(svg, start, end, $"{window.Width:0.#} cm", "#dc2626", 18);
        }
    }

    private static void DrawFurniture(StringBuilder svg, FloorPlan plan)
    {
        foreach (var item in plan.FurnitureItems)
        {
            var x = item.Location.X - item.Width / 2;
            var y = item.Location.Y - item.Height / 2;
            svg.AppendLine($"  <g transform=\"rotate({F(item.RotationDegrees)} {F(item.Location.X)} {F(item.Location.Y)})\">");
            svg.AppendLine($"  <rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(item.Width)}\" height=\"{F(item.Height)}\" rx=\"5\" fill=\"#ebebe8\" fill-opacity=\"0.45\" stroke=\"#6e6e6a\" stroke-opacity=\"0.65\" stroke-width=\"1.5\"/>");
            if (item.Kind == FurnitureKind.Stairs)
            {
                for (var step = 1; step < 10; step++)
                {
                    var stepX = x + item.Width * step / 10;
                    AppendLine(svg, new Point(stepX, y), new Point(stepX, y + item.Height), "#777773", 1);
                }
            }
            AppendText(svg, item.Location.X, item.Location.Y + 4, GetFurnitureName(item.Kind), 12, "#3e3e3b", "600");
            AppendMeasurement(svg, new Point(x, y), new Point(x + item.Width, y), $"{item.Width:0.#} cm", "#dc2626", -15);
            AppendMeasurement(svg, new Point(x + item.Width, y), new Point(x + item.Width, y + item.Height), $"{item.Height:0.#} cm", "#dc2626", -15);
            svg.AppendLine("  </g>");
        }
    }

    private static void DrawLabels(StringBuilder svg, FloorPlan plan)
    {
        foreach (var label in plan.RoomLabels)
        {
            AppendText(svg, label.Location.X, label.Location.Y, label.Text, label.FontSize, "#20201f", "600",
                transform: $"rotate({F(label.RotationDegrees)} {F(label.Location.X)} {F(label.Location.Y)})");
        }
    }

    private static void DrawDimensions(StringBuilder svg, FloorPlan plan)
    {
        foreach (var dimension in plan.Dimensions)
        {
            var direction = dimension.EndPoint - dimension.StartPoint;
            if (direction.Length < .001)
            {
                continue;
            }
            direction.Normalize();
            var normal = new Vector(-direction.Y, direction.X);
            AppendLine(svg, dimension.StartPoint, dimension.EndPoint, "#38bdf8", 1.5);
            AppendLine(svg, dimension.StartPoint - normal * 7, dimension.StartPoint + normal * 7, "#38bdf8", 1.5);
            AppendLine(svg, dimension.EndPoint - normal * 7, dimension.EndPoint + normal * 7, "#38bdf8", 1.5);
            AppendMeasurement(svg, dimension.StartPoint, dimension.EndPoint,
                $"{dimension.Length:0.#} cm", "#38bdf8", 13);
        }
    }

    private static void DrawInformationPanel(StringBuilder svg, FloorPlan plan, double gridSize,
        string selectedElementName, IReadOnlyList<string> selectedDetails, Rect panel)
    {
        svg.AppendLine($"  <rect x=\"{F(panel.X)}\" y=\"{F(panel.Y)}\" width=\"{F(panel.Width)}\" height=\"{F(panel.Height)}\" rx=\"10\" fill=\"#ffffff\" stroke=\"#d8d8d5\" stroke-width=\"1\"/>");
        var left = panel.X + 16;
        var right = panel.Right - 16;
        var y = panel.Y + 25;

        AppendText(svg, left, y, "PROPERTIES", 11, "#777774", "700", "start");
        y += 24;
        AppendInfoRow(svg, left, right, y, "Grid", $"{gridSize:0.#} cm");
        y += 19;
        AppendInfoRow(svg, left, right, y, "Rooms / Walls", $"{plan.RoomAreas.Count} / {plan.Walls.Count}");
        y += 19;
        AppendInfoRow(svg, left, right, y, "Doors / Windows", $"{plan.Doors.Count} / {plan.Windows.Count}");
        y += 19;
        AppendInfoRow(svg, left, right, y, "Furniture / Site", $"{plan.FurnitureItems.Count} / {plan.SiteElements.Count}");
        y += 19;
        AppendInfoRow(svg, left, right, y, "Dimensions", $"{plan.Dimensions.Count}");
        y += 24;
        AppendText(svg, left, y, $"Selection · {selectedElementName}", 11, "#20201f", "600", "start");
        foreach (var detail in selectedDetails)
        {
            y += 17;
            AppendText(svg, left, y, detail, 10, "#666663", "400", "start");
        }

        y += 28;
        svg.AppendLine($"  <line x1=\"{F(left)}\" y1=\"{F(y - 13)}\" x2=\"{F(right)}\" y2=\"{F(y - 13)}\" stroke=\"#e4e4e1\"/>");
        AppendText(svg, left, y, "TOTAL", 11, "#777774", "700", "start");
        y += 24;
        var totalSquareMeters = plan.RoomAreas.Sum(room => room.AreaSquareMeters);
        AppendInfoRow(svg, left, right, y, "Total Level Area", FormatArea(totalSquareMeters));
        foreach (var level in plan.RoomAreas.GroupBy(room => room.Level).OrderBy(group => group.Key))
        {
            y += 20;
            AppendInfoRow(svg, left, right, y, $"Level {level.Key} Area",
                FormatArea(level.Sum(room => room.AreaSquareMeters)), "#666663");
        }
    }

    private static void AppendInfoRow(StringBuilder svg, double left, double right, double y,
        string label, string value, string color = "#20201f")
    {
        AppendText(svg, left, y, label, 10, color, "500", "start");
        AppendText(svg, right, y, value, 10, color, "600", "end");
    }

    private static string FormatArea(double squareMeters) =>
        $"{squareMeters:0.##} m² (약 {Math.Round(squareMeters / 3.305785):0}평)";

    private static Rect GetDrawingBounds(FloorPlan plan)
    {
        var bounds = Rect.Empty;
        foreach (var room in plan.RoomAreas)
        {
            bounds.Union(room.Bounds);
        }
        foreach (var element in plan.SiteElements)
        {
            foreach (var corner in GetSiteElementCorners(element))
            {
                UnionPoint(ref bounds, corner, 1);
            }
        }
        foreach (var wall in plan.Walls)
        {
            UnionPoint(ref bounds, wall.StartPoint, wall.Thickness / 2);
            UnionPoint(ref bounds, wall.EndPoint, wall.Thickness / 2);
        }
        foreach (var opening in plan.Doors.Cast<WallOpening>().Concat(plan.Windows))
        {
            var (center, _, _) = GetOpeningGeometry(opening);
            UnionPoint(ref bounds, center, opening.Width);
        }
        foreach (var item in plan.FurnitureItems)
        {
            foreach (var corner in GetFurnitureCorners(item))
            {
                UnionPoint(ref bounds, corner, 1);
            }
        }
        foreach (var label in plan.RoomLabels)
        {
            UnionPoint(ref bounds, label.Location, Math.Max(30, label.Text.Length * label.FontSize / 2));
        }
        foreach (var dimension in plan.Dimensions)
        {
            UnionPoint(ref bounds, dimension.StartPoint, 15);
            UnionPoint(ref bounds, dimension.EndPoint, 15);
        }
        return bounds.IsEmpty ? new Rect(0, 0, 100, 100) : bounds;
    }

    private static void UnionPoint(ref Rect bounds, Point point, double radius)
    {
        bounds.Union(new Rect(point.X - radius, point.Y - radius, radius * 2, radius * 2));
    }

    private static IEnumerable<Point> GetFurnitureCorners(Furniture item)
    {
        var radians = item.RotationDegrees * Math.PI / 180;
        var xAxis = new Vector(Math.Cos(radians), Math.Sin(radians));
        var yAxis = new Vector(-Math.Sin(radians), Math.Cos(radians));
        var halfWidth = item.Width / 2;
        var halfHeight = item.Height / 2;
        yield return item.Location - xAxis * halfWidth - yAxis * halfHeight;
        yield return item.Location + xAxis * halfWidth - yAxis * halfHeight;
        yield return item.Location - xAxis * halfWidth + yAxis * halfHeight;
        yield return item.Location + xAxis * halfWidth + yAxis * halfHeight;
    }

    private static IEnumerable<Point> GetSiteElementCorners(SiteElement element)
    {
        var radians = element.RotationDegrees * Math.PI / 180;
        var xAxis = new Vector(Math.Cos(radians), Math.Sin(radians));
        var yAxis = new Vector(-Math.Sin(radians), Math.Cos(radians));
        var halfWidth = element.Width / 2;
        var halfHeight = element.Height / 2;
        yield return element.Location - xAxis * halfWidth - yAxis * halfHeight;
        yield return element.Location + xAxis * halfWidth - yAxis * halfHeight;
        yield return element.Location - xAxis * halfWidth + yAxis * halfHeight;
        yield return element.Location + xAxis * halfWidth + yAxis * halfHeight;
    }

    private static (Point Center, Vector Tangent, Vector Normal) GetOpeningGeometry(WallOpening opening)
    {
        var vector = opening.ParentWall.EndPoint - opening.ParentWall.StartPoint;
        var tangent = vector;
        if (tangent.Length < .001)
        {
            tangent = new Vector(1, 0);
        }
        else
        {
            tangent.Normalize();
        }
        return (opening.ParentWall.StartPoint + vector * opening.Position,
            tangent, new Vector(-tangent.Y, tangent.X));
    }

    private static void AppendLine(StringBuilder svg, Point start, Point end, string color, double width) =>
        svg.AppendLine($"  <line x1=\"{F(start.X)}\" y1=\"{F(start.Y)}\" x2=\"{F(end.X)}\" y2=\"{F(end.Y)}\" stroke=\"{color}\" stroke-width=\"{F(width)}\" stroke-linecap=\"round\"/>");

    private static void AppendMeasurement(StringBuilder svg, Point start, Point end, string value, string color, double offset)
    {
        var direction = end - start;
        if (direction.Length < .001)
        {
            return;
        }
        direction.Normalize();
        var normal = new Vector(-direction.Y, direction.X);
        var center = start + (end - start) * .5 + normal * offset;
        AppendText(svg, center.X, center.Y + 3, value, 10, color, "600");
    }

    private static void AppendText(StringBuilder svg, double x, double y, string value, double size,
        string color, string weight, string anchor = "middle", string? transform = null) =>
        svg.AppendLine($"  <text x=\"{F(x)}\" y=\"{F(y)}\" text-anchor=\"{anchor}\" font-family=\"Segoe UI, sans-serif\" font-size=\"{F(size)}\" font-weight=\"{weight}\" fill=\"{color}\"{(transform is null ? string.Empty : $" transform=\"{transform}\"")}>{SecurityElement.Escape(value)}</text>");

    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string GetFurnitureName(FurnitureKind kind) => kind switch
    {
        FurnitureKind.Bed => "침대",
        FurnitureKind.Sofa => "소파",
        FurnitureKind.Table => "테이블",
        FurnitureKind.Sink => "싱크대",
        FurnitureKind.Toilet => "변기",
        FurnitureKind.DiningTable => "식탁",
        FurnitureKind.Refrigerator => "냉장고",
        FurnitureKind.Stove => "레인지",
        FurnitureKind.Wardrobe => "옷장",
        FurnitureKind.Bathtub => "욕조",
        FurnitureKind.Shower => "샤워부스",
        FurnitureKind.Stairs => "계단",
        FurnitureKind.Washer => "세탁기",
        FurnitureKind.Washbasin => "세면대",
        _ => kind.ToString()
    };

    private static string GetSiteElementName(SiteElementKind kind) => kind switch
    {
        SiteElementKind.Road => "도로",
        SiteElementKind.Tree => "나무",
        SiteElementKind.Bench => "벤치",
        _ => kind.ToString()
    };
}
