namespace HouseDesigner.Models;

public sealed class Door : WallOpening
{
    public Door(Wall parentWall, double position, double width = 90)
        : base(parentWall, position, width)
    {
    }
}
