using System.Collections.ObjectModel;

namespace HouseDesigner.Models;

/// <summary>
/// 한 개 층의 설계 데이터를 보관하는 루트 모델입니다.
/// </summary>
public sealed class FloorPlan
{
    public ObservableCollection<Wall> Walls { get; } = [];

    public ObservableCollection<Door> Doors { get; } = [];

    public ObservableCollection<WindowElement> Windows { get; } = [];

    public ObservableCollection<Furniture> FurnitureItems { get; } = [];

    public ObservableCollection<RoomLabel> RoomLabels { get; } = [];

    public ObservableCollection<RoomArea> RoomAreas { get; } = [];

    public ObservableCollection<DimensionLine> Dimensions { get; } = [];

    public ObservableCollection<SiteElement> SiteElements { get; } = [];

    public void Remove(PlanElement element)
    {
        switch (element)
        {
            case Wall wall:
                for (var i = Doors.Count - 1; i >= 0; i--)
                {
                    if (ReferenceEquals(Doors[i].ParentWall, wall))
                    {
                        Doors.RemoveAt(i);
                    }
                }
                for (var i = Windows.Count - 1; i >= 0; i--)
                {
                    if (ReferenceEquals(Windows[i].ParentWall, wall))
                    {
                        Windows.RemoveAt(i);
                    }
                }
                Walls.Remove(wall);
                break;
            case Door door:
                Doors.Remove(door);
                break;
            case WindowElement window:
                Windows.Remove(window);
                break;
            case Furniture furniture:
                FurnitureItems.Remove(furniture);
                break;
            case RoomLabel label:
                RoomLabels.Remove(label);
                break;
            case RoomArea room:
                RoomAreas.Remove(room);
                break;
            case DimensionLine dimension:
                Dimensions.Remove(dimension);
                break;
            case SiteElement siteElement:
                SiteElements.Remove(siteElement);
                break;
        }
    }

    public void Clear()
    {
        Doors.Clear();
        Windows.Clear();
        FurnitureItems.Clear();
        RoomLabels.Clear();
        RoomAreas.Clear();
        Dimensions.Clear();
        SiteElements.Clear();
        Walls.Clear();
    }
}
