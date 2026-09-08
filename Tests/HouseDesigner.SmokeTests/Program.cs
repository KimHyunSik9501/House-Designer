using System.IO;
using HouseDesigner.Models;
using HouseDesigner.Services;
using HouseDesigner.ViewModels;

var testDirectory = Path.Combine(Path.GetTempPath(), "HouseDesignerSmokeTests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(testDirectory);

try
{
    var plan = new FloorPlan();
    var wall = new Wall(new Point(0, 0), new Point(500, 0), 15);
    plan.Walls.Add(wall);
    plan.Doors.Add(new Door(wall, .25, 90) { RotationQuarterTurns = 2 });
    plan.Windows.Add(new WindowElement(wall, .7, 120) { RotationQuarterTurns = 1 });
    plan.RoomAreas.Add(new RoomArea(new Rect(0, 0, 500, 400), "거실", 2));
    plan.RoomAreas.Add(new RoomArea(new Rect(520, 0, 300, 300), "침실", 1));
    plan.RoomLabels.Add(new RoomLabel(new Point(250, 200), "거실") { FontSize = 22, RotationDegrees = 270 });
    plan.FurnitureItems.Add(new Furniture(FurnitureKind.Stairs, new Point(350, 250))
    {
        Width = 240,
        Height = 100,
        RotationDegrees = 90
    });
    plan.FurnitureItems.Add(new Furniture(FurnitureKind.Washbasin, new Point(80, 120)));
    plan.SiteElements.Add(new SiteElement(SiteElementKind.Road, new Point(250, 520)) { RotationDegrees = 90 });
    plan.SiteElements.Add(new SiteElement(SiteElementKind.Tree, new Point(-100, 100)));
    plan.SiteElements.Add(new SiteElement(SiteElementKind.Bench, new Point(650, 300)));
    plan.Dimensions.Add(new DimensionLine(new Point(0, -50), new Point(500, -50)));

    var jsonPath = Path.Combine(testDirectory, "roundtrip.json");
    var csvPath = Path.Combine(testDirectory, "elements.csv");
    var svgPath = Path.Combine(testDirectory, "drawing.svg");

    PlanDocumentService.Save(jsonPath, plan, 10, true);
    var loaded = PlanDocumentService.Load(jsonPath);
    Assert(loaded.FloorPlan.Walls.Count == 1, "벽 복원 실패");
    Assert(loaded.FloorPlan.Doors.Count == 1, "문 복원 실패");
    Assert(loaded.FloorPlan.Windows.Count == 1, "창문 복원 실패");
    Assert(loaded.FloorPlan.FurnitureItems.Any(item => item.Kind == FurnitureKind.Stairs), "계단 복원 실패");
    Assert(loaded.FloorPlan.FurnitureItems.Any(item => item.Kind == FurnitureKind.Washbasin), "세면대 복원 실패");
    Assert(loaded.FloorPlan.FurnitureItems.Single(item => item.Kind == FurnitureKind.Stairs).RotationDegrees == 90, "가구 회전 복원 실패");
    Assert(loaded.FloorPlan.SiteElements.Count == 3, "대지 요소 복원 실패");
    Assert(loaded.FloorPlan.SiteElements.Single(item => item.Kind == SiteElementKind.Road).RotationDegrees == 90, "도로 회전 복원 실패");
    Assert(loaded.FloorPlan.Doors.Single().RotationQuarterTurns == 2, "문 방향 복원 실패");
    Assert(loaded.FloorPlan.Windows.Single().RotationQuarterTurns == 1, "창문 방향 복원 실패");
    Assert(loaded.FloorPlan.RoomLabels.Single().RotationDegrees == 270, "텍스트 회전 복원 실패");
    Assert(loaded.FloorPlan.RoomAreas.Single(room => room.Name == "거실").Level == 2, "방 Level 복원 실패");
    Assert(ReferenceEquals(loaded.FloorPlan.Doors.Single().ParentWall, loaded.FloorPlan.Walls.Single()), "문의 부모 벽 연결 복원 실패");
    Assert(ReferenceEquals(loaded.FloorPlan.Windows.Single().ParentWall, loaded.FloorPlan.Walls.Single()), "창문의 부모 벽 연결 복원 실패");

    CsvExportService.Export(csvPath, loaded.FloorPlan);
    SvgExportService.Export(svgPath, loaded.FloorPlan, 10, "계단", "크기 240 × 100 cm\n방향 90°");
    Assert(File.ReadAllText(csvPath).Contains("계단"), "CSV 계단 데이터 누락");
    Assert(File.ReadAllText(csvPath).Contains("Level"), "CSV Level 열 누락");
    Assert(File.ReadAllText(csvPath).Contains("RotationDegrees"), "CSV 회전 열 누락");
    Assert(File.ReadAllText(csvPath).Contains("도로") && File.ReadAllText(csvPath).Contains("세면대"), "CSV 신규 요소 누락");
    var svg = File.ReadAllText(svgPath);
    Assert(svg.Contains("viewBox="), "SVG 도면 맞춤 viewBox 누락");
    Assert(svg.Contains("#38bdf8"), "SVG 치수선 색상 누락");
    Assert(svg.Contains("거실 · L2"), "SVG 방 Level 표시 누락");
    Assert(svg.Contains("PROPERTIES (속성)") && svg.Contains("TOTAL (합계)"), "SVG 한영 정보 패널 누락");
    Assert(svg.Contains("Grid (격자)") && svg.Contains("Rooms / Walls (방 / 벽)"), "SVG 속성 한글 병기 누락");
    Assert(svg.Contains("Doors / Windows (문 / 창문)") && svg.Contains("Dimensions (치수선)"), "SVG 요소 한글 병기 누락");
    Assert(svg.Contains("Total Level Area (전체 층 면적)") && svg.Contains("Level 2 Area (2층 면적)"),
        "SVG 면적 한글 병기 누락");
    Assert(svg.Contains("filter=\"url(#panelShadow)\"") && svg.Contains("font-size=\"13\""),
        "SVG 정보 패널 시각 스타일 누락");
    Assert(!svg.Contains("Selection ·", StringComparison.OrdinalIgnoreCase), "SVG 선택 요소 정보가 제거되지 않음");
    Assert(svg.Contains("rotate(90"), "SVG 가구 회전 누락");
    Assert(svg.Contains("#dc2626"), "SVG 빨간색 일반 치수 누락");
    Assert(svg.Contains("fill-opacity=\"0.45\""), "SVG 가구 투명도 누락");
    Assert(svg.Contains("도로") && svg.Contains("나무") && svg.Contains("벤치"), "SVG 대지 요소 누락");
    Assert(svg.IndexOf("fill-opacity=\"0.45\"", StringComparison.Ordinal) < svg.IndexOf("거실 · L2", StringComparison.Ordinal), "SVG 가구 후면 순서 실패");

    var viewModel = new MainViewModel();
    viewModel.SelectionCount = 3;
    Assert(viewModel.SelectedElementName == "3개 요소 선택", "다중 선택 개수 표시 실패");
    Assert(viewModel.SelectedElementDetails.Contains("함께 드래그"), "다중 선택 안내 표시 실패");
    viewModel.SelectionCount = 0;
    foreach (var room in loaded.FloorPlan.RoomAreas)
    {
        viewModel.FloorPlan.RoomAreas.Add(room);
    }
    Assert(Math.Abs(viewModel.TotalRoomAreaSquareMeters - 29) < .001, "전체 면적 집계 실패");
    Assert(viewModel.LevelAreaSummaries.Count == 2, "Level별 면적 그룹 집계 실패");
    Assert(Math.Abs(viewModel.LevelAreaSummaries.Single(item => item.Level == 1).SquareMeters - 9) < .001, "Level 1 면적 집계 실패");
    Assert(Math.Abs(viewModel.LevelAreaSummaries.Single(item => item.Level == 2).SquareMeters - 20) < .001, "Level 2 면적 집계 실패");

    Console.WriteLine("PASS: JSON round-trip, Level totals, wall references, CSV and fitted SVG export");
}
finally
{
    Directory.Delete(testDirectory, true);
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
