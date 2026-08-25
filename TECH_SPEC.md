# Technical Specification

## 1. 기준 환경

- Unity 프로젝트 루트: `RPG DEMO/`
- 정식 Unity 버전: `6000.3.10f1`
- 렌더 파이프라인: URP 2D
- 주요 타깃: WebGL, 16:9
- 씬/프리팹 직렬화 파일과 `.meta` 파일은 항상 함께 버전 관리한다.

다른 Unity 버전으로 열어 자동 변경된 `ProjectVersion.txt`, `Packages/*`, `ProjectSettings/*`, URP 설정은 기능 작업 커밋에 포함하지 않는다. 버전 업그레이드는 별도의 합의와 전용 커밋으로만 수행한다.

## 2. 빌드 씬 순서

| 순서 | 씬 | 책임 |
|---:|---|---|
| 0 | `Assets/_Game/Scenes/Bootstrap.unity` | 공용 시스템 설치 |
| 1 | `Assets/_Game/Scenes/MainMenu.unity` | 시작/설정 메뉴 |
| 2 | `Assets/_Game/Scenes/Story.unity` | 5컷 자동 스토리 도입 |
| 3 | `Assets/Scenes/Tutorial.unity` | 기본 전투 조작 안내 |
| 4 | `Assets/Scenes/Stage01.unity` | 일반 스테이지 시작 |
| 5 | `Assets/Scenes/Stage02.unity` | 일반 스테이지 |
| 6 | `Assets/Scenes/Stage03.unity` | 일반 스테이지 |
| 7 | `Assets/Scenes/MiddleBoss.unity` | 중간 보스 스테이지 |
| 8 | `Assets/Scenes/Stage05.unity` | 일반 스테이지 |
| 9 | `Assets/Scenes/Stage06.unity` | 일반 스테이지 |
| 10 | `Assets/Scenes/Stage07.unity` | 일반 스테이지 |
| 11 | `Assets/Scenes/BossArena.unity` | 학습형 보스 스테이지 |

새 씬은 씬 담당자가 제작하고 공통 시스템 담당자가 Build Settings 및 전환 경로를 검토한다.

## 3. 폴더 계약

| 경로 | 용도 |
|---|---|
| `Assets/_Game/Scripts/Core` | 앱 생명주기, 게임 상태, 세션 |
| `Assets/_Game/Scripts/SceneManagement` | 공통 씬 로딩과 전환 |
| `Assets/_Game/Scripts/Save` | 영구 저장 |
| `Assets/_Game/Scripts/Audio` | BGM·SFX 재생 진입점 |
| `Assets/_Game/Scripts/UI` | 공통 메뉴·페이드·UI 생성 보조 |
| `Assets/_Game/Prefabs/Core` | 공통 시스템 프리팹 |
| `Assets/_Game/Prefabs/UI` | 재사용 UI 프리팹 |
| `Assets/_Game/Data` | 공용 설정 데이터와 ScriptableObject |
| `Assets/Scenes` | 담당자가 소유하는 플레이 씬 |
| `Assets/Prefabs` | 플레이어·투사체·적 등 현재 게임플레이 프리팹 |
| `Assets/Scripts` | 현재 전투·보스·스테이지 런타임 코드 |

새 최상위 폴더나 두 번째 공통 시스템 폴더를 만들지 않는다.

## 4. 공용 시스템 API

### 앱과 상태

- `Game.Core.Bootstrapper`: 유일한 `App` 루트를 유지하고 최초 씬을 로드한다.
- `Game.Core.RuntimeBootstrapInstaller`: 누락된 공용 컴포넌트와 공통 UI를 설치한다.
- `Game.Core.GameManager`: `Booting`, `Loading`, `Playing`, `Paused`, `GameOver` 상태를 관리한다.
- 상태 변경 구독: `GameManager.StateChanged`.

별도의 `DontDestroyOnLoad` 매니저를 만들지 않는다. 지속성이 필요하면 기존 `App`에 공통 시스템 담당자가 추가한다.

### 세션과 저장

- `GameSession.EnterStage(stageNumber, sceneName)`: 현재 런의 스테이지를 기록한다.
- `GameSession.StorePlayerHP(currentHP)`: 씬 사이에 유지할 HP를 기록한다.
- `GameSession.ResetRun()`: 새 런을 시작한다.
- `SaveManager.UnlockStage(stageNumber)`: 영구 진행도를 갱신한다.
- `SaveManager.SetMasterVolume`, `SetMusicVolume`, `SetSfxVolume`: 설정을 저장한다.

`GameSession`에는 현재 런의 일시 데이터만, `SaveManager`에는 종료 후에도 유지할 데이터만 넣는다.

### 씬 전환

- 정식 경로: `SceneLoader.Instance.LoadScene(sceneName)`
- 현재 씬 재시작: `SceneLoader.Instance.ReloadCurrentScene()`
- 스테이지 단독 테스트에서 `SceneLoader.Instance`가 없을 때만 `SceneManager.LoadScene`을 폴백으로 허용한다.
- 씬 이름 문자열은 Build Settings의 실제 이름과 정확히 일치해야 한다.

### 오디오

- BGM/SFX는 `Game.Audio.AudioManager`만 재생한다.
- UI: `PlayUiHover`, `PlayUiClick`
- 전투: `PlayPlayerMeleeSwing`, `PlayCombatHit`, `PlayPlayerRangedShot`, `PlayPlayerDash`, `PlayPlayerDeathExplosion`
- 전환: `PlayStageTransition`
- 새 AudioSource 싱글턴이나 스테이지별 볼륨 저장을 만들지 않는다.

## 5. 현재 이벤트 계약

| 발생 주체 | 이벤트 | 구독 용도 |
|---|---|---|
| `GameManager` | `StateChanged(GameState)` | 일시정지, 메뉴, 상태 UI |
| `Health` | `onDamaged` | HP HUD, 피격 반응 |
| `Health` | `onParrySuccess` | 패링 연출과 학습 교란 |
| `Health` | `onDeath` | 승패, 리스폰, 출구 개방 |
| `PlayerCombatTracker` | `ParrySucceeded` | 보스 경직과 적응 약화 |

현재 `StageCleared`, `BossPhaseChanged`, `LearningDataChanged`, 공통 `ResultRequested` 이벤트는 구현되어 있지 않다. 필요하면 임의로 유사 이벤트를 여러 개 만들지 말고 계약 변경 요청을 먼저 작성한다.

## 6. 의존성 규칙

- 스테이지와 보스는 UI 오브젝트의 텍스트를 직접 찾거나 수정하지 않는다. 게임 상태 또는 이벤트를 제공한다.
- UI는 공격 판정, 피해 계산, 씬 클리어 조건을 구현하지 않는다.
- 아트 에셋은 런타임 싱글턴이나 저장 코드를 포함하지 않는다.
- 공용 시스템은 특정 보스 클래스에 의존하지 않는다.
- 다른 영역의 구현체를 직접 참조하기보다 공개 API, UnityEvent 또는 합의된 C# 이벤트를 사용한다.
- `Find*ObjectByType`는 초기 연결의 폴백으로만 사용하고 매 프레임 호출하지 않는다.

## 7. 씬·프리팹 규칙

- 한 씬 또는 프리팹의 활성 편집자는 한 명이다.
- 공용 프리팹 원본을 스테이지에 복제해 별도 버전으로 만들지 않는다.
- 스테이지별 차이가 필요하면 Prefab Variant 또는 별도 설정 컴포넌트를 사용한다.
- 기존 프리팹 컴포넌트를 제거하거나 GUID를 바꾸는 변경은 소유자 승인이 필요하다.
- 런타임 생성 UI는 임시 프로토타입으로 취급하며 최종 UI 프리팹으로 교체할 때 이벤트 계약은 유지한다.

## 8. 계약 변경 절차

구현 전에 다음 형식으로 팀에 보고한다.

```text
[계약 변경 요청]
- 변경하려는 계약:
- 현재 방식으로 불가능한 이유:
- 영향받는 담당/파일:
- 제안 API 또는 이벤트:
- 기존 씬·세이브 호환 방법:
- 테스트 방법:
```

승인 전에는 공용 API 시그니처, enum 값, 세이브 구조, 씬 이름, 프리팹 GUID를 변경하지 않는다.

## 9. 검증 기준

- C# 컴파일 오류 0개
- Console 반복 예외 0개
- `Bootstrap`부터 대상 씬까지 전환 확인
- 대상 씬 단독 실행 폴백 확인
- 16:9 Game View에서 UI 잘림 확인
- WebGL에서 키 입력, 저장 실패 처리, 오디오 로드 확인
- `git diff --check` 및 의도한 파일만 스테이징
