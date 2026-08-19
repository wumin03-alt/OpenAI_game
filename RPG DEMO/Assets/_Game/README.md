# RPG DEMO 공통 구조

## 폴더 책임

- `Scenes`: Bootstrap 및 공통 진입 씬
- `Scripts/Core`: 앱 수명주기, 게임 상태, 현재 세션
- `Scripts/SceneManagement`: 공통 씬 전환
- `Scripts/Save`: 영구 저장 데이터
- `Scripts/Audio`: BGM 및 효과음
- `Scripts/UI`: 로딩, 페이드 등 공통 UI
- `Prefabs/Core`: 공통 시스템 프리팹
- `Prefabs/UI`: 스테이지에서 재사용하는 UI 프리팹
- `Data`: 스테이지, 적, 아이템 설정 데이터

## 팀 공통 규칙

1. 씬 전환은 가능하면 `SceneLoader.Instance.LoadScene(sceneName)`을 사용합니다.
2. Bootstrap의 `App`만 `DontDestroyOnLoad`로 유지합니다.
3. 플레이어, 카메라, HUD, 적, 보스는 각 스테이지가 소유합니다.
4. 씬 사이에 유지할 런타임 값은 `GameSession`에 저장합니다.
5. 디스크에 남길 진행도와 설정만 `SaveManager`에 저장합니다.
6. 스테이지 씬 단독 테스트를 위해 `SceneLoader`가 없을 때의 직접 로딩 호환 경로를 유지합니다.

## 실행 흐름

`Bootstrap → MainMenu → Stage01` 순서로 시작합니다. 플레이 중 `ESC`를 누르면 공통 일시정지 메뉴가 열립니다.

## 메인 메뉴 배경 교체

현재는 어두운 기본 배경색을 사용합니다. 원하는 이미지를 `Assets/Resources/MainMenuBackground.png`에 Sprite로 추가하면 메인 메뉴가 자동으로 해당 이미지를 사용합니다.
