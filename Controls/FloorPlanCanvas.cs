using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using HouseDesigner.Models;
using HouseDesigner.ViewModels;

namespace HouseDesigner.Controls;

/// <summary>
/// 모델 좌표(1 단위 = 1 cm)를 화면에 렌더링하고 모든 편집 입력을 처리합니다.
/// </summary>
public sealed class FloorPlanCanvas : FrameworkElement
{
    private enum ResizeHandle
    {
        None,
        WallStart,
        WallEnd,
        DimensionStart,
        DimensionEnd,
        OpeningStart,
        OpeningEnd,
        FurnitureTopLeft,
        FurnitureTopRight,
        FurnitureBottomLeft,
        FurnitureBottomRight,
        SiteTopLeft,
        SiteTopRight,
        SiteBottomLeft,
        SiteBottomRight,
        RoomTopLeft,
        RoomTopRight,
        RoomBottomLeft,
        RoomBottomRight,
        LabelScale
    }

    public static readonly DependencyProperty FloorPlanProperty = DependencyProperty.Register(
        nameof(FloorPlan), typeof(FloorPlan), typeof(FloorPlanCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnFloorPlanChanged));
    public static readonly DependencyProperty ActiveToolProperty = DependencyProperty.Register(
        nameof(ActiveTool), typeof(EditorTool), typeof(FloorPlanCanvas),
        new FrameworkPropertyMetadata(EditorTool.Wall, FrameworkPropertyMetadataOptions.AffectsRender, OnToolChanged));
    public static readonly DependencyProperty IsGridSnapEnabledProperty = DependencyProperty.Register(
        nameof(IsGridSnapEnabled), typeof(bool), typeof(FloorPlanCanvas),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty GridSizeProperty = DependencyProperty.Register(
        nameof(GridSize), typeof(double), typeof(FloorPlanCanvas),
        new FrameworkPropertyMetadata(10d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty SelectedElementProperty = DependencyProperty.Register(
        nameof(SelectedElement), typeof(PlanElement), typeof(FloorPlanCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty SelectedFurnitureKindProperty = DependencyProperty.Register(
        nameof(SelectedFurnitureKind), typeof(FurnitureKind), typeof(FloorPlanCanvas),
        new FrameworkPropertyMetadata(FurnitureKind.Bed, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty SelectedSiteElementKindProperty = DependencyProperty.Register(
        nameof(SelectedSiteElementKind), typeof(SiteElementKind), typeof(FloorPlanCanvas),
        new FrameworkPropertyMetadata(SiteElementKind.Road, FrameworkPropertyMetadataOptions.AffectsRender));

    private const double MinimumZoom = 0.25;
    private const double MaximumZoom = 5;
    private static readonly Brush WallBrush = Brushes.Black;
    private static readonly Brush DoorBrush = new SolidColorBrush(Color.FromRgb(139, 94, 60));
    private static readonly Brush WindowBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
    private static readonly Brush DimensionBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
    private static readonly Brush MeasurementTextBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38));
    private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(38, 38, 38));
    private static readonly Brush SelectionBrush = new SolidColorBrush(Color.FromRgb(92, 92, 92));
    private readonly Pen _minorGridPen = new(new SolidColorBrush(Color.FromRgb(235, 235, 232)), 1);
    private readonly Pen _majorGridPen = new(new SolidColorBrush(Color.FromRgb(211, 211, 207)), 1);
    private readonly Pen _previewPen = new(AccentBrush, 2) { DashStyle = DashStyles.Dash };

    private Point? _wallStart;
    private Point? _roomStart;
    private Point? _dimensionStart;
    private Point _previewEnd;
    private Point _panOffset = new(80, 80);
    private Point _panStart;
    private Point _panOrigin;
    private bool _isPanning;
    private bool _isDragging;
    private ResizeHandle _activeResizeHandle;
    private Point _dragStartWorld;
    private Point _dragElementOrigin;
    private Point _dragWallStart;
    private Point _dragWallEnd;
    private Rect _dragRoomBounds;
    private double _resizeOpeningFixedPosition;
    private Point _resizeFurnitureOpposite;
    private Point _resizeStartScreen;
    private double _resizeOriginalFontSize;
    private double _zoom = 1;

    public FloorPlanCanvas()
    {
        Focusable = true;
        ClipToBounds = true;
        Cursor = Cursors.Cross;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseRightButtonDown += BeginPan;
        MouseRightButtonUp += EndPan;
        MouseMove += OnMouseMove;
        MouseWheel += OnMouseWheel;
    }

    public FloorPlan? FloorPlan
    {
        get => (FloorPlan?)GetValue(FloorPlanProperty);
        set => SetValue(FloorPlanProperty, value);
    }

    public EditorTool ActiveTool
    {
        get => (EditorTool)GetValue(ActiveToolProperty);
        set => SetValue(ActiveToolProperty, value);
    }

    public bool IsGridSnapEnabled
    {
        get => (bool)GetValue(IsGridSnapEnabledProperty);
        set => SetValue(IsGridSnapEnabledProperty, value);
    }

    public double GridSize
    {
        get => (double)GetValue(GridSizeProperty);
        set => SetValue(GridSizeProperty, value);
    }

    public PlanElement? SelectedElement
    {
        get => (PlanElement?)GetValue(SelectedElementProperty);
        set => SetValue(SelectedElementProperty, value);
    }

    public FurnitureKind SelectedFurnitureKind
    {
        get => (FurnitureKind)GetValue(SelectedFurnitureKindProperty);
        set => SetValue(SelectedFurnitureKindProperty, value);
    }

    public SiteElementKind SelectedSiteElementKind
    {
        get => (SiteElementKind)GetValue(SelectedSiteElementKindProperty);
        set => SetValue(SelectedSiteElementKindProperty, value);
    }

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        context.DrawRectangle(Brushes.White, null, new Rect(RenderSize));
        DrawGrid(context);
        DrawSiteElements(context);
        DrawRoomAreas(context);
        DrawFurniture(context);
        DrawWalls(context);
        DrawOpenings(context);
        DrawRoomAreaLabels(context);
        DrawRoomLabels(context);
        DrawDimensions(context);
        DrawSelectionHandles(context);
        DrawPlacementPreview(context);
        DrawHelp(context);
    }

    private void DrawGrid(DrawingContext context)
    {
        if (GridSize <= 0 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }
        var topLeft = ScreenToWorld(new Point(0, 0));
        var bottomRight = ScreenToWorld(new Point(ActualWidth, ActualHeight));
        var firstX = Math.Floor(topLeft.X / GridSize) * GridSize;
        var firstY = Math.Floor(topLeft.Y / GridSize) * GridSize;
        for (var x = firstX; x <= bottomRight.X; x += GridSize)
        {
            var screenX = WorldToScreen(new Point(x, 0)).X;
            var index = (long)Math.Round(x / GridSize);
            context.DrawLine(index % 5 == 0 ? _majorGridPen : _minorGridPen,
                new Point(screenX, 0), new Point(screenX, ActualHeight));
        }
        for (var y = firstY; y <= bottomRight.Y; y += GridSize)
        {
            var screenY = WorldToScreen(new Point(0, y)).Y;
            var index = (long)Math.Round(y / GridSize);
            context.DrawLine(index % 5 == 0 ? _majorGridPen : _minorGridPen,
                new Point(0, screenY), new Point(ActualWidth, screenY));
        }
    }

    private void DrawSiteElements(DrawingContext context)
    {
        if (FloorPlan is null)
        {
            return;
        }
        foreach (var element in FloorPlan.SiteElements)
        {
            var center = WorldToScreen(element.Location);
            var width = element.Width * _zoom;
            var height = element.Height * _zoom;
            var rect = new Rect(center.X - width / 2, center.Y - height / 2, width, height);
            var selected = ReferenceEquals(SelectedElement, element);
            context.PushTransform(new RotateTransform(element.RotationDegrees, center.X, center.Y));
            var borderPen = new Pen(selected ? SelectionBrush : new SolidColorBrush(Color.FromRgb(105, 105, 101)), selected ? 3 : 1.5);
            switch (element.Kind)
            {
                case SiteElementKind.Road:
                    context.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(125, 150, 150, 146)), borderPen, rect, 8, 8);
                    var centerPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), 2) { DashStyle = DashStyles.Dash };
                    context.DrawLine(centerPen, new Point(rect.Left + 12, center.Y), new Point(rect.Right - 12, center.Y));
                    break;
                case SiteElementKind.Tree:
                    context.DrawEllipse(new SolidColorBrush(Color.FromArgb(120, 132, 157, 116)), borderPen, center, width / 2, height / 2);
                    context.DrawLine(borderPen, new Point(center.X, rect.Top + 8), new Point(center.X, rect.Bottom - 8));
                    context.DrawLine(borderPen, new Point(rect.Left + 8, center.Y), new Point(rect.Right - 8, center.Y));
                    break;
                case SiteElementKind.Bench:
                    context.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(125, 150, 132, 111)), borderPen, rect, 5, 5);
                    for (var y = rect.Top + rect.Height / 4; y < rect.Bottom; y += Math.Max(5, rect.Height / 4))
                    {
                        context.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(150, 100, 84, 68)), 1),
                            new Point(rect.Left + 5, y), new Point(rect.Right - 5, y));
                    }
                    break;
            }
            DrawCenteredText(context, GetSiteElementName(element.Kind), center, Math.Clamp(12 * _zoom, 9, 15), WallBrush);
            DrawScreenMeasurementLabel(context, rect.TopLeft, rect.TopRight,
                $"{element.Width:0.#} cm", MeasurementTextBrush, -15);
            DrawScreenMeasurementLabel(context, rect.TopRight, rect.BottomRight,
                $"{element.Height:0.#} cm", MeasurementTextBrush, -15);
            context.Pop();
        }
    }

    private void DrawRoomAreas(DrawingContext context)
    {
        if (FloorPlan is null)
        {
            return;
        }
        foreach (var room in FloorPlan.RoomAreas)
        {
            var rect = WorldRectToScreen(room.Bounds);
            var selected = ReferenceEquals(SelectedElement, room);
            context.DrawRectangle(new SolidColorBrush(Color.FromRgb(247, 247, 245)),
                new Pen(selected ? SelectionBrush : new SolidColorBrush(Color.FromRgb(210, 210, 207)), selected ? 2 : 1), rect);

            context.PushClip(new RectangleGeometry(rect));
            var patternPen = new Pen(new SolidColorBrush(Color.FromArgb(55, 100, 100, 96)), 1);
            for (var x = rect.Left - rect.Height; x < rect.Right; x += 18)
            {
                context.DrawLine(patternPen, new Point(x, rect.Bottom), new Point(x + rect.Height, rect.Top));
            }
            context.Pop();

        }
    }

    private void DrawRoomAreaLabels(DrawingContext context)
    {
        if (FloorPlan is null)
        {
            return;
        }
        foreach (var room in FloorPlan.RoomAreas)
        {
            var rect = WorldRectToScreen(room.Bounds);
            var center = new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);
            var nameText = CreateText($"{room.Name} · L{room.Level}", Math.Clamp(15 * _zoom, 11, 18), WallBrush, FontWeights.SemiBold);
            var areaText = CreateText($"{room.AreaSquareMeters:0.##} m² (약 {Math.Round(room.AreaPyeong):0}평)", Math.Clamp(11 * _zoom, 9, 14),
                new SolidColorBrush(Color.FromRgb(100, 100, 96)));
            var labelRect = new Rect(center.X - Math.Max(nameText.Width, areaText.Width) / 2 - 9,
                center.Y - (nameText.Height + areaText.Height) / 2 - 5,
                Math.Max(nameText.Width, areaText.Width) + 18, nameText.Height + areaText.Height + 10);
            context.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(238, 255, 255, 255)), null, labelRect, 4, 4);
            context.DrawText(nameText, new Point(center.X - nameText.Width / 2, labelRect.Top + 4));
            context.DrawText(areaText, new Point(center.X - areaText.Width / 2, labelRect.Top + 4 + nameText.Height));
        }
    }

    private void DrawWalls(DrawingContext context)
    {
        if (FloorPlan is null)
        {
            return;
        }
        foreach (var wall in FloorPlan.Walls)
        {
            var start = WorldToScreen(wall.StartPoint);
            var end = WorldToScreen(wall.EndPoint);
            if (ReferenceEquals(SelectedElement, wall))
            {
                context.DrawLine(new Pen(SelectionBrush, Math.Max(5, wall.Thickness * _zoom + 6)), start, end);
            }
            context.DrawLine(new Pen(WallBrush, Math.Max(2, wall.Thickness * _zoom)), start, end);
            DrawLengthLabel(context, wall.StartPoint, wall.EndPoint, wall.Length, ReferenceEquals(SelectedElement, wall));
        }
    }

    private void DrawOpenings(DrawingContext context)
    {
        if (FloorPlan is null)
        {
            return;
        }
        foreach (var door in FloorPlan.Doors)
        {
            DrawDoor(context, door);
        }
        foreach (var window in FloorPlan.Windows)
        {
            DrawWindow(context, window);
        }
    }

    private void DrawDoor(DrawingContext context, Door door)
    {
        var (center, tangent, normal) = GetOpeningScreenGeometry(door);
        var width = Math.Max(8, door.Width * _zoom);
        var half = tangent * (width / 2);
        var openingStart = center - half;
        var openingEnd = center + half;
        var hingeAtStart = door.RotationQuarterTurns is 0 or 3;
        var hinge = hingeAtStart ? openingStart : openingEnd;
        var jamb = hingeAtStart ? openingEnd : openingStart;
        if (door.RotationQuarterTurns >= 2)
        {
            normal *= -1;
        }
        var leafEnd = hinge + normal * width;
        var wallThickness = Math.Max(3, door.ParentWall.Thickness * _zoom + 3);
        context.DrawLine(new Pen(Brushes.White, wallThickness), hinge, jamb);
        if (ReferenceEquals(SelectedElement, door))
        {
            context.DrawEllipse(null, new Pen(SelectionBrush, 3), center, width / 2 + 5, width / 2 + 5);
        }
        var symbolPen = new Pen(DoorBrush, 2);
        context.DrawLine(symbolPen, hinge, leafEnd);
        context.DrawLine(symbolPen, hinge - normal * 4, hinge + normal * 4);
        context.DrawLine(symbolPen, jamb - normal * 4, jamb + normal * 4);
        var arc = new StreamGeometry();
        using (var geometry = arc.Open())
        {
            geometry.BeginFigure(jamb, false, false);
            geometry.ArcTo(leafEnd, new Size(width, width), 0, false, SweepDirection.Counterclockwise, true, false);
        }
        context.DrawGeometry(null, new Pen(DoorBrush, 1), arc);
        DrawScreenMeasurementLabel(context, openingStart, openingEnd, $"{door.Width:0.#} cm", MeasurementTextBrush, -18);
    }

    private void DrawWindow(DrawingContext context, WindowElement window)
    {
        var (center, tangent, normal) = GetOpeningScreenGeometry(window);
        var width = Math.Max(8, window.Width * _zoom);
        var half = tangent * (width / 2);
        var start = center - half;
        var end = center + half;
        var wallThickness = Math.Max(3, window.ParentWall.Thickness * _zoom + 3);
        if (ReferenceEquals(SelectedElement, window))
        {
            context.DrawLine(new Pen(SelectionBrush, wallThickness + 6), start, end);
        }
        context.DrawLine(new Pen(Brushes.White, wallThickness), start, end);
        var pen = new Pen(WindowBrush, 2);
        context.DrawLine(pen, start + normal * 3, end + normal * 3);
        context.DrawLine(pen, start - normal * 3, end - normal * 3);
        context.DrawLine(pen, start - normal * 6, start + normal * 6);
        context.DrawLine(pen, end - normal * 6, end + normal * 6);
        var sashNormal = window.RotationQuarterTurns >= 2 ? normal * -1 : normal;
        var diagonalStart = window.RotationQuarterTurns % 2 == 0 ? start : end;
        var diagonalEnd = window.RotationQuarterTurns % 2 == 0 ? end : start;
        context.DrawLine(new Pen(WindowBrush, 1), diagonalStart + sashNormal * 3, diagonalEnd - sashNormal * 3);
        DrawScreenMeasurementLabel(context, start, end, $"{window.Width:0.#} cm", MeasurementTextBrush, 18);
    }

    private void DrawFurniture(DrawingContext context)
    {
        if (FloorPlan is null)
        {
            return;
        }
        foreach (var furniture in FloorPlan.FurnitureItems)
        {
            var center = WorldToScreen(furniture.Location);
            var width = furniture.Width * _zoom;
            var height = furniture.Height * _zoom;
            var rect = new Rect(center.X - width / 2, center.Y - height / 2, width, height);
            var selected = ReferenceEquals(SelectedElement, furniture);
            context.PushTransform(new RotateTransform(furniture.RotationDegrees, center.X, center.Y));
            context.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(115, 235, 235, 232)),
                new Pen(selected ? SelectionBrush : new SolidColorBrush(Color.FromArgb(150, 110, 110, 106)), selected ? 3 : 1.5),
                rect, 5, 5);
            DrawFurnitureDetails(context, furniture, rect);
            DrawCenteredText(context, GetFurnitureName(furniture.Kind), center, Math.Clamp(12 * _zoom, 9, 15),
                new SolidColorBrush(Color.FromRgb(62, 62, 59)));
            DrawScreenMeasurementLabel(context, rect.TopLeft, rect.TopRight,
                $"{furniture.Width:0.#} cm", MeasurementTextBrush, -15);
            DrawScreenMeasurementLabel(context, rect.TopRight, rect.BottomRight,
                $"{furniture.Height:0.#} cm", MeasurementTextBrush, -15);
            context.Pop();
        }
    }

    private void DrawFurnitureDetails(DrawingContext context, Furniture furniture, Rect rect)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(145, 105, 105, 101)), 1);
        switch (furniture.Kind)
        {
            case FurnitureKind.Bed:
                context.DrawLine(pen, new Point(rect.Left, rect.Top + rect.Height * .28), new Point(rect.Right, rect.Top + rect.Height * .28));
                context.DrawRoundedRectangle(null, pen, new Rect(rect.Left + 5, rect.Top + 5, Math.Max(8, rect.Width * .38), Math.Max(6, rect.Height * .2)), 3, 3);
                break;
            case FurnitureKind.Sofa:
                context.DrawLine(pen, new Point(rect.Left + 6, rect.Top + rect.Height * .28), new Point(rect.Right - 6, rect.Top + rect.Height * .28));
                context.DrawLine(pen, new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height * .28), new Point(rect.Left + rect.Width / 2, rect.Bottom - 5));
                break;
            case FurnitureKind.Table:
                context.DrawEllipse(null, pen, new Point(rect.Left + 8, rect.Top + 8), 3, 3);
                context.DrawEllipse(null, pen, new Point(rect.Right - 8, rect.Bottom - 8), 3, 3);
                break;
            case FurnitureKind.Sink:
                context.DrawEllipse(null, pen, new Point(rect.Left + rect.Width * .3, rect.Top + rect.Height / 2), Math.Max(5, rect.Width * .16), Math.Max(4, rect.Height * .28));
                context.DrawEllipse(null, pen, new Point(rect.Left + rect.Width * .7, rect.Top + rect.Height / 2), Math.Max(5, rect.Width * .16), Math.Max(4, rect.Height * .28));
                break;
            case FurnitureKind.Toilet:
                context.DrawEllipse(null, pen, new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height * .62), Math.Max(5, rect.Width * .3), Math.Max(6, rect.Height * .28));
                break;
            case FurnitureKind.DiningTable:
                context.DrawRoundedRectangle(null, pen, new Rect(rect.Left + rect.Width * .15, rect.Top + rect.Height * .18, rect.Width * .7, rect.Height * .64), 5, 5);
                break;
            case FurnitureKind.Refrigerator:
                context.DrawLine(pen, new Point(rect.Left, rect.Top + rect.Height * .38), new Point(rect.Right, rect.Top + rect.Height * .38));
                break;
            case FurnitureKind.Stove:
                for (var x = .3; x <= .7; x += .4)
                for (var y = .3; y <= .7; y += .4)
                    context.DrawEllipse(null, pen, new Point(rect.Left + rect.Width * x, rect.Top + rect.Height * y), 4, 4);
                break;
            case FurnitureKind.Wardrobe:
                context.DrawLine(pen, new Point(rect.Left + rect.Width / 2, rect.Top), new Point(rect.Left + rect.Width / 2, rect.Bottom));
                break;
            case FurnitureKind.Bathtub:
            case FurnitureKind.Shower:
                context.DrawRoundedRectangle(null, pen, new Rect(rect.Left + 6, rect.Top + 6, Math.Max(4, rect.Width - 12), Math.Max(4, rect.Height - 12)), 12, 12);
                break;
            case FurnitureKind.Stairs:
                for (var x = rect.Left + rect.Width / 10; x < rect.Right; x += Math.Max(5, rect.Width / 10))
                    context.DrawLine(pen, new Point(x, rect.Top), new Point(x, rect.Bottom));
                break;
            case FurnitureKind.Washer:
                context.DrawEllipse(null, pen, new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2), Math.Max(5, rect.Width * .3), Math.Max(5, rect.Height * .3));
                break;
            case FurnitureKind.Washbasin:
                context.DrawEllipse(null, pen, new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height * .58), Math.Max(5, rect.Width * .32), Math.Max(4, rect.Height * .24));
                context.DrawLine(pen, new Point(rect.Left + rect.Width / 2, rect.Top + 4), new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height * .3));
                break;
        }
    }

    private void DrawRoomLabels(DrawingContext context)
    {
        if (FloorPlan is null)
        {
            return;
        }
        foreach (var label in FloorPlan.RoomLabels)
        {
            var center = WorldToScreen(label.Location);
            var text = CreateText(label.Text, Math.Max(8, label.FontSize * _zoom), WallBrush, FontWeights.SemiBold);
            var rect = GetRoomLabelRect(label, text);
            context.PushTransform(new RotateTransform(label.RotationDegrees, center.X, center.Y));
            context.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                ReferenceEquals(SelectedElement, label) ? new Pen(SelectionBrush, 2) : null, rect, 4, 4);
            context.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
            context.Pop();
        }
    }

    private void DrawDimensions(DrawingContext context)
    {
        if (FloorPlan is null)
        {
            return;
        }
        foreach (var dimension in FloorPlan.Dimensions)
        {
            DrawDimension(context, dimension.StartPoint, dimension.EndPoint,
                ReferenceEquals(SelectedElement, dimension));
        }
    }

    private void DrawDimension(DrawingContext context, Point startWorld, Point endWorld, bool selected)
    {
        var start = WorldToScreen(startWorld);
        var end = WorldToScreen(endWorld);
        var direction = end - start;
        if (direction.Length < 1)
        {
            return;
        }
        var length = (endWorld - startWorld).Length;
        direction.Normalize();
        var normal = new Vector(-direction.Y, direction.X);
        var pen = new Pen(DimensionBrush, selected ? 2.5 : 1.4);
        context.DrawLine(pen, start, end);
        context.DrawLine(pen, start - normal * 7, start + normal * 7);
        context.DrawLine(pen, end - normal * 7, end + normal * 7);
        context.DrawLine(pen, start, start + direction * 9 + normal * 4);
        context.DrawLine(pen, start, start + direction * 9 - normal * 4);
        context.DrawLine(pen, end, end - direction * 9 + normal * 4);
        context.DrawLine(pen, end, end - direction * 9 - normal * 4);

        var center = start + (end - start) * .5 + normal * 13;
        var text = CreateText($"{length:0.#} cm", 11, pen.Brush, FontWeights.SemiBold);
        var background = new Rect(center.X - text.Width / 2 - 4, center.Y - text.Height / 2 - 2, text.Width + 8, text.Height + 4);
        context.DrawRectangle(Brushes.White, null, background);
        context.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
    }

    private void DrawSelectionHandles(DrawingContext context)
    {
        if (SelectedElement is null || ActiveTool != EditorTool.Select)
        {
            return;
        }

        switch (SelectedElement)
        {
            case Wall wall:
                DrawHandle(context, WorldToScreen(wall.StartPoint), true);
                DrawHandle(context, WorldToScreen(wall.EndPoint), true);
                break;
            case DimensionLine dimension:
                DrawHandle(context, WorldToScreen(dimension.StartPoint), true);
                DrawHandle(context, WorldToScreen(dimension.EndPoint), true);
                break;
            case WallOpening opening:
                var (center, tangent, _) = GetOpeningScreenGeometry(opening);
                var half = tangent * (opening.Width * _zoom / 2);
                DrawHandle(context, center - half, false);
                DrawHandle(context, center + half, false);
                break;
            case Furniture furniture:
                foreach (var point in GetFurnitureCorners(furniture))
                {
                    DrawHandle(context, point, false);
                }
                break;
            case SiteElement siteElement:
                foreach (var point in GetSiteElementCorners(siteElement))
                {
                    DrawHandle(context, point, false);
                }
                break;
            case RoomArea room:
                foreach (var point in GetRoomCorners(room))
                {
                    DrawHandle(context, point, false);
                }
                break;
            case RoomLabel label:
                var labelCenter = WorldToScreen(label.Location);
                DrawHandle(context, RotatePoint(GetRoomLabelRect(label).BottomRight, labelCenter, label.RotationDegrees), false);
                break;
        }
    }

    private static void DrawHandle(DrawingContext context, Point center, bool round)
    {
        var pen = new Pen(SelectionBrush, 2);
        if (round)
        {
            context.DrawEllipse(Brushes.White, pen, center, 6, 6);
        }
        else
        {
            context.DrawRectangle(Brushes.White, pen, new Rect(center.X - 5, center.Y - 5, 10, 10));
        }
    }

    private void DrawPlacementPreview(DrawingContext context)
    {
        if (_wallStart is not null && ActiveTool == EditorTool.Wall)
        {
            var start = WorldToScreen(_wallStart.Value);
            var end = WorldToScreen(_previewEnd);
            context.DrawLine(_previewPen, start, end);
            context.DrawEllipse(Brushes.White, _previewPen, start, 4, 4);
            context.DrawEllipse(Brushes.White, _previewPen, end, 4, 4);
            DrawLengthLabel(context, _wallStart.Value, _previewEnd, (_previewEnd - _wallStart.Value).Length, true);
        }
        if (_roomStart is not null && ActiveTool == EditorTool.RoomArea)
        {
            var rect = WorldRectToScreen(CreateWorldRect(_roomStart.Value, _previewEnd));
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(45, 30, 30, 30)), _previewPen, rect);
        }
        if (_dimensionStart is not null && ActiveTool == EditorTool.Dimension)
        {
            DrawDimension(context, _dimensionStart.Value, _previewEnd, true);
        }
    }

    private void DrawLengthLabel(DrawingContext context, Point startWorld, Point endWorld, double length, bool emphasized)
    {
        if (length < 0.1)
        {
            return;
        }
        var start = WorldToScreen(startWorld);
        var end = WorldToScreen(endWorld);
        var direction = end - start;
        if (direction.Length < 1)
        {
            return;
        }
        direction.Normalize();
        var normal = new Vector(-direction.Y, direction.X);
        var center = start + (end - start) * .5 + normal * 18;
        var text = CreateText($"{length:0.#} cm", 12, MeasurementTextBrush, FontWeights.SemiBold);
        var background = new Rect(center.X - text.Width / 2 - 5, center.Y - text.Height / 2 - 2, text.Width + 10, text.Height + 4);
        context.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(225, 255, 255, 255)), null, background, 3, 3);
        context.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
    }

    private void DrawScreenMeasurementLabel(DrawingContext context, Point start, Point end, string value, Brush brush, double offset)
    {
        var direction = end - start;
        if (direction.Length < 1)
        {
            return;
        }
        direction.Normalize();
        var normal = new Vector(-direction.Y, direction.X);
        var center = start + (end - start) * .5 + normal * offset;
        var text = CreateText(value, 10, brush, FontWeights.SemiBold);
        var background = new Rect(center.X - text.Width / 2 - 4, center.Y - text.Height / 2 - 1, text.Width + 8, text.Height + 2);
        context.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(225, 255, 255, 255)), null, background, 3, 3);
        context.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
    }

    private void DrawHelp(DrawingContext context)
    {
        var message = ActiveTool switch
        {
            EditorTool.Wall when _wallStart is not null => "끝점을 클릭하세요 · 길이는 cm 단위입니다 · Esc: 취소",
            EditorTool.Wall => "벽의 시작점과 끝점을 차례로 클릭하세요",
            EditorTool.Door => "문을 배치할 기존 벽을 클릭하세요 (기본 너비 90 cm)",
            EditorTool.Window => "창문을 배치할 기존 벽을 클릭하세요 (기본 너비 120 cm)",
            EditorTool.RoomArea when _roomStart is not null => "방 영역의 반대쪽 모서리를 클릭하세요",
            EditorTool.RoomArea => "방 바닥 영역의 두 모서리를 차례로 클릭하세요",
            EditorTool.RoomLabel => "방 이름을 놓을 위치를 클릭하세요",
            EditorTool.Furniture => $"{GetFurnitureName(SelectedFurnitureKind)}을(를) 놓을 위치를 클릭하세요",
            EditorTool.Dimension when _dimensionStart is not null => "치수의 끝점을 클릭하세요",
            EditorTool.Dimension => "측정할 두 점을 차례로 클릭하세요",
            EditorTool.SiteElement => $"{GetSiteElementName(SelectedSiteElementKind)}을(를) 놓을 위치를 클릭하세요",
            _ => "요소를 드래그해 이동하고 파란 핸들을 드래그해 크기를 조절하세요 · Delete: 삭제"
        };
        var text = CreateText(message + "  |  우클릭 드래그: 화면 이동  |  휠: 확대/축소", 12,
            new SolidColorBrush(Color.FromRgb(90, 98, 110)));
        var y = Math.Max(8, ActualHeight - 36);
        context.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)), null,
            new Rect(12, y, Math.Min(text.Width + 20, Math.Max(0, ActualWidth - 24)), 26), 4, 4);
        context.PushClip(new RectangleGeometry(new Rect(12, y, Math.Max(0, ActualWidth - 24), 26)));
        context.DrawText(text, new Point(22, y + 5));
        context.Pop();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Focus();
        if (FloorPlan is null)
        {
            return;
        }
        var screen = e.GetPosition(this);
        var world = ScreenToWorld(screen);
        switch (ActiveTool)
        {
            case EditorTool.Wall:
                PlaceWallPoint(SnapPoint(world));
                break;
            case EditorTool.Door:
                PlaceOpening(screen, true);
                break;
            case EditorTool.Window:
                PlaceOpening(screen, false);
                break;
            case EditorTool.RoomArea:
                PlaceRoomPoint(SnapPoint(world));
                break;
            case EditorTool.RoomLabel:
                var label = new RoomLabel(SnapPoint(world), $"방 {FloorPlan.RoomLabels.Count + 1}");
                FloorPlan.RoomLabels.Add(label);
                Select(label);
                break;
            case EditorTool.Furniture:
                var furniture = new Furniture(SelectedFurnitureKind, SnapPoint(world));
                FloorPlan.FurnitureItems.Add(furniture);
                Select(furniture);
                break;
            case EditorTool.Dimension:
                PlaceDimensionPoint(SnapPoint(world));
                break;
            case EditorTool.SiteElement:
                var siteElement = new SiteElement(SelectedSiteElementKind, SnapPoint(world));
                FloorPlan.SiteElements.Add(siteElement);
                Select(siteElement);
                break;
            case EditorTool.Select:
                var handle = HitTestResizeHandle(screen);
                if (handle != ResizeHandle.None)
                {
                    BeginElementResize(handle, screen);
                }
                else
                {
                    BeginElementDrag(HitTestElement(screen), world);
                }
                break;
        }
        InvalidateVisual();
        e.Handled = true;
    }

    private void PlaceWallPoint(Point point)
    {
        if (_wallStart is null)
        {
            _wallStart = point;
            _previewEnd = point;
            return;
        }
        var start = _wallStart.Value;
        if ((point - start).Length > 0.01 && FloorPlan is not null)
        {
            var wall = new Wall(start, point);
            FloorPlan.Walls.Add(wall);
            Select(wall);
        }
        _wallStart = null;
    }

    private void PlaceOpening(Point screenPoint, bool isDoor)
    {
        if (FloorPlan is null)
        {
            return;
        }
        var wall = HitTestWall(screenPoint);
        if (wall is null)
        {
            return;
        }
        var width = isDoor ? 90d : 120d;
        var position = ClampOpeningPosition(wall, ProjectToWall(wall, ScreenToWorld(screenPoint)), width);
        if (isDoor)
        {
            var door = new Door(wall, position, width);
            FloorPlan.Doors.Add(door);
            Select(door);
        }
        else
        {
            var window = new WindowElement(wall, position, width);
            FloorPlan.Windows.Add(window);
            Select(window);
        }
    }

    private void PlaceRoomPoint(Point point)
    {
        if (_roomStart is null)
        {
            _roomStart = point;
            _previewEnd = point;
            return;
        }
        var bounds = CreateWorldRect(_roomStart.Value, point);
        if (FloorPlan is not null && bounds.Width >= 20 && bounds.Height >= 20)
        {
            var room = new RoomArea(bounds, $"방 {FloorPlan.RoomAreas.Count + 1}");
            FloorPlan.RoomAreas.Add(room);
            Select(room);
        }
        _roomStart = null;
    }

    private void PlaceDimensionPoint(Point point)
    {
        if (_dimensionStart is null)
        {
            _dimensionStart = point;
            _previewEnd = point;
            return;
        }
        if (FloorPlan is not null && (point - _dimensionStart.Value).Length >= 10)
        {
            var dimension = new DimensionLine(_dimensionStart.Value, point);
            FloorPlan.Dimensions.Add(dimension);
            Select(dimension);
        }
        _dimensionStart = null;
    }

    private void BeginElementDrag(PlanElement? element, Point world)
    {
        Select(element);
        if (element is null)
        {
            return;
        }
        _isDragging = true;
        _dragStartWorld = world;
        switch (element)
        {
            case Wall wall:
                _dragWallStart = wall.StartPoint;
                _dragWallEnd = wall.EndPoint;
                break;
            case DimensionLine dimension:
                _dragWallStart = dimension.StartPoint;
                _dragWallEnd = dimension.EndPoint;
                break;
            case Furniture furniture:
                _dragElementOrigin = furniture.Location;
                break;
            case RoomLabel label:
                _dragElementOrigin = label.Location;
                break;
            case RoomArea room:
                _dragRoomBounds = room.Bounds;
                break;
            case SiteElement siteElement:
                _dragElementOrigin = siteElement.Location;
                break;
        }
        CaptureMouse();
        Cursor = Cursors.SizeAll;
    }

    private void BeginElementResize(ResizeHandle handle, Point screen)
    {
        if (SelectedElement is null)
        {
            return;
        }
        _activeResizeHandle = handle;
        _resizeStartScreen = screen;
        switch (SelectedElement)
        {
            case WallOpening opening:
                var halfPosition = opening.ParentWall.Length < .001
                    ? 0
                    : opening.Width / (2 * opening.ParentWall.Length);
                _resizeOpeningFixedPosition = handle == ResizeHandle.OpeningStart
                    ? opening.Position + halfPosition
                    : opening.Position - halfPosition;
                break;
            case Furniture furniture:
                var corners = GetFurnitureWorldCorners(furniture);
                _resizeFurnitureOpposite = handle switch
                {
                    ResizeHandle.FurnitureTopLeft => corners[3],
                    ResizeHandle.FurnitureTopRight => corners[2],
                    ResizeHandle.FurnitureBottomLeft => corners[1],
                    _ => corners[0]
                };
                break;
            case SiteElement siteElement:
                var siteCorners = GetSiteElementWorldCorners(siteElement);
                _resizeFurnitureOpposite = handle switch
                {
                    ResizeHandle.SiteTopLeft => siteCorners[3],
                    ResizeHandle.SiteTopRight => siteCorners[2],
                    ResizeHandle.SiteBottomLeft => siteCorners[1],
                    _ => siteCorners[0]
                };
                break;
            case RoomArea room:
                _resizeFurnitureOpposite = handle switch
                {
                    ResizeHandle.RoomTopLeft => room.Bounds.BottomRight,
                    ResizeHandle.RoomTopRight => room.Bounds.BottomLeft,
                    ResizeHandle.RoomBottomLeft => room.Bounds.TopRight,
                    _ => room.Bounds.TopLeft
                };
                break;
            case RoomLabel label:
                _resizeOriginalFontSize = label.FontSize;
                break;
        }
        CaptureMouse();
        Cursor = Cursors.SizeNWSE;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging && _activeResizeHandle == ResizeHandle.None)
        {
            return;
        }
        _isDragging = false;
        _activeResizeHandle = ResizeHandle.None;
        ReleaseMouseCapture();
        Cursor = ActiveTool == EditorTool.Wall ? Cursors.Cross : Cursors.Arrow;
        e.Handled = true;
    }

    private void BeginPan(object sender, MouseButtonEventArgs e)
    {
        _isPanning = true;
        _panStart = e.GetPosition(this);
        _panOrigin = _panOffset;
        CaptureMouse();
        Cursor = Cursors.Hand;
        e.Handled = true;
    }

    private void EndPan(object sender, MouseButtonEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }
        _isPanning = false;
        ReleaseMouseCapture();
        Cursor = ActiveTool == EditorTool.Wall ? Cursors.Cross : Cursors.Arrow;
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var screen = e.GetPosition(this);
        if (_isPanning)
        {
            _panOffset = _panOrigin + (screen - _panStart);
            InvalidateVisual();
            return;
        }
        var world = ScreenToWorld(screen);
        if (_activeResizeHandle != ResizeHandle.None && SelectedElement is not null)
        {
            ResizeSelectedElement(screen, world);
            InvalidateVisual();
            return;
        }
        if (_isDragging && SelectedElement is not null)
        {
            MoveSelectedElement(world);
            InvalidateVisual();
            return;
        }
        if (_wallStart is not null || _roomStart is not null || _dimensionStart is not null)
        {
            _previewEnd = SnapPoint(world);
            InvalidateVisual();
        }
    }

    private void MoveSelectedElement(Point pointerWorld)
    {
        var delta = pointerWorld - _dragStartWorld;
        switch (SelectedElement)
        {
            case Wall wall:
                var snappedStart = SnapPoint(_dragWallStart + delta);
                var snappedDelta = snappedStart - _dragWallStart;
                wall.StartPoint = _dragWallStart + snappedDelta;
                wall.EndPoint = _dragWallEnd + snappedDelta;
                break;
            case DimensionLine dimension:
                var dimensionStart = SnapPoint(_dragWallStart + delta);
                var dimensionDelta = dimensionStart - _dragWallStart;
                dimension.StartPoint = _dragWallStart + dimensionDelta;
                dimension.EndPoint = _dragWallEnd + dimensionDelta;
                break;
            case WallOpening opening:
                opening.Position = ClampOpeningPosition(opening.ParentWall,
                    ProjectToWall(opening.ParentWall, pointerWorld), opening.Width);
                break;
            case Furniture furniture:
                furniture.Location = SnapPoint(_dragElementOrigin + delta);
                break;
            case RoomLabel label:
                label.Location = SnapPoint(_dragElementOrigin + delta);
                break;
            case RoomArea room:
                var roomOrigin = SnapPoint(new Point(_dragRoomBounds.X + delta.X, _dragRoomBounds.Y + delta.Y));
                room.Bounds = new Rect(roomOrigin.X, roomOrigin.Y, _dragRoomBounds.Width, _dragRoomBounds.Height);
                break;
            case SiteElement siteElement:
                siteElement.Location = SnapPoint(_dragElementOrigin + delta);
                break;
        }
    }

    private void ResizeSelectedElement(Point pointerScreen, Point pointerWorld)
    {
        switch (SelectedElement)
        {
            case Wall wall when _activeResizeHandle == ResizeHandle.WallStart:
                var newStart = SnapPoint(pointerWorld);
                if ((wall.EndPoint - newStart).Length >= 10)
                {
                    wall.StartPoint = newStart;
                    ConstrainOpeningsToWall(wall);
                }
                break;
            case Wall wall when _activeResizeHandle == ResizeHandle.WallEnd:
                var newEnd = SnapPoint(pointerWorld);
                if ((newEnd - wall.StartPoint).Length >= 10)
                {
                    wall.EndPoint = newEnd;
                    ConstrainOpeningsToWall(wall);
                }
                break;
            case DimensionLine dimension when _activeResizeHandle == ResizeHandle.DimensionStart:
                var dimensionStart = SnapPoint(pointerWorld);
                if ((dimension.EndPoint - dimensionStart).Length >= 10)
                {
                    dimension.StartPoint = dimensionStart;
                }
                break;
            case DimensionLine dimension when _activeResizeHandle == ResizeHandle.DimensionEnd:
                var dimensionEnd = SnapPoint(pointerWorld);
                if ((dimensionEnd - dimension.StartPoint).Length >= 10)
                {
                    dimension.EndPoint = dimensionEnd;
                }
                break;
            case WallOpening opening:
                ResizeOpening(opening, pointerWorld);
                break;
            case Furniture furniture:
                var corner = SnapPoint(pointerWorld);
                var radians = furniture.RotationDegrees * Math.PI / 180;
                var xAxis = new Vector(Math.Cos(radians), Math.Sin(radians));
                var yAxis = new Vector(-Math.Sin(radians), Math.Cos(radians));
                var resizeVector = corner - _resizeFurnitureOpposite;
                var width = Math.Max(20, Math.Abs(Vector.Multiply(resizeVector, xAxis)));
                var height = Math.Max(20, Math.Abs(Vector.Multiply(resizeVector, yAxis)));
                var xSign = _activeResizeHandle is ResizeHandle.FurnitureTopLeft or ResizeHandle.FurnitureBottomLeft ? -1 : 1;
                var ySign = _activeResizeHandle is ResizeHandle.FurnitureTopLeft or ResizeHandle.FurnitureTopRight ? -1 : 1;
                var adjustedCorner = _resizeFurnitureOpposite + xAxis * (width * xSign) + yAxis * (height * ySign);
                furniture.Location = new Point(
                    (_resizeFurnitureOpposite.X + adjustedCorner.X) / 2,
                    (_resizeFurnitureOpposite.Y + adjustedCorner.Y) / 2);
                furniture.Width = width;
                furniture.Height = height;
                break;
            case SiteElement siteElement:
                var siteCorner = SnapPoint(pointerWorld);
                var siteRadians = siteElement.RotationDegrees * Math.PI / 180;
                var siteXAxis = new Vector(Math.Cos(siteRadians), Math.Sin(siteRadians));
                var siteYAxis = new Vector(-Math.Sin(siteRadians), Math.Cos(siteRadians));
                var siteResizeVector = siteCorner - _resizeFurnitureOpposite;
                var siteWidth = Math.Max(20, Math.Abs(Vector.Multiply(siteResizeVector, siteXAxis)));
                var siteHeight = Math.Max(20, Math.Abs(Vector.Multiply(siteResizeVector, siteYAxis)));
                var siteXSign = _activeResizeHandle is ResizeHandle.SiteTopLeft or ResizeHandle.SiteBottomLeft ? -1 : 1;
                var siteYSign = _activeResizeHandle is ResizeHandle.SiteTopLeft or ResizeHandle.SiteTopRight ? -1 : 1;
                var adjustedSiteCorner = _resizeFurnitureOpposite
                                         + siteXAxis * (siteWidth * siteXSign)
                                         + siteYAxis * (siteHeight * siteYSign);
                siteElement.Location = new Point(
                    (_resizeFurnitureOpposite.X + adjustedSiteCorner.X) / 2,
                    (_resizeFurnitureOpposite.Y + adjustedSiteCorner.Y) / 2);
                siteElement.Width = siteWidth;
                siteElement.Height = siteHeight;
                break;
            case RoomLabel label:
                var delta = ((pointerScreen.X - _resizeStartScreen.X) + (pointerScreen.Y - _resizeStartScreen.Y)) / (2 * _zoom);
                label.FontSize = _resizeOriginalFontSize + delta;
                break;
            case RoomArea room:
                var roomCorner = SnapPoint(pointerWorld);
                var roomBounds = CreateWorldRect(_resizeFurnitureOpposite, roomCorner);
                if (roomBounds.Width >= 20 && roomBounds.Height >= 20)
                {
                    room.Bounds = roomBounds;
                }
                break;
        }
    }

    private void ResizeOpening(WallOpening opening, Point pointerWorld)
    {
        var wallLength = opening.ParentWall.Length;
        if (wallLength < 10)
        {
            return;
        }
        var projected = ProjectToWall(opening.ParentWall, pointerWorld);
        var minimumPositionWidth = Math.Min(.5, 20 / wallLength);
        projected = _activeResizeHandle == ResizeHandle.OpeningStart
            ? Math.Clamp(projected, 0, Math.Max(0, _resizeOpeningFixedPosition - minimumPositionWidth))
            : Math.Clamp(projected, Math.Min(1, _resizeOpeningFixedPosition + minimumPositionWidth), 1);
        opening.Position = (projected + _resizeOpeningFixedPosition) / 2;
        opening.Width = Math.Abs(projected - _resizeOpeningFixedPosition) * wallLength;
    }

    private void ConstrainOpeningsToWall(Wall wall)
    {
        if (FloorPlan is null)
        {
            return;
        }
        foreach (var opening in FloorPlan.Doors.Cast<WallOpening>().Concat(FloorPlan.Windows)
                     .Where(opening => ReferenceEquals(opening.ParentWall, wall)))
        {
            opening.Width = Math.Min(opening.Width, Math.Max(10, wall.Length));
            opening.Position = ClampOpeningPosition(wall, opening.Position, opening.Width);
        }
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var mouse = e.GetPosition(this);
        var worldAtMouse = ScreenToWorld(mouse);
        var factor = e.Delta > 0 ? 1.15 : 1 / 1.15;
        _zoom = Math.Clamp(_zoom * factor, MinimumZoom, MaximumZoom);
        _panOffset = new Point(mouse.X - worldAtMouse.X * _zoom, mouse.Y - worldAtMouse.Y * _zoom);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            _wallStart = null;
            _roomStart = null;
            _dimensionStart = null;
            if (ActiveTool == EditorTool.Select)
            {
                Select(null);
            }
            InvalidateVisual();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && SelectedElement is not null && FloorPlan is not null)
        {
            FloorPlan.Remove(SelectedElement);
            Select(null);
            InvalidateVisual();
            e.Handled = true;
        }
    }

    private PlanElement? HitTestElement(Point screen)
    {
        if (FloorPlan is null)
        {
            return null;
        }
        for (var i = FloorPlan.RoomLabels.Count - 1; i >= 0; i--)
        {
            var label = FloorPlan.RoomLabels[i];
            var labelCenter = WorldToScreen(label.Location);
            var localPoint = RotatePoint(screen, labelCenter, -label.RotationDegrees);
            if (GetRoomLabelRect(label).Contains(localPoint))
            {
                return label;
            }
        }
        for (var i = FloorPlan.Windows.Count - 1; i >= 0; i--)
        {
            var window = FloorPlan.Windows[i];
            if ((GetOpeningScreenGeometry(window).Center - screen).Length <= Math.Max(12, window.Width * _zoom / 2))
            {
                return window;
            }
        }
        for (var i = FloorPlan.Doors.Count - 1; i >= 0; i--)
        {
            var door = FloorPlan.Doors[i];
            if ((GetOpeningScreenGeometry(door).Center - screen).Length <= Math.Max(12, door.Width * _zoom / 2))
            {
                return door;
            }
        }
        for (var i = FloorPlan.Dimensions.Count - 1; i >= 0; i--)
        {
            var dimension = FloorPlan.Dimensions[i];
            if (DistanceToSegment(screen, WorldToScreen(dimension.StartPoint), WorldToScreen(dimension.EndPoint)) <= 8)
            {
                return dimension;
            }
        }
        var wall = HitTestWall(screen);
        if (wall is not null)
        {
            return wall;
        }
        for (var i = FloorPlan.FurnitureItems.Count - 1; i >= 0; i--)
        {
            var furniture = FloorPlan.FurnitureItems[i];
            var center = WorldToScreen(furniture.Location);
            var localPoint = RotatePoint(screen, center, -furniture.RotationDegrees);
            if (Math.Abs(localPoint.X - center.X) <= furniture.Width * _zoom / 2 + 5
                && Math.Abs(localPoint.Y - center.Y) <= furniture.Height * _zoom / 2 + 5)
            {
                return furniture;
            }
        }
        for (var i = FloorPlan.RoomAreas.Count - 1; i >= 0; i--)
        {
            if (WorldRectToScreen(FloorPlan.RoomAreas[i].Bounds).Contains(screen))
            {
                return FloorPlan.RoomAreas[i];
            }
        }
        for (var i = FloorPlan.SiteElements.Count - 1; i >= 0; i--)
        {
            var siteElement = FloorPlan.SiteElements[i];
            var center = WorldToScreen(siteElement.Location);
            var localPoint = RotatePoint(screen, center, -siteElement.RotationDegrees);
            if (Math.Abs(localPoint.X - center.X) <= siteElement.Width * _zoom / 2 + 5
                && Math.Abs(localPoint.Y - center.Y) <= siteElement.Height * _zoom / 2 + 5)
            {
                return siteElement;
            }
        }
        return null;
    }

    private ResizeHandle HitTestResizeHandle(Point screen)
    {
        const double tolerance = 10;
        switch (SelectedElement)
        {
            case Wall wall:
                if ((WorldToScreen(wall.StartPoint) - screen).Length <= tolerance)
                {
                    return ResizeHandle.WallStart;
                }
                if ((WorldToScreen(wall.EndPoint) - screen).Length <= tolerance)
                {
                    return ResizeHandle.WallEnd;
                }
                break;
            case DimensionLine dimension:
                if ((WorldToScreen(dimension.StartPoint) - screen).Length <= tolerance)
                {
                    return ResizeHandle.DimensionStart;
                }
                if ((WorldToScreen(dimension.EndPoint) - screen).Length <= tolerance)
                {
                    return ResizeHandle.DimensionEnd;
                }
                break;
            case WallOpening opening:
                var (center, tangent, _) = GetOpeningScreenGeometry(opening);
                var half = tangent * (opening.Width * _zoom / 2);
                if ((center - half - screen).Length <= tolerance)
                {
                    return ResizeHandle.OpeningStart;
                }
                if ((center + half - screen).Length <= tolerance)
                {
                    return ResizeHandle.OpeningEnd;
                }
                break;
            case Furniture furniture:
                var corners = GetFurnitureCorners(furniture);
                var handles = new[]
                {
                    ResizeHandle.FurnitureTopLeft, ResizeHandle.FurnitureTopRight,
                    ResizeHandle.FurnitureBottomLeft, ResizeHandle.FurnitureBottomRight
                };
                for (var i = 0; i < corners.Length; i++)
                {
                    if ((corners[i] - screen).Length <= tolerance)
                    {
                        return handles[i];
                    }
                }
                break;
            case RoomLabel label:
                var labelCenter = WorldToScreen(label.Location);
                var labelHandle = RotatePoint(GetRoomLabelRect(label).BottomRight, labelCenter, label.RotationDegrees);
                if ((labelHandle - screen).Length <= tolerance)
                {
                    return ResizeHandle.LabelScale;
                }
                break;
            case RoomArea room:
                var roomCorners = GetRoomCorners(room);
                var roomHandles = new[]
                {
                    ResizeHandle.RoomTopLeft, ResizeHandle.RoomTopRight,
                    ResizeHandle.RoomBottomLeft, ResizeHandle.RoomBottomRight
                };
                for (var i = 0; i < roomCorners.Length; i++)
                {
                    if ((roomCorners[i] - screen).Length <= tolerance)
                    {
                        return roomHandles[i];
                    }
                }
                break;
            case SiteElement siteElement:
                var siteCorners = GetSiteElementCorners(siteElement);
                var siteHandles = new[]
                {
                    ResizeHandle.SiteTopLeft, ResizeHandle.SiteTopRight,
                    ResizeHandle.SiteBottomLeft, ResizeHandle.SiteBottomRight
                };
                for (var i = 0; i < siteCorners.Length; i++)
                {
                    if ((siteCorners[i] - screen).Length <= tolerance)
                    {
                        return siteHandles[i];
                    }
                }
                break;
        }
        return ResizeHandle.None;
    }

    private Wall? HitTestWall(Point screen)
    {
        if (FloorPlan is null)
        {
            return null;
        }
        for (var i = FloorPlan.Walls.Count - 1; i >= 0; i--)
        {
            var wall = FloorPlan.Walls[i];
            var distance = DistanceToSegment(screen, WorldToScreen(wall.StartPoint), WorldToScreen(wall.EndPoint));
            if (distance <= Math.Max(8, wall.Thickness * _zoom / 2 + 5))
            {
                return wall;
            }
        }
        return null;
    }

    private Point[] GetFurnitureCorners(Furniture furniture)
    {
        var center = WorldToScreen(furniture.Location);
        var halfWidth = furniture.Width * _zoom / 2;
        var halfHeight = furniture.Height * _zoom / 2;
        return
        [
            RotatePoint(new Point(center.X - halfWidth, center.Y - halfHeight), center, furniture.RotationDegrees),
            RotatePoint(new Point(center.X + halfWidth, center.Y - halfHeight), center, furniture.RotationDegrees),
            RotatePoint(new Point(center.X - halfWidth, center.Y + halfHeight), center, furniture.RotationDegrees),
            RotatePoint(new Point(center.X + halfWidth, center.Y + halfHeight), center, furniture.RotationDegrees)
        ];
    }

    private static Point[] GetFurnitureWorldCorners(Furniture furniture)
    {
        var center = furniture.Location;
        var halfWidth = furniture.Width / 2;
        var halfHeight = furniture.Height / 2;
        return
        [
            RotatePoint(new Point(center.X - halfWidth, center.Y - halfHeight), center, furniture.RotationDegrees),
            RotatePoint(new Point(center.X + halfWidth, center.Y - halfHeight), center, furniture.RotationDegrees),
            RotatePoint(new Point(center.X - halfWidth, center.Y + halfHeight), center, furniture.RotationDegrees),
            RotatePoint(new Point(center.X + halfWidth, center.Y + halfHeight), center, furniture.RotationDegrees)
        ];
    }

    private Point[] GetSiteElementCorners(SiteElement element)
    {
        var center = WorldToScreen(element.Location);
        var halfWidth = element.Width * _zoom / 2;
        var halfHeight = element.Height * _zoom / 2;
        return
        [
            RotatePoint(new Point(center.X - halfWidth, center.Y - halfHeight), center, element.RotationDegrees),
            RotatePoint(new Point(center.X + halfWidth, center.Y - halfHeight), center, element.RotationDegrees),
            RotatePoint(new Point(center.X - halfWidth, center.Y + halfHeight), center, element.RotationDegrees),
            RotatePoint(new Point(center.X + halfWidth, center.Y + halfHeight), center, element.RotationDegrees)
        ];
    }

    private static Point[] GetSiteElementWorldCorners(SiteElement element)
    {
        var center = element.Location;
        var halfWidth = element.Width / 2;
        var halfHeight = element.Height / 2;
        return
        [
            RotatePoint(new Point(center.X - halfWidth, center.Y - halfHeight), center, element.RotationDegrees),
            RotatePoint(new Point(center.X + halfWidth, center.Y - halfHeight), center, element.RotationDegrees),
            RotatePoint(new Point(center.X - halfWidth, center.Y + halfHeight), center, element.RotationDegrees),
            RotatePoint(new Point(center.X + halfWidth, center.Y + halfHeight), center, element.RotationDegrees)
        ];
    }

    private static Point RotatePoint(Point point, Point center, double degrees)
    {
        if (Math.Abs(degrees) < .001)
        {
            return point;
        }
        var radians = degrees * Math.PI / 180;
        var x = point.X - center.X;
        var y = point.Y - center.Y;
        return new Point(center.X + x * Math.Cos(radians) - y * Math.Sin(radians),
            center.Y + x * Math.Sin(radians) + y * Math.Cos(radians));
    }

    private Point[] GetRoomCorners(RoomArea room)
    {
        var rect = WorldRectToScreen(room.Bounds);
        return [rect.TopLeft, rect.TopRight, rect.BottomLeft, rect.BottomRight];
    }

    private Rect WorldRectToScreen(Rect worldRect)
    {
        var topLeft = WorldToScreen(worldRect.TopLeft);
        return new Rect(topLeft.X, topLeft.Y, worldRect.Width * _zoom, worldRect.Height * _zoom);
    }

    private static Rect CreateWorldRect(Point first, Point second) => new(
        Math.Min(first.X, second.X), Math.Min(first.Y, second.Y),
        Math.Abs(second.X - first.X), Math.Abs(second.Y - first.Y));

    private Rect GetRoomLabelRect(RoomLabel label)
    {
        var text = CreateText(label.Text, Math.Max(8, label.FontSize * _zoom), WallBrush, FontWeights.SemiBold);
        return GetRoomLabelRect(label, text);
    }

    private Rect GetRoomLabelRect(RoomLabel label, FormattedText text)
    {
        var center = WorldToScreen(label.Location);
        return new Rect(center.X - text.Width / 2 - 7, center.Y - text.Height / 2 - 4, text.Width + 14, text.Height + 8);
    }

    private (Point Center, Vector Tangent, Vector Normal) GetOpeningScreenGeometry(WallOpening opening)
    {
        var start = WorldToScreen(opening.ParentWall.StartPoint);
        var end = WorldToScreen(opening.ParentWall.EndPoint);
        var tangent = end - start;
        if (tangent.Length < .001)
        {
            tangent = new Vector(1, 0);
        }
        else
        {
            tangent.Normalize();
        }
        var center = start + (end - start) * opening.Position;
        return (center, tangent, new Vector(-tangent.Y, tangent.X));
    }

    private static double ProjectToWall(Wall wall, Point point)
    {
        var direction = wall.EndPoint - wall.StartPoint;
        var lengthSquared = direction.LengthSquared;
        return lengthSquared < .001 ? 0 : Math.Clamp(Vector.Multiply(point - wall.StartPoint, direction) / lengthSquared, 0, 1);
    }

    private static double ClampOpeningPosition(Wall wall, double position, double width)
    {
        var length = wall.Length;
        if (length <= width || length < .001)
        {
            return .5;
        }
        var margin = width / (2 * length);
        return Math.Clamp(position, margin, 1 - margin);
    }

    private static double DistanceToSegment(Point point, Point start, Point end)
    {
        var segment = end - start;
        if (segment.LengthSquared < .001)
        {
            return (point - start).Length;
        }
        var t = Math.Clamp(Vector.Multiply(point - start, segment) / segment.LengthSquared, 0, 1);
        return (point - (start + segment * t)).Length;
    }

    private Point SnapPoint(Point point)
    {
        if (!IsGridSnapEnabled || GridSize <= 0)
        {
            return point;
        }
        return new Point(Math.Round(point.X / GridSize) * GridSize, Math.Round(point.Y / GridSize) * GridSize);
    }

    private void Select(PlanElement? element) => SetCurrentValue(SelectedElementProperty, element);

    private Point WorldToScreen(Point point) => new(point.X * _zoom + _panOffset.X, point.Y * _zoom + _panOffset.Y);

    private Point ScreenToWorld(Point point) => new((point.X - _panOffset.X) / _zoom, (point.Y - _panOffset.Y) / _zoom);

    private FormattedText CreateText(string value, double size, Brush brush, FontWeight? weight = null) =>
        new(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal),
            size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private void DrawCenteredText(DrawingContext context, string value, Point center, double size, Brush brush)
    {
        var text = CreateText(value, size, brush, FontWeights.SemiBold);
        context.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
    }

    private static string GetFurnitureName(FurnitureKind kind) => kind switch
    {
        FurnitureKind.Bed => "침대",
        FurnitureKind.Sofa => "소파",
        FurnitureKind.Table => "테이블",
        FurnitureKind.Sink => "싱크대",
        FurnitureKind.Toilet => "변기",
        FurnitureKind.DiningTable => "식탁",
        FurnitureKind.Refrigerator => "냉장고",
        FurnitureKind.Stove => "레인지",
        FurnitureKind.Wardrobe => "옷장",
        FurnitureKind.Bathtub => "욕조",
        FurnitureKind.Shower => "샤워부스",
        FurnitureKind.Stairs => "계단",
        FurnitureKind.Washer => "세탁기",
        FurnitureKind.Washbasin => "세면대",
        _ => kind.ToString()
    };

    private static string GetSiteElementName(SiteElementKind kind) => kind switch
    {
        SiteElementKind.Road => "도로",
        SiteElementKind.Tree => "나무",
        SiteElementKind.Bench => "벤치",
        _ => kind.ToString()
    };

    private static void OnToolChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (FloorPlanCanvas)d;
        canvas._wallStart = null;
        canvas._roomStart = null;
        canvas._dimensionStart = null;
        canvas._isDragging = false;
        canvas._activeResizeHandle = ResizeHandle.None;
        canvas.Cursor = (EditorTool)e.NewValue == EditorTool.Wall ? Cursors.Cross : Cursors.Arrow;
        canvas.InvalidateVisual();
    }

    private static void OnFloorPlanChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (FloorPlanCanvas)d;
        if (e.OldValue is FloorPlan oldPlan)
        {
            canvas.SubscribeToPlan(oldPlan, false);
        }
        if (e.NewValue is FloorPlan newPlan)
        {
            canvas.SubscribeToPlan(newPlan, true);
        }
        canvas.InvalidateVisual();
    }

    private void SubscribeToPlan(FloorPlan plan, bool subscribe)
    {
        var collections = new INotifyCollectionChanged[]
        {
            plan.Walls, plan.Doors, plan.Windows, plan.FurnitureItems, plan.RoomLabels,
            plan.RoomAreas, plan.Dimensions, plan.SiteElements
        };
        foreach (var collection in collections)
        {
            if (subscribe)
            {
                collection.CollectionChanged += OnPlanCollectionChanged;
            }
            else
            {
                collection.CollectionChanged -= OnPlanCollectionChanged;
            }
        }
        foreach (var element in plan.Walls.Cast<PlanElement>().Concat(plan.Doors).Concat(plan.Windows)
                     .Concat(plan.FurnitureItems).Concat(plan.RoomLabels).Concat(plan.RoomAreas).Concat(plan.Dimensions)
                     .Concat(plan.SiteElements))
        {
            if (subscribe)
            {
                element.PropertyChanged += OnElementPropertyChanged;
            }
            else
            {
                element.PropertyChanged -= OnElementPropertyChanged;
            }
        }
    }

    private void OnPlanCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (PlanElement element in e.OldItems)
            {
                element.PropertyChanged -= OnElementPropertyChanged;
            }
        }
        if (e.NewItems is not null)
        {
            foreach (PlanElement element in e.NewItems)
            {
                element.PropertyChanged += OnElementPropertyChanged;
            }
        }
        InvalidateVisual();
    }

    private void OnElementPropertyChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();
}
