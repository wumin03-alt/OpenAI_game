# RPG DEMO 오디오 준비 가이드

## 결론

직접 작곡하거나 모든 효과음을 직접 만들 필요는 없다. 프로토타입에서는 라이선스가 명확한 음원을 수급하고, 게임 방향이 확정된 뒤 핵심 음원만 교체하는 방식이 가장 현실적이다.

권장 우선순위는 다음과 같다.

1. CC0 효과음 에셋 사용
2. 상업 이용 가능한 루프 BGM 사용
3. 게임 완성도가 올라가면 필요한 음원만 직접 제작하거나 외주 진행

## 이번에 필요한 파일

다음 파일명으로 준비하면 프로젝트 연결이 쉽다.

| 용도 | 권장 파일명 | 길이와 느낌 |
|---|---|---|
| UI 클릭 | `UI_Click_01.wav` | 0.05~0.2초, 짧고 부드러운 디지털 클릭 |
| 플레이어 근접 공격 | `Player_Attack_Melee_01.wav` | 0.15~0.4초, 가벼운 휘두르기 |
| 플레이어 원거리 공격 | `Player_Attack_Ranged_01.wav` | 0.15~0.4초, 에너지 발사음 |
| 플레이어 피격 | `Player_Hit_01.wav` | 0.15~0.35초, 너무 크거나 불쾌하지 않은 충격음 |
| 플레이어 사망 | `Player_Death_Explosion_01.wav` | 0.7~1.3초, 팝과 파편이 섞인 폭발음 |
| 메인 메뉴 BGM | `BGM_MainMenu_Loop.ogg` | 60~120초, 70~90 BPM, 잔잔한 긴장감 |
| Stage01 BGM | `BGM_Stage01_Loop.ogg` | 90~180초, 105~130 BPM, 반복 가능한 액션 리듬 |

## 초보자에게 추천하는 수급 방법

### 효과음

- Kenney UI Audio: UI 클릭음을 찾기 좋고 CC0이다.
  - https://kenney.nl/assets/ui-audio
- Freesound: 검색 필터에서 `CC0`만 선택하는 것이 가장 간단하다.
  - https://freesound.org/

### Freesound에서 효과음 받는 순서

Freesound는 미리듣기는 로그아웃 상태에서도 가능하지만, 원본 파일 다운로드에는 무료 계정 로그인이 필요하다.

1. https://freesound.org/ 에 접속한다.
2. 오른쪽 위 `Register`를 눌러 무료 계정을 만들고 이메일 인증을 완료한다.
3. `Log in`으로 로그인한다.
4. 위쪽 `Search sounds...` 입력란에 아래 검색어 중 하나를 입력한다.
5. 검색 결과 화면 오른쪽의 `Licenses` 필터를 찾는다.
6. 반드시 `Creative Commons 0`을 선택한다.
7. 삼각형 재생 버튼으로 여러 소리를 미리 듣는다.
8. 마음에 드는 소리의 제목을 눌러 상세 페이지로 이동한다.
9. 상세 페이지에서도 라이선스가 `Creative Commons 0`인지 다시 확인한다.
10. 오른쪽의 빨간색 `Download` 버튼을 누른다.

다운로드할 때 결제는 필요하지 않다. 파일 형식은 `WAV`, `OGG`, `FLAC` 중 어느 것이어도 괜찮으며 원본 그대로 받으면 된다. 변환과 Unity 연결은 나중에 한꺼번에 처리한다.

이번에 찾을 소리는 아래 3개다.

| 순서 | 용도 | 그대로 입력할 검색어 | 고르는 기준 | 변경할 파일명 |
|---|---|---|---|---|
| 1 | 플레이어 공격 | `short sword whoosh` | 0.2~0.6초, 짧은 휘두르기 | `Player_Attack_Melee_01` |
| 2 | 플레이어 피격 | `short game hit impact` | 0.1~0.5초, 짧고 둔한 충격 | `Player_Hit_01` |
| 3 | 플레이어 사망 | `cartoon pop explosion` | 0.5~1.5초, 너무 현실적이지 않은 폭발 | `Player_Death_Explosion_01` |

검색 결과가 마음에 들지 않으면 한 번에 여러 단어를 추가하기보다 `short`, `soft`, `cartoon`, `game`을 하나씩 바꾸어 본다. 사람의 비명이나 실제 총소리보다는 짧고 단순한 게임 효과음이 현재 도형 플레이어와 잘 어울린다.

다운로드한 뒤에는 다음 정보만 메모한다.

- 소리 제목
- 업로더 이름
- 해당 소리의 상세 페이지 주소
- `Creative Commons 0` 표시

파일 3개를 모두 받으면 이름을 직접 바꾸거나 변환하지 말고 ZIP으로 묶어 이 대화에 첨부하거나 `Assets/_Game/Audio/Incoming/`에 넣는다. 이후 파일 선별, 이름 변경, 볼륨 정리, 라이선스 기록 및 공격·피격·사망 코드 연결을 진행한다.

추천 검색어:

- `soft ui click`
- `sword whoosh short`
- `energy shot`
- `game hit impact`
- `cartoon explosion debris`

Freesound의 `CC BY` 파일도 사용할 수 있지만 크레딧이 필요하다. `CC BY-NC`는 상업 게임 가능성을 고려해 사용하지 않는다.

### BGM

- Pixabay Music에서 `loop`, `dark ambient`, `action game`, `synth battle` 등으로 검색한다.
  - https://pixabay.com/music/
- Unity Asset Store의 무료 또는 유료 음악 팩도 사용할 수 있다.
  - https://assetstore.unity.com/audio/music

Pixabay 음원은 게임에 포함해 사용할 수 있지만 원본 음원 자체를 따로 재판매하면 안 된다. 다운로드한 페이지 주소와 라이선스 확인 날짜를 반드시 기록한다.

## BGM 선정 기준

### 메인 메뉴

- 전투곡보다 조용해야 한다.
- 멜로디가 너무 강해서 반복 청취가 피곤하지 않아야 한다.
- 첫 5초가 갑자기 크지 않아야 한다.
- 루프의 시작과 끝이 자연스럽게 연결되어야 한다.

추천 검색어:

- `dark ambient game menu loop`
- `minimal fantasy menu`
- `sci fi ambient loop`

### Stage01

- 메인 메뉴보다 리듬이 분명해야 한다.
- 짧은 구간이 지나치게 반복되지 않는 90초 이상의 곡이 좋다.
- 플레이어 공격음과 피격음이 묻히지 않도록 중저음이 지나치게 크지 않은 곡을 선택한다.

추천 검색어:

- `2d action game loop`
- `electronic battle loop`
- `dark synth action`

## 다운로드 후 작업

1. 원본 파일을 다운로드한다.
2. 출처 URL, 제작자, 라이선스, 다운로드 날짜를 `LICENSES.md`에 기록한다.
3. Audacity 같은 오디오 편집기로 앞뒤 무음과 과도한 볼륨을 정리한다.
4. 효과음은 WAV, BGM은 OGG로 내보낸다.
5. 위 표의 파일명 규칙으로 이름을 변경한다.
6. Unity의 `Assets/_Game/Audio` 아래에 역할별 폴더를 만들어 넣는다.

권장 최종 구조:

```text
Assets/_Game/Audio/
├─ Music/
│  ├─ BGM_MainMenu_Loop.ogg
│  └─ BGM_Stage01_Loop.ogg
├─ SFX/
│  ├─ UI/UI_Click_01.wav
│  └─ Player/
│     ├─ Player_Attack_Melee_01.wav
│     ├─ Player_Attack_Ranged_01.wav
│     ├─ Player_Hit_01.wav
│     └─ Player_Death_Explosion_01.wav
└─ LICENSES.md
```

## Unity 임포트 설정

짧은 효과음:

- Load Type: `Decompress On Load`
- Preload Audio Data: 활성화
- Force To Mono: 원본이 모노 효과음이면 활성화
- Load In Background: 비활성화

BGM:

- Load Type: `Compressed In Memory`
- Compression Format: Vorbis
- Quality: 60~80부터 테스트
- Loop 설정은 AudioSource에서 활성화

Web 빌드에서는 브라우저가 사용자 입력 전 자동재생을 막을 수 있다. BGM은 Bootstrap 시작 시점이 아니라 `PLAY` 버튼 클릭 이후 시작한다.

## 최소 검수 체크리스트

- [ ] 이어폰과 스피커 양쪽에서 확인
- [ ] 효과음끼리 볼륨 차이가 지나치게 크지 않음
- [ ] BGM이 공격·피격음을 가리지 않음
- [ ] BGM 루프 연결부에서 끊김이나 클릭 노이즈가 없음
- [ ] 모든 외부 음원의 라이선스를 기록함
- [ ] Web 빌드에서 첫 클릭 이후 정상 재생됨

## 이후 Unity 연결 순서

1. AudioMixer에 Master, Music, SFX, UI 그룹 생성
2. AudioCatalog 데이터에 준비한 AudioClip 등록
3. 메인 메뉴 PLAY 클릭 이후 메뉴/스테이지 BGM 재생
4. 버튼, 공격, 피격, 사망 이벤트에서 공통 AudioManager 호출
5. 설정 화면의 볼륨 슬라이더를 Mixer 그룹 볼륨에 연결
