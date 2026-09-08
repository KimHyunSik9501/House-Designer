using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HouseDesigner.Models;
using HouseDesigner.Services;
using Microsoft.Win32;

namespace HouseDesigner.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private const string DefaultDesignFolder = "Design";
    private EditorTool _activeTool = EditorTool.Wall;
    private bool _isGridSnapEnabled = true;
    private double _gridSize = 10;
    private FurnitureKind _selectedFurnitureKind = FurnitureKind.Bed;
    private SiteElementKind _selectedSiteElementKind = SiteElementKind.Road;
    private PlanElement? _selectedElement;
    private string? _currentFilePath;
    private string _statusMessage = "새 도면 · 저장되지 않음";
    private bool _isInternalOperation;
    private readonly HashSet<RoomArea> _trackedRoomAreas = [];

    public MainViewModel()
    {
        SelectToolCommand = new RelayCommand(parameter =>
        {
            if (parameter is string value && Enum.TryParse<EditorTool>(value, out var tool))
            {
                ActiveTool = tool;
            }
        });

        ClearCommand = new RelayCommand(_ =>
        {
            SelectedElement = null;
            FloorPlan.Clear();
        }, _ => ElementCount > 0);
        DeleteSelectedCommand = new RelayCommand(_ =>
        {
            if (SelectedElement is not null)
            {
                FloorPlan.Remove(SelectedElement);
                SelectedElement = null;
            }
        }, _ => SelectedElement is not null);
        SaveCommand = new RelayCommand(_ => SaveDocument(false));
        SaveAsCommand = new RelayCommand(_ => SaveDocument(true));
        LoadCommand = new RelayCommand(_ => LoadDocument());
        ExportCsvCommand = new RelayCommand(_ => ExportCsv(), _ => ElementCount > 0);
        ExportSvgCommand = new RelayCommand(_ => ExportSvg(), _ => ElementCount > 0);
        PlaceStairsCommand = new RelayCommand(_ =>
        {
            SelectedFurnitureKind = FurnitureKind.Stairs;
            ActiveTool = EditorTool.Furniture;
            StatusMessage = "계단 배치 모드 · 캔버스를 클릭하세요";
        });
        RotateLeftCommand = new RelayCommand(_ => RotateSelected(-1), _ => CanRotateSelected);
        RotateRightCommand = new RelayCommand(_ => RotateSelected(1), _ => CanRotateSelected);
        PlaceSiteElementCommand = new RelayCommand(parameter =>
        {
            if (parameter is string value && Enum.TryParse<SiteElementKind>(value, out var kind))
            {
                SelectedSiteElementKind = kind;
                ActiveTool = EditorTool.SiteElement;
                StatusMessage = $"{GetSiteElementName(kind)} 배치 모드 · 캔버스를 클릭하세요";
            }
        });

        FloorPlan.Walls.CollectionChanged += (_, _) => OnPlanCollectionChanged();
        FloorPlan.Doors.CollectionChanged += (_, _) => OnPlanCollectionChanged();
        FloorPlan.Windows.CollectionChanged += (_, _) => OnPlanCollectionChanged();
        FloorPlan.FurnitureItems.CollectionChanged += (_, _) => OnPlanCollectionChanged();
        FloorPlan.RoomLabels.CollectionChanged += (_, _) => OnPlanCollectionChanged();
        FloorPlan.RoomAreas.CollectionChanged += OnRoomAreasChanged;
        FloorPlan.Dimensions.CollectionChanged += (_, _) => OnPlanCollectionChanged();
        FloorPlan.SiteElements.CollectionChanged += (_, _) => OnPlanCollectionChanged();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public FloorPlan FloorPlan { get; } = new();

    public ICommand SelectToolCommand { get; }

    public ICommand ClearCommand { get; }

    public ICommand DeleteSelectedCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand SaveAsCommand { get; }

    public ICommand LoadCommand { get; }

    public ICommand ExportCsvCommand { get; }

    public ICommand ExportSvgCommand { get; }

    public ICommand PlaceStairsCommand { get; }

    public ICommand RotateLeftCommand { get; }

    public ICommand RotateRightCommand { get; }

    public ICommand PlaceSiteElementCommand { get; }

    public EditorTool ActiveTool
    {
        get => _activeTool;
        set
        {
            if (SetField(ref _activeTool, value))
            {
                OnPropertyChanged(nameof(ActiveToolName));
            }
        }
    }

    public string ActiveToolName => ActiveTool switch
    {
        EditorTool.Select => "선택 및 이동",
        EditorTool.Wall => "벽 그리기",
        EditorTool.Door => "문 배치",
        EditorTool.Window => "창문 배치",
        EditorTool.RoomArea => "방 영역",
        EditorTool.RoomLabel => "텍스트",
        EditorTool.Furniture => "가구 및 설비",
        EditorTool.Dimension => "치수선",
        EditorTool.SiteElement => "대지 요소",
        _ => ActiveTool.ToString()
    };

    public SiteElementKind SelectedSiteElementKind
    {
        get => _selectedSiteElementKind;
        set => SetField(ref _selectedSiteElementKind, value);
    }

    public string DocumentTitle => _currentFilePath is null ? "새 도면" : Path.GetFileName(_currentFilePath);

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public bool IsGridSnapEnabled
    {
        get => _isGridSnapEnabled;
        set => SetField(ref _isGridSnapEnabled, value);
    }

    public double GridSize
    {
        get => _gridSize;
        set
        {
            if (value > 0)
            {
                SetField(ref _gridSize, value);
            }
        }
    }

    public int WallCount => FloorPlan.Walls.Count;

    public int ElementCount => FloorPlan.Walls.Count + FloorPlan.Doors.Count + FloorPlan.Windows.Count
                               + FloorPlan.FurnitureItems.Count + FloorPlan.RoomLabels.Count
                               + FloorPlan.RoomAreas.Count + FloorPlan.Dimensions.Count + FloorPlan.SiteElements.Count;

    public string ElementSummary =>
        $"방 {FloorPlan.RoomAreas.Count}  벽 {FloorPlan.Walls.Count}  문 {FloorPlan.Doors.Count}  창 {FloorPlan.Windows.Count}\n가구·설비 {FloorPlan.FurnitureItems.Count}  대지 {FloorPlan.SiteElements.Count}  치수 {FloorPlan.Dimensions.Count}";

    public IReadOnlyList<FurnitureChoice> FurnitureChoices { get; } =
    [
        new(FurnitureKind.Bed, "침대"),
        new(FurnitureKind.Sofa, "소파"),
        new(FurnitureKind.Table, "테이블"),
        new(FurnitureKind.Sink, "싱크대"),
        new(FurnitureKind.Toilet, "변기"),
        new(FurnitureKind.DiningTable, "식탁"),
        new(FurnitureKind.Refrigerator, "냉장고"),
        new(FurnitureKind.Stove, "레인지"),
        new(FurnitureKind.Wardrobe, "옷장"),
        new(FurnitureKind.Bathtub, "욕조"),
        new(FurnitureKind.Shower, "샤워부스"),
        new(FurnitureKind.Stairs, "계단"),
        new(FurnitureKind.Washer, "세탁기"),
        new(FurnitureKind.Washbasin, "세면대")
    ];

    public FurnitureKind SelectedFurnitureKind
    {
        get => _selectedFurnitureKind;
        set => SetField(ref _selectedFurnitureKind, value);
    }

    public PlanElement? SelectedElement
    {
        get => _selectedElement;
        set
        {
            if (ReferenceEquals(_selectedElement, value))
            {
                return;
            }

            if (_selectedElement is not null)
            {
                _selectedElement.PropertyChanged -= OnSelectedElementChanged;
            }
            _selectedElement = value;
            if (_selectedElement is not null)
            {
                _selectedElement.PropertyChanged += OnSelectedElementChanged;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedElementName));
            OnPropertyChanged(nameof(SelectedElementDetails));
            OnPropertyChanged(nameof(SelectedRoomName));
            OnPropertyChanged(nameof(CanEditRoomName));
            OnPropertyChanged(nameof(SelectedRoomLevel));
            OnPropertyChanged(nameof(CanEditRoomLevel));
            NotifySizePropertiesChanged();
            ((RelayCommand)DeleteSelectedCommand).RaiseCanExecuteChanged();
            ((RelayCommand)RotateLeftCommand).RaiseCanExecuteChanged();
            ((RelayCommand)RotateRightCommand).RaiseCanExecuteChanged();
        }
    }

    public string SelectedRoomName
    {
        get => SelectedElement switch
        {
            RoomLabel label => label.Text,
            RoomArea room => room.Name,
            _ => string.Empty
        };
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }
            if (SelectedElement is RoomLabel label)
            {
                label.Text = value;
                OnPropertyChanged();
            }
            else if (SelectedElement is RoomArea room)
            {
                room.Name = value;
                OnPropertyChanged();
            }
        }
    }

    public bool CanEditRoomName => SelectedElement is RoomLabel or RoomArea;

    public int? SelectedRoomLevel
    {
        get => SelectedElement is RoomArea room ? room.Level : null;
        set
        {
            if (SelectedElement is RoomArea room && value is >= 1)
            {
                room.Level = value.Value;
            }
        }
    }

    public bool CanEditRoomLevel => SelectedElement is RoomArea;

    public bool CanRotateSelected => SelectedElement is Furniture or RoomLabel or WallOpening or SiteElement;

    public double TotalRoomAreaSquareMeters => FloorPlan.RoomAreas.Sum(room => room.AreaSquareMeters);

    public string TotalRoomAreaText =>
        $"{TotalRoomAreaSquareMeters:0.##} m² (약 {Math.Round(TotalRoomAreaSquareMeters / 3.305785):0}평)";

    public IReadOnlyList<LevelAreaSummary> LevelAreaSummaries => FloorPlan.RoomAreas
        .GroupBy(room => room.Level)
        .OrderBy(group => group.Key)
        .Select(group => new LevelAreaSummary(group.Key, group.Sum(room => room.AreaSquareMeters)))
        .ToList();

    public double? SelectedLength
    {
        get => SelectedElement switch
        {
            Wall selectedWall => selectedWall.Length,
            DimensionLine selectedDimension => selectedDimension.Length,
            _ => null
        };
        set
        {
            if (value is not >= 10)
            {
                return;
            }
            var (start, end) = SelectedElement switch
            {
                Wall sourceWall => (sourceWall.StartPoint, sourceWall.EndPoint),
                DimensionLine sourceDimension => (sourceDimension.StartPoint, sourceDimension.EndPoint),
                _ => (default, default)
            };
            if (SelectedElement is not Wall and not DimensionLine)
            {
                return;
            }
            var direction = end - start;
            if (direction.Length < .001)
            {
                direction = new System.Windows.Vector(1, 0);
            }
            else
            {
                direction.Normalize();
            }
            if (SelectedElement is Wall wall)
            {
                wall.EndPoint = wall.StartPoint + direction * value.Value;
                ConstrainOpeningsToWall(wall);
            }
            else if (SelectedElement is DimensionLine dimension)
            {
                dimension.EndPoint = dimension.StartPoint + direction * value.Value;
            }
        }
    }

    public double? SelectedWidth
    {
        get => SelectedElement switch
        {
            WallOpening opening => opening.Width,
            Furniture furniture => furniture.Width,
            RoomArea room => room.Bounds.Width,
            SiteElement siteElement => siteElement.Width,
            _ => null
        };
        set
        {
            if (value is not >= 10)
            {
                return;
            }
            switch (SelectedElement)
            {
                case WallOpening opening:
                    SetOpeningWidth(opening, value.Value);
                    break;
                case Furniture furniture:
                    furniture.Width = value.Value;
                    break;
                case RoomArea room:
                    room.Bounds = new System.Windows.Rect(room.Bounds.X, room.Bounds.Y, value.Value, room.Bounds.Height);
                    break;
                case SiteElement siteElement:
                    siteElement.Width = value.Value;
                    break;
            }
        }
    }

    public double? SelectedHeight
    {
        get => SelectedElement switch
        {
            Furniture furniture => furniture.Height,
            RoomArea room => room.Bounds.Height,
            SiteElement siteElement => siteElement.Height,
            _ => null
        };
        set
        {
            if (value is not >= 20)
            {
                return;
            }
            if (SelectedElement is Furniture furniture)
            {
                furniture.Height = value.Value;
            }
            else if (SelectedElement is RoomArea room)
            {
                room.Bounds = new System.Windows.Rect(room.Bounds.X, room.Bounds.Y, room.Bounds.Width, value.Value);
            }
            else if (SelectedElement is SiteElement siteElement)
            {
                siteElement.Height = value.Value;
            }
        }
    }

    public double? SelectedThickness
    {
        get => SelectedElement is Wall wall ? wall.Thickness : null;
        set
        {
            if (SelectedElement is Wall wall && value is >= 1 and <= 100)
            {
                wall.Thickness = value.Value;
            }
        }
    }

    public double? SelectedFontSize
    {
        get => SelectedElement is RoomLabel label ? label.FontSize : null;
        set
        {
            if (SelectedElement is RoomLabel label && value is >= 8 and <= 72)
            {
                label.FontSize = value.Value;
            }
        }
    }

    public bool CanEditLength => SelectedElement is Wall or DimensionLine;

    public bool CanEditWidth => SelectedElement is WallOpening or Furniture or RoomArea or SiteElement;

    public bool CanEditHeight => SelectedElement is Furniture or RoomArea or SiteElement;

    public bool CanEditThickness => SelectedElement is Wall;

    public bool CanEditFontSize => SelectedElement is RoomLabel;

    public string SelectedElementName => SelectedElement switch
    {
        Wall => "벽",
        Door => "문",
        WindowElement => "창문",
        Furniture furniture => GetFurnitureName(furniture.Kind),
        RoomLabel => "방 이름",
        RoomArea => "방 영역",
        DimensionLine => "치수선",
        SiteElement siteElement => GetSiteElementName(siteElement.Kind),
        _ => "선택된 요소 없음"
    };

    public string SelectedElementDetails => SelectedElement switch
    {
        Wall wall => $"길이  {wall.Length:0.#} cm\n두께  {wall.Thickness:0.#} cm\n시작  ({wall.StartPoint.X:0.#}, {wall.StartPoint.Y:0.#})\n끝  ({wall.EndPoint.X:0.#}, {wall.EndPoint.Y:0.#})",
        Door door => $"너비  {door.Width:0.#} cm\n방향  {door.RotationQuarterTurns * 90}°\n벽 위 위치  {door.Position:P0}",
        WindowElement window => $"너비  {window.Width:0.#} cm\n방향  {window.RotationQuarterTurns * 90}°\n벽 위 위치  {window.Position:P0}",
        Furniture furniture => $"크기  {furniture.Width:0.#} × {furniture.Height:0.#} cm\n방향  {furniture.RotationDegrees}°\n위치  ({furniture.Location.X:0.#}, {furniture.Location.Y:0.#})",
        RoomLabel label => $"이름  {label.Text}\n글자 크기  {label.FontSize:0.#}\n방향  {label.RotationDegrees}°\n위치  ({label.Location.X:0.#}, {label.Location.Y:0.#})",
        RoomArea room => $"이름  {room.Name}\nLevel  {room.Level}\n크기  {room.Bounds.Width:0.#} × {room.Bounds.Height:0.#} cm\n면적  {room.AreaSquareMeters:0.##} m² (약 {Math.Round(room.AreaPyeong):0}평)",
        DimensionLine dimension => $"측정 길이  {dimension.Length:0.#} cm\n시작  ({dimension.StartPoint.X:0.#}, {dimension.StartPoint.Y:0.#})\n끝  ({dimension.EndPoint.X:0.#}, {dimension.EndPoint.Y:0.#})",
        SiteElement siteElement => $"크기  {siteElement.Width:0.#} × {siteElement.Height:0.#} cm\n방향  {siteElement.RotationDegrees}°\n위치  ({siteElement.Location.X:0.#}, {siteElement.Location.Y:0.#})",
        _ => "선택 도구로 요소를 클릭하세요."
    };

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

    private void OnPlanCollectionChanged()
    {
        ((RelayCommand)ClearCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ExportCsvCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ExportSvgCommand).RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(WallCount));
        OnPropertyChanged(nameof(ElementCount));
        OnPropertyChanged(nameof(ElementSummary));
        if (!_isInternalOperation)
        {
            StatusMessage = "저장되지 않은 변경사항";
        }
    }

    private void OnRoomAreasChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncRoomAreaSubscriptions();
        OnPlanCollectionChanged();
        NotifyAreaTotalsChanged();
    }

    private void SyncRoomAreaSubscriptions()
    {
        var currentRooms = FloorPlan.RoomAreas.ToHashSet();
        foreach (var removedRoom in _trackedRoomAreas.Where(room => !currentRooms.Contains(room)).ToList())
        {
            removedRoom.PropertyChanged -= OnRoomAreaPropertyChanged;
            _trackedRoomAreas.Remove(removedRoom);
        }
        foreach (var addedRoom in currentRooms.Where(room => !_trackedRoomAreas.Contains(room)))
        {
            addedRoom.PropertyChanged += OnRoomAreaPropertyChanged;
            _trackedRoomAreas.Add(addedRoom);
        }
    }

    private void OnRoomAreaPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RoomArea.Bounds) or nameof(RoomArea.AreaSquareMeters)
            or nameof(RoomArea.AreaPyeong) or nameof(RoomArea.Level))
        {
            NotifyAreaTotalsChanged();
            if (!_isInternalOperation)
            {
                StatusMessage = "저장되지 않은 변경사항";
            }
        }
    }

    private void NotifyAreaTotalsChanged()
    {
        OnPropertyChanged(nameof(TotalRoomAreaSquareMeters));
        OnPropertyChanged(nameof(TotalRoomAreaText));
        OnPropertyChanged(nameof(LevelAreaSummaries));
    }

    private void RotateSelected(int direction)
    {
        switch (SelectedElement)
        {
            case Furniture furniture:
                furniture.RotationDegrees += direction * 90;
                break;
            case RoomLabel label:
                label.RotationDegrees += direction * 90;
                break;
            case WallOpening opening:
                opening.RotationQuarterTurns += direction;
                break;
            case SiteElement siteElement:
                siteElement.RotationDegrees += direction * 90;
                break;
            default:
                return;
        }
        StatusMessage = "저장되지 않은 변경사항";
        OnPropertyChanged(nameof(SelectedElementDetails));
    }

    private void OnSelectedElementChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SelectedElementDetails));
        OnPropertyChanged(nameof(SelectedRoomLevel));
        NotifySizePropertiesChanged();
        if (!_isInternalOperation)
        {
            StatusMessage = "저장되지 않은 변경사항";
        }
    }

    private void SaveDocument(bool forceChoosePath)
    {
        try
        {
            var path = !forceChoosePath ? _currentFilePath : null;
            if (path is null)
            {
                var dialog = new SaveFileDialog
                {
                    Title = "도면 저장",
                    Filter = "House Designer JSON (*.json)|*.json",
                    DefaultExt = ".json",
                    AddExtension = true,
                    FileName = _currentFilePath is null ? "house-plan.json" : Path.GetFileName(_currentFilePath),
                    InitialDirectory = GetDesignDirectory()
                };
                if (dialog.ShowDialog() != true)
                {
                    return;
                }
                path = ToPreferredRelativePath(dialog.FileName);
            }

            _isInternalOperation = true;
            PlanDocumentService.Save(path, FloorPlan, GridSize, IsGridSnapEnabled);
            _currentFilePath = path;
            OnPropertyChanged(nameof(DocumentTitle));
            StatusMessage = $"저장됨 · {DateTime.Now:HH:mm}";
        }
        catch (Exception exception)
        {
            ShowFileError("도면을 저장하지 못했습니다.", exception);
        }
        finally
        {
            _isInternalOperation = false;
        }
    }

    private void LoadDocument()
    {
        var dialog = new OpenFileDialog
        {
            Title = "도면 불러오기",
            Filter = "House Designer JSON (*.json)|*.json|모든 파일 (*.*)|*.*",
            DefaultExt = ".json",
            InitialDirectory = GetDesignDirectory()
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            _isInternalOperation = true;
            var documentPath = ToPreferredRelativePath(dialog.FileName);
            var document = PlanDocumentService.Load(documentPath);
            SelectedElement = null;
            ReplaceFloorPlan(document.FloorPlan);
            GridSize = document.GridSize;
            IsGridSnapEnabled = document.IsGridSnapEnabled;
            _currentFilePath = documentPath;
            OnPropertyChanged(nameof(DocumentTitle));
            StatusMessage = $"불러옴 · {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception exception)
        {
            ShowFileError("도면을 불러오지 못했습니다.", exception);
        }
        finally
        {
            _isInternalOperation = false;
        }
    }

    private void ExportCsv()
    {
        var dialog = new SaveFileDialog
        {
            Title = "요소 데이터 CSV 내보내기",
            Filter = "CSV 파일 (*.csv)|*.csv",
            DefaultExt = ".csv",
            AddExtension = true,
            FileName = GetExportBaseName() + ".csv",
            InitialDirectory = GetDesignDirectory()
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        try
        {
            CsvExportService.Export(dialog.FileName, FloorPlan);
            StatusMessage = $"CSV 내보내기 완료 · {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception exception)
        {
            ShowFileError("CSV 파일을 만들지 못했습니다.", exception);
        }
    }

    private void ExportSvg()
    {
        var dialog = new SaveFileDialog
        {
            Title = "도면 맞춤 SVG 내보내기",
            Filter = "SVG 벡터 도면 (*.svg)|*.svg",
            DefaultExt = ".svg",
            AddExtension = true,
            FileName = GetExportBaseName() + ".svg",
            InitialDirectory = GetDesignDirectory()
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        try
        {
            SvgExportService.Export(dialog.FileName, FloorPlan, GridSize,
                SelectedElementName, SelectedElementDetails);
            StatusMessage = $"SVG 내보내기 완료 · {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception exception)
        {
            ShowFileError("SVG 파일을 만들지 못했습니다.", exception);
        }
    }

    private void ReplaceFloorPlan(FloorPlan source)
    {
        FloorPlan.Clear();
        foreach (var wall in source.Walls) FloorPlan.Walls.Add(wall);
        foreach (var door in source.Doors) FloorPlan.Doors.Add(door);
        foreach (var window in source.Windows) FloorPlan.Windows.Add(window);
        foreach (var room in source.RoomAreas) FloorPlan.RoomAreas.Add(room);
        foreach (var item in source.FurnitureItems) FloorPlan.FurnitureItems.Add(item);
        foreach (var label in source.RoomLabels) FloorPlan.RoomLabels.Add(label);
        foreach (var dimension in source.Dimensions) FloorPlan.Dimensions.Add(dimension);
        foreach (var siteElement in source.SiteElements) FloorPlan.SiteElements.Add(siteElement);
    }

    private string GetExportBaseName() => _currentFilePath is null
        ? "house-plan"
        : Path.GetFileNameWithoutExtension(_currentFilePath);

    private static string GetDesignDirectory()
    {
        Directory.CreateDirectory(DefaultDesignFolder);
        return Path.GetFullPath(DefaultDesignFolder);
    }

    private static string ToPreferredRelativePath(string path)
    {
        var workingDirectory = Path.GetFullPath(".");
        var fullPath = Path.GetFullPath(path);
        var relativePath = Path.GetRelativePath(workingDirectory, fullPath);
        return relativePath == ".." || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? fullPath
            : relativePath;
    }

    private void ShowFileError(string message, Exception exception)
    {
        StatusMessage = message;
        System.Windows.MessageBox.Show($"{message}\n\n{exception.Message}", "House Designer",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
    }

    private static void SetOpeningWidth(WallOpening opening, double width)
    {
        var wallLength = opening.ParentWall.Length;
        opening.Width = wallLength > 0 ? Math.Min(width, wallLength) : width;
        if (wallLength <= opening.Width || wallLength < .001)
        {
            opening.Position = .5;
            return;
        }
        var margin = opening.Width / (2 * wallLength);
        opening.Position = Math.Clamp(opening.Position, margin, 1 - margin);
    }

    private void ConstrainOpeningsToWall(Wall wall)
    {
        foreach (var opening in FloorPlan.Doors.Cast<WallOpening>().Concat(FloorPlan.Windows)
                     .Where(opening => ReferenceEquals(opening.ParentWall, wall)))
        {
            SetOpeningWidth(opening, opening.Width);
        }
    }

    private void NotifySizePropertiesChanged()
    {
        OnPropertyChanged(nameof(SelectedLength));
        OnPropertyChanged(nameof(SelectedWidth));
        OnPropertyChanged(nameof(SelectedHeight));
        OnPropertyChanged(nameof(SelectedThickness));
        OnPropertyChanged(nameof(SelectedFontSize));
        OnPropertyChanged(nameof(CanEditLength));
        OnPropertyChanged(nameof(CanEditWidth));
        OnPropertyChanged(nameof(CanEditHeight));
        OnPropertyChanged(nameof(CanEditThickness));
        OnPropertyChanged(nameof(CanEditFontSize));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record FurnitureChoice(FurnitureKind Kind, string Name);

public sealed record LevelAreaSummary(int Level, double SquareMeters)
{
    public string Label => $"Level {Level} Area";

    public string AreaText => $"{SquareMeters:0.##} m² (약 {Math.Round(SquareMeters / 3.305785):0}평)";
}
