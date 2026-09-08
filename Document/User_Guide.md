# Basic House Floor Plan Editor

## Goal

기초적인 주택 평면도를 직접 그릴 수 있는 간단한 데스크톱 프로그램을 만들어줘.

전문 CAD 수준이 아니라, 사용자가 마우스로 벽을 그리고 문/창문을 배치하면서 기본적인 집 구조를 설계할 수 있는 수준이면 된다.
또는 사용자의 말(텍스트)를 입력으로 받은걸 도면으로 반영해주는 기능도 있어야 해

코드는 유지보수와 기능 확장이 쉽도록 작성해줘.

## Development Environment

* Language: C#
* Framework: WPF or Avalonia
* Platform: Windows
* Architecture: MVVM을 권장하지만, 초기 구현이 지나치게 복잡해진다면 단순한 구조로 시작해도 됨

## Basic UI

메인 화면은 다음과 같이 구성한다.

* 상단 Toolbar
* 왼쪽 Tool Panel
* 중앙 Drawing Canvas
* 오른쪽 Property Panel

중앙 Canvas 영역에서 실제 평면도를 작성한다.

## Required Features

### 1. Drawing Canvas

사용자가 평면도를 작성할 수 있는 Canvas를 만든다.

기본적으로 다음 기능을 지원한다.

* Grid 표시
* 마우스 Wheel Zoom
* Canvas Pan
* 객체 선택
* 객체 이동
* 객체 삭제

### 2. Wall

Wall Tool을 선택한 상태에서 Canvas를 클릭하여 벽을 그릴 수 있게 한다.

기본 동작:

1. 첫 번째 클릭으로 시작점 지정
2. 두 번째 클릭으로 끝점 지정
3. 두 점 사이에 벽 생성

벽은 Line이 아니라 향후 두께를 표현할 수 있도록 별도의 Wall 객체로 관리한다.

Wall 데이터에는 최소한 다음 정보가 있어야 한다.

```text
StartPoint
EndPoint
Thickness
```

기본 벽 두께는 임의의 적당한 값으로 설정한다.

### 3. Grid Snap

벽을 그릴 때 일정 간격의 Grid에 자동으로 Snap 되도록 한다.

예:

```text
GridSize = 10
```

Grid Snap 기능은 On/Off 할 수 있도록 확장 가능한 구조로 만든다.

### 4. Door

Door Tool을 선택하고 기존 Wall을 클릭하면 해당 Wall에 Door를 배치할 수 있게 한다.

초기 버전에서는 단순한 사각형 또는 기본적인 문 Symbol로 표현해도 된다.

Door 데이터에는 최소한 다음 정보를 가진다.

```text
ParentWall
Position
Width
```

### 5. Window

Window도 Door와 동일하게 기존 Wall 위에 배치한다.

최소 데이터:

```text
ParentWall
Position
Width
```

### 6. Selection

Select Tool을 제공한다.

객체를 클릭하면 선택 상태가 되고 시각적으로 선택되었다는 표시를 한다.

선택 가능한 객체:

* Wall
* Door
* Window

Delete 키를 누르면 선택된 객체를 삭제한다.

### 7. Property Panel

선택한 객체의 기본 속성을 오른쪽 패널에서 표시한다.

예를 들어 Wall 선택 시:

```text
Length
Thickness
Start X
Start Y
End X
End Y
```

초기 버전에서는 읽기 전용이어도 된다.

## Data Model

Canvas의 UI Element 자체를 데이터로 사용하지 말고 별도의 Model 객체를 만들어 관리한다.

예:

```text
FloorPlan
 ├─ Walls
 ├─ Doors
 └─ Windows
```

예상 Model:

```text
Wall
Door
Window
```

Rendering과 데이터를 가능한 분리해서 설계한다.

향후 다음 기능을 추가할 수 있도록 구조를 고려한다.

* 방(Room) 자동 인식
* 치수선(Dimension)
* 가구 배치
* Undo / Redo
* Save / Load
* JSON 저장
* 여러 층(Floor)
* 실제 단위(mm, cm, m)
* PDF / Image Export

## Implementation Priority

한 번에 모든 기능을 구현하지 말고 아래 순서대로 구현해줘.

1. WPF 프로젝트 기본 구조
2. Drawing Canvas
3. Grid
4. Zoom / Pan
5. Wall Model
6. Wall Drawing
7. Selection
8. Wall 이동 / 삭제
9. Door
10. Window
11. Property Panel

우선 1~6 단계까지 정상 동작하는 최소 버전을 구현해줘.

각 주요 클래스의 역할이 명확하도록 작성하고, 불필요하게 복잡한 추상화는 피한다.

프로그램이 실제로 실행 가능한 상태가 되도록 필요한 XAML과 C# 코드를 모두 작성해줘.
