namespace HouseDesigner.Models;

public sealed class RoomLabel : PlanElement
{
    private Point _location;
    private string _text;
    private double _fontSize = 18;
    private int _rotationDegrees;

    public RoomLabel(Point location, string text)
    {
        _location = location;
        _text = text;
    }

    public Point Location
    {
        get => _location;
        set => SetField(ref _location, value);
    }

    public string Text
    {
        get => _text;
        set => SetField(ref _text, value);
    }

    public double FontSize
    {
        get => _fontSize;
        set => SetField(ref _fontSize, Math.Clamp(value, 8, 72));
    }

    public int RotationDegrees
    {
        get => _rotationDegrees;
        set => SetField(ref _rotationDegrees, ((value % 360) + 360) % 360);
    }
}
