using System.Windows;

namespace HouseDesigner.Models;

public enum FurnitureKind
{
    Bed,
    Sofa,
    Table,
    Sink,
    Toilet,
    DiningTable,
    Refrigerator,
    Stove,
    Wardrobe,
    Bathtub,
    Shower,
    Stairs,
    Washer,
    Washbasin
}

public sealed class Furniture : PlanElement
{
    private Point _location;
    private double _width;
    private double _height;
    private int _rotationDegrees;

    public Furniture(FurnitureKind kind, Point location)
    {
        Kind = kind;
        _location = location;
        (_width, _height) = kind switch
        {
            FurnitureKind.Bed => (200, 150),
            FurnitureKind.Sofa => (200, 85),
            FurnitureKind.Table => (140, 80),
            FurnitureKind.Sink => (120, 60),
            FurnitureKind.Toilet => (70, 110),
            FurnitureKind.DiningTable => (180, 100),
            FurnitureKind.Refrigerator => (90, 80),
            FurnitureKind.Stove => (60, 60),
            FurnitureKind.Wardrobe => (180, 60),
            FurnitureKind.Bathtub => (170, 75),
            FurnitureKind.Shower => (90, 90),
            FurnitureKind.Stairs => (250, 100),
            FurnitureKind.Washer => (70, 70),
            FurnitureKind.Washbasin => (60, 50),
            _ => (100, 100)
        };
    }

    public FurnitureKind Kind { get; }

    public Point Location
    {
        get => _location;
        set => SetField(ref _location, value);
    }

    public double Width
    {
        get => _width;
        set => SetField(ref _width, Math.Max(20, value));
    }

    public double Height
    {
        get => _height;
        set => SetField(ref _height, Math.Max(20, value));
    }

    public int RotationDegrees
    {
        get => _rotationDegrees;
        set => SetField(ref _rotationDegrees, NormalizeRotation(value));
    }

    private static int NormalizeRotation(int value) => ((value % 360) + 360) % 360;
}
