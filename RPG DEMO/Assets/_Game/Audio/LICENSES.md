# Audio License Log

외부에서 가져온 음원은 프로젝트에 추가하는 즉시 아래 표에 기록한다.

| 게임 사용 파일 | 용도 | 제작자 | 원본 URL | 라이선스 | 다운로드 날짜 | 크레딧 필요 | 가공/비고 |
|---|---|---|---|---|---|---|---|
| `rollover2.ogg` | UI 마우스 호버 | Kenney | https://kenney.nl/assets/ui-audio | CC0 1.0 | 2026-08-19 | 아니오 | Kenney UI Audio 원본 |
| `click1.ogg` | UI 클릭 | Kenney | https://kenney.nl/assets/ui-audio | CC0 1.0 | 2026-08-19 | 아니오 | Kenney UI Audio 원본 |
| `Player_Melee_Swing_01.wav` | 플레이어 근접 공격 | Kinoton | https://freesound.org/people/Kinoton/sounds/427979/ | CC0 1.0 | 2026-08-19 | 아니오 | 원본 `427979__kinoton__short-whoosh-13x.wav`의 2.30~2.58초 구간 추출, PCM16 변환 및 짧은 페이드 적용 |
| `Combat_Hit_01.wav` | 근접/원거리 명중 및 몬스터 피격 | BarryTheWhite | https://freesound.org/people/BarryTheWhite/sounds/396345/ | CC0 1.0 | 2026-08-19 | 아니오 | 원본 `396345__barrythewhite__smacks-and-thwacks-m.wav`의 18.35~18.65초 구간 추출, PCM16 변환 및 짧은 페이드 적용 |
| `Player_Ranged_Shot_01.flac` | 플레이어 원거리 발사 | unfa | https://freesound.org/people/unfa/sounds/584198/ | CC0 1.0 | 2026-08-19 | 아니오 | 원본 `584198__unfa__weapons-plasma-shot-06.flac` 전체 사용 |
| `Player_Dash_01.wav` | 플레이어 대시 | Jofae | https://freesound.org/people/Jofae/sounds/389590/ | CC0 1.0 | 2026-08-19 | 아니오 | 원본 `389590__jofae__swing-woosh.wav` 전체 사용 |
| `Player_Death_Explosion_01.wav` | 플레이어 전투 사망 폭발 | ReadeOnly | https://freesound.org/people/ReadeOnly/sounds/186958/ | CC0 1.0 | 2026-08-19 | 아니오 | 원본 `186958__readeonly__explosion7.wav` 전체 사용 |
| `BGM_MainMenu_Loop.mp3` | 메인 메뉴 BGM | FoggySunrise | https://pixabay.com/music/video-games-glass-gardens-loop-371924/ | Pixabay Content License | 2026-08-19 | 법적 필수 아님, 제작자 표기 권장 | 원본 `foggysunrise-glass-gardens-loop-371924.mp3` 전체 사용, 페이지에 Loop 및 게임/메인 메뉴 용도 명시 |
| `BGM_Stage01_Loop.mp3` | 일반 스테이지 BGM(Stage01~04, 06~09) | nojisuma | https://pixabay.com/music/beats-%E9%81%8A%E6%92%83-action-110116/ | Pixabay Content License | 2026-08-19 | 법적 필수 아님, 제작자 표기 권장 | 원본 `nojisuma-action-110116.mp3` 전체 사용, 제작자가 Pixabay 제공곡의 영상·게임 BGM 상업 이용 허용을 명시 |
| `BGM_MidBoss_Loop.mp3` | Stage05 중간보스 BGM | BackgroundMusicMaster | https://pixabay.com/music/main-title-bossroom-battle-431358/ | Pixabay Content License | 2026-08-19 | 법적 필수 아님, 제작자 표기 권장 | 원본 `backgroundmusicmaster-bossroom-battle-431358.mp3` 전체 사용. 원본 페이지에 `Video Game Loop`, `Boss Fight Music` 명시. AI 생성 콘텐츠 표시 있음 |
| `BGM_FinalBoss_Loop.mp3` | Stage10/BossArena 최종보스 BGM | NyxAurora | https://pixabay.com/music/main-title-final-battle-ii-epic-cinematic-battle-music-with-intense-orchestral-361155/ | Pixabay Content License | 2026-08-19 | 법적 필수 아님, 제작자 표기 권장 | 원본 `nyxaurora-final-battle-ii-epic-cinematic-battle-music-with-intense-orchestral-361155.mp3` 전체 사용. 원본 페이지에 `Final Boss`, `Epic`, `Battle` 명시. AI 생성 콘텐츠 표시 있음 |
| `Stage_Transition_01.wav` | 스테이지 전환 징글 | CogFireStudios | https://freesound.org/people/CogFireStudios/sounds/619840/ | CC0 1.0 | 2026-08-19 | 아니오 | 원본 `619840__cogfirestudios__achievement-accomplish-jingle-app-ui.wav` 전체 사용, 2.371초 WAV |

## 파일 전수 검사 결과

- 검사일: 2026-08-19
- `Resources/Audio`에서 게임이 사용하는 음원: 12개
- 위 게임 사용 파일 표에 기록된 음원: 12개
- `Audio/Incoming`에 보관된 원본 음원: 10개
- 아래 원본 대응표에 기록된 원본: 10개
- 누락된 게임 사용 음원 또는 출처 불명 음원: 0개

`Incoming`은 편집 전 원본을 보관하는 폴더이며 게임에서는 직접 불러오지 않는다. 실제 재생 파일과 원본 파일의 관계는 다음과 같다.

| Incoming 원본 파일 | 게임 사용 파일 | 처리 상태 |
|---|---|---|
| `427979__kinoton__short-whoosh-13x.wav` | `Player_Melee_Swing_01.wav` | 2.30~2.58초 추출, PCM16 및 페이드 |
| `396345__barrythewhite__smacks-and-thwacks-m.wav` | `Combat_Hit_01.wav` | 18.35~18.65초 추출, PCM16 및 페이드 |
| `584198__unfa__weapons-plasma-shot-06.flac` | `Player_Ranged_Shot_01.flac` | 전체 사용 |
| `389590__jofae__swing-woosh.wav` | `Player_Dash_01.wav` | 전체 사용 |
| `186958__readeonly__explosion7.wav` | `Player_Death_Explosion_01.wav` | 전체 사용 |
| `619840__cogfirestudios__achievement-accomplish-jingle-app-ui.wav` | `Stage_Transition_01.wav` | 전체 사용 |
| `foggysunrise-glass-gardens-loop-371924.mp3` | `BGM_MainMenu_Loop.mp3` | 전체 사용 |
| `nojisuma-action-110116.mp3` | `BGM_Stage01_Loop.mp3` | 전체 사용 |
| `backgroundmusicmaster-bossroom-battle-431358.mp3` | `BGM_MidBoss_Loop.mp3` | 전체 사용 |
| `nyxaurora-final-battle-ii-epic-cinematic-battle-music-with-intense-orchestral-361155.mp3` | `BGM_FinalBoss_Loop.mp3` | 전체 사용 |

Kenney의 `rollover2.ogg`, `click1.ogg`는 별도의 `Incoming` 복사본 없이 원본 파일을 그대로 `Resources/Audio/UI`에 배치했다. 두 파일 모두 위 게임 사용 파일 표에 기록되어 있다.

## 현재 프로젝트 크레딧 표기

Freesound와 Kenney 음원은 `CC0 1.0 Universal`로 확인되어 게임 내 제작자 표기가 법적으로 요구되지는 않는다. Pixabay BGM은 `Pixabay Content License`에 따라 게임의 일부로 사용할 수 있고 표기가 필수는 아니지만, 원본 음원 단독 재판매·재배포는 하지 않는다. 출처 추적과 팀 협업을 위해 이 문서에 제작자, 원본 페이지, 가공 내역을 계속 보존한다.

CC0 전문: https://creativecommons.org/publicdomain/zero/1.0/

Pixabay 라이선스 요약: https://pixabay.com/service/license-summary/

## 라이선스 원칙

- CC0: 우선 사용
- CC BY: 제작자와 출처를 크레딧에 표시할 수 있을 때만 사용
- CC BY-NC: 상업화 가능성이 있는 본 프로젝트에서는 사용하지 않음
- 출처 또는 라이선스가 불명확한 파일: 사용하지 않음
- 유튜브나 다른 게임에서 음원을 직접 추출하지 않음
- Unity Asset Store 음원: 구매 계정, 에셋 이름, 에셋 URL과 적용 EULA를 기록
- Pixabay 음원: 원본 페이지 URL과 다운로드 당시 라이선스 확인 기록 보관
