using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Windows;
using HouseDesigner.Models;

namespace HouseDesigner.Services;

/// <summary>
/// UI나 WPF 렌더링 형식에 의존하지 않는 JSON 문서 저장소입니다.
/// 문과 창문은 부모 벽의 목록 인덱스로 연결해 복원합니다.
/// </summary>
public static class PlanDocumentService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void Save(string path, FloorPlan plan, double gridSize, bool isGridSnapEnabled)
    {
        var document = new PlanDocumentDto
        {
            GridSize = gridSize,
            IsGridSnapEnabled = isGridSnapEnabled,
            Walls = plan.Walls.Select(wall => new WallDto
            {
                StartX = wall.StartPoint.X,
                StartY = wall.StartPoint.Y,
                EndX = wall.EndPoint.X,
                EndY = wall.EndPoint.Y,
                Thickness = wall.Thickness
            }).ToList(),
            Doors = plan.Doors.Select(door => new OpeningDto
            {
                ParentWallIndex = plan.Walls.IndexOf(door.ParentWall),
                Position = door.Position,
                Width = door.Width,
                RotationQuarterTurns = door.RotationQuarterTurns
            }).Where(opening => opening.ParentWallIndex >= 0).ToList(),
            Windows = plan.Windows.Select(window => new OpeningDto
            {
                ParentWallIndex = plan.Walls.IndexOf(window.ParentWall),
                Position = window.Position,
                Width = window.Width,
                RotationQuarterTurns = window.RotationQuarterTurns
            }).Where(opening => opening.ParentWallIndex >= 0).ToList(),
            Furniture = plan.FurnitureItems.Select(item => new FurnitureDto
            {
                Kind = item.Kind,
                X = item.Location.X,
                Y = item.Location.Y,
                Width = item.Width,
                Height = item.Height,
                RotationDegrees = item.RotationDegrees
            }).ToList(),
            RoomLabels = plan.RoomLabels.Select(label => new RoomLabelDto
            {
                X = label.Location.X,
                Y = label.Location.Y,
                Text = label.Text,
                FontSize = label.FontSize,
                RotationDegrees = label.RotationDegrees
            }).ToList(),
            RoomAreas = plan.RoomAreas.Select(room => new RoomAreaDto
            {
                X = room.Bounds.X,
                Y = room.Bounds.Y,
                Width = room.Bounds.Width,
                Height = room.Bounds.Height,
                Name = room.Name,
                Level = room.Level
            }).ToList(),
            Dimensions = plan.Dimensions.Select(dimension => new DimensionDto
            {
                StartX = dimension.StartPoint.X,
                StartY = dimension.StartPoint.Y,
                EndX = dimension.EndPoint.X,
                EndY = dimension.EndPoint.Y
            }).ToList(),
            SiteElements = plan.SiteElements.Select(element => new SiteElementDto
            {
                Kind = element.Kind,
                X = element.Location.X,
                Y = element.Location.Y,
                Width = element.Width,
                Height = element.Height,
                RotationDegrees = element.RotationDegrees
            }).ToList()
        };

        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(path, json, new System.Text.UTF8Encoding(false));
    }

    public static LoadedPlanDocument Load(string path)
    {
        var json = File.ReadAllText(path);
        var document = JsonSerializer.Deserialize<PlanDocumentDto>(json, JsonOptions)
                       ?? throw new InvalidDataException("JSON 문서가 비어 있습니다.");
        if (document.Version is < 1 or > 1)
        {
            throw new InvalidDataException($"지원하지 않는 문서 버전입니다: {document.Version}");
        }

        var plan = new FloorPlan();
        foreach (var wall in document.Walls)
        {
            plan.Walls.Add(new Wall(new Point(wall.StartX, wall.StartY),
                new Point(wall.EndX, wall.EndY), Math.Max(1, wall.Thickness)));
        }
        foreach (var door in document.Doors)
        {
            if (TryGetWall(plan, door.ParentWallIndex, out var parentWall))
            {
                plan.Doors.Add(new Door(parentWall, door.Position, door.Width)
                {
                    RotationQuarterTurns = door.RotationQuarterTurns
                });
            }
        }
        foreach (var window in document.Windows)
        {
            if (TryGetWall(plan, window.ParentWallIndex, out var parentWall))
            {
                plan.Windows.Add(new WindowElement(parentWall, window.Position, window.Width)
                {
                    RotationQuarterTurns = window.RotationQuarterTurns
                });
            }
        }
        foreach (var item in document.Furniture)
        {
            plan.FurnitureItems.Add(new Furniture(item.Kind, new Point(item.X, item.Y))
            {
                Width = item.Width,
                Height = item.Height,
                RotationDegrees = item.RotationDegrees
            });
        }
        foreach (var label in document.RoomLabels)
        {
            plan.RoomLabels.Add(new RoomLabel(new Point(label.X, label.Y), label.Text)
            {
                FontSize = label.FontSize,
                RotationDegrees = label.RotationDegrees
            });
        }
        foreach (var room in document.RoomAreas)
        {
            if (room.Width > 0 && room.Height > 0)
            {
                plan.RoomAreas.Add(new RoomArea(new Rect(room.X, room.Y, room.Width, room.Height), room.Name,
                    room.Level <= 0 ? 1 : room.Level));
            }
        }
        foreach (var dimension in document.Dimensions)
        {
            plan.Dimensions.Add(new DimensionLine(new Point(dimension.StartX, dimension.StartY),
                new Point(dimension.EndX, dimension.EndY)));
        }
        foreach (var element in document.SiteElements)
        {
            plan.SiteElements.Add(new SiteElement(element.Kind, new Point(element.X, element.Y))
            {
                Width = element.Width,
                Height = element.Height,
                RotationDegrees = element.RotationDegrees
            });
        }

        return new LoadedPlanDocument(plan, Math.Max(1, document.GridSize), document.IsGridSnapEnabled);
    }

    private static bool TryGetWall(FloorPlan plan, int index, out Wall wall)
    {
        if (index >= 0 && index < plan.Walls.Count)
        {
            wall = plan.Walls[index];
            return true;
        }
        wall = null!;
        return false;
    }

    private sealed class PlanDocumentDto
    {
        public int Version { get; set; } = 1;
        public double GridSize { get; set; } = 10;
        public bool IsGridSnapEnabled { get; set; } = true;
        public List<WallDto> Walls { get; set; } = [];
        public List<OpeningDto> Doors { get; set; } = [];
        public List<OpeningDto> Windows { get; set; } = [];
        public List<FurnitureDto> Furniture { get; set; } = [];
        public List<RoomLabelDto> RoomLabels { get; set; } = [];
        public List<RoomAreaDto> RoomAreas { get; set; } = [];
        public List<DimensionDto> Dimensions { get; set; } = [];
        public List<SiteElementDto> SiteElements { get; set; } = [];
    }

    private sealed class WallDto
    {
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }
        public double Thickness { get; set; }
    }

    private sealed class OpeningDto
    {
        public int ParentWallIndex { get; set; }
        public double Position { get; set; }
        public double Width { get; set; }
        public int RotationQuarterTurns { get; set; }
    }

    private sealed class FurnitureDto
    {
        public FurnitureKind Kind { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public int RotationDegrees { get; set; }
    }

    private sealed class RoomLabelDto
    {
        public double X { get; set; }
        public double Y { get; set; }
        public string Text { get; set; } = "방";
        public double FontSize { get; set; } = 18;
        public int RotationDegrees { get; set; }
    }

    private sealed class RoomAreaDto
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Name { get; set; } = "방";
        public int Level { get; set; } = 1;
    }

    private sealed class DimensionDto
    {
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }
    }

    private sealed class SiteElementDto
    {
        public SiteElementKind Kind { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public int RotationDegrees { get; set; }
    }
}

public sealed record LoadedPlanDocument(FloorPlan FloorPlan, double GridSize, bool IsGridSnapEnabled);
