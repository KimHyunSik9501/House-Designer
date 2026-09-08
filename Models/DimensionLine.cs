namespace HouseDesigner.Models;

public sealed class DimensionLine : PlanElement
{
    private Point _startPoint;
    private Point _endPoint;

    public DimensionLine(Point startPoint, Point endPoint)
    {
        _startPoint = startPoint;
        _endPoint = endPoint;
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

    public double Length => (EndPoint - StartPoint).Length;
}
