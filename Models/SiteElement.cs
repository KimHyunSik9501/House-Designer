using System.Windows;

namespace HouseDesigner.Models;

public enum SiteElementKind
{
    Road,
    Tree,
    Bench
}

/// <summary>
/// 건물 외부의 대지 구성 요소를 나타냅니다.
/// </summary>
public sealed class SiteElement : PlanElement
{
    private Point _location;
    private double _width;
    private double _height;
    private int _rotationDegrees;

    public SiteElement(SiteElementKind kind, Point location)
    {
        Kind = kind;
        _location = location;
        (_width, _height) = kind switch
        {
            SiteElementKind.Road => (600, 200),
            SiteElementKind.Tree => (120, 120),
            SiteElementKind.Bench => (180, 60),
            _ => (100, 100)
        };
    }

    public SiteElementKind Kind { get; }

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
        set => SetField(ref _rotationDegrees, ((value % 360) + 360) % 360);
    }
}
