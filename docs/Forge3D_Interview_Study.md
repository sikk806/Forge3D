# Forge3D 면접 / 자소서 학습 정리

이 문서는 Forge3D 프로젝트에서 지금까지 구현한 기능을 면접, 포트폴리오, 자기소개서에서 설명할 수 있도록 정리한 학습 자료입니다.

핵심 포인트는 단순히 "WPF로 3D 화면을 만들었다"가 아니라, **C#으로 직접 3D 물리 엔진의 핵심 파이프라인을 구현하고, 이를 실시간으로 분석할 수 있는 엔지니어링 도구까지 설계했다**는 점입니다.

---

## 1. 프로젝트 한 줄 소개

Forge3D는 C#과 WPF로 만든 실시간 3D 강체 물리 시뮬레이션 엔진 및 엔지니어링 에디터입니다.

직접 구현한 물리 코어에서 강체, 충돌 판정, 충돌 반응, 마찰, 반발, 고정 시간 간격 시뮬레이션을 처리하고, WPF 3D Viewport에서 이를 실시간으로 시각화, 조작, 디버깅합니다.

면접용 한 문장:

> C#으로 3D Rigid Body Physics Core를 직접 구현하고, WPF 기반 Editor에서 실시간 시뮬레이션, 디버그 시각화, 차량 경로 추종, 센서/안전/고장 시나리오까지 관찰할 수 있게 만든 프로젝트입니다.

---

## 2. 전체 구조

```text
Forge3D

├─ Forge3D.Core
│  ├─ Dynamics        물리 바디, 월드, fixed step
│  ├─ Collision       collider, broad phase, narrow phase
│  ├─ Solver          impulse solver
│  ├─ Navigation      A*, Hybrid A*, path following
│  ├─ Simulation      vehicle, mission, sensor, safety, fault
│  ├─ Data            CSV/JSON import, schema detection
│  └─ Diagnostics     physics profiling stats
│
├─ Forge3D.Editor
│  ├─ ViewModels      MVVM 상태/명령
│  ├─ Rendering       scene renderer, debug renderer, render loop
│  ├─ Input           camera input/controller
│  └─ XAML            WPF UI
│
└─ Forge3D.Tests
   ├─ Dynamics
   ├─ Collision
   ├─ Navigation
   ├─ Simulation
   └─ Data
```

가장 중요한 설계 원칙:

> `Forge3D.Core`는 WPF를 모릅니다. 물리/시뮬레이션 로직은 UI와 분리되어 있고, `Forge3D.Editor`는 Core의 상태를 보여주고 조작하는 계층입니다.

이 구조 덕분에 Core는 단위 테스트가 가능하고, 나중에 WPF가 아닌 다른 렌더러로 교체할 수도 있습니다.

---

## 3. 구현한 핵심 기능 목록

### Physics Core

- `RigidBody` 기반 강체 상태 관리
- 위치, 회전, 선형 속도, 각속도
- Force, Torque, Impulse 적용
- 중심이 아닌 지점에 충격량 적용 시 회전 발생
- Quaternion 기반 3D 회전 적분
- 질량, 역질량, 관성, 역관성
- 선형 감쇠, 각 감쇠
- 고정 물체와 동적 물체 구분
- XZ 평면 차량용 motion constraint

### Collision

- `SphereCollider`
- `BoxCollider`
- `PlaneCollider`
- Sphere-Sphere 충돌
- Sphere-Plane 충돌
- Sphere-Box 충돌
- Box-Plane 충돌
- Box-Box SAT 충돌
- AABB 기반 broad phase
- Contact point, normal, penetration 계산

### Solver

- Impulse 기반 충돌 반응
- 반발 계수 restitution
- 마찰 friction
- penetration correction
- contact point 기반 angular impulse

### Simulation Loop

- `FixedStepRunner`
- 고정 timestep 기반 physics update
- frame delta 누적 accumulator
- 최대 substep 제한
- physics step profiling

### Rendering / Smoothness

- WPF `Viewport3D` 기반 렌더링
- `CompositionTarget.Rendering` 기반 render loop
- physics update와 render update 분리
- `PhysicsPose` 기반 previous/current pose 저장
- render interpolation 적용
- mesh/material 재생성 최소화
- `SceneRenderer`, `SceneVisual`로 visual transform 갱신 분리
- gizmo transform 재사용
- debug visual 갱신 빈도 제한

### Editor UI

- Scene Hierarchy
- Inspector
- Transform 편집
- Physics material 편집
- Velocity 편집
- Force/Torque/Impulse 적용
- Run/Pause/Step/Reset
- Drop/Bounce/Friction/Stack/Stress demo
- Profiler
- Telemetry graph
- Replay timeline
- 다크 테마
- 한국어/영어 UI 표시
- 단축키 표시

### Interaction

- Viewport object selection
- 선택된 도형 drag 이동
- local X/Y/Z gizmo handle 이동
- Ctrl snap 이동
- Unity 유사 camera orbit/pan/zoom
- 우클릭 + WASD/QE camera movement
- Shift fast movement
- Ctrl precise movement
- camera target/current state smoothing

### Navigation / Vehicle

- Grid A*
- Hybrid A*
- Holonomic / Car-like mobility model
- planner selector
- path collision checking
- path simplifier
- path visualization
- waypoint mission
- look-ahead path following
- target speed smoothing
- turn torque smoothing
- heading error 기반 자동 감속

### Engineering Scenario

- Autonomous Vehicle Safety Test
- Vehicle entity
- Waypoint entity
- Sensor entity
- range/FOV detection
- Safety evaluator
- warning/critical state
- TTC, Time To Collision
- automatic emergency stop
- manual emergency stop
- fault injection
- sensor failure
- wheel slip
- motor degradation
- communication loss
- event/alarm log

### Data Layer

- CSV parser
- JSON parser
- schema detection
- data validation
- capability detection
- imported obstacle generation

---

## 4. 물리 엔진 흐름 설명

Forge3D의 physics step은 다음 순서로 진행됩니다.

```text
1. 이전 pose 저장
2. force / torque 적분
3. broad phase collision candidate 탐색
4. narrow phase contact 생성
5. impulse solver로 충돌 반응 계산
6. transform 적분
7. force / torque accumulator 초기화
8. profiler stats 저장
```

면접에서 설명할 때:

> 물리 엔진을 단순 위치 업데이트가 아니라 force accumulation, velocity integration, collision detection, impulse solving, transform integration 단계로 나눠 구현했습니다. 각 단계의 책임을 분리했고, profiler stats를 통해 broad phase, narrow phase, solver 시간을 따로 확인할 수 있게 했습니다.

---

## 5. Fixed Timestep을 사용한 이유

렌더링 FPS에 따라 물리 결과가 달라지면 시뮬레이션이 불안정해집니다.

그래서 Forge3D는 `FixedStepRunner`에서 frame delta를 accumulator에 쌓고, 일정 시간 간격마다 physics step을 실행합니다.

```text
Frame Delta
    ↓
Accumulator
    ↓
Fixed Physics Step
```

장점:

- FPS가 흔들려도 물리 계산 간격이 일정함
- 충돌 반응이 더 예측 가능함
- Pause/Step 디버깅이 쉬움
- 테스트 재현성이 좋아짐

면접 답변:

> 실시간 렌더링 환경에서는 frame delta가 매번 달라질 수 있기 때문에, 물리를 variable delta로 직접 적분하면 결과가 흔들릴 수 있습니다. 그래서 fixed timestep과 accumulator를 사용해 physics update는 일정 간격으로 수행하고, rendering은 별도로 보간하도록 분리했습니다.

---

## 6. Rendering Smoothness 개선

초기에는 simulation tick 이후 곧바로 WPF visual transform을 갱신했습니다.

문제:

- physics step 사이에서 화면이 끊겨 보임
- object transform이 fixed step 단위로 jump함
- camera input도 즉시 반영되어 뻣뻣함
- material/transform/gizmo/debug visual 재생성이 많음

개선:

- `RenderLoop` 추가
- `CompositionTarget.Rendering` 사용
- `PhysicsPose`로 previous/current pose 저장
- render 시점에 interpolation alpha 계산
- `Vector3.Lerp`, `Quaternion.Slerp` 사용
- 실제 physics state는 변경하지 않고 visual만 보간

핵심 구조:

```text
Physics
PreviousPose → CurrentPose

Render
Interpolate(PreviousPose, CurrentPose, alpha)
```

면접 답변:

> Physics state를 직접 보간하지 않고, 렌더링용 pose만 따로 계산했습니다. 실제 시뮬레이션 state는 authoritative하게 유지하고, WPF visual transform만 interpolation pose를 사용하도록 분리해 물리 정확도와 시각적 부드러움을 동시에 유지했습니다.

---

## 7. Scene Visual 재사용

초기에는 sync 과정에서 material과 transform 객체가 자주 새로 만들어졌습니다.

개선 후:

- `SceneRenderer`가 scene visual 생성 담당
- `SceneVisual`이 `GeometryModel3D`, `ModelUIElement3D`, transform을 보관
- geometry와 material은 가능한 한 재사용
- 매 render frame에는 position/rotation transform 값만 갱신
- 선택 상태가 바뀔 때만 material 갱신

면접 답변:

> WPF 3D는 객체 생성 비용과 GC 영향을 받기 쉬워서, 매 프레임 mesh나 material을 다시 만드는 구조를 피했습니다. scene object 생성 시 visual을 캐싱하고, render loop에서는 transform 값만 갱신하도록 분리했습니다.

---

## 8. Camera 조작 개선

초기 camera는 input delta가 곧바로 yaw/pitch/distance에 반영되었습니다.

문제:

- 마우스 작은 움직임에도 딱딱하게 반응
- wheel zoom이 단계적으로 튐
- WASD 이동이 KeyDown repeat에 의존

개선:

- target state와 current state 분리
- mouse input은 target yaw/pitch/distance/focus만 변경
- render loop에서 current state가 exponential smoothing으로 target을 따라감
- WASD/QE는 key state를 저장하고 deltaTime 기반 이동

구조:

```text
Input
TargetYaw / TargetPitch / TargetDistance / TargetFocus 변경

Render
CurrentYaw / CurrentPitch / CurrentDistance / CurrentFocus smoothing
```

면접 답변:

> 카메라 입력을 즉시 camera transform에 반영하지 않고 target state만 변경하도록 만들었습니다. 렌더 프레임에서 deltaTime 기반 exponential smoothing을 적용해 FPS에 덜 의존하는 자연스러운 orbit, pan, zoom, WASD 이동을 만들었습니다.

---

## 9. Vehicle 움직임 개선

차량이 waypoint를 따라갈 때 뻣뻣하거나 경로를 지나치는 문제가 있었습니다.

원인:

- target heading이 즉시 바뀜
- target speed가 즉시 적용됨
- heading error가 커도 계속 빠르게 전진
- torque 상한에 걸리면 회전보다 직진이 먼저 발생
- path point 하나만 직접 바라봄

개선:

- `CommandedSpeed` 추가
- target speed를 acceleration/deceleration limit으로 추종
- `CommandedTurnTorque` 추가
- turn torque rate 제한
- heading error가 클수록 자동 감속
- max torque와 heading gain 조정
- look-ahead 기반 waypoint/path following

면접 답변:

> 차량을 직접 위치 이동시키지 않고, 여전히 force와 torque 기반으로 움직이게 유지했습니다. 대신 desired speed와 commanded speed를 분리해 acceleration limit을 적용했고, heading error가 큰 상황에서는 자동으로 감속하도록 만들어 코너에서 경로를 지나치는 현상을 줄였습니다.

---

## 10. Path Following / Look-Ahead

초기 path following은 현재 target point 하나를 직접 바라보는 방식이었습니다.

문제:

- waypoint가 바뀌는 순간 heading이 급변
- Grid A* 경로의 계단형 point를 그대로 따라가면 차량 움직임이 뻣뻣함
- 지나간 point를 다시 바라보는 상황 가능

개선:

- `PathFollower.LookAheadDistance`
- `LookAheadSpeedFactor`
- 현재 위치에서 경로를 따라 일정 거리 앞 point를 target으로 선택
- 속도가 빠를수록 더 앞을 봄
- `PathSimplifier`로 직선 중간점 제거

면접 답변:

> 차량이 현재 waypoint 하나만 바라보면 코너에서 목표 방향이 순간적으로 바뀝니다. 그래서 현재 위치에서 경로를 따라 일정 거리 앞의 look-ahead target을 계산하도록 바꿨고, Grid A*의 직선 구간 중간점은 제거해 계단식 추종을 줄였습니다.

---

## 11. Collision 설명 포인트

Forge3D의 충돌 처리는 두 단계로 나눕니다.

```text
Broad Phase
가능성이 있는 collider pair를 빠르게 찾음

Narrow Phase
실제 충돌 여부와 contact 정보를 계산
```

Broad phase:

- AABB 기반 pair filtering
- 모든 collider 조합을 바로 정밀 검사하지 않음

Narrow phase:

- Sphere-Sphere
- Sphere-Plane
- Sphere-Box
- Box-Plane
- Box-Box SAT

SAT 설명:

> Box-Box 충돌에서는 Separating Axis Theorem을 사용했습니다. 두 박스가 분리되는 축이 하나라도 있으면 충돌하지 않은 것으로 판단하고, 모든 후보 축에서 overlap이 있으면 최소 침투 축을 contact normal로 사용합니다.

---

## 12. Impulse Solver 설명 포인트

Impulse solver는 충돌 순간 속도를 바꾸는 방식입니다.

처리 항목:

- 충돌 법선 방향 impulse
- restitution에 따른 튕김
- tangent 방향 friction impulse
- penetration correction
- contact point 기준 angular velocity 변화

면접 답변:

> 충돌 반응은 위치를 강제로 밀어내는 방식만 쓰지 않고, 상대 속도와 contact normal을 기준으로 impulse를 계산했습니다. restitution은 normal impulse에 반영하고, friction은 tangent 방향 impulse로 처리했습니다. 또한 contact point가 center of mass에서 떨어져 있으면 angular impulse도 적용했습니다.

---

## 13. Sensor / Safety / Fault 시나리오

Engineering scenario는 단순 물리 데모에서 한 단계 확장된 시뮬레이션입니다.

구성:

- Vehicle
- Waypoint Mission
- Range/FOV Sensor
- Safety Evaluator
- Fault Manager
- Event Log

센서:

- 차량 위치와 방향 기준으로 target 감지
- range 제한
- FOV 제한
- 감지 거리와 상대 bearing 계산

안전:

- 감지 결과 기반 Warning/Critical 판단
- Time To Collision 계산
- Critical 상황에서 automatic emergency stop

고장:

- Sensor Failure
- Wheel Slip
- Motor Degradation
- Communication Loss

면접 답변:

> 단순히 물체를 떨어뜨리는 데모를 넘어서, 차량-센서-안전-고장이라는 엔지니어링 시나리오를 구성했습니다. SensorEntity가 주변 장애물을 감지하고, SafetyEvaluator가 위험도를 판단하며, Critical 상태에서는 자동 비상정지를 발생시킵니다. FaultManager는 센서 고장, 휠 미끄럼, 모터 저하 같은 상태 변화를 주입해 시스템 반응을 볼 수 있게 합니다.

---

## 14. Data Layer 설명 포인트

데이터 레이어는 외부 CSV/JSON을 읽어 시뮬레이션 환경으로 변환하는 역할입니다.

구성:

- `CsvDataParser`
- `JsonDataParser`
- `SchemaDetector`
- `DataValidator`
- `CapabilityDetector`
- `EnvironmentBuilder`

흐름:

```text
CSV/JSON
  ↓
ParsedDataSet
  ↓
Schema Detection
  ↓
Validation
  ↓
Capability Detection
  ↓
Obstacle Generation
```

면접 답변:

> 외부 데이터를 바로 시뮬레이션에 넣지 않고, parsing, schema detection, validation, environment building 단계를 나눴습니다. 이를 통해 입력 포맷과 시뮬레이션 객체 생성을 분리했고, 잘못된 데이터나 필드 누락을 검사할 수 있게 했습니다.

---

## 15. WPF / MVVM 설명 포인트

Editor는 WPF와 MVVM 스타일로 구성되어 있습니다.

역할:

- `MainViewModel`: 시뮬레이션 상태, 명령, UI 표시 값
- `SceneObjectViewModel`: 선택된 physics body 편집
- `EntityViewModel`: simulation entity 표시
- `MainWindow.xaml`: UI layout
- `MainWindow.xaml.cs`: WPF event forwarding, viewport interaction
- `SceneRenderer`: visual 생성/갱신
- `CameraController`: camera state 계산

면접 답변:

> ViewModel에는 시뮬레이션 명령과 바인딩 상태를 두고, WPF 3D visual 생성이나 camera smoothing 같은 렌더링 계산은 별도 클래스로 분리했습니다. 처음에는 code-behind에 많은 책임이 있었지만, 이후 RenderLoop, SceneRenderer, CameraController로 책임을 나눠 유지보수성을 개선했습니다.

---

## 16. 테스트 전략

Forge3D는 UI 자체보다 Core 로직을 중심으로 테스트합니다.

테스트 범위:

- force/impulse 적용
- torque와 angular velocity
- fixed timestep
- collider bounds
- collision generation
- impulse solver
- physics pose interpolation
- A* path planning
- Hybrid A* path planning
- path following look-ahead
- path simplification
- vehicle acceleration limiting
- sensor detection
- safety evaluation
- fault injection
- replay service
- data parsing/validation

면접 답변:

> WPF rendering event 자체는 단위 테스트하기 어렵기 때문에, 테스트 가능한 순수 로직을 Core로 분리했습니다. 물리 적분, 충돌, 경로 탐색, 차량 제어, 센서/안전/고장 로직은 단위 테스트로 검증하고, WPF는 빌드 및 실행 확인으로 검증했습니다.

---

## 17. 성능 개선 설명 포인트

개선 전 문제:

- simulation update와 render update 결합
- fixed step마다 visual transform jump
- material/transform/gizmo/debug visual 재생성
- debug visual 매번 clear/rebuild
- key repeat 기반 camera movement

개선 후:

- `CompositionTarget.Rendering` 기반 render loop
- physics interpolation
- scene visual cache
- transform만 갱신
- debug 갱신 빈도 제한
- camera deltaTime smoothing
- vehicle command smoothing

면접 답변:

> 성능 최적화는 알고리즘만의 문제가 아니라 프레임마다 어떤 객체를 생성하는지도 중요합니다. WPF 3D에서는 mesh나 material을 계속 새로 만들면 GC와 UI thread 부담이 커지기 때문에, scene visual을 재사용하고 transform만 갱신하도록 변경했습니다.

---

## 18. 자기소개서에 쓸 수 있는 문장

### 버전 1: 기술 중심

> C# 기반 3D 강체 물리 시뮬레이션 엔진 Forge3D를 개발하며, RigidBody 동역학, Quaternion 회전, Collider 기반 충돌 판정, Impulse Solver, Fixed Timestep Simulation Loop를 직접 구현했습니다. 또한 WPF 기반 Editor를 구성해 물리 상태를 실시간으로 시각화하고, Inspector, Debug Visualization, Profiler, Replay, 차량 경로 추종 및 센서 안전 시나리오를 통해 엔진 내부 상태를 분석할 수 있도록 설계했습니다.

### 버전 2: 문제 해결 중심

> 초기 구현에서는 시뮬레이션 tick과 렌더링 갱신이 결합되어 화면 움직임이 뻣뻣하게 보이는 문제가 있었습니다. 이를 해결하기 위해 Physics state와 Render state를 분리하고, previous/current pose 기반 interpolation, CompositionTarget.Rendering 기반 render loop, camera smoothing, vehicle command smoothing을 적용했습니다. 이 과정에서 단순히 겉보기만 부드럽게 만드는 것이 아니라, 물리 정확도를 유지하면서 렌더링 계층만 보간하도록 구조를 개선했습니다.

### 버전 3: 아키텍처 중심

> Forge3D에서는 Core와 Editor의 책임을 명확히 분리했습니다. Core는 WPF에 의존하지 않는 순수 물리/시뮬레이션 라이브러리로 유지하고, Editor는 이를 시각화하고 조작하는 UI 계층으로 설계했습니다. 이후 RenderLoop, SceneRenderer, CameraController, VehicleController, PathFollower 등으로 책임을 세분화해 기능 확장과 테스트가 가능한 구조를 만들었습니다.

### 버전 4: 포트폴리오 짧은 소개

> Forge3D는 C#으로 직접 구현한 3D 물리 엔진과 WPF 기반 엔지니어링 에디터입니다. 강체 동역학, 충돌 판정/반응, 고정 timestep, 경로 탐색, 차량 제어, 센서 안전 시나리오, 디버그 시각화와 성능 프로파일링을 포함합니다.

---

## 19. 면접 예상 질문과 답변

### Q1. 왜 물리 엔진을 직접 구현했나요?

직접 구현하면서 게임/시뮬레이션 엔진 내부의 기본 구조를 이해하고 싶었습니다. 특히 force, impulse, collision detection, solver, fixed timestep 같은 핵심 개념을 라이브러리 사용이 아니라 코드 레벨에서 검증하는 것이 목표였습니다.

### Q2. Fixed timestep을 쓴 이유는 무엇인가요?

렌더링 FPS는 환경에 따라 달라지기 때문에 variable delta를 그대로 물리에 쓰면 결과가 흔들릴 수 있습니다. Fixed timestep은 일정한 간격으로 물리를 계산해 안정성과 재현성을 높입니다.

### Q3. Rendering interpolation은 왜 필요한가요?

Physics는 60Hz로 고정되어 있어도 모니터 렌더링은 60Hz, 120Hz 등 다양할 수 있습니다. fixed step 결과를 그대로 화면에 반영하면 물체가 step 단위로 끊겨 보이기 때문에, previous/current pose 사이를 보간해 시각적으로 부드럽게 만들었습니다.

### Q4. Physics state를 직접 보간하지 않은 이유는 무엇인가요?

물리 state를 보간해버리면 simulation 결과 자체가 변할 수 있습니다. 그래서 authoritative physics state는 그대로 두고, renderer가 사용하는 visual pose만 보간했습니다.

### Q5. Quaternion을 사용한 이유는 무엇인가요?

3D 회전에서 Euler angle은 gimbal lock 문제가 있고, 회전 누적에도 불리합니다. 내부 simulation state는 Quaternion으로 관리하고, Inspector 표시처럼 사람이 읽는 영역에서만 Euler angle로 변환했습니다.

### Q6. Box-Box 충돌은 어떻게 구현했나요?

SAT를 사용했습니다. 두 박스의 후보 축을 검사해서 분리 축이 하나라도 있으면 충돌하지 않은 것으로 판단하고, 모든 축에서 overlap이 있으면 최소 침투 축을 normal로 사용합니다.

### Q7. Impulse solver는 어떤 방식인가요?

contact normal과 상대 속도를 기반으로 normal impulse를 계산하고, restitution을 반영해 튕김을 처리합니다. tangent 방향으로는 friction impulse를 적용하고, penetration correction으로 물체가 겹친 상태를 완화합니다.

### Q8. 차량이 경로를 잘 못 따라가던 문제는 어떻게 해결했나요?

차량이 waypoint 하나만 바라보며 일정 속도로 계속 전진해서 코너를 지나치는 문제가 있었습니다. 이를 해결하기 위해 look-ahead target을 계산하고, heading error가 클수록 속도를 낮추며, commanded speed와 turn torque를 smoothing했습니다.

### Q9. WPF에서 성능상 주의한 점은 무엇인가요?

WPF 3D 객체를 매 프레임 재생성하지 않도록 했습니다. Scene visual을 캐싱하고 transform 값만 갱신하며, debug visual도 매 render frame마다 무조건 rebuild하지 않도록 빈도를 제한했습니다.

### Q10. 테스트는 어떻게 구성했나요?

WPF UI보다 Core 로직을 중심으로 테스트했습니다. 물리 적분, 충돌, solver, navigation, path following, vehicle controller, sensor/safety/fault, data parsing 같은 순수 로직은 단위 테스트로 검증했습니다.

---

## 20. 기술 키워드별 공부 포인트

### C# / .NET

- class와 record struct 사용
- nullable enable
- `System.Numerics.Vector3`
- `System.Numerics.Quaternion`
- `ObservableCollection`
- `ICommand`
- event 기반 구조

### Physics

- Rigid Body
- Force
- Torque
- Impulse
- Linear Velocity
- Angular Velocity
- Inertia
- Damping
- Restitution
- Friction

### Collision

- Collider
- Contact
- AABB
- Broad Phase
- Narrow Phase
- SAT
- Penetration
- Contact Normal

### Rendering

- WPF `Viewport3D`
- `ModelVisual3D`
- `ModelUIElement3D`
- `GeometryModel3D`
- `Transform3DGroup`
- `TranslateTransform3D`
- `RotateTransform3D`
- `CompositionTarget.Rendering`

### Architecture

- Core/UI separation
- MVVM
- Single Responsibility Principle
- Renderer abstraction
- Input state separation
- Testable domain logic

### Navigation

- Grid A*
- Hybrid A*
- Mobility model
- Path simplification
- Look-ahead following
- Vehicle kinematics

---

## 21. 내가 설명할 수 있어야 하는 코드 위치

### 물리 상태와 적분

- `Forge3D.Core/Dynamics/RigidBody.cs`
- `Forge3D.Core/Dynamics/PhysicsWorld.cs`
- `Forge3D.Core/Dynamics/FixedStepRunner.cs`
- `Forge3D.Core/Dynamics/PhysicsPose.cs`

### 충돌

- `Forge3D.Core/Collision/CollisionDispatcher.cs`
- `Forge3D.Core/Collision/AabbBroadPhase.cs`
- `Forge3D.Core/Collision/BoxCollider.cs`
- `Forge3D.Core/Collision/SphereCollider.cs`
- `Forge3D.Core/Collision/PlaneCollider.cs`

### 충돌 반응

- `Forge3D.Core/Solver/ImpulseContactSolver.cs`

### 차량 / 미션 / 센서

- `Forge3D.Core/Simulation/Vehicle/VehicleController.cs`
- `Forge3D.Core/Simulation/Vehicle/VehicleEntity.cs`
- `Forge3D.Core/Simulation/Mission/MissionController.cs`
- `Forge3D.Core/Simulation/Sensors/SensorEntity.cs`
- `Forge3D.Core/Simulation/Safety/SafetyEvaluator.cs`
- `Forge3D.Core/Simulation/Faults/FaultManager.cs`

### 경로 탐색

- `Forge3D.Core/Navigation/Planning/GridAStarPlanner.cs`
- `Forge3D.Core/Navigation/Planning/HybridAStarPlanner.cs`
- `Forge3D.Core/Navigation/Following/PathFollower.cs`
- `Forge3D.Core/Navigation/PathSimplifier.cs`

### WPF Editor

- `Forge3D.Editor/MainWindow.xaml`
- `Forge3D.Editor/MainWindow.xaml.cs`
- `Forge3D.Editor/ViewModels/MainViewModel.cs`
- `Forge3D.Editor/Rendering/SceneRenderer.cs`
- `Forge3D.Editor/Rendering/SceneVisual.cs`
- `Forge3D.Editor/Rendering/RenderLoop.cs`
- `Forge3D.Editor/Rendering/DebugRenderer.cs`
- `Forge3D.Editor/Input/CameraController.cs`
- `Forge3D.Editor/Input/CameraInputState.cs`

---

## 22. 이 프로젝트로 어필할 수 있는 역량

- 수학 기반 3D 시뮬레이션 이해
- 물리 엔진 내부 구조 이해
- C#/.NET 기반 애플리케이션 설계
- WPF와 MVVM 사용 경험
- UI와 Core 로직 분리
- 실시간 시스템에서 update loop 설계
- 성능 병목을 의식한 visual caching
- 단위 테스트 중심의 안정화
- 문제를 관찰하고 구조적으로 개선하는 능력
- 단순 기능 구현이 아니라 디버깅 가능한 도구까지 만드는 능력

---

## 23. 면접에서 조심할 점

과장하지 말아야 할 부분:

- 상용 물리 엔진 수준이라고 말하지 않기
- DirectX renderer를 직접 만든 것처럼 말하지 않기
- 모든 충돌 형태를 지원한다고 말하지 않기
- 고급 CCD, GJK/EPA, BVH, multithread solver까지 구현했다고 말하지 않기

정확한 표현:

- "기초적인 3D rigid body physics core를 직접 구현했다"
- "Sphere/Box/Plane 기반 collision과 impulse solver를 구현했다"
- "WPF Viewport3D로 실시간 시각화 도구를 만들었다"
- "Fixed timestep과 render interpolation을 분리했다"
- "차량 경로 추종과 센서/안전 시나리오를 시뮬레이션 레이어로 확장했다"

---

## 24. 30초 자기소개 버전

> Forge3D는 제가 C#으로 직접 만든 3D 강체 물리 시뮬레이션 프로젝트입니다. Core에서는 RigidBody, Collider, SAT 충돌 판정, Impulse Solver, Fixed Timestep을 구현했고, WPF Editor에서는 이를 실시간으로 시각화하며 Inspector, Debug View, Profiler, Replay로 내부 상태를 확인할 수 있게 했습니다. 이후 차량 경로 추종, 센서 감지, 안전 판단, 고장 주입 시나리오까지 확장했고, 최근에는 physics update와 render loop를 분리해 interpolation과 camera smoothing을 적용하면서 시각적 부드러움과 구조적 책임 분리를 개선했습니다.

---

## 25. 1분 설명 버전

> Forge3D는 C# 기반 3D 물리 엔진과 WPF 엔지니어링 에디터를 함께 만든 프로젝트입니다. Core 라이브러리에는 RigidBody, force/torque/impulse, Quaternion 회전, Sphere/Box/Plane collider, AABB broad phase, SAT 기반 Box collision, impulse solver를 구현했습니다. Editor에서는 Scene Hierarchy, Inspector, Debug Visualization, Profiler, Replay 기능을 제공해 시뮬레이션 상태를 실시간으로 분석할 수 있게 했습니다. 또한 차량과 waypoint mission, Grid A*/Hybrid A* 경로 탐색, sensor FOV detection, safety evaluator, fault injection을 추가해 단순 물리 데모를 엔지니어링 시나리오로 확장했습니다. 최근에는 fixed physics step과 render loop를 분리하고 previous/current pose interpolation, scene visual caching, camera smoothing, vehicle command smoothing을 적용해 움직임이 뻣뻣하게 보이던 문제를 구조적으로 개선했습니다.

---

## 26. 추가로 공부하면 좋은 주제

- Semi-implicit Euler integration
- Quaternion integration
- Separating Axis Theorem
- Sequential impulse solver
- Baumgarte stabilization
- Continuous Collision Detection
- Spatial partitioning
- Sweep and prune
- BVH
- A* heuristic
- Hybrid A* motion primitive
- Pure pursuit path following
- WPF retained mode rendering
- MVVM command/data binding

---

## 27. 현재 검증 상태

최근 확인 기준:

```powershell
dotnet test Forge3D.Tests\Forge3D.Tests.csproj
```

전체 테스트 통과:

```text
48 passed
0 failed
```

WPF Editor 빌드:

```powershell
dotnet build Forge3D.Editor\Forge3D.Editor.csproj -p:UseAppHost=false -o .\artifacts\editor-build-check
```

결과:

```text
0 warnings
0 errors
```

---

## 28. 앞으로 발전시키면 좋은 방향

자소서나 면접에서 "다음 개선 계획"으로 말하기 좋은 항목입니다.

- DebugRenderer object pooling
- Spatial hash 또는 sweep-and-prune broad phase
- CCD로 빠른 물체 tunneling 완화
- constraint/joint 시스템
- 더 안정적인 stacking solver
- scene save/load
- render profiler 수치 UI 분리
- path smoothing 고도화
- vehicle pure pursuit controller 고도화
- sensor simulation 다양화

---

## 29. 기억해야 할 핵심 메시지

이 프로젝트에서 가장 중요한 메시지는 이것입니다.

> Forge3D는 단순히 기능을 많이 붙인 프로젝트가 아니라, 물리 시뮬레이션의 핵심 책임을 직접 구현하고, 그 내부 상태를 실시간으로 관찰할 수 있는 도구까지 함께 설계한 프로젝트입니다.

면접에서는 "무엇을 만들었는가"보다 다음을 강조하면 좋습니다.

- 왜 Core와 Editor를 분리했는지
- 왜 Fixed Timestep을 썼는지
- 왜 Physics state와 Render state를 분리했는지
- 충돌 판정과 충돌 반응을 어떻게 나눴는지
- 차량 경로 추종 문제를 어떻게 관찰하고 개선했는지
- 성능 문제를 어떻게 재생성/측정/완화했는지
- 테스트 가능한 로직을 어떻게 Core에 남겼는지
