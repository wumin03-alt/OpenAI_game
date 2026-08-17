using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace PowerfulSpell
{
    public sealed class SpellGamePrototype : MonoBehaviour
    {
        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;
        private const string UnlockKey = "PowerfulSpell.HighestUnlockedStage";
        private const string MicrophoneKey = "PowerfulSpell.MicrophoneDevice";

        private enum ScreenState { Title, StageSelect, Battle, Result }

        private ScreenState state = ScreenState.Title;
        private StageDefinition stage;
        private SpellDefinition selectedSpell;
        private OfflineWhisperSpellRecognizer offlineSpeech;
        private SpellAudioDirector audioDirector;
        private Font koreanFont;
        private Texture2D pixel;
        private Texture2D circle;
        private Texture2D glow;

        private int playerHealth;
        private int enemyHealth;
        private int combo;
        private int bestCombo;
        private int lastDamage;
        private float enemyAttackTimer;
        private float spellCooldown;
        private float hitFlash;
        private float enemyCastFlash;
        private float resultDelay;
        private bool playerWon;
        private string feedback = "아래 스킬을 고르고 주문을 외우세요.";
        private string recognizedText = string.Empty;
        private string testInput = string.Empty;
        private bool settingsOpen;
        private bool microphonePermissionResolved;
        private bool microphonePermissionGranted;
        private string selectedMicrophone;
        private float nextSpeechAttemptTime;
        private bool microphoneTestRunning;
        private string microphoneTestStatus = "마이크를 고른 뒤 테스트할 수 있습니다.";
        private AudioClip microphoneTestClip;
        private float microphoneTestLevel;
        private float smoothedInputLevel;
        private float castResultTimer;
        private string castResultTitle = string.Empty;
        private string castResultDetail = string.Empty;
        private bool castResultSuccess;
        private float enemyAttackGraceTimer;

        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;
        private GUIStyle centerStyle;
        private GUIStyle buttonStyle;
        private GUIStyle inputStyle;
        private GUIStyle badgeStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindFirstObjectByType<SpellGamePrototype>() != null) return;
            var host = new GameObject("Powerful Spell - Prototype Runtime");
            DontDestroyOnLoad(host);
            host.AddComponent<SpellGamePrototype>();
        }

        private void Awake()
        {
            Application.targetFrameRate = 120;
            pixel = MakePixel();
            circle = MakeCircle(128, false);
            glow = MakeCircle(128, true);
            koreanFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 24);
            offlineSpeech = new OfflineWhisperSpellRecognizer(this);
            audioDirector = gameObject.AddComponent<SpellAudioDirector>();
            if (FindFirstObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            StartCoroutine(RequestMicrophonePermission());
            if (Camera.main != null)
            {
                Camera.main.backgroundColor = new Color(.018f, .022f, .042f);
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
            }
        }

        private IEnumerator RequestMicrophonePermission()
        {
            microphonePermissionResolved = false;
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            microphonePermissionGranted = Application.HasUserAuthorization(UserAuthorization.Microphone);
            microphonePermissionResolved = true;
            RefreshMicrophones();
        }

        private void OnDestroy()
        {
            offlineSpeech?.Dispose();
        }

        private void Update()
        {
            ISpellSpeechRecognizer activeSpeech = ActiveSpeech;
            if (activeSpeech == null) return; // Play 중 스크립트 hot reload 직후 한 프레임 보호
            float dt = Time.deltaTime;
            hitFlash = Mathf.Max(0f, hitFlash - dt * 2.8f);
            enemyCastFlash = Mathf.Max(0f, enemyCastFlash - dt * 2f);
            spellCooldown = Mathf.Max(0f, spellCooldown - dt);
            castResultTimer = Mathf.Max(0f, castResultTimer - dt);
            enemyAttackGraceTimer = Mathf.Max(0f, enemyAttackGraceTimer - dt);
            float rawLevel = microphoneTestRunning ? MeasureMicrophoneLevel(microphoneTestClip, selectedMicrophone) : activeSpeech.InputLevel;
            smoothedInputLevel = Mathf.Lerp(smoothedInputLevel, rawLevel, 1f - Mathf.Exp(-dt * (rawLevel > smoothedInputLevel ? 18f : 6f)));

            if (state == ScreenState.Battle)
            {
                // 플레이어가 말하거나 PC가 음성을 분석하는 시간은 전투 시간이 아니다.
                if (!activeSpeech.IsListening && enemyAttackGraceTimer <= 0f)
                {
                    enemyAttackTimer -= dt;
                    if (enemyAttackTimer <= 0f) EnemyAttack();
                }
            }
            else if (state == ScreenState.Result)
            {
                resultDelay = Mathf.Max(0f, resultDelay - dt);
            }
        }

        private void OnGUI()
        {
            if (ActiveSpeech == null) return;
            EnsureStyles();
            float scale = Mathf.Min(Screen.width / DesignWidth, Screen.height / DesignHeight);
            float offsetX = (Screen.width - DesignWidth * scale) * .5f;
            float offsetY = (Screen.height - DesignHeight * scale) * .5f;
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0), Quaternion.identity, new Vector3(scale, scale, 1));

            DrawBackdrop();
            if (settingsOpen)
            {
                DrawSettings();
            }
            else
            {
                switch (state)
                {
                    case ScreenState.Title: DrawTitle(); break;
                    case ScreenState.StageSelect: DrawStageSelect(); break;
                    case ScreenState.Battle: DrawBattle(); break;
                    case ScreenState.Result: DrawResult(); break;
                }
            }
            GUI.matrix = oldMatrix;
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            GUI.skin.font = koreanFont;
            titleStyle = NewStyle(58, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(.96f, .91f, .76f));
            headingStyle = NewStyle(30, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            bodyStyle = NewStyle(22, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(.86f, .88f, .94f));
            bodyStyle.wordWrap = true;
            smallStyle = NewStyle(17, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(.65f, .69f, .78f));
            smallStyle.wordWrap = true;
            centerStyle = NewStyle(24, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            centerStyle.wordWrap = true;
            badgeStyle = NewStyle(16, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            buttonStyle = NewStyle(22, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            buttonStyle.normal.background = MakeSolidTexture(new Color(.17f, .13f, .28f));
            buttonStyle.hover.background = MakeSolidTexture(new Color(.29f, .2f, .48f));
            buttonStyle.active.background = MakeSolidTexture(new Color(.42f, .27f, .68f));
            buttonStyle.padding = new RectOffset(18, 18, 10, 10);
            inputStyle = new GUIStyle(GUI.skin.textField)
            {
                font = koreanFont, fontSize = 20, alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white, background = MakeSolidTexture(new Color(.055f, .06f, .1f)) },
                focused = { textColor = Color.white, background = MakeSolidTexture(new Color(.08f, .075f, .14f)) },
                padding = new RectOffset(14, 14, 8, 8)
            };
        }

        private GUIStyle NewStyle(int size, FontStyle fontStyle, TextAnchor anchor, Color color) => new GUIStyle
        {
            font = koreanFont, fontSize = size, fontStyle = fontStyle, alignment = anchor,
            normal = { textColor = color }, richText = true
        };

        private void DrawBackdrop()
        {
            DrawRect(new Rect(0, 0, DesignWidth, DesignHeight), new Color(.018f, .021f, .041f));
            DrawRect(new Rect(0, 0, DesignWidth, 5), new Color(.54f, .31f, .92f));
            GUI.color = new Color(.23f, .12f, .48f, .2f);
            GUI.DrawTexture(new Rect(-180, -250, 850, 850), glow);
            GUI.color = new Color(.09f, .42f, .62f, .12f);
            GUI.DrawTexture(new Rect(1370, 520, 800, 800), glow);
            GUI.color = Color.white;
            for (int i = 0; i < 12; i++)
            {
                float x = (i * 173 + 81) % 1900;
                float y = (i * 257 + 103) % 1050;
                float pulse = .25f + .2f * Mathf.Sin(Time.time * (1f + i * .07f) + i);
                DrawRect(new Rect(x, y, 2, 2), new Color(.8f, .72f, 1f, pulse));
            }
        }

        private void DrawTitle()
        {
            GUI.Label(new Rect(0, 164, DesignWidth, 54), "VOICE DUEL PROTOTYPE", Centered(smallStyle, new Color(.64f, .5f, 1f)));
            GUI.Label(new Rect(220, 225, 1480, 105), "SAY A POWERFUL SPELL", titleStyle);
            GUI.Label(new Rect(440, 340, 1040, 70), "목소리로 주문을 완성하고, 침묵을 깨뜨려라", Centered(bodyStyle, new Color(.79f, .81f, .9f)));

            DrawRune(new Vector2(960, 535), 150, new Color(.58f, .33f, 1f));

            if (GUI.Button(new Rect(740, 740, 440, 76), "스테이지 모드 입장", buttonStyle))
                state = ScreenState.StageSelect;
            if (GUI.Button(new Rect(740, 834, 440, 58), "음성 인식 설정", buttonStyle))
                settingsOpen = true;
            GUI.Label(new Rect(0, 972, DesignWidth, 35), "SPACE 키 또는 버튼으로 음성 입력 · API 없이도 테스트 입력 가능", Centered(smallStyle, new Color(.48f, .52f, .63f)));
        }

        private void DrawStageSelect()
        {
            GUI.Label(new Rect(110, 70, 900, 58), "스테이지 선택", titleStyleLeft());
            GUI.Label(new Rect(112, 130, 900, 38), "승리할 때마다 더 강하고 더 난처한 주문이 해금됩니다.", bodyStyle);
            if (GUI.Button(new Rect(1610, 76, 190, 52), "설정", buttonStyle)) settingsOpen = true;
            if (GUI.Button(new Rect(1388, 76, 190, 52), "처음으로", buttonStyle)) state = ScreenState.Title;

            int highest = HighestUnlockedStage;
            for (int i = 0; i < SpellGameData.Stages.Count; i++)
            {
                StageDefinition item = SpellGameData.Stages[i];
                float x = 100 + i * 356;
                Rect card = new Rect(x, 230, 320, 650);
                bool unlocked = item.number <= highest;
                DrawPanel(card, unlocked ? new Color(item.primaryColor.r, item.primaryColor.g, item.primaryColor.b, .34f) : new Color(.045f, .048f, .075f), unlocked ? item.glowColor : new Color(.2f, .22f, .28f));
                DrawMonster(new Vector2(x + 160, 410), 95, item, !unlocked, 0f);
                GUI.Label(new Rect(x + 28, 527, 264, 35), $"STAGE {item.number:00}", Centered(smallStyle, unlocked ? item.glowColor : new Color(.35f, .37f, .44f)));
                GUI.Label(new Rect(x + 24, 568, 272, 78), item.title, Centered(headingStyle, unlocked ? Color.white : new Color(.4f, .42f, .48f)));
                GUI.Label(new Rect(x + 30, 650, 260, 50), unlocked ? item.enemyName : "잠긴 스테이지", Centered(bodyStyle, unlocked ? new Color(.85f, .87f, .93f) : new Color(.34f, .36f, .43f)));
                GUI.Label(new Rect(x + 30, 708, 260, 62), unlocked ? item.subtitle : $"스테이지 {item.number - 1} 클리어 필요", Centered(smallStyle, new Color(.56f, .59f, .68f)));
                if (unlocked && GUI.Button(new Rect(x + 38, 795, 244, 58), item.number < highest ? "다시 도전" : "도전하기", buttonStyle))
                    StartStage(item);
            }

            GUI.Label(new Rect(100, 920, 1720, 55), $"해금된 일반 주문  {UnlockedSpellCount} / {NormalSpellCount}     ·     필살기: 전투 중 5콤보 달성     ·     최고 도달 스테이지  {highest} / 5", Centered(bodyStyle, new Color(.72f, .68f, .88f)));
        }

        private void StartStage(StageDefinition definition)
        {
            audioDirector.PlayClick();
            stage = definition;
            playerHealth = 100;
            enemyHealth = definition.enemyHealth;
            combo = 0;
            bestCombo = 0;
            lastDamage = 0;
            enemyAttackTimer = definition.attackInterval;
            spellCooldown = 0f;
            selectedSpell = SpellGameData.Spells.First(s => !s.isUltimate && s.unlockAfterStage < HighestUnlockedStage);
            feedback = "스킬을 고른 뒤 마이크 버튼을 누르고 주문을 말하세요.";
            recognizedText = string.Empty;
            testInput = selectedSpell.incantation;
            state = ScreenState.Battle;
        }

        private void DrawBattle()
        {
            DrawBattleHeader();
            DrawEnemyArena();
            DrawIncantation();
            DrawCastResultBanner();
            DrawSpeechPanel();
            DrawSpellBar();
            HandleBattleHotkeys();
        }

        private void DrawBattleHeader()
        {
            GUI.Label(new Rect(78, 38, 600, 42), $"STAGE {stage.number:00}  ·  {stage.title}", headingStyle);
            if (GUI.Button(new Rect(1470, 34, 170, 48), "음성 설정", buttonStyle))
            {
                StopSpeech();
                settingsOpen = true;
            }
            if (GUI.Button(new Rect(1670, 34, 170, 48), "포기하기", buttonStyle))
            {
                StopSpeech();
                state = ScreenState.StageSelect;
            }

            DrawHealthBar(new Rect(80, 100, 580, 34), playerHealth / 100f, new Color(.16f, .78f, .56f), $"플레이어  {Mathf.Max(0, playerHealth)} / 100");
            DrawHealthBar(new Rect(1260, 100, 580, 34), enemyHealth / (float)stage.enemyHealth, stage.glowColor, $"{stage.enemyName}  {Mathf.Max(0, enemyHealth)} / {stage.enemyHealth}");
            GUI.Label(new Rect(780, 89, 360, 55), combo > 1 ? $"{combo} COMBO  ×{ComboMultiplier:0.00}" : "COMBO READY", Centered(headingStyle, combo > 1 ? new Color(1f, .76f, .22f) : new Color(.42f, .45f, .55f)));
        }

        private void DrawEnemyArena()
        {
            // 전투 공간을 작은 패널로 가두지 않고 화면 전체 배경과 연결한다.
            DrawRect(new Rect(0, 145, DesignWidth, 470), new Color(stage.primaryColor.r * .12f, stage.primaryColor.g * .12f, stage.primaryColor.b * .15f, .34f));
            DrawRect(new Rect(0, 470, DesignWidth, 145), new Color(.012f, .014f, .026f, .72f));
            DrawRect(new Rect(0, 469, DesignWidth, 2), new Color(stage.glowColor.r, stage.glowColor.g, stage.glowColor.b, .18f));

            for (int i = 0; i < 9; i++)
            {
                float x = 90 + i * 235f;
                float height = 55 + (i % 3) * 28;
                DrawRotatedRect(new Rect(x, 420 - height * .5f, 105, height), i % 2 == 0 ? -27f : 25f, new Color(.018f, .021f, .036f, .9f));
            }

            GUI.color = new Color(stage.glowColor.r, stage.glowColor.g, stage.glowColor.b, .16f + hitFlash * .22f);
            GUI.DrawTexture(new Rect(585, 85, 750, 750), glow);
            GUI.color = new Color(0, 0, 0, .48f);
            GUI.DrawTexture(new Rect(690, 520, 540, 105), circle);
            GUI.color = Color.white;
            DrawMonster(new Vector2(960, 402 + Mathf.Sin(Time.time * 1.8f) * 7f), 225 + enemyCastFlash * 14f, stage, false, hitFlash);
            DrawPlayerForeground();
            GUI.Label(new Rect(690, 558, 540, 35), enemyCastFlash > 0 ? "적의 즉시 시전 공격!" : $"다음 공격까지  {Mathf.Max(0, enemyAttackTimer):0.0}초", Centered(smallStyle, enemyCastFlash > 0 ? new Color(1f, .35f, .28f) : new Color(.68f, .7f, .8f)));
            if (lastDamage > 0 && hitFlash > 0)
                GUI.Label(new Rect(1170, 265, 260, 80), $"-{lastDamage}", Centered(titleStyle, selectedSpell.color));
        }

        private void DrawPlayerForeground()
        {
            Color sleeve = new Color(.055f, .045f, .085f);
            Color skin = new Color(.63f, .48f, .42f);
            DrawRotatedRect(new Rect(120, 505, 390, 120), -12f, sleeve);
            DrawRotatedRect(new Rect(1410, 505, 390, 120), 12f, sleeve);
            GUI.color = skin;
            GUI.DrawTexture(new Rect(430, 500, 125, 110), circle);
            GUI.DrawTexture(new Rect(1365, 500, 125, 110), circle);
            GUI.color = Color.white;
            DrawRune(new Vector2(960, 535), 54 + Mathf.Sin(Time.time * 3f) * 3f, selectedSpell.color);
        }

        private void DrawIncantation()
        {
            Rect panel = new Rect(300, 150, 1320, 126);
            DrawPanel(panel, new Color(.018f, .021f, .045f, .88f), new Color(selectedSpell.color.r, selectedSpell.color.g, selectedSpell.color.b, .75f));
            string phrase = DisruptedPhrase(selectedSpell);
            float alpha = selectedSpell.disruption == SpellDisruption.Blink && Mathf.Sin(Time.time * 8f) > .55f ? .12f : 1f;
            GUIStyle phraseStyle = Centered(centerStyle, new Color(1f, .94f, .8f, alpha));
            phraseStyle.fontSize = selectedSpell.isUltimate ? 17 : (selectedSpell.incantation.Length > 65 ? 19 : 23);
            GUI.Label(new Rect(330, 159, 1260, 108), phrase, phraseStyle);
        }

        private void DrawSpeechPanel()
        {
            Rect panel = new Rect(180, 615, 1560, 150);
            DrawPanel(panel, new Color(.032f, .035f, .065f, .96f), new Color(.28f, .24f, .5f));
            bool listening = ActiveSpeech.IsListening && !ActiveSpeech.IsProcessing;
            string instruction = ActiveSpeech.IsProcessing
                ? "음성을 분석하고 있습니다… 잠시 기다려주세요"
                : listening
                ? "지금 주문을 외우세요! 말을 마치면 자동으로 판정됩니다"
                : "SPACE를 한 번 누른 뒤 주문을 외우세요! (누르고 있을 필요 없음)";
            GUI.Label(new Rect(220, 625, 1010, 36), instruction, Left(headingStyle, listening ? new Color(1f, .78f, .28f) : new Color(.82f, .84f, .94f)));

            string live = ActiveSpeech.LiveTranscript;
            string heard = !string.IsNullOrWhiteSpace(live)
                ? "받아쓰는 중: “" + live + "…”"
                : (!string.IsNullOrWhiteSpace(recognizedText) ? "인식 결과: “" + recognizedText + "”" : feedback);
            GUI.Label(new Rect(220, 666, 1010, 30), heard, smallStyle);
            DrawInputMeter(new Rect(220, 708, 1010, 24), smoothedInputLevel, listening ? "VOICE INPUT" : "MIC READY");

            string micLabel = ActiveSpeech.IsProcessing ? "분석 중…" : (ActiveSpeech.IsListening ? "● 듣는 중 — 주문을 말하세요" : "● 주문 말하기  [SPACE 한 번]");
            GUI.enabled = spellCooldown <= 0f || ActiveSpeech.IsListening;
            if (GUI.Button(new Rect(1250, 638, 440, 76), micLabel, buttonStyle)) ToggleSpeech();
            GUI.enabled = true;
            GUI.Label(new Rect(1250, 718, 440, 26), $"{ActiveSpeech.DisplayName}  ·  {MicrophoneSummary}", Centered(smallStyle, microphonePermissionGranted ? new Color(.58f, .72f, .66f) : new Color(1f, .48f, .38f)));
        }

        private void DrawInputMeter(Rect rect, float level, string label)
        {
            DrawRect(rect, new Color(.018f, .02f, .04f, .95f));
            float fill = Mathf.Clamp01(level);
            Color meterColor = Color.Lerp(new Color(.2f, .64f, 1f), fill > .78f ? new Color(1f, .35f, .22f) : new Color(.34f, 1f, .58f), fill);
            DrawRect(new Rect(rect.x + 3, rect.y + 3, (rect.width - 6) * fill, rect.height - 6), meterColor);
            for (int i = 1; i < 10; i++) DrawRect(new Rect(rect.x + rect.width * i / 10f, rect.y + 3, 2, rect.height - 6), new Color(.02f, .025f, .05f, .75f));
            GUI.Label(new Rect(rect.x, rect.y - 1, rect.width - 8, rect.height), label, Right(badgeStyle, Color.white));
        }

        private void DrawSpellBar()
        {
            GUI.Label(new Rect(180, 787, 620, 34), combo >= 5 ? "보유 주문  ·  필살기 사용 가능!" : $"보유 주문  ·  필살기까지 {Mathf.Max(0, 5 - combo)}콤보", Left(headingStyle, combo >= 5 ? new Color(1f, .82f, .2f) : Color.white));
            int highest = HighestUnlockedStage;
            for (int i = 0; i < SpellGameData.Spells.Count; i++)
            {
                SpellDefinition spell = SpellGameData.Spells[i];
                bool stageUnlocked = spell.unlockAfterStage < highest;
                bool comboUnlocked = combo >= spell.requiredCombo;
                bool unlocked = stageUnlocked && comboUnlocked;
                float x = 180 + i * 270;
                Rect card = new Rect(x, 835, 250, 170);
                Color border = selectedSpell == spell ? spell.color : new Color(.19f, .2f, .3f);
                DrawPanel(card, unlocked ? new Color(.05f, .052f, .09f, .98f) : new Color(.025f, .027f, .045f, .98f), border);
                DrawRune(new Vector2(x + 42, 879), 22, unlocked ? spell.color : new Color(.24f, .25f, .3f));
                GUI.Label(new Rect(x + 76, 852, 158, 44), stageUnlocked ? spell.displayName : "???", Left(bodyStyle, unlocked ? Color.white : new Color(.42f, .43f, .5f)));
                string lockText = !stageUnlocked ? $"스테이지 {spell.unlockAfterStage} 클리어" : (!comboUnlocked ? $"5 COMBO 필요  ({combo}/5)" : spell.shortDescription);
                GUI.Label(new Rect(x + 20, 910, 210, 48), lockText, Left(smallStyle, spell.isUltimate && comboUnlocked ? spell.color : new Color(.55f, .58f, .67f)));
                GUI.Label(new Rect(x + 20, 970, 210, 24), unlocked ? $"기본 피해 {spell.damage}" : "LOCKED", Left(badgeStyle, unlocked ? spell.color : new Color(.36f, .37f, .44f)));
                if (unlocked && GUI.Button(card, GUIContent.none, GUIStyle.none))
                {
                    selectedSpell = spell;
                    testInput = spell.incantation;
                    feedback = $"{spell.displayName} 선택 — 위 문장을 소리 내어 읽으세요.";
                    audioDirector.PlayClick();
                }
            }
        }

        private void ToggleSpeech()
        {
            if (ActiveSpeech.IsListening)
            {
                feedback = "이미 듣고 있습니다. Space에서 손을 떼고 주문을 끝까지 말하세요.";
                return;
            }
            if (spellCooldown > 0f)
            {
                feedback = $"주문 재사용 대기 중: {spellCooldown:0.0}초";
                return;
            }
            if (Time.unscaledTime < nextSpeechAttemptTime)
            {
                feedback = "음성 인식 장치를 정리하는 중입니다. 잠시 후 다시 시도하세요.";
                return;
            }
            if (!microphonePermissionResolved)
            {
                feedback = "마이크 권한을 확인하는 중입니다. 잠시 후 다시 눌러주세요.";
                return;
            }
            if (!microphonePermissionGranted)
            {
                feedback = "마이크 권한이 없습니다. Windows 개인정보 설정에서 Unity Editor의 마이크 접근을 허용하세요.";
                return;
            }
            if (!ActiveSpeech.Available)
            {
                feedback = offlineSpeech.AvailabilityError;
                return;
            }
            recognizedText = string.Empty;
            feedback = "듣고 있습니다… 주문을 끝까지 말한 뒤 잠시 기다리세요.";
            nextSpeechAttemptTime = Time.unscaledTime + 1.25f;
            audioDirector.PlayListen();
            ActiveSpeech.Begin(selectedSpell.incantation, OnSpeechResult, OnSpeechError);
        }

        private void OnSpeechResult(string text)
        {
            recognizedText = text;
            JudgePhrase(text);
        }

        private void OnSpeechError(string error)
        {
            feedback = error;
            enemyAttackGraceTimer = 1.1f;
            nextSpeechAttemptTime = Time.unscaledTime + 2.5f;
            audioDirector.PlayFizzle();
        }

        private void JudgePhrase(string text)
        {
            if (state != ScreenState.Battle || spellCooldown > 0f) return;
            float requiredScore = RequiredMatchThreshold(selectedSpell);
            PhraseMatchResult match = SpellPhraseMatcher.Match(selectedSpell.incantation, text, requiredScore);
            recognizedText = text;
            Debug.Log($"[Spell Voice] expected=\"{match.NormalizedExpected}\" recognized=\"{match.NormalizedRecognized}\" " +
                $"success={match.Success} score={match.Score:0.000}/{requiredScore:0.000} length={match.LengthRatio:0.000} " +
                $"phonetic={match.PhoneticScore:0.000} bigram={match.BigramScore:0.000} words={match.WordCoverage:0.000} ending={match.EndingScore:0.000}");
            enemyAttackGraceTimer = 1.1f;
            if (match.Success)
            {
                bool usedUltimate = selectedSpell.isUltimate;
                int damageCombo = usedUltimate ? combo : combo + 1;
                if (!usedUltimate) combo++;
                bestCombo = Mathf.Max(bestCombo, damageCombo);
                lastDamage = Mathf.RoundToInt(selectedSpell.damage * GetComboMultiplier(damageCombo));
                enemyHealth -= lastDamage;
                hitFlash = 1f;
                spellCooldown = selectedSpell.cooldown;
                feedback = $"주문 성공!  정확도 {match.Score * 100f:0}%  ·  {lastDamage} 피해";
                ShowCastResult(true, "주문 성공!", $"정확도 {match.Score * 100f:0}%  ·  {lastDamage} 피해" + (usedUltimate ? "  ·  필살기 발동" : $"  ·  {combo} COMBO"));
                int spellIndex = SpellGameData.Spells.ToList().IndexOf(selectedSpell);
                audioDirector.PlayCast(1f + spellIndex * .08f);
                if (usedUltimate)
                {
                    combo = 0;
                    selectedSpell = SpellGameData.Spells.First(s => !s.isUltimate && s.unlockAfterStage < HighestUnlockedStage);
                    testInput = selectedSpell.incantation;
                }
                if (enemyHealth <= 0)
                {
                    enemyAttackTimer = 999f;
                    StartCoroutine(FinishAfterCastResult());
                }
            }
            else
            {
                bool failedUltimate = selectedSpell.isUltimate;
                combo = 0;
                feedback = $"주문 실패... 유사도 {match.Score * 100f:0}%  (성공 기준 {requiredScore * 100f:0}%)";
                ShowCastResult(false, "주문 실패...", $"유사도 {match.Score * 100f:0}% / 기준 {requiredScore * 100f:0}%  ·  다시 시도하세요");
                audioDirector.PlayFizzle();
                if (failedUltimate)
                {
                    selectedSpell = SpellGameData.Spells.First(s => !s.isUltimate && s.unlockAfterStage < HighestUnlockedStage);
                    testInput = selectedSpell.incantation;
                }
            }
        }

        private void ShowCastResult(bool success, string title, string detail)
        {
            castResultSuccess = success;
            castResultTitle = title;
            castResultDetail = detail;
            castResultTimer = 2.1f;
        }

        private static float RequiredMatchThreshold(SpellDefinition spell)
        {
            if (spell.isUltimate) return .64f;
            if (spell.incantation.Length > 65) return .66f;
            return .68f;
        }

        private IEnumerator FinishAfterCastResult()
        {
            yield return new WaitForSecondsRealtime(1.35f);
            if (state == ScreenState.Battle && enemyHealth <= 0) FinishBattle(true);
        }

        private void DrawCastResultBanner()
        {
            if (castResultTimer <= 0f) return;
            float alpha = Mathf.Clamp01(castResultTimer * 2f);
            Color accent = castResultSuccess ? new Color(.28f, 1f, .62f, alpha) : new Color(1f, .25f, .28f, alpha);
            Rect panel = new Rect(650, 285, 620, 110);
            DrawPanel(panel, new Color(.015f, .018f, .038f, .9f * alpha), accent);
            GUI.Label(new Rect(675, 294, 570, 54), castResultTitle, Centered(headingStyle, accent));
            GUI.Label(new Rect(675, 347, 570, 34), castResultDetail, Centered(smallStyle, new Color(1f, 1f, 1f, alpha)));
        }

        private void EnemyAttack()
        {
            if (state != ScreenState.Battle) return;
            playerHealth -= stage.enemyDamage;
            enemyCastFlash = 1f;
            enemyAttackTimer = stage.attackInterval;
            audioDirector.PlayEnemyAttack();
            feedback = $"{stage.enemyName}의 공격! {stage.enemyDamage} 피해 — 적은 음성 없이 즉시 시전합니다.";
            if (playerHealth <= 0) FinishBattle(false);
        }

        private void FinishBattle(bool victory)
        {
            StopSpeech();
            playerWon = victory;
            if (victory) audioDirector.PlayVictory(); else audioDirector.PlayDefeat();
            resultDelay = .5f;
            if (victory)
            {
                int next = Mathf.Min(5, stage.number + 1);
                if (next > HighestUnlockedStage)
                {
                    PlayerPrefs.SetInt(UnlockKey, next);
                    PlayerPrefs.Save();
                }
            }
            state = ScreenState.Result;
        }

        private void DrawResult()
        {
            GUI.Label(new Rect(0, 170, DesignWidth, 70), playerWon ? "STAGE CLEAR" : "VOICE LOST", Centered(titleStyle, playerWon ? new Color(1f, .77f, .25f) : new Color(1f, .3f, .3f)));
            DrawMonster(new Vector2(960, 410), 125, stage, !playerWon, playerWon ? 1f : 0f);
            GUI.Label(new Rect(500, 550, 920, 60), playerWon ? $"{stage.enemyName}을(를) 물리쳤습니다!" : "목소리를 가다듬고 다시 도전하세요.", Centered(headingStyle, Color.white));
            GUI.Label(new Rect(500, 620, 920, 45), $"최고 콤보  {bestCombo}     ·     남은 체력  {Mathf.Max(0, playerHealth)}", Centered(bodyStyle, new Color(.72f, .75f, .84f)));
            if (playerWon && stage.number < 5)
            {
                SpellDefinition unlocked = SpellGameData.Spells.FirstOrDefault(s => s.unlockAfterStage == stage.number);
                if (unlocked != null) GUI.Label(new Rect(500, 680, 920, 45), $"새 주문 해금:  {unlocked.displayName}", Centered(bodyStyle, unlocked.color));
            }
            GUI.enabled = resultDelay <= 0f;
            if (GUI.Button(new Rect(610, 790, 330, 68), "스테이지 선택", buttonStyle)) state = ScreenState.StageSelect;
            if (GUI.Button(new Rect(980, 790, 330, 68), "다시 도전", buttonStyle)) StartStage(stage);
            GUI.enabled = true;
        }

        private void DrawSettings()
        {
            DrawRect(new Rect(0, 0, DesignWidth, DesignHeight), new Color(0, 0, 0, .72f));
            Rect panel = new Rect(455, 125, 1010, 855);
            DrawPanel(panel, new Color(.035f, .038f, .072f), new Color(.45f, .3f, .8f));
            GUI.Label(new Rect(515, 205, 800, 55), "음성 인식 설정", headingStyle);
            GUI.Label(new Rect(515, 275, 880, 60), "음성 인식은 게임에 포함된 오프라인 Whisper만 사용합니다. API 키와 인터넷 연결이 필요하지 않습니다.", bodyStyle);
            GUI.Label(new Rect(515, 330, 890, 30), $"마이크 상태: {MicrophoneSummary}", Left(smallStyle, microphonePermissionGranted ? new Color(.36f, .9f, .68f) : new Color(1f, .45f, .36f)));

            DrawPanel(new Rect(515, 385, 890, 110), new Color(.055f, .06f, .1f), new Color(.28f, .65f, .9f));
            bool whisperReady = offlineSpeech.Available;
            string whisperStatus = whisperReady ? "✓ 오프라인 Whisper 준비됨" : "! Whisper 준비 필요";
            string whisperDetail = whisperReady
                ? "녹음 파일은 PC의 임시 폴더에서 처리 후 삭제되며 외부로 전송되지 않습니다."
                : offlineSpeech.AvailabilityError;
            GUI.Label(new Rect(545, 402, 830, 38), whisperStatus,
                Left(headingStyle, whisperReady ? new Color(.36f, .9f, .68f) : new Color(1f, .58f, .28f)));
            GUI.Label(new Rect(545, 447, 830, 40), whisperDetail, smallStyle);

            GUI.Label(new Rect(515, 545, 890, 35), "입력 마이크 선택", bodyStyle);
            if (GUI.Button(new Rect(515, 590, 80, 54), "◀", buttonStyle)) CycleMicrophone(-1);
            DrawPanel(new Rect(610, 590, 700, 54), new Color(.055f, .06f, .1f), new Color(.22f, .2f, .38f));
            GUI.Label(new Rect(625, 597, 670, 40), MicrophoneSummary, Centered(bodyStyle, Color.white));
            if (GUI.Button(new Rect(1325, 590, 80, 54), "▶", buttonStyle)) CycleMicrophone(1);
            GUI.Label(new Rect(515, 652, 890, 30), "선택한 마이크의 한국어 음성을 게임 PC 안에서만 분석합니다.", smallStyle);
            GUI.enabled = !microphoneTestRunning && microphonePermissionGranted && Microphone.devices.Length > 0;
            if (GUI.Button(new Rect(515, 705, 410, 58), microphoneTestRunning ? "테스트 녹음 중…" : "2초 녹음 후 재생", buttonStyle)) StartCoroutine(TestSelectedMicrophone());
            GUI.enabled = true;
            if (GUI.Button(new Rect(995, 705, 410, 58), "저장하고 닫기", buttonStyle)) settingsOpen = false;
            GUI.Label(new Rect(515, 775, 890, 28), microphoneTestStatus, smallStyle);
            DrawInputMeter(new Rect(515, 815, 890, 24), microphoneTestRunning ? microphoneTestLevel : smoothedInputLevel, microphoneTestRunning ? "RECORDING" : "INPUT LEVEL");
        }

        private void HandleBattleHotkeys()
        {
            GUI.SetNextControlName("PrototypeTestInput");
            testInput = GUI.TextField(new Rect(1060, 1018, 520, 42), testInput, inputStyle);
            if (GUI.Button(new Rect(1595, 1018, 145, 42), "문장 판정", buttonStyle)) JudgePhrase(testInput);
            GUI.Label(new Rect(740, 1018, 300, 42), "프로토타입 테스트 입력 →", Right(smallStyle, new Color(.5f, .53f, .62f)));

            Event current = Event.current;
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Space && GUI.GetNameOfFocusedControl() != "PrototypeTestInput")
            {
                ToggleSpeech();
                current.Use();
            }
        }

        private ISpellSpeechRecognizer ActiveSpeech => offlineSpeech;
        private string MicrophoneSummary
        {
            get
            {
                if (!microphonePermissionResolved) return "권한 확인 중";
                if (!microphonePermissionGranted) return "권한 거부됨";
                if (Microphone.devices.Length == 0) return "입력 장치 없음";
                return string.IsNullOrWhiteSpace(selectedMicrophone) ? Microphone.devices[0] : selectedMicrophone;
            }
        }

        private void RefreshMicrophones()
        {
            string[] devices = Microphone.devices;
            if (devices.Length == 0)
            {
                selectedMicrophone = null;
                return;
            }

            string saved = PlayerPrefs.GetString(MicrophoneKey, string.Empty);
            selectedMicrophone = devices.Contains(saved) ? saved : devices[0];
            offlineSpeech.SetMicrophoneDevice(selectedMicrophone);
        }

        private void CycleMicrophone(int direction)
        {
            string[] devices = Microphone.devices;
            if (devices.Length == 0)
            {
                RefreshMicrophones();
                return;
            }

            int current = Array.IndexOf(devices, selectedMicrophone);
            if (current < 0) current = 0;
            current = (current + direction + devices.Length) % devices.Length;
            selectedMicrophone = devices[current];
            offlineSpeech.SetMicrophoneDevice(selectedMicrophone);
            PlayerPrefs.SetString(MicrophoneKey, selectedMicrophone);
            PlayerPrefs.Save();
            audioDirector.PlayClick();
        }

        private IEnumerator TestSelectedMicrophone()
        {
            if (microphoneTestRunning) yield break;
            StopSpeech();
            if (!microphonePermissionGranted || Microphone.devices.Length == 0)
            {
                microphoneTestStatus = "마이크 권한 또는 입력 장치를 확인하세요.";
                yield break;
            }

            microphoneTestRunning = true;
            microphoneTestStatus = $"{MicrophoneSummary}에서 녹음 중… 지금 말해보세요.";
            microphoneTestClip = Microphone.Start(selectedMicrophone, false, 3, 16000);
            if (microphoneTestClip == null)
            {
                microphoneTestStatus = "테스트 녹음을 시작하지 못했습니다.";
                microphoneTestRunning = false;
                yield break;
            }

            float endTime = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < endTime)
            {
                microphoneTestLevel = MeasureMicrophoneLevel(microphoneTestClip, selectedMicrophone);
                yield return null;
            }
            int position = Microphone.GetPosition(selectedMicrophone);
            Microphone.End(selectedMicrophone);
            microphoneTestRunning = false;
            microphoneTestLevel = 0f;
            if (position <= 0)
            {
                microphoneTestStatus = "녹음 데이터가 없습니다. Windows 마이크 권한을 확인하세요.";
                yield break;
            }

            AudioClip trimmed = CreateNormalizedPreview(microphoneTestClip, position);
            microphoneTestClip = null;
            if (trimmed == null)
            {
                microphoneTestStatus = "목소리가 감지되지 않았습니다. 게이지가 움직이는지 확인하세요.";
                yield break;
            }
            microphoneTestStatus = "녹음 완료 — 들리기 쉽게 증폭하여 재생합니다.";
            audioDirector.PlayMicrophonePreview(trimmed);
        }

        private static float MeasureMicrophoneLevel(AudioClip clip, string device)
        {
            if (clip == null) return 0f;
            int position = Microphone.GetPosition(device);
            if (position <= 0) return 0f;
            int count = Mathf.Min(512, position);
            int offset = Mathf.Max(0, position - count);
            var samples = new float[count * clip.channels];
            if (!clip.GetData(samples, offset)) return 0f;
            double sum = 0d;
            for (int i = 0; i < samples.Length; i++) sum += samples[i] * samples[i];
            return Mathf.Clamp01(Mathf.Sqrt((float)(sum / Math.Max(1, samples.Length))) * 10f);
        }

        private static AudioClip CreateNormalizedPreview(AudioClip source, int frames)
        {
            if (source == null || frames <= 0) return null;
            var data = new float[frames * source.channels];
            source.GetData(data, 0);
            float peak = 0f;
            for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            if (peak < .001f) return null;
            float gain = Mathf.Min(8f, .82f / peak);
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i] * gain, -.9f, .9f);
            AudioClip result = AudioClip.Create("Microphone Preview", frames, source.channels, source.frequency, false);
            result.SetData(data, 0);
            return result;
        }
        private float ComboMultiplier => GetComboMultiplier(combo);
        private static float GetComboMultiplier(int comboCount) => Mathf.Min(2f, 1f + Mathf.Max(0, comboCount - 1) * .12f);
        private int HighestUnlockedStage => Mathf.Clamp(PlayerPrefs.GetInt(UnlockKey, 1), 1, 5);
        private int UnlockedSpellCount => SpellGameData.Spells.Count(s => !s.isUltimate && s.unlockAfterStage < HighestUnlockedStage);
        private int NormalSpellCount => SpellGameData.Spells.Count(s => !s.isUltimate);

        private void StopSpeech()
        {
            offlineSpeech.Stop();
        }

        private string DisruptedPhrase(SpellDefinition spell)
        {
            if (spell.disruption != SpellDisruption.FadeWords) return spell.incantation;
            string[] words = spell.incantation.Split(' ');
            int hidden = Mathf.Abs(Mathf.FloorToInt(Time.time * 1.4f)) % words.Length;
            for (int i = 0; i < words.Length; i++)
                if (i == hidden || (i + 4) % words.Length == hidden) words[i] = "████";
            return string.Join(" ", words);
        }

        private void DrawHealthBar(Rect rect, float ratio, Color color, string label)
        {
            DrawRect(rect, new Color(.045f, .048f, .072f));
            DrawRect(new Rect(rect.x + 4, rect.y + 4, (rect.width - 8) * Mathf.Clamp01(ratio), rect.height - 8), color);
            GUI.Label(rect, label, Centered(badgeStyle, Color.white));
        }

        private void DrawPanel(Rect rect, Color fill, Color border)
        {
            DrawRect(rect, border);
            DrawRect(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4), fill);
            DrawRect(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, 3), new Color(border.r, border.g, border.b, .8f));
        }

        private void DrawMonster(Vector2 center, float size, StageDefinition monsterStage, bool locked, float flash)
        {
            Color baseColor = locked ? new Color(.12f, .13f, .17f) : Color.Lerp(monsterStage.primaryColor, Color.white, flash * .65f);
            Color accent = locked ? new Color(.25f, .26f, .3f) : monsterStage.glowColor;
            GUI.color = new Color(accent.r, accent.g, accent.b, locked ? .05f : .22f);
            GUI.DrawTexture(new Rect(center.x - size * 1.15f, center.y - size * 1.15f, size * 2.3f, size * 2.3f), glow);
            GUI.color = baseColor;
            GUI.DrawTexture(new Rect(center.x - size * .66f, center.y - size * .5f, size * 1.32f, size * 1.22f), circle);
            GUI.DrawTexture(new Rect(center.x - size * .48f, center.y + size * .25f, size * .96f, size * .66f), circle);
            DrawRotatedRect(new Rect(center.x - size * .72f, center.y - size * .72f, size * .22f, size * .8f), -34f, baseColor);
            DrawRotatedRect(new Rect(center.x + size * .5f, center.y - size * .72f, size * .22f, size * .8f), 34f, baseColor);
            GUI.color = accent;
            GUI.DrawTexture(new Rect(center.x - size * .39f, center.y - size * .12f, size * .24f, size * .16f), circle);
            GUI.DrawTexture(new Rect(center.x + size * .15f, center.y - size * .12f, size * .24f, size * .16f), circle);
            GUI.color = new Color(.015f, .018f, .028f);
            GUI.DrawTexture(new Rect(center.x - size * .30f, center.y - size * .06f, size * .06f, size * .06f), circle);
            GUI.DrawTexture(new Rect(center.x + size * .24f, center.y - size * .06f, size * .06f, size * .06f), circle);
            DrawRotatedRect(new Rect(center.x - size * .28f, center.y + size * .2f, size * .56f, size * .07f), 0f, new Color(.02f, .02f, .035f));
            GUI.color = Color.white;
        }

        private void DrawRune(Vector2 center, float radius, Color color)
        {
            GUI.color = new Color(color.r, color.g, color.b, .22f);
            GUI.DrawTexture(new Rect(center.x - radius * 1.7f, center.y - radius * 1.7f, radius * 3.4f, radius * 3.4f), glow);
            GUI.color = color;
            DrawRotatedRect(new Rect(center.x - radius, center.y - 2, radius * 2, 4), Time.time * 12f, color);
            DrawRotatedRect(new Rect(center.x - radius, center.y - 2, radius * 2, 4), 60f + Time.time * 12f, color);
            DrawRotatedRect(new Rect(center.x - radius, center.y - 2, radius * 2, 4), 120f + Time.time * 12f, color);
            GUI.DrawTexture(new Rect(center.x - radius * .24f, center.y - radius * .24f, radius * .48f, radius * .48f), circle);
            GUI.color = Color.white;
        }

        private void DrawRotatedRect(Rect rect, float angle, Color color)
        {
            Matrix4x4 matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, rect.center);
            DrawRect(rect, color);
            GUI.matrix = matrix;
        }

        private void DrawRect(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, pixel);
            GUI.color = old;
        }

        private GUIStyle Centered(GUIStyle source, Color color) => Styled(source, TextAnchor.MiddleCenter, color);
        private GUIStyle Left(GUIStyle source, Color color) => Styled(source, TextAnchor.MiddleLeft, color);
        private GUIStyle Right(GUIStyle source, Color color) => Styled(source, TextAnchor.MiddleRight, color);
        private GUIStyle titleStyleLeft() => Styled(titleStyle, TextAnchor.MiddleLeft, titleStyle.normal.textColor);

        private static GUIStyle Styled(GUIStyle source, TextAnchor alignment, Color color)
        {
            var style = new GUIStyle(source) { alignment = alignment };
            style.normal.textColor = color;
            return style;
        }

        private static Texture2D MakePixel()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "Prototype Pixel" };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return texture;
        }

        private static Texture2D MakeSolidTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static Texture2D MakeCircle(int size, bool radial)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = radial ? "Prototype Glow" : "Prototype Circle" };
            var colors = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * .5f, (size - 1) * .5f);
            float radius = size * .5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = radial ? Mathf.Clamp01(1f - distance) * Mathf.Clamp01(1f - distance) : Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(.92f, 1f, distance));
                colors[y * size + x] = new Color(1, 1, 1, alpha);
            }
            texture.SetPixels(colors);
            texture.Apply();
            return texture;
        }
    }
}
