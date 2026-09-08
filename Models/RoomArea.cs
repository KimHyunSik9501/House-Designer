using System.Windows;

namespace HouseDesigner.Models;

/// <summary>
/// 방의 바닥 영역과 면적 정보를 나타냅니다. 1 모델 단위는 1 cm입니다.
/// </summary>
public sealed class RoomArea : PlanElement
{
    private Rect _bounds;
    private string _name;
    private int _level;

    public RoomArea(Rect bounds, string name, int level = 1)
    {
        _bounds = bounds;
        _name = name;
        _level = Math.Max(1, level);
    }

    public Rect Bounds
    {
        get => _bounds;
        set
        {
            if (SetField(ref _bounds, value))
            {
                OnPropertyChanged(nameof(AreaSquareMeters));
                OnPropertyChanged(nameof(AreaPyeong));
            }
        }
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public int Level
    {
        get => _level;
        set => SetField(ref _level, Math.Max(1, value));
    }

    public double AreaSquareMeters => Bounds.Width * Bounds.Height / 10_000;

    public double AreaPyeong => AreaSquareMeters / 3.305785;
}
