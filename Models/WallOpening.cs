namespace HouseDesigner.Models;

/// <summary>
/// 벽 위의 정규화된 위치(0~1)에 부착되는 문과 창문의 공통 데이터입니다.
/// </summary>
public abstract class WallOpening : PlanElement
{
    private double _position;
    private double _width;
    private int _rotationQuarterTurns;

    protected WallOpening(Wall parentWall, double position, double width)
    {
        ParentWall = parentWall;
        _position = Math.Clamp(position, 0, 1);
        _width = width;
    }

    public Wall ParentWall { get; }

    public double Position
    {
        get => _position;
        set => SetField(ref _position, Math.Clamp(value, 0, 1));
    }

    public double Width
    {
        get => _width;
        set => SetField(ref _width, Math.Max(10, value));
    }

    /// <summary>
    /// 벽 부착을 유지한 채 힌지/창짝 방향을 나타내는 0~3의 방향 값입니다.
    /// </summary>
    public int RotationQuarterTurns
    {
        get => _rotationQuarterTurns;
        set => SetField(ref _rotationQuarterTurns, ((value % 4) + 4) % 4);
    }
}
