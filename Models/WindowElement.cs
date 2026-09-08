namespace HouseDesigner.Models;

public sealed class WindowElement : WallOpening
{
    public WindowElement(Wall parentWall, double position, double width = 120)
        : base(parentWall, position, width)
    {
    }
}
