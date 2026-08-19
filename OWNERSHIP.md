# Ownership & Collaboration Rules

## 1. 기본 원칙

- 모든 파일에는 한 명 또는 한 역할의 최종 소유자가 있다.
- 소유자는 해당 파일의 설계와 병합 충돌 해결을 책임진다.
- 다른 담당자는 소유 파일을 직접 수정하지 않고 공개 API·이벤트·프리팹 슬롯으로 연결한다.
- 소유자가 아직 정해지지 않은 영역은 임시로 구현하지 않고 담당자를 먼저 지정한다.
- 이 문서의 소유권 변경 자체도 팀 합의가 필요한 계약 변경이다.

## 2. 담당 영역

| 영역 | 담당 | 소유 파일/폴더 | 책임 |
|---|---|---|---|
| 공통 시스템 | 주민 | `Assets/_Game/Scripts/Core`, `Save`, `Audio`, `SceneManagement` | 플레이어 공통 동작, 전투 기반, 세션, 저장, 오디오, 씬 전환 |
| 공통 플레이어 | 주민 | `Assets/Prefabs/Player.prefab`, `PlayerController.cs`, `Health.cs`, 공통 투사체·피해 코드 | 모든 스테이지가 사용하는 플레이어 계약 유지 |
| 보스 스테이지 | 도현 | `BossArena.unity`, `BossController.cs`, `AnalysisUI.cs`, `AdaptiveBossVisual.cs`, `PlayerCombatTracker.cs` | 보스 패턴, 학습, 교란, 페이즈 전환 |
| 보스 임시 UI | 도현, UI 이관 전까지 | `BossLearningHUD.cs` | 보스 프로토타입 HUD·결과 화면 유지, 최종 UI 계약 제공 |
| 일반 스테이지 | 담당자 지정 필요 | `Stage01.unity`, 일반 적·웨이브·스테이지 전용 코드 | 웨이브, 배치, 클리어 조건, 보스 진입 |
| 시작 화면 | 담당자 지정 필요 | `MainMenu.unity`, 시작 화면 전용 연출 | 시작·설정 진입과 스토리 도입 |
| UI | 담당자 지정 필요 | `Assets/_Game/Scripts/UI`, `Assets/_Game/Prefabs/UI` | HUD, 메뉴, 튜토리얼, 공통 결과 화면 |
| 아트 | 담당자 지정 필요 | 최종 스프라이트·애니메이션·VFX·폰트 | `UI_ART_GUIDE.md` 유지와 비주얼 통일 |
| 통합/빌드 | 팀 리드 지정 필요 | `Packages`, `ProjectSettings`, Build Settings, `main` | Unity 버전, 패키지, 빌드, 최종 병합 |

`RPG DEMO/`를 기준으로 표의 경로를 해석한다.

## 3. 공통 계약 소유권

다음은 공통 시스템 담당자의 승인 없이 이름, 타입 또는 의미를 변경할 수 없다.

- `GameManager`와 `GameState`
- `GameSession` 공개 속성과 메서드
- `SaveData` 구조와 `SaveManager` 공개 API
- `SceneLoader` 공개 API와 빌드 씬 이름
- `AudioManager` 공개 API와 Resources 경로
- `PlayerController` 공개 상태값
- `Health` 이벤트와 피해/사망 의미
- `Player` 프리팹의 필수 컴포넌트

다음은 보스 담당자의 승인 없이 변경할 수 없다.

- `PlayerCombatTracker`의 학습 데이터 의미
- `BossController`의 Phase와 적응 공개 속성
- 보스 학습/교란 판정 기준
- `BossArena`의 공격 판정, 보스 히트박스와 페이즈 연결

## 4. 파일별 금지 사례

- 일반 스테이지 담당자가 Player 프리팹을 복제해 스테이지 전용 Player를 만드는 행위
- UI 담당자가 `Health.TakeDamage` 또는 보스 패턴을 호출해 승패를 조작하는 행위
- 보스 담당자가 공통 Result·Pause 메뉴를 별도 규격으로 확정하는 행위
- 공통 시스템 담당자가 보스 씬의 히트박스와 공격 가중치를 수정하는 행위
- 아트 담당자가 GUID가 연결된 프리팹·씬을 재생성해 참조를 끊는 행위
- 누구든 승인 없이 `Packages` 또는 Unity 기준 버전을 올리는 행위

## 5. 현재 알려진 임시 경계

- `BossLearningHUD`의 결과 화면은 보스전 테스트를 위한 임시 구현이다. UI 담당자는 보스 학습 데이터의 공개 속성을 구독하는 최종 HUD/Result 프리팹으로 교체해야 한다.
- `PlayerRespawn`은 공통 코드로 존재하지만 현재 Player 프리팹에는 연결하지 않았다. 보스 결과 화면과 자동 재시작이 충돌하므로 공통 Result 계약이 확정된 뒤 연결한다.
- `Assets/_Game/Prefabs/Core`와 `UI`는 현재 구조만 있고 최종 공용 프리팹이 비어 있다.
- 공통 `StageCleared`, `BossPhaseChanged`, `LearningDataChanged`, `ResultRequested` 이벤트는 아직 없다.
- 한글 TMP 폰트와 최종 UI 담당자는 아직 확정되지 않았다.

## 6. 브랜치와 병합

- 각 담당자는 본인의 `dev/<name>` 브랜치에서 작업한다.
- 작업 전 원격 변경을 fetch하고 기준 브랜치와 차이를 확인한다.
- 통합 브랜치는 팀 리드가 지정한다. 지정 전에는 `main`에 직접 푸시하지 않는다.
- Unity 씬/프리팹 변경은 코드 변경과 가능하면 별도 커밋으로 나눈다.
- 혼합된 작업 트리에서는 `git add -A`를 사용하지 않고 담당 파일만 명시적으로 스테이징한다.
- 다른 담당자의 미추적·미커밋 파일은 삭제, 복구, 포맷 또는 커밋하지 않는다.

## 7. 작업 요청 템플릿

```text
작업 전에 GAME_SPEC.md, TECH_SPEC.md, UI_ART_GUIDE.md,
OWNERSHIP.md를 모두 읽어라.

담당 영역 밖의 씬, 프리팹, 공통 인터페이스는 수정하지 마라.
공용 시스템이 필요하면 새로 만들지 말고 기존 계약을 사용하라.
계약 변경이 꼭 필요하면 구현하지 말고 변경 필요성을 먼저 보고하라.

이번 담당 영역:
이번에 수정 가능한 파일/폴더:
완료 조건:

완료 후 다음을 보고하라:
1. 변경한 파일
2. 발생시키거나 구독하는 이벤트
3. 다른 시스템이 연결할 방법
4. Unity에서 테스트하는 방법
5. 아직 연결되지 않은 부분
```

## 8. 완료 보고 템플릿

```text
1. 변경한 파일
- 경로와 변경 목적

2. 이벤트
- 발생시킨 이벤트
- 구독한 이벤트
- 이벤트가 없다면 없음

3. 다른 시스템 연결 방법
- 필요한 컴포넌트, API, Inspector 참조

4. Unity 테스트 방법
- 시작 씬
- 재현 순서
- 기대 결과

5. 아직 연결되지 않은 부분
- 임시 구현
- 담당자 결정 또는 계약 변경이 필요한 항목
```

## 9. Definition of Done

- 담당 파일만 변경했다.
- 공용 계약을 무단으로 변경하지 않았다.
- 새 에셋의 `.meta`가 포함되어 있다.
- 컴파일 오류가 없다.
- 대상 씬 단독 실행과 연결 실행을 확인했다.
- UI 문구와 실제 조작이 일치한다.
- 변경 파일·이벤트·연결법·테스트·미연결 항목을 보고했다.
- 커밋 전에 diff와 브랜치를 확인했다.
