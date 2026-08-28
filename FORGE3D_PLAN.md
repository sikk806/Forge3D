# Forge3D Project Plan

## 0. 프로젝트 개요

**프로젝트명:** Forge3D  
**목표:** C# 기반 3D Rigid Body Physics Engine + WPF Engineering Simulator  
**개발 기간:** 2주(14일)  
**주요 용도:** 포트폴리오 / 기술 면접 / 방산·반도체·산업자동화·의료기기·디지털트윈 계열 지원

Forge3D는 단순한 "WPF 3D 데모"가 아니라, **UI와 독립된 물리 시뮬레이션 코어**를 직접 구현하고 WPF를 시각화·디버깅·실험 도구로 사용하는 엔지니어링 툴을 목표로 한다.

핵심 메시지:

> C#으로 직접 구현한 3D 강체 물리 엔진과, 이를 실시간으로 관찰·조작·분석할 수 있는 WPF 기반 Engineering Simulation Tool을 개발한다.

---

# 1. 핵심 전략

## 1.1 2주 내 목표

2주 동안 "완전한 물리엔진"을 만드는 것이 목표가 아니다.

목표는 다음 3가지를 만족하는 **작지만 깊은 엔진**을 완성하는 것이다.

1. 실제 물리 코어가 직접 구현되어 있을 것
2. WPF에서 실시간으로 시각화/제어 가능할 것
3. 면접과 포트폴리오 영상에서 WOW 포인트가 나올 것

---

## 1.2 개발 순서 원칙

기능은 아래 우선순위를 반드시 따른다.

1. **MUST**
2. **SHOULD**
3. **COULD**

MUST가 완성되기 전에는 COULD 기능을 시작하지 않는다.

2주 전에 MUST가 모두 끝난다면 그때부터 SHOULD, 이후 COULD를 추가한다.

즉:

```text
MUST 완료
    ↓
SHOULD 완료
    ↓
COULD 확장
```

---

# 2. 최종 시연 목표

최종 프로그램 실행 시 다음 흐름이 가능해야 한다.

1. 프로그램 실행
2. 3D 공간에 Box / Sphere / Plane 존재
3. Run 클릭
4. 중력 적용
5. 물체 낙하
6. 충돌
7. 반발
8. 마찰
9. 회전
10. 물체 선택
11. Inspector에서 Mass/Friction/Restitution 수정
12. Apply Force / Apply Impulse
13. Debug View 활성화
14. Contact Point / Normal / Velocity 표시
15. Pause
16. Frame Step
17. 충돌 과정을 프레임 단위 분석
18. Stress Test
19. 수백 개 Body 생성
20. Physics Time / Contact Count / FPS 확인

---

# 3. 프로젝트 구조

초기에는 과한 프로젝트 분리를 피한다.

권장 솔루션:

```text
Forge3D.sln

├─ Forge3D.Core
├─ Forge3D.Editor
└─ Forge3D.Tests
```

---

## 3.1 Forge3D.Core

```text
Forge3D.Core

├─ Mathematics
├─ Dynamics
├─ Collision
├─ Solver
├─ Constraints
└─ Diagnostics
```

역할:

- Physics 계산
- Collision Detection
- Collision Response
- RigidBody 관리
- Fixed Timestep
- Profiler 데이터 생성

중요:

> Forge3D.Core는 WPF를 참조하지 않는다.

---

## 3.2 Forge3D.Editor

WPF 프로젝트.

역할:

- 3D Viewport
- Scene Hierarchy
- Inspector
- Toolbar
- Debug Visualization
- Profiler
- Simulation Controls

---

## 3.3 Forge3D.Tests

테스트 대상:

- Collision Detection
- Vector/Math Utility
- Force / Impulse
- Fixed Timestep
- Contact 생성
- 기본 Solver

---

# 4. Physics Core 설계

# 4.1 Math

가능하면 `System.Numerics`를 적극 활용한다.

사용:

```text
Vector3
Quaternion
Matrix4x4
```

직접 구현을 고려할 수 있는 것:

```text
Matrix3
MathUtil
PhysicsMath
Transform
```

수학 라이브러리를 직접 만드는 것 자체는 목표가 아니다.

포트폴리오 핵심은 Physics/Solver다.

---

# 4.2 Transform

```text
Transform

Position : Vector3
Rotation : Quaternion
Scale    : Vector3
```

Physics에서는 Scale의 런타임 변경을 최소화한다.

---

# 4.3 RigidBody

필수 데이터:

```text
RigidBody

Position
Orientation

LinearVelocity
AngularVelocity

ForceAccumulator
TorqueAccumulator

Mass
InverseMass

LinearDamping
AngularDamping

IsStatic
IsSleeping
```

필수 메서드:

```text
ApplyForce()
ApplyTorque()
ApplyImpulse()
ClearForces()
Integrate()
```

---

# 4.4 자유도(DOF) Constraint

장기적으로 2D/3D 범용성을 확보하기 위한 기능.

```text
MotionConstraints

LockTranslationX
LockTranslationY
LockTranslationZ

LockRotationX
LockRotationY
LockRotationZ
```

예:

### Full 3D

```text
Translation: X Y Z
Rotation:    X Y Z
```

### 2D XY

```text
Translation: X Y
Rotation:    Z

Lock Translation Z
Lock Rotation X
Lock Rotation Y
```

이 설계는 추후 다음으로 확장 가능하다.

```text
Rail
Elevator
Turntable
Planar Simulation
Vehicle Constraint
```

단, **2주 내 필수 기능은 아니다.**

---

# 5. Fixed Timestep

Physics는 렌더링 FPS와 독립적으로 돌아가야 한다.

기준:

```text
Fixed Delta Time = 1 / 60 sec
```

개념:

```text
accumulator += frameDelta

while accumulator >= fixedDelta:
    PhysicsWorld.Step(fixedDelta)
    accumulator -= fixedDelta
```

면접 포인트:

- Variable timestep의 불안정성
- 렌더링 FPS와 Physics FPS 분리
- Deterministic simulation의 기반

---

# 6. Force Integration

초기 Integration 방식:

**Semi-Implicit Euler**

개념:

```text
acceleration = force * inverseMass

velocity += acceleration * dt
position += velocity * dt
```

회전:

```text
angularAcceleration
angularVelocity
Quaternion orientation
```

---

# 7. Collider

2주 기본 범위:

```text
SphereCollider
BoxCollider
PlaneCollider
```

공통 인터페이스 예시:

```text
Collider

Body
Material
Bounds
Type
```

---

# 8. Physics Material

```text
PhysicsMaterial

Friction
Restitution
```

Preset 예:

```text
Rubber
Friction:     0.8
Restitution:  0.9

Steel
Friction:     0.4
Restitution:  0.2

Ice
Friction:     0.05
Restitution:  0.1
```

Inspector에서 런타임 변경 가능하게 만든다.

---

# 9. Collision Detection

구현 순서:

1. Sphere ↔ Sphere
2. Sphere ↔ Plane
3. Box ↔ Plane
4. Sphere ↔ Box
5. Box ↔ Box

Box ↔ Box는 초기에는 AABB 방식으로 구현 가능하다.

OBB / SAT는 시간 남을 경우 추가한다.

---

# 10. Contact

충돌 결과는 Contact 구조로 통일한다.

```text
Contact

BodyA
BodyB

Point
Normal
Penetration

Restitution
Friction
```

PhysicsWorld는 충돌 단계에서 Contact 목록을 생성한다.

---

# 11. Collision Response

방식:

**Impulse Based Solver**

필수 처리:

```text
Normal Impulse
Restitution
Friction Impulse
Penetration Correction
```

---

# 12. Penetration Correction

물체가 바닥에 파묻히거나 떨리는 문제를 완화한다.

개념:

```text
correction =
max(penetration - slop, 0)
* percent
```

목표는 완벽한 안정성이 아니라 포트폴리오 시연에서 자연스럽게 보이는 수준이다.

---

# 13. Rotation

필수 요소:

```text
Quaternion Orientation
Angular Velocity
Torque
Inertia
Inverse Inertia
```

최소 성공 조건:

> Box의 중심이 아닌 위치에 Impulse를 적용하면 회전한다.

---

# 14. PhysicsWorld

핵심 API:

```text
PhysicsWorld

Bodies
Colliders
Contacts
Gravity

AddBody()
RemoveBody()

Step(dt)
```

개념적인 Step 흐름:

```text
1. Apply Gravity
2. Integrate Forces
3. Broad Phase
4. Narrow Phase
5. Generate Contacts
6. Solve Contacts
7. Integrate Position / Orientation
8. Clear Forces
```

구현 중 필요하면 순서는 조정한다.

---

# 15. Broad Phase

초기:

```text
Brute Force
O(n²)
```

2주 내 시간이 허용되면:

```text
AABB Broad Phase
```

Profiler에는 최소 다음 값을 표시한다.

```text
Body Count
Potential Pair Count
Contact Count
```

추후 확장:

```text
Spatial Hash
Sweep And Prune
BVH
```

---

# 16. WPF Editor 설계

기본 레이아웃:

```text
┌──────────────────────────────────────────┐
│ Toolbar                                  │
├──────────┬─────────────────────┬─────────┤
│Hierarchy │    3D Viewport       │Inspector│
│          │                      │         │
├──────────┴─────────────────────┴─────────┤
│ Profiler / Simulation Status             │
└──────────────────────────────────────────┘
```

---

# 17. Toolbar

필수:

```text
Run
Pause
Step
Reset

Add Box
Add Sphere

Debug View
Stress Test
```

추가 가능:

```text
Time Scale
Scene Preset
Planar Mode
```

---

# 18. Scene Hierarchy

예:

```text
World

├─ Ground
├─ Box_001
├─ Box_002
├─ Sphere_001
└─ Sphere_002
```

MVVM:

```text
SelectedObject
```

Hierarchy 선택 ↔ Viewport 선택 ↔ Inspector가 모두 동기화되어야 한다.

---

# 19. Inspector

표시/수정 대상:

```text
Transform

Position X/Y/Z
Rotation X/Y/Z

Physics

Mass
Friction
Restitution

Linear Velocity
Angular Velocity

Apply Force
Apply Impulse
```

실행 중 변경 가능해야 한다.

---

# 20. Rendering

2주 동안 자체 DirectX Renderer를 만들지 않는다.

원칙:

> Physics Core는 직접 구현하고, 렌더링은 검증된 3D 표시 수단을 사용한다.

목표는 Renderer 제작이 아니라 Simulation Engine이다.

Rendering Layer의 책임:

```text
Physics State
    ↓
Render Transform
    ↓
3D View
```

Physics Core에서 WPF 객체를 직접 다루지 않는다.

---

# 21. Picking / Selection

Viewport에서 물체 클릭 시 해당 Body가 선택된다.

선택 시:

- Hierarchy selection 변경
- Inspector 갱신
- 선택 Highlight 표시

가능하면 Raycast를 구현한다.

시간이 부족하면 WPF/Viewport HitTest를 활용해도 된다.

---

# 22. Debug Visualization

이 프로젝트의 핵심 WOW Point.

Toggle 항목:

```text
Velocity
Contact Point
Contact Normal
Bounding Box
Center Of Mass
Sleep State
```

2주 필수:

```text
Velocity
Contact Point
Contact Normal
```

예:

```text
       ┌──────────┐
       │   Box    │──────→ Velocity
       └────●─────┘
            ↑
          Normal
```

---

# 23. Simulation Control

필수:

```text
Run
Pause
Frame Step
Reset
```

Frame Step:

```text
1 Step = 1 Fixed Timestep
```

예:

```text
1 / 60 sec
```

이 기능으로 충돌 직전/직후 상태를 한 프레임씩 확인 가능하게 한다.

---

# 24. Profiler

실시간 표시:

```text
FPS
Bodies
Contacts
Potential Pairs

Physics Time
Collision Time
Solver Time
Render Time
```

초기에는 Stopwatch 기반으로 충분하다.

---

# 25. Stress Test

버튼:

```text
Spawn 100
Spawn 300
Spawn 500
```

성능이 충분하면:

```text
Spawn 1000
```

추가.

표시:

```text
Bodies
Potential Pairs
Contacts
Physics ms
FPS
```

---

# 26. Demo Scenes

## 26.1 Drop Test

```text
Sphere
   ↓

────────────
```

검증:

- Gravity
- Collision
- Restitution

---

## 26.2 Bounce Test

```text
○       ○       ○

0.1     0.5     0.9
```

각 공의 Restitution이 다르다.

---

## 26.3 Friction Test

```text
□
 ╲
  ╲
   ╲
```

다른 Friction 값의 물체를 경사면에서 비교한다.

---

## 26.4 Stack Test

```text
      □
     □□
    □□□
   □□□□
────────────
```

Solver 안정성을 보여준다.

---

## 26.5 Stress Test

수백 개 Box/Sphere 낙하.

시각적으로 가장 강한 Demo.

---

# 27. MUST / SHOULD / COULD

# MUST

2주 안에 반드시 완료.

```text
RigidBody

Gravity
Force
Impulse

Sphere
Box
Plane

Collision Detection
Impulse Solver

Friction
Restitution

Rotation
Quaternion

Fixed Timestep

WPF 3D View

Hierarchy
Inspector

Run
Pause
Step
Reset

Debug Contact Visualization

Profiler

Basic Demo Scenes
```

---

# SHOULD

MUST 완료 후 추가.

```text
Stress Test

Runtime Physics Editing

AABB Broad Phase

Scene Presets

Bounding Box Debug Draw

Selection Highlight

Time Scale
```

---

# COULD

시간이 남을 때만.

```text
DOF Constraints
2D Planar Mode

Distance Joint
Hinge Joint

Sleeping

Replay

Scene Save / Load

OBB / SAT

GJK
EPA

Spatial Hash
Sweep And Prune

Telemetry
Networking
```

---

# 28. 2D / 3D 범용 모드

3D 엔진에서 축을 제한해 2D처럼 사용할 수 있다.

예: XY Mode

```text
Translation:
X = Enabled
Y = Enabled
Z = Locked

Rotation:
X = Locked
Y = Locked
Z = Enabled
```

UI 예:

```text
Simulation Mode

3D
2D - XY
2D - XZ
2D - YZ
```

또는 Body 단위 DOF Inspector:

```text
Translation
X ☑
Y ☑
Z ☐

Rotation
X ☐
Y ☐
Z ☑
```

이 기능은 2주 이후 확장 기능으로 둔다.

---

# 29. 14일 개발 계획

# Day 1 — 프로젝트 골격

구현:

```text
Solution
Core
Editor
Tests

Transform
RigidBody
PhysicsWorld
```

목표:

- 프로젝트 구조 확정
- Core에서 WPF 참조 없음
- Body 생성 가능

완료 조건:

```text
body.Position.Y
```

값을 코드에서 변경/확인 가능.

---

# Day 2 — Gravity / Force / Fixed Timestep

구현:

```text
Gravity
Force
Impulse
Velocity
Damping
FixedStepRunner
```

테스트:

```text
ApplyForce
ApplyImpulse
Gravity
```

완료 조건:

> Body에 impulse를 주면 포물선 운동을 한다.

---

# Day 3 — Sphere Collision

구현:

```text
SphereCollider
PlaneCollider

Sphere-Sphere
Sphere-Plane

Contact
```

완료 조건:

충돌 시 다음 정보가 정확히 생성된다.

```text
Point
Normal
Penetration
```

---

# Day 4 — Impulse Solver

구현:

```text
Normal Impulse
Restitution
Position Correction
```

완료 조건:

> 공이 바닥에서 튕긴다.

첫 번째 주요 Milestone.

---

# Day 5 — Friction / Box

구현:

```text
Friction Impulse

BoxCollider
Box-Plane
Sphere-Box
```

완료 조건:

> Box가 바닥에 떨어지고 옆으로 밀면 마찰을 받으면서 미끄러진다.

---

# Day 6 — Rotation

구현:

```text
Angular Velocity
Torque
Quaternion Integration
Inertia
```

완료 조건:

> Box의 모서리에 Impulse를 가하면 회전한다.

---

# Day 7 — WPF 3D Viewer

구현:

```text
Viewport
Camera
Ground
Box Render
Sphere Render
Physics → Render Sync
```

완료 조건:

> WPF 화면에서 물리 시뮬레이션이 보인다.

Week 1 Milestone.

---

# Day 8 — Editor UI

구현:

```text
Toolbar
Hierarchy
Inspector
SelectedObject
```

완료 조건:

> 객체 선택 시 Inspector가 갱신된다.

---

# Day 9 — Runtime Editing

구현:

```text
Mass
Friction
Restitution

Apply Force
Apply Impulse
```

완료 조건:

> Simulation 실행 중 값을 바꾸면 동작이 즉시 달라진다.

---

# Day 10 — Debug Visualization

구현:

```text
Velocity Vector
Contact Point
Contact Normal
```

완료 조건:

> Debug View에서 Physics 내부 상태가 시각적으로 표시된다.

---

# Day 11 — Pause / Step / Reset

구현:

```text
Run
Pause
Step
Reset
Time Scale
```

완료 조건:

> 충돌 직전에 Pause하고 한 프레임씩 진행 가능.

---

# Day 12 — Profiler / Stress Test

구현:

```text
Physics ms
Collision ms
Solver ms
FPS

Bodies
Contacts
Potential Pairs

Spawn 100
Spawn 300
Spawn 500
```

완료 조건:

> 수백 개 Body 테스트와 실시간 성능 수치 확인.

---

# Day 13 — Demo + Polish

새로운 대형 기능을 추가하지 않는다.

작업:

```text
Bounce Demo
Friction Demo
Stack Demo
Stress Demo

Camera
Formatting
Naming
Exception Handling
Reset Stability
UI Polish
```

---

# Day 14 — Portfolio Packaging

개발 30%
문서 70%

작성:

```text
README
Architecture Diagram
Physics Pipeline
Screenshots
Demo GIF
Demo Video
Performance Results
Trade-offs
Future Work
```

---

# 30. 포트폴리오 Demo 영상

목표 길이:

```text
60 ~ 90 seconds
```

---

## 0 ~ 10초

Stress Test.

수백 개 Box 낙하.

화면:

```text
Bodies: 500
FPS: 60
Physics: 3.8 ms
```

첫 화면에서 WOW.

---

## 10 ~ 25초

물체 선택.

Inspector에서 Restitution 변경.

공 튀는 높이가 즉시 달라짐.

---

## 25 ~ 40초

Box에 Impulse 적용.

Box가 이동하면서 회전.

---

## 40 ~ 55초

Debug View.

표시:

```text
Velocity
Contact Point
Normal
```

---

## 55 ~ 70초

Pause.

Frame Step.

충돌 한 프레임씩 분석.

---

## 70 ~ 90초

Architecture Diagram.

표시:

```text
Physics Core
    ↓
Simulation
    ↓
WPF Visualization
```

마무리.

---

# 31. 포트폴리오 설명

GitHub 제목:

> Forge3D — Real-Time 3D Physics & Engineering Simulation Framework

한글 설명:

> C# 기반 3D 강체 물리 시뮬레이션 엔진 및 WPF 실시간 디버깅/시각화 도구

영문 설명:

> A C# rigid-body physics simulation engine with a WPF-based real-time visualization, debugging, and profiling environment.

---

# 32. 지원 직무별 포지셔닝

## 방산

타깃 예:

```text
한화시스템
LIG넥스원
한화에어로스페이스
현대로템
```

강조:

```text
Simulation
Real-time
3D
WPF
HMI
Debugging
Fixed Timestep
Telemetry-ready Architecture
```

---

## 반도체 / 산업 자동화

타깃 예:

```text
세메스
SFA
고영테크놀러지
AP시스템
유진테크
```

강조:

```text
Engineering Tool
Equipment Simulation
Digital Twin
WPF/MVVM
Performance
Motion
Profiler
```

---

## 의료기기 / 의료로봇

타깃 예:

```text
삼성메디슨
큐렉소
오스템임플란트
```

강조:

```text
3D
C#
WPF
Real-time visualization
Math
Quaternion
Parallel-ready Architecture
```

---

## 현대 그룹 제조 / 비전 / 디지털트윈

타깃 예:

```text
현대오토에버
현대로템
```

강조:

```text
3D Simulation
Digital Twin
Equipment
Robot Motion
WPF Tool
Real-time State
```

---

# 33. 면접 핵심 질문 대비

반드시 답변 준비.

## Q1. 왜 Fixed Timestep인가?

핵심:

- Renderer FPS와 Physics 분리
- 시뮬레이션 안정성
- 동일한 dt 기반 계산
- Frame rate dependency 제거

---

## Q2. 왜 Physics Core와 WPF를 분리했는가?

핵심:

- UI Framework Dependency 제거
- Unit Test 가능
- 다른 Renderer로 교체 가능
- 재사용성
- 유지보수성

---

## Q3. Collision Detection과 Response 차이는?

Detection:

```text
충돌 여부
Contact Point
Normal
Penetration
```

Response:

```text
Impulse
Restitution
Friction
Velocity Correction
```

---

## Q4. Quaternion을 왜 사용했는가?

핵심:

- 3D Orientation
- Euler 기반 표현의 문제
- Gimbal Lock 회피
- Rotation composition
- Interpolation 확장 가능

---

## Q5. 성능은 어떻게 측정했는가?

핵심:

```text
Stopwatch
Broad Phase
Narrow Phase
Solver
Total Physics
FPS
```

수치 기반 비교.

---

# 34. 하지 말아야 할 것

MUST 완료 전 금지.

```text
Mesh-Mesh Collision

Full GJK/EPA

Continuous Collision Detection

Soft Body

Fluid

Cloth

Vehicle Physics

Photorealistic Rendering

Custom DirectX Engine

Multiplayer

Networking

Replay

Scene Serialization

Plugin System

Scripting

ECS
```

기능 자체가 나쁜 것이 아니라, **2주 프로젝트를 완성하지 못하게 만드는 범위 폭발 요소**이기 때문이다.

---

# 35. MUST 완료 후 확장 로드맵

2주 안에 기본 목표를 완료했다면 아래 순서로 추가한다.

---

## Phase 1 — Physics Quality

```text
OBB Collision
SAT

Improved Contact Solver
Multiple Contacts

Sleeping

Better Friction
```

---

## Phase 2 — Constraints

```text
DOF Constraint
Planar 2D Mode

Distance Joint
Hinge Joint
Ball Joint
```

---

## Phase 3 — Collision Algorithms

```text
Convex Hull
GJK
EPA
```

GJK/EPA는 Debug Visualization과 함께 구현하면 포트폴리오 가치가 매우 높다.

---

## Phase 4 — Performance

```text
Spatial Hash
Sweep And Prune
BVH

Island Solver
Parallel Broad Phase
```

Profiler 비교:

```text
Before
vs
After
```

---

## Phase 5 — Engineering Features

```text
Replay
Timeline
Scene Save/Load
Telemetry
TCP/UDP
Binary Protocol

Sensor Simulation
Robot / Mechanism Demo
```

---

## Phase 6 — Digital Twin

최종적으로 확장 가능.

```text
Physics Engine
    ↓
3D Equipment Simulation
    ↓
Sensor / Telemetry
    ↓
WPF Engineering Tool
```

적용 예:

```text
Defense Simulation

Robot Simulation

Semiconductor Equipment

Medical Robot

Industrial Digital Twin
```

---

# 36. 최종 목표 아키텍처

장기적으로:

```text
                 Forge3D

┌──────────────────────────────────┐
│           WPF Editor             │
│                                  │
│ Hierarchy │ Viewport │ Inspector │
│ Debug     │ Profiler │ Timeline  │
└────────────────┬─────────────────┘
                 │
                 ▼
┌──────────────────────────────────┐
│       Visualization Layer        │
└────────────────┬─────────────────┘
                 │
                 ▼
┌──────────────────────────────────┐
│         Physics World            │
│                                  │
│ RigidBody                        │
│ Collider                         │
│ Collision                       │
│ Solver                           │
│ Constraints                      │
└────────────────┬─────────────────┘
                 │
                 ▼
┌──────────────────────────────────┐
│       Math / Diagnostics         │
└──────────────────────────────────┘
```

---

# 37. Definition of Done — 2주 완료 기준

아래 항목이 모두 충족되면 2주 프로젝트는 성공으로 판단한다.

- [ ] Core가 WPF를 참조하지 않는다.
- [ ] Sphere가 중력으로 낙하한다.
- [ ] Sphere가 Plane과 충돌한다.
- [ ] Sphere가 튕긴다.
- [ ] Friction이 적용된다.
- [ ] Box가 존재한다.
- [ ] Box가 충돌한다.
- [ ] Box가 회전한다.
- [ ] Fixed Timestep이 적용되어 있다.
- [ ] WPF에서 Physics World가 렌더링된다.
- [ ] 객체 선택이 가능하다.
- [ ] Inspector가 동작한다.
- [ ] 런타임 Physics 값 수정이 가능하다.
- [ ] Apply Force 또는 Impulse가 가능하다.
- [ ] Pause가 가능하다.
- [ ] Frame Step이 가능하다.
- [ ] Contact Point가 보인다.
- [ ] Contact Normal이 보인다.
- [ ] Velocity Vector가 보인다.
- [ ] Profiler 수치가 보인다.
- [ ] Stress Test가 동작한다.
- [ ] Demo Scene 3개 이상이 있다.
- [ ] README가 작성되어 있다.
- [ ] Architecture Diagram이 있다.
- [ ] Demo GIF 또는 영상이 있다.

---

# 38. Codex 작업 원칙

Codex 작업 시 다음 원칙을 유지한다.

## Architecture

- Core에서 UI Framework 참조 금지
- 책임별 클래스를 작게 유지
- Global State 최소화
- PhysicsWorld를 중심으로 관리
- 렌더링 데이터와 물리 데이터를 분리

## Development

- 한 기능 구현 후 반드시 테스트
- 큰 기능을 동시에 여러 개 시작하지 않기
- 매일 실행 가능한 상태 유지
- Day 종료 조건을 만족하기 전 다음 Day 범위로 넘어가지 않기

## Git

권장 커밋 단위:

```text
feat: add rigid body integration

feat: implement sphere-plane collision

feat: add impulse-based collision response

feat: add WPF physics viewport

feat: add contact debug visualization

perf: add physics profiler
```

---

# 39. 첫 작업

가장 먼저 생성할 클래스 후보:

```text
Forge3D.Core

Transform.cs
RigidBody.cs
PhysicsWorld.cs
PhysicsMaterial.cs
PhysicsSettings.cs

Forge3D.Tests

RigidBodyTests.cs
PhysicsWorldTests.cs
```

첫 번째 Milestone:

> PhysicsWorld에 RigidBody 하나를 추가하고 Fixed Timestep으로 중력을 적용했을 때, 시간에 따라 Body가 정상적으로 낙하하는 것을 테스트 코드로 검증한다.

그 다음:

```text
SphereCollider
PlaneCollider
Contact
CollisionDispatcher
```

순으로 진행한다.

---

# 40. 프로젝트 성공 기준

이 프로젝트의 목적은 Unity/PhysX/Bullet을 대체하는 엔진을 만드는 것이 아니다.

성공 기준은 다음이다.

> 물리 엔진의 핵심 구조와 알고리즘을 이해하고 직접 구현했으며, 실시간으로 그 내부 상태를 시각화·분석할 수 있는 엔지니어링 도구를 완성했다.

그리고 기술 면접에서 다음을 실제 구현을 근거로 설명할 수 있어야 한다.

```text
C#
WPF
MVVM
3D Math
Quaternion
Rigid Body Dynamics
Collision Detection
Impulse Solver
Fixed Timestep
Performance Profiling
Software Architecture
Testing
```

이것이 Forge3D의 핵심 목표다.
