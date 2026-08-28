# Forge3D

> **C# 기반 실시간 3D 강체 물리 시뮬레이션 엔진 및 WPF 엔지니어링 도구**

Forge3D는 C#으로 직접 구현하는 **3D Rigid Body Physics Engine**과, 이를 실시간으로 시각화·조작·디버깅하기 위한 **WPF 기반 Engineering Simulation Tool**을 함께 개발하는 프로젝트입니다.

단순히 3D 오브젝트를 화면에 띄우는 데 목적이 있는 것이 아니라, 강체 물리 시뮬레이션의 핵심 구성 요소인 **힘, 속도, 회전, 충돌 판정, 충돌 반응, 마찰, 반발, 고정 시간 간격 시뮬레이션**을 직접 구현하고, 엔진 내부 상태를 실시간으로 관찰할 수 있는 데스크톱 도구까지 구성하는 것을 목표로 합니다.

Forge3D의 물리 코어는 WPF와 분리된 독립 라이브러리로 설계하며, WPF는 시뮬레이션 결과를 표시하고 상태를 분석하는 UI 계층으로 사용합니다.

---

# 현재 구현 상태

```text
Forge3D.Core

- RigidBody 기반 강체 상태
- Gravity / Force / Torque / Impulse
- Quaternion orientation integration
- Fixed timestep runner
- Sphere / Box / Plane collider
- Sphere-Sphere / Sphere-Plane / Sphere-Box
- Box-Plane / Box-Box SAT collision
- Contact point / normal / penetration
- Impulse solver with friction / restitution
- Contact-point angular impulse
- Physics step profiler stats
```

```text
Forge3D.Editor

- WPF 3D Viewport
- Scene Hierarchy
- Runtime Inspector with editable transform, velocity, material, damping
- Run / Pause / Step / Reset
- Add Box / Add Sphere
- Drop / Bounce / Friction / Stack / Stress demos
- Velocity / Contact / Normal / Bounds debug visualization
- Mouse selection
- Orbit / Pan / Zoom camera control
- FPS / Body / Contact / Physics time profiler
- Contact Inspector
- Live graph for selected body: height, speed, kinetic energy
- Snapshot replay and timeline playback
- Engineering Scenario: Autonomous Vehicle Safety Test
- Vehicle control, waypoint mission, range/FOV sensor detection
- Telemetry, safety state, TTC, automatic/manual emergency stop
- Fault injection: sensor failure, wheel slip, motor degradation, communication loss
- Event/alarm log
```

```text
Forge3D.Core.Simulation

- SimulationEntity application layer
- VehicleEntity / VehicleController
- WaypointEntity / MissionController
- SensorEntity range and FOV detection
- SafetyEvaluator with warning/critical zones and TTC
- FaultManager
- SimulationEvent / EventSeverity
```

```text
Forge3D.Tests

- RigidBody force/impulse tests
- Fixed timestep tests
- Collider bounds tests
- Sphere-plane collision/solver tests
- Rotated box bounds and SAT contact tests
```

---

# 실행 방법

VSCode에서 폴더를 연 뒤 추천 확장 설치:

```text
C# Dev Kit
C#
.NET Install Tool
```

터미널 실행:

```powershell
dotnet build
dotnet test Forge3D.Tests\Forge3D.Tests.csproj
dotnet run --project Forge3D.Editor
```

VSCode의 Run and Debug에서 `Forge3D Editor` 구성을 선택해도 실행할 수 있습니다.

카메라 조작:

```text
Alt + Left Drag   Orbit
Right Drag        Orbit
Right + W         Move camera forward
Right + A         Move camera left
Right + S         Move camera backward
Right + D         Move camera right
Right + Q         Move camera up
Right + E         Move camera down
Middle Drag       Pan
Alt + Right Drag  Zoom
Mouse Wheel       Zoom
```

오브젝트 이동:

```text
Click Object            Select
Drag Object             Move on camera view plane
Drag Red X Handle       Move on selected object's local X axis
Drag Green Y Handle     Move on selected object's local Y axis
Drag Blue Z Handle      Move on selected object's local Z axis
Ctrl + Drag             Snap movement to 0.25 units
```

---

# 프로젝트 목표

Forge3D의 핵심 목표는 다음과 같습니다.

- C# 기반 3D 강체 물리 시뮬레이션 엔진 구현
- UI와 독립된 Physics Core 설계
- Sphere / Box / Plane 기반 충돌 처리
- 힘, 토크, 충격량 기반 동역학 구현
- Quaternion 기반 3D 회전 처리
- Impulse 기반 충돌 반응 구현
- 마찰 및 반발 계수 처리
- Fixed Timestep 기반 안정적인 Simulation Loop 구현
- WPF 기반 실시간 3D 시각화
- Runtime Inspector를 통한 물리 값 변경
- Contact Point / Normal / Velocity 등의 Debug Visualization
- Pause / Frame Step 기반 시뮬레이션 분석
- Physics / Collision / Solver 단위 성능 측정
- Stress Test를 통한 성능 특성 분석

---

# 핵심 구성

Forge3D는 크게 두 영역으로 나뉩니다.

```text
Forge3D

┌──────────────────────────────┐
│        Forge3D.Editor        │
│             WPF              │
│                              │
│ Scene Hierarchy              │
│ 3D Viewport                  │
│ Inspector                    │
│ Debug Visualization          │
│ Profiler                     │
└──────────────┬───────────────┘
               │
               ▼
┌──────────────────────────────┐
│         Forge3D.Core         │
│                              │
│ PhysicsWorld                 │
│ RigidBody                    │
│ Collision Detection          │
│ Contact Generation           │
│ Impulse Solver               │
│ Constraints                  │
│ Diagnostics                  │
└──────────────────────────────┘
```

중요한 원칙은 다음과 같습니다.

> `Forge3D.Core`는 WPF를 참조하지 않습니다.

이를 통해 물리 엔진을 UI 프레임워크와 분리하고, 독립적으로 테스트하거나 다른 시각화 계층에 재사용할 수 있도록 합니다.

---

# 주요 기능

## 3D 강체 시뮬레이션

RigidBody는 다음 상태를 가집니다.

```text
Position
Orientation

Linear Velocity
Angular Velocity

Force
Torque

Mass
Inverse Mass

Linear Damping
Angular Damping
```

물체에는 중력, 힘, 토크, 충격량을 적용할 수 있으며, 이를 기반으로 위치와 회전을 갱신합니다.

---

# 힘과 충격량

RigidBody는 다음 연산을 지원하도록 설계합니다.

```text
ApplyForce
ApplyTorque
ApplyImpulse
```

중심에서 벗어난 위치에 충격량이 적용되는 경우 이동뿐 아니라 회전도 발생하도록 구현합니다.

이를 위해 선형 운동과 회전 운동을 함께 처리합니다.

---

# 3D 회전

3D 회전 상태는 내부적으로 Quaternion을 사용합니다.

주요 요소는 다음과 같습니다.

```text
Quaternion Orientation
Angular Velocity
Torque
Moment of Inertia
Inverse Inertia
```

Euler Angle은 사용자 입력이나 Inspector 표시 등 사람이 읽기 쉬운 표현이 필요한 경우에만 사용하고, 실제 시뮬레이션 상태는 Quaternion 중심으로 관리합니다.

---

# Collider

초기 물리 엔진은 다음 충돌체를 지원합니다.

```text
SphereCollider
BoxCollider
PlaneCollider
```

우선 구현 대상 충돌 조합은 다음과 같습니다.

```text
Sphere ↔ Sphere
Sphere ↔ Plane
Box ↔ Plane
Sphere ↔ Box
Box ↔ Box
```

---

# 충돌 판정

Collision Detection 단계에서는 두 물체가 충돌했는지 여부뿐 아니라, Solver가 사용할 수 있는 Contact 정보를 생성합니다.

예:

```text
Contact

BodyA
BodyB

Point
Normal
Penetration

Friction
Restitution
```

Collision Detection과 Collision Response는 별도의 책임으로 분리합니다.

---

# 충돌 반응

충돌 반응은 **Impulse 기반 Solver**로 구현합니다.

주요 처리 항목은 다음과 같습니다.

```text
Normal Impulse
Restitution
Friction Impulse
Penetration Correction
```

이를 통해 물체가 충돌했을 때 튕기거나, 미끄러지거나, 멈추는 동작을 계산합니다.

---

# 마찰과 반발

각 Collider 또는 Body는 Physics Material을 가질 수 있습니다.

예:

```text
PhysicsMaterial

Friction
Restitution
```

이를 통해 서로 다른 표면 특성을 표현할 수 있습니다.

예를 들어:

```text
Rubber
Friction     0.8
Restitution  0.9

Steel
Friction     0.4
Restitution  0.2

Ice
Friction     0.05
Restitution  0.1
```

WPF Inspector에서 값을 변경해 시뮬레이션 결과가 즉시 달라지는 것을 확인할 수 있도록 구성합니다.

---

# Fixed Timestep

Physics 계산은 렌더링 FPS와 분리하여 고정된 시간 간격으로 수행합니다.

기본 목표:

```text
Physics Step = 1 / 60 sec
```

구조:

```text
Frame Delta
    ↓
Accumulator
    ↓
Fixed Physics Step
```

이를 통해 렌더링 프레임 시간이 달라져도 물리 시뮬레이션 결과가 지나치게 흔들리지 않도록 합니다.

---

# Physics Pipeline

기본적인 Simulation Step은 다음 구조를 가집니다.

```text
Apply Forces
     ↓
Integrate Velocity
     ↓
Broad Phase
     ↓
Narrow Phase
     ↓
Generate Contacts
     ↓
Solve Contacts
     ↓
Integrate Transform
     ↓
Clear Forces
```

각 단계는 가능하면 독립적으로 측정하고 디버깅할 수 있도록 설계합니다.

---

# WPF Engineering Tool

WPF는 단순 렌더링 창이 아니라, 물리 엔진을 직접 관찰하고 조작하는 엔지니어링 도구 역할을 합니다.

주요 영역은 다음과 같습니다.

```text
Scene Hierarchy
3D Viewport
Inspector
Simulation Control
Debug Visualization
Profiler
```

---

# Scene Hierarchy

현재 Physics World 안에 존재하는 객체를 계층 형태로 확인합니다.

예:

```text
World

├─ Ground
├─ Box_001
├─ Box_002
├─ Sphere_001
└─ Sphere_002
```

객체 선택 상태는 다음 영역과 연동합니다.

```text
Hierarchy
Viewport
Inspector
```

---

# Runtime Inspector

선택된 RigidBody의 상태를 실시간으로 확인하고 수정할 수 있도록 합니다.

예:

```text
Transform

Position
Rotation

Physics

Mass
Friction
Restitution

Linear Velocity
Angular Velocity
```

또한 다음 동작을 직접 실행할 수 있도록 합니다.

```text
Apply Force
Apply Torque
Apply Impulse
```

---

# Debug Visualization

Forge3D는 물리 엔진 내부 상태를 가능한 한 화면에서 직접 확인할 수 있도록 만드는 것을 중요하게 생각합니다.

표시 대상:

```text
Velocity Vector
Contact Point
Contact Normal
Bounding Box
Center of Mass
Sleep State
```

초기 핵심 기능은 다음 세 가지입니다.

```text
Velocity
Contact Point
Contact Normal
```

이 기능을 통해 물체가 단순히 "움직이는 것"만 보는 것이 아니라, 왜 그런 움직임이 발생했는지 엔진 내부 상태를 확인할 수 있습니다.

---

# Pause / Frame Step

Simulation은 다음 제어 기능을 제공합니다.

```text
Run
Pause
Step
Reset
```

Pause 상태에서 Step을 실행하면 정확히 한 번의 Fixed Physics Step만 수행합니다.

이를 통해 충돌 직전과 직후를 한 프레임씩 확인할 수 있습니다.

예를 들어 다음 항목을 분석할 수 있습니다.

- Contact가 생성되는 순간
- Collision Normal
- Penetration
- Impulse 적용 전/후 Velocity
- 회전 변화

---

# Profiler

Physics Pipeline의 주요 구간을 실시간으로 측정합니다.

예:

```text
Bodies
Contacts
Potential Pairs

Broad Phase
Narrow Phase
Solver
Total Physics

FPS
```

초기에는 `Stopwatch` 기반의 단순 측정부터 시작합니다.

목표는 "최적화했다"라고 주장하는 것이 아니라, 실제 측정 결과를 기반으로 병목을 확인하고 개선하는 것입니다.

---

# Stress Test

다수의 RigidBody를 한 번에 생성하여 물리 엔진의 성능 특성을 확인합니다.

예:

```text
Spawn 100
Spawn 300
Spawn 500
```

성능이 충분하면 1000개 이상의 Body까지 확장합니다.

Stress Test에서는 다음 수치를 함께 확인합니다.

```text
Body Count
Potential Pair Count
Contact Count
Physics Time
FPS
```

---

# Demo Scene

Forge3D는 물리 기능을 확인할 수 있도록 여러 테스트 Scene을 제공합니다.

## Drop Test

검증 대상:

```text
Gravity
Collision
Restitution
```

---

## Bounce Test

Restitution이 다른 물체들을 같은 높이에서 떨어뜨려 반발 차이를 확인합니다.

---

## Friction Test

서로 다른 Friction 값을 가진 물체가 동일한 표면 또는 경사면에서 어떻게 다르게 움직이는지 확인합니다.

---

## Stack Test

여러 Box를 쌓아 Contact Solver와 Penetration Correction의 안정성을 확인합니다.

---

## Stress Test

수백 개의 Body를 동시에 생성해 충돌 처리 성능과 Solver 부하를 측정합니다.

---

# 프로젝트 구조

```text
Forge3D.sln

├─ Forge3D.Core
│  ├─ Mathematics
│  ├─ Dynamics
│  ├─ Collision
│  ├─ Solver
│  ├─ Constraints
│  └─ Diagnostics
│
├─ Forge3D.Editor
│  ├─ ViewModels
│  ├─ Views
│  ├─ Rendering
│  ├─ DebugDrawing
│  └─ Controls
│
└─ Forge3D.Tests
```

---

# 기술 스택

## Core

```text
C#
.NET
System.Numerics

Vector3
Quaternion
Rigid Body Dynamics
Collision Detection
Impulse Solver
Fixed Timestep
```

## Desktop / UI

```text
WPF
XAML
MVVM
Data Binding
Command
3D Visualization
```

## Engineering

```text
Unit Test
Performance Profiling
Debug Visualization
Layer Separation
Git / GitHub
```

---

# 설계 원칙

## Physics Core는 UI와 독립적이어야 한다

Physics Core가 WPF 타입에 의존하지 않도록 합니다.

이를 통해 다음을 가능하게 합니다.

- Unit Test
- 독립 실행
- Renderer 교체
- 재사용
- 성능 분석
- 유지보수

---

## 엔진 내부 상태를 숨기지 않는다

물리 엔진에서 중요한 정보 대부분은 눈에 보이지 않습니다.

예:

```text
Velocity
Normal
Penetration
Contact
Bounding Volume
Solver Time
```

Forge3D는 이런 정보를 Debug Visualization과 Inspector를 통해 최대한 확인할 수 있도록 설계합니다.

---

## 측정 후 최적화한다

성능 최적화는 반드시 측정 결과를 기반으로 진행합니다.

예:

```text
Brute Force Broad Phase
        ↓
Profiler 측정
        ↓
AABB / Spatial Optimization
        ↓
다시 측정
```

최적화 전후의 수치를 비교할 수 있도록 합니다.

---

# 2D Planar Simulation 확장

Forge3D는 기본적으로 3D Physics Engine이지만, RigidBody의 자유도(DOF)를 제한하여 동일한 Physics Core를 2D 형태로 사용할 수 있도록 확장할 수 있습니다.

예: XY 평면

```text
Translation

X  Enabled
Y  Enabled
Z  Locked

Rotation

X  Locked
Y  Locked
Z  Enabled
```

이를 통해 다음 모드를 지원할 수 있습니다.

```text
XY Plane
XZ Plane
YZ Plane
```

별도의 2D 엔진을 만드는 것이 아니라, 3D Physics Core 위에서 Planar Constraint를 적용하는 방식입니다.

---

# 향후 확장

기본 Physics Core가 안정화된 이후 다음 기능을 추가할 수 있습니다.

## Collision

```text
OBB
SAT
Convex Hull
GJK
EPA
```

## Constraints

```text
DOF Constraint
Distance Joint
Hinge Joint
Ball Joint
```

## Simulation

```text
Sleeping
Continuous Collision Detection
Replay
Timeline
```

## Performance

```text
Spatial Hash
Sweep And Prune
BVH
Island Solver
Parallel Collision Processing
```

## Engineering

```text
Scene Save / Load
Telemetry
TCP / UDP
Sensor Simulation
Digital Twin Scenario
```

---

# 적용 가능한 영역

Forge3D는 게임 전용 물리엔진이 아니라, 엔지니어링 시뮬레이션을 염두에 둔 프로젝트입니다.

## 방산

```text
무인체계 Simulation
Mission Simulation
플랫폼 상태 시각화
Control System Visualization
```

## 산업 자동화

```text
Equipment Digital Twin
Robot Motion Simulation
Collision Verification
Engineering Tool
```

## 반도체 장비

```text
Mechanism Simulation
Equipment Visualization
Motion Debugging
```

## 의료기기 / 의료로봇

```text
3D Mechanism Visualization
Robot Simulation
Engineering Analysis Tool
```

---

# 이 프로젝트가 목표로 하지 않는 것

Forge3D는 다음을 목표로 하지 않습니다.

- PhysX 대체
- Bullet 대체
- Unity 대체
- 완전한 Game Engine
- 상용 CAD
- 고정밀 FEM Solver
- 사실적인 Rendering Engine

목표는 **실시간 3D 강체 물리 시뮬레이션의 핵심 구조와 알고리즘을 직접 구현하고, 이를 분석할 수 있는 엔지니어링 도구를 완성하는 것**입니다.

---

# 주요 기술 키워드

```text
C#
WPF
MVVM

3D Mathematics
Vector3
Quaternion

Rigid Body Dynamics
Collision Detection
Collision Response

Impulse Solver
Friction
Restitution

Fixed Timestep

Debug Visualization
Performance Profiling

Software Architecture
Unit Testing
```

---
