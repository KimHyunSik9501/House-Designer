using System.Windows;

namespace HouseDesigner.Models;

/// <summary>
/// 평면도 좌표계에서 벽 하나를 나타냅니다. 화면 요소와 독립된 데이터 모델입니다.
/// </summary>
public sealed class Wall : PlanElement
{
    private Point _startPoint;
    private Point _endPoint;
    private double _thickness;

    public Wall(Point startPoint, Point endPoint, double thickness = 12)
    {
        _startPoint = startPoint;
        _endPoint = endPoint;
        _thickness = thickness;
    }

    public Point StartPoint
    {
        get => _startPoint;
        set
        {
            if (SetField(ref _startPoint, value))
            {
                OnPropertyChanged(nameof(Length));
            }
        }
    }

    public Point EndPoint
    {
        get => _endPoint;
        set
        {
            if (SetField(ref _endPoint, value))
            {
                OnPropertyChanged(nameof(Length));
            }
        }
    }

    public double Thickness
    {
        get => _thickness;
        set => SetField(ref _thickness, value);
    }

    public double Length => (EndPoint - StartPoint).Length;
}
