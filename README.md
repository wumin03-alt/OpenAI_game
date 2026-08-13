# 마왕컴퍼니 — 오늘도 채용 중 (Phase 1 Unity Prototype)

몬스터 지원자를 면접하고, 숨겨진 성격을 추론해 채용한 뒤 던전 방어전에 투입하는 2D 자동 전투 프로토타입입니다.

이번 Phase 1의 목표는 실제 AI 연결 없이 다음 코어 루프를 끝까지 플레이할 수 있게 검증하는 것입니다.

`INTERVIEW → HIRE → DEPLOY → AUTO BATTLE → PERFORMANCE REVIEW`

## 구현 플랫폼

- Unity `6000.5.5f1`
- C# 런타임 UI 및 2D 도형 렌더링
- 씬: `Assets/Scenes/Prototype.unity`
- 외부 패키지, 커스텀 폰트, 사운드 에셋 없음
- 지원자 3명의 개별 픽셀 아트 초상화와 투명 PNG 리소스

마스터 문서의 브라우저용 TypeScript/Vite/Phaser 제안 대신, 이 저장소와 작업 요청의 실행 대상에 맞춰 Unity 프로젝트로 구현했습니다. 게임 규칙과 Phase 1 범위는 동일하게 유지했습니다.

## 게임 기획 기준

플레이어는 마왕군 인사담당자입니다. 면접에서 지원자의 말만 듣고 채용 여부를 판단하지만, 지원자의 실제 Trait과 전투 능력은 게임 데이터가 소유합니다. 면접 답변은 그 진실을 바꾸지 않으며, 전투 후 성과 평가에서 면접 발언과 실제 행동을 비교하게 됩니다.

핵심 재미는 “면접에서 한 말이 거짓말이었구나”를 전투 행동으로 즉시 이해하는 데 있습니다.

## Phase 1 구현 기능

### 지원자 데이터

| 이름 | 종족 / 역할 | 급여 | 숨겨진 Trait |
|---|---|---:|---|
| Gruk | Goblin / Archer | 30 | `COWARD` |
| Rokka | Orc / Warrior | 50 | `RECKLESS` |
| Mellu | Slime / Support | 35 | `TEAM_PLAYER` |

지원자 데이터는 `Assets/Scripts/Phase1/CandidateDatabase.cs`에서 관리합니다. 상세 전투 스탯과 숨겨진 과거 사건도 고정 데이터이며 면접 답변이 변경하지 않습니다.

### 면접

- 지원자 이름, 종족, 역할, 급여, 이력서 표시
- 지원자별 자유 텍스트 질문 최대 3회
- 질문/답변 히스토리 표시
- Hire / Reject 결정
- Trait은 성과 평가 전까지 비공개

`FakeInterviewProvider`가 질문의 한국어·영어 키워드를 분류해 미리 작성된 답변을 반환합니다.

- 용기: `겁`, `위험`, `도망`, `fear`, `danger`, `run` 등
- 규율: `명령`, `규율`, `대형`, `discipline`, `order`, `formation` 등
- 팀워크: `팀`, `동료`, `협력`, `team`, `ally`, `together` 등
- `COWARD` 지원자는 용기 질문에 끝까지 싸운다고 거짓말합니다.
- `RECKLESS` 지원자는 규율 질문에 명령과 대형을 지킨다고 거짓말합니다.

### 채용

- 총 급여 예산: 100
- 최대 채용 인원: 2명
- 한도 또는 예산을 넘는 채용 차단
- 현재 예산과 채용 인원을 상단 HUD에 표시

### 배치

- Defense Formation 슬롯 3개
- 채용 몬스터 선택 후 슬롯 클릭 방식
- 배치된 슬롯을 다시 선택해 위치 변경 가능
- 채용 인원을 모두 배치해야 자동 전투 시작 가능

### 자동 전투

- 오른쪽에서 Enemy Warrior 5명 순차 등장
- 적은 던전 방향으로 이동하며 지정 몬스터를 근접 공격
- 몬스터는 사거리 안의 가장 가까운 적을 자동 공격
- 던전 HP 100, 적이 Gate에 도착하면 20 피해
- 공격 순간을 색상 선으로 표시
- 몬스터 HP/상태와 최근 전투·Trait 이벤트를 HUD에 표시

### Trait

- `COWARD`: HP가 50% 이하가 되면 “COWARD! RUNNING AWAY”를 표시하고 빠르게 전장 밖으로 도주
- `RECKLESS`: 전투 시작 즉시 대형을 이탈해 적 방향으로 돌진
- `TEAM_PLAYER`: 인접 아군의 Attack Speed를 20% 증가시키고 청록색 Aura 표시

Trait 효과와 관련 사건은 전투 로그 및 성과 평가에 함께 기록됩니다.

### 성과 평가와 종료

- 결과: Wave Clear 또는 Game Over
- 몬스터별 Damage, Kills, Damage Taken
- Trait Incident 및 Discovered Trait 공개
- Restart Game 버튼으로 면접부터 새 게임 시작

## 실행 방법

1. Unity Hub에서 이 저장소 루트를 프로젝트로 추가합니다.
2. Unity `6000.5.5f1`로 프로젝트를 엽니다.
3. `Assets/Scenes/Prototype.unity` 씬을 엽니다.
4. Game 뷰를 `16:9`로 맞춥니다.
5. Play 버튼을 누릅니다.

씬은 Build Settings에도 등록되어 있습니다. 첫 실행 때 Unity가 `Library/`를 생성하므로 임포트에 시간이 걸릴 수 있습니다.

## Acceptance Test 순서

1. Play 후 Candidate Files에서 Gruk, Rokka, Mellu 세 명을 차례로 확인합니다.
2. Gruk을 선택하고 `위험하면 도망가나요?`를 포함해 최대 3회 질문합니다.
3. Rokka에게 `명령과 대형을 지키나요?`라고 질문합니다.
4. 두 명을 Hire하고 세 번째 Hire가 차단되는지 확인합니다.
5. `DEPLOY`를 누른 뒤 채용 몬스터를 각각 다른 슬롯에 배치합니다.
6. `START AUTO BATTLE`을 누릅니다.
7. 채용 조합에 따라 다음 중 하나 이상을 확인합니다.
   - Gruk이 HP 50% 이하에서 도주
   - Rokka가 전투 시작 직후 돌진
   - Mellu가 인접 아군의 공격속도를 20% 증가
8. Wave 종료 후 성과 평가에서 Damage, Kills, Damage Taken, Incident, Trait을 확인합니다.
9. `RESTART GAME`을 누르고 면접 화면과 예산이 초기화되는지 확인합니다.

Trait 3개를 한 번에 모두 확인하려면 채용 한도가 2명이므로 게임을 재시작해 서로 다른 조합으로 테스트해야 합니다.

## 코드 구조

```text
Assets/
  Resources/
    CandidatePortraits/
      gruk.png
      rokka.png
      mellu.png
  Scenes/
    Prototype.unity
  Scripts/
    GameBootstrap.cs
    Phase1/
      CandidateDatabase.cs
      FakeInterviewProvider.cs
      Phase1GameController.cs
      Phase1Models.cs
      Phase1UI.cs
```

- `GameBootstrap`: 카메라, EventSystem, 게임 컨트롤러 생성
- `CandidateDatabase`: 고정 지원자와 실제 스탯/Trait
- `FakeInterviewProvider`: 교체 가능한 면접 공급자 인터페이스와 키워드 답변
- `Phase1GameController`: 채용 상태, 배치, 전투, Trait, 승패, 재시작
- `Phase1UI`: Interview, Deployment, Battle HUD, Performance Review 런타임 UI

## Phase 1 제외 범위

- OpenAI API, LLM, 음성 입력
- 다중 Wave, Boss, 전체 몬스터/적/Trait
- 복잡한 애니메이션, 사운드, 저장 시스템
- 메타 진행, 상점, 설정

## 현재 알려진 제한

- 도형과 기본 런타임 폰트를 사용하는 기능 검증용 화면입니다.
- 자동 전투는 단일 직선 전장과 단일 근접 Enemy Warrior만 사용합니다.
- 실제 OpenAI 연결은 없으며 `FakeInterviewProvider` 답변만 사용합니다.
- UI는 1920×1080 기준으로 설계되어 극단적인 종횡비에서는 여백이 달라질 수 있습니다.
- Unity 배치 검증 환경의 라이선스 클라이언트가 연결되지 않아 자동 Play Mode 화면 캡처는 수행하지 못했습니다. Unity 내장 Roslyn 컴파일과 순수 데이터/면접 로직 테스트는 통과했습니다.

## 다음 권장 단계

Phase 2의 첫 작업은 현재 `IInterviewProvider` 인터페이스를 유지한 채 `OpenAIInterviewProvider`를 별도 구현하고, 구조화된 응답 검증과 실패 시 Fake Provider 폴백을 추가하는 것입니다. 전투 스탯, Trait, 승패 판정은 계속 Game State가 소유해야 합니다.
