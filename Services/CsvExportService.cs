using System.Globalization;
using System.IO;
using System.Text;
using HouseDesigner.Models;

namespace HouseDesigner.Services;

/// <summary>
/// 도면 요소를 스프레드시트에서 분석할 수 있는 행 데이터로 내보냅니다.
/// </summary>
public static class CsvExportService
{
    public static void Export(string path, FloorPlan plan)
    {
        var csv = new StringBuilder();
        AppendRow(csv, "ElementType", "Name", "StartX_cm", "StartY_cm", "EndX_cm", "EndY_cm",
            "CenterX_cm", "CenterY_cm", "Width_cm", "Height_cm", "Length_cm", "Area_m2", "Area_pyeong", "ParentWallIndex", "Level", "RotationDegrees");

        for (var i = 0; i < plan.Walls.Count; i++)
        {
            var wall = plan.Walls[i];
            AppendRow(csv, "Wall", $"Wall {i + 1}", wall.StartPoint.X, wall.StartPoint.Y,
                wall.EndPoint.X, wall.EndPoint.Y, null, null, wall.Thickness, null, wall.Length, null, null, null, null, null);
        }
        foreach (var door in plan.Doors)
        {
            var center = GetOpeningCenter(door);
            AppendRow(csv, "Door", "Door", null, null, null, null, center.X, center.Y,
                door.Width, null, door.Width, null, null, plan.Walls.IndexOf(door.ParentWall), null, door.RotationQuarterTurns * 90);
        }
        foreach (var window in plan.Windows)
        {
            var center = GetOpeningCenter(window);
            AppendRow(csv, "Window", "Window", null, null, null, null, center.X, center.Y,
                window.Width, null, window.Width, null, null, plan.Walls.IndexOf(window.ParentWall), null, window.RotationQuarterTurns * 90);
        }
        foreach (var furniture in plan.FurnitureItems)
        {
            AppendRow(csv, "Furniture", GetFurnitureName(furniture.Kind), null, null, null, null,
                furniture.Location.X, furniture.Location.Y, furniture.Width, furniture.Height,
                null, null, null, null, null, furniture.RotationDegrees);
        }
        foreach (var element in plan.SiteElements)
        {
            AppendRow(csv, "SiteElement", GetSiteElementName(element.Kind), null, null, null, null,
                element.Location.X, element.Location.Y, element.Width, element.Height,
                null, null, null, null, null, element.RotationDegrees);
        }
        foreach (var room in plan.RoomAreas)
        {
            AppendRow(csv, "RoomArea", room.Name, null, null, null, null,
                room.Bounds.X + room.Bounds.Width / 2, room.Bounds.Y + room.Bounds.Height / 2,
                room.Bounds.Width, room.Bounds.Height, null, room.AreaSquareMeters, room.AreaPyeong, null, room.Level, null);
        }
        foreach (var label in plan.RoomLabels)
        {
            AppendRow(csv, "Text", label.Text, null, null, null, null,
                label.Location.X, label.Location.Y, null, null, null, null, null, null, null, label.RotationDegrees);
        }
        foreach (var dimension in plan.Dimensions)
        {
            AppendRow(csv, "Dimension", "Dimension", dimension.StartPoint.X, dimension.StartPoint.Y,
                dimension.EndPoint.X, dimension.EndPoint.Y, null, null, null, null,
                dimension.Length, null, null, null, null, null);
        }

        File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));
    }

    private static System.Windows.Point GetOpeningCenter(WallOpening opening)
    {
        var vector = opening.ParentWall.EndPoint - opening.ParentWall.StartPoint;
        return opening.ParentWall.StartPoint + vector * opening.Position;
    }

    private static void AppendRow(StringBuilder builder, params object?[] values)
    {
        builder.AppendLine(string.Join(',', values.Select(FormatValue)));
    }

    private static string FormatValue(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            double number => number.ToString("0.###", CultureInfo.InvariantCulture),
            float number => number.ToString("0.###", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
        return text.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }

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
