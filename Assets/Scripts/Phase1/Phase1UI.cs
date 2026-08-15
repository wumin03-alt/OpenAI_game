using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DemonCompany.Phase1
{
    public sealed class Phase1UI : MonoBehaviour
    {
        private static readonly Color Background = new Color(0.025f, 0.035f, 0.065f, 1f);
        private static readonly Color Panel = new Color(0.075f, 0.095f, 0.15f, 0.96f);
        private static readonly Color PanelLight = new Color(0.12f, 0.15f, 0.22f, 0.98f);
        private static readonly Color Accent = new Color(0.3f, 0.9f, 0.78f, 1f);
        private static readonly Color Gold = new Color(1f, 0.75f, 0.28f, 1f);
        private Phase1GameController controller;
        private RectTransform canvasRoot;
        private GameObject screenRoot;
        private Text phaseText;
        private Text budgetText;
        private Text noticeText;
        private Text battleDungeonText;
        private Text battleRosterText;
        private Text battleEventText;
        private InterviewSpriteAnimator interviewAnimator;
        private Coroutine noticeRoutine;

        public void Build(Phase1GameController gameController)
        {
            controller = gameController;
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            canvasRoot = gameObject.GetComponent<RectTransform>();

            GameObject background = MakePanel(canvasRoot, "Background", new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, Background);
            background.transform.SetAsFirstSibling();
            GameObject header = MakePanel(canvasRoot, "Header", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -104f), Vector2.zero, new Color(0.055f, 0.075f, 0.125f, 1f));
            phaseText = MakeText(header.transform, "Phase", new Vector2(32f, -18f), new Vector2(1240f, 66f), 34,
                TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            budgetText = MakeText(header.transform, "Budget", new Vector2(-32f, -18f), new Vector2(560f, 66f), 30,
                TextAnchor.MiddleRight, Gold, FontStyle.Bold, true);

            noticeText = MakeText(canvasRoot, "Notice", new Vector2(0f, 24f), new Vector2(1500f, 58f), 25,
                TextAnchor.MiddleCenter, Color.white, FontStyle.Bold, false, new Vector2(0.5f, 0f));
            noticeText.gameObject.SetActive(false);
        }

        public void ShowInterview()
        {
            BuildScreen("Interview Screen");
            phaseText.text = "01  INTERVIEW  ·  FACE TO FACE";
            UpdateBudget();

            Color wall = new Color(0.11f, 0.085f, 0.085f, 1f);
            Color wallInset = new Color(0.075f, 0.058f, 0.065f, 1f);
            Color wood = new Color(0.27f, 0.145f, 0.075f, 1f);
            Color woodLight = new Color(0.48f, 0.275f, 0.12f, 1f);
            Color parchment = new Color(0.86f, 0.78f, 0.61f, 1f);

            MakeFixedPanel(screenRoot.transform, "Interview Room Wall", new Vector2(0f, -104f), new Vector2(1920f, 690f), wall);
            MakeFixedPanel(screenRoot.transform, "Lower Wall", new Vector2(0f, -626f), new Vector2(1920f, 168f), wallInset);
            MakeFixedPanel(screenRoot.transform, "Floor", new Vector2(0f, -794f), new Vector2(1920f, 286f), new Color(0.045f, 0.038f, 0.045f, 1f));
            MakeFixedPanel(screenRoot.transform, "Left Column", new Vector2(286f, -104f), new Vector2(26f, 690f), new Color(0.16f, 0.105f, 0.075f, 1f));
            MakeFixedPanel(screenRoot.transform, "Right Column", new Vector2(1404f, -104f), new Vector2(26f, 690f), new Color(0.16f, 0.105f, 0.075f, 1f));
            MakeFixedPanel(screenRoot.transform, "Warm Window", new Vector2(668f, -142f), new Vector2(370f, 330f), new Color(0.36f, 0.18f, 0.075f, 1f));
            MakeFixedPanel(screenRoot.transform, "Window Glow", new Vector2(687f, -161f), new Vector2(332f, 292f), new Color(0.93f, 0.56f, 0.2f, 0.13f));

            GameObject waitingRoom = MakeFixedPanel(screenRoot.transform, "Waiting Room", new Vector2(28f, -128f),
                new Vector2(238f, 426f), new Color(0.04f, 0.045f, 0.065f, 0.96f));
            MakeText(waitingRoom.transform, "Waiting Header", new Vector2(14f, -12f), new Vector2(210f, 38f), 20,
                TextAnchor.MiddleLeft, Gold, FontStyle.Bold).text = "WAITING ROOM";

            for (int i = 0; i < controller.Candidates.Count; i++)
            {
                int index = i;
                CandidateRuntime entry = controller.Candidates[i];
                string status = entry.Decision == CandidateDecision.Hired ? "HIRED"
                    : entry.Decision == CandidateDecision.Rejected ? "REJECTED" : "PENDING";
                Color color = ReferenceEquals(entry, controller.Selected) ? Accent : new Color(0.35f, 0.42f, 0.55f);
                MakeCandidateTab(waitingRoom.transform, entry.Candidate, status, new Vector2(12f, -60f - i * 116f),
                    new Vector2(214f, 102f), color, () => controller.ShowInterview(index));
            }

            CandidateRuntime selected = controller.Selected;
            GameObject chair = MakeFixedPanel(screenRoot.transform, "Candidate Chair", new Vector2(524f, -450f),
                new Vector2(654f, 290f), new Color(0.065f, 0.045f, 0.055f, 1f));
            MakeFixedPanel(chair.transform, "Chair Back", new Vector2(58f, -44f), new Vector2(538f, 246f), new Color(0.095f, 0.055f, 0.06f, 1f));
            Image candidateImage = MakeAnimatedCandidate(screenRoot.transform, selected.Candidate,
                new Vector2(490f, -166f), new Vector2(730f, 600f));
            interviewAnimator = candidateImage.gameObject.AddComponent<InterviewSpriteAnimator>();
            interviewAnimator.Configure(candidateImage, selected.Candidate.InterviewSpriteSheetResource);

            GameObject speech = MakeFixedPanel(screenRoot.transform, "Speech Bubble", new Vector2(1160f, -164f),
                new Vector2(708f, 250f), new Color(0.92f, 0.9f, 0.82f, 0.98f));
            GameObject speechTail = MakeFixedPanel(screenRoot.transform, "Speech Tail", new Vector2(1144f, -300f),
                new Vector2(42f, 42f), new Color(0.92f, 0.9f, 0.82f, 0.98f));
            speechTail.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            string spokenLine = selected.InterviewHistory.Count == 0
                ? $"I'm {selected.Candidate.Name}. Thank you for meeting me.\nWhat would you like to know?"
                : selected.InterviewHistory[selected.InterviewHistory.Count - 1].Answer;
            MakeText(speech.transform, "Speaker", new Vector2(26f, -18f), new Vector2(656f, 36f), 22,
                TextAnchor.MiddleLeft, new Color(0.38f, 0.22f, 0.12f), FontStyle.Bold).text = selected.Candidate.Name.ToUpperInvariant();
            MakeText(speech.transform, "Latest Answer", new Vector2(26f, -62f), new Vector2(656f, 164f), 25,
                TextAnchor.UpperLeft, new Color(0.12f, 0.095f, 0.085f), FontStyle.Normal).text = spokenLine;

            GameObject clipboard = MakeFixedPanel(screenRoot.transform, "Resume Clipboard", new Vector2(1442f, -442f),
                new Vector2(426f, 300f), parchment);
            MakeFixedPanel(clipboard.transform, "Clipboard Clip", new Vector2(138f, 10f), new Vector2(150f, 26f), new Color(0.3f, 0.24f, 0.2f, 1f));
            MakeText(clipboard.transform, "Candidate File", new Vector2(24f, -26f), new Vector2(378f, 40f), 22,
                TextAnchor.MiddleLeft, new Color(0.28f, 0.16f, 0.08f), FontStyle.Bold).text = "CANDIDATE DOSSIER";
            MakeText(clipboard.transform, "Profile", new Vector2(24f, -72f), new Vector2(378f, 72f), 21,
                TextAnchor.UpperLeft, new Color(0.16f, 0.11f, 0.08f), FontStyle.Bold).text =
                $"{selected.Candidate.Species}  ·  {selected.Candidate.Role}\nSalary  {selected.Candidate.Salary}";
            MakeText(clipboard.transform, "Resume", new Vector2(24f, -148f), new Vector2(378f, 124f), 20,
                TextAnchor.UpperLeft, new Color(0.2f, 0.13f, 0.09f), FontStyle.Normal).text = selected.Candidate.Resume;

            GameObject transcript = MakeFixedPanel(screenRoot.transform, "Transcript", new Vector2(28f, -566f),
                new Vector2(360f, 226f), new Color(0.04f, 0.045f, 0.065f, 0.94f));
            MakeText(transcript.transform, "Transcript Header", new Vector2(16f, -10f), new Vector2(328f, 34f), 19,
                TextAnchor.MiddleLeft, Accent, FontStyle.Bold).text = "INTERVIEW NOTES";
            string history = selected.InterviewHistory.Count == 0
                ? "No questions recorded yet."
                : string.Join("\n\n", selected.InterviewHistory.Select((message, index) =>
                    $"Q{index + 1}. {message.Question}\nA. {message.Answer}"));
            MakeText(transcript.transform, "History", new Vector2(16f, -50f), new Vector2(328f, 164f), 14,
                TextAnchor.UpperLeft, Color.white, FontStyle.Normal).text = history;

            MakeFixedPanel(screenRoot.transform, "Desk Top", new Vector2(326f, -724f), new Vector2(1098f, 46f), woodLight);
            GameObject desk = MakeFixedPanel(screenRoot.transform, "Interviewer Desk", new Vector2(362f, -770f),
                new Vector2(1026f, 282f), wood);
            MakeFixedPanel(desk.transform, "Desk Inlay", new Vector2(32f, -30f), new Vector2(962f, 214f), new Color(0.22f, 0.105f, 0.055f, 1f));
            GameObject namePlate = MakeFixedPanel(screenRoot.transform, "Name Plate", new Vector2(694f, -681f),
                new Vector2(370f, 72f), new Color(0.12f, 0.075f, 0.045f, 1f));
            MakeText(namePlate.transform, "Name", Vector2.zero, new Vector2(370f, 72f), 29,
                TextAnchor.MiddleCenter, Gold, FontStyle.Bold).text = $"{selected.Candidate.Name}  ·  {selected.Candidate.Role}";

            int remaining = Phase1GameController.QuestionLimit - selected.InterviewHistory.Count;
            MakeText(desk.transform, "Interviewer Label", new Vector2(34f, -22f), new Vector2(500f, 36f), 20,
                TextAnchor.MiddleLeft, new Color(0.94f, 0.77f, 0.42f), FontStyle.Bold).text =
                $"YOU · INTERVIEWER     QUESTIONS {remaining} / 3";
            InputField questionInput = MakeInput(desk.transform, "Question Input", new Vector2(34f, -68f), new Vector2(692f, 66f),
                "Ask face-to-face... (e.g. What do you do when danger comes?)");
            Button askButton = MakeButton(desk.transform, "Ask", new Vector2(744f, -68f), new Vector2(248f, 66f), "ASK", Accent,
                () => controller.AskQuestion(questionInput.text));
            askButton.interactable = remaining > 0;

            bool pending = selected.Decision == CandidateDecision.Pending;
            Button hire = MakeButton(desk.transform, "Hire", new Vector2(34f, -158f), new Vector2(220f, 66f), "HIRE", new Color(0.28f, 0.86f, 0.5f),
                controller.HireSelected);
            Button reject = MakeButton(desk.transform, "Reject", new Vector2(270f, -158f), new Vector2(220f, 66f), "REJECT", new Color(0.82f, 0.3f, 0.32f),
                controller.RejectSelected);
            hire.interactable = pending;
            reject.interactable = pending;
            MakeText(desk.transform, "Decision", new Vector2(512f, -158f), new Vector2(480f, 66f), 19,
                TextAnchor.MiddleLeft, new Color(0.86f, 0.8f, 0.7f), FontStyle.Bold).text =
                selected.Decision == CandidateDecision.Pending ? "TRAIT: CONFIDENTIAL UNTIL REVIEW" : $"DECISION: {selected.Decision.ToString().ToUpperInvariant()}";

            GameObject roster = MakeFixedPanel(screenRoot.transform, "Roster", new Vector2(1442f, -770f), new Vector2(426f, 282f), PanelLight);
            string hiredNames = string.Join(", ", controller.Candidates.Where(entry => entry.Decision == CandidateDecision.Hired).Select(entry => entry.Candidate.Name));
            if (hiredNames.Length == 0) hiredNames = "No hires yet";
            MakeText(roster.transform, "Roster Text", new Vector2(22f, -18f), new Vector2(382f, 92f), 22,
                TextAnchor.UpperLeft, Color.white, FontStyle.Bold).text = $"HIRED  {controller.HiredCount} / 2\n{hiredNames}";
            Button deploy = MakeButton(roster.transform, "Deploy", new Vector2(22f, -132f), new Vector2(382f, 76f), "DEPLOY TEAM  →", Gold,
                controller.BeginDeployment);
            deploy.interactable = controller.HiredCount > 0;
        }

        public void PlayCandidateResponse()
        {
            if (interviewAnimator != null) interviewAnimator.PlayTalking();
        }

        public void ShowDeployment()
        {
            BuildScreen("Deployment Screen");
            phaseText.text = "02  DEPLOYMENT";
            UpdateBudget();
            MakeText(screenRoot.transform, "Instructions", new Vector2(0f, -132f), new Vector2(1700f, 68f), 29,
                TextAnchor.MiddleCenter, Color.white, FontStyle.Bold, false, new Vector2(0.5f, 1f)).text =
                "채용 몬스터를 선택한 뒤 슬롯을 클릭하세요 · 모든 채용 인원을 배치하면 전투를 시작할 수 있습니다.";

            List<int> hiredIndices = Enumerable.Range(0, controller.Candidates.Count)
                .Where(i => controller.Candidates[i].Decision == CandidateDecision.Hired).ToList();
            GameObject hires = MakeFixedPanel(screenRoot.transform, "Hired List", new Vector2(80f, -245f), new Vector2(520f, 580f), Panel);
            MakeText(hires.transform, "Header", new Vector2(28f, -24f), new Vector2(464f, 48f), 27,
                TextAnchor.MiddleLeft, Accent, FontStyle.Bold).text = "HIRED MONSTERS";
            for (int i = 0; i < hiredIndices.Count; i++)
            {
                int candidateIndex = hiredIndices[i];
                CandidateRuntime runtime = controller.Candidates[candidateIndex];
                bool selected = candidateIndex == controller.SelectedDeploymentCandidate;
                string placement = runtime.SlotIndex < 0 ? "UNASSIGNED" : $"SLOT {runtime.SlotIndex + 1}";
                MakeButton(hires.transform, "Hire " + i, new Vector2(28f, -100f - i * 142f), new Vector2(464f, 112f),
                    $"{runtime.Candidate.Name} · {runtime.Candidate.Species}\n{runtime.Candidate.Role}  |  {placement}",
                    selected ? Accent : new Color(0.4f, 0.48f, 0.62f), () => controller.SelectDeploymentCandidate(candidateIndex));
            }

            GameObject slots = MakeFixedPanel(screenRoot.transform, "Defense Slots", new Vector2(650f, -245f), new Vector2(1190f, 580f), Panel);
            MakeText(slots.transform, "Header", new Vector2(28f, -24f), new Vector2(1134f, 48f), 27,
                TextAnchor.MiddleLeft, Gold, FontStyle.Bold).text = "DEFENSE FORMATION · DUNGEON GATE  ←";
            for (int i = 0; i < 3; i++)
            {
                int slotIndex = i;
                CandidateRuntime occupant = controller.Candidates.FirstOrDefault(entry => entry.SlotIndex == slotIndex);
                string label = occupant == null ? $"SLOT {i + 1}\nEMPTY" : $"SLOT {i + 1}\n{occupant.Candidate.Name}\n{occupant.Candidate.Role}";
                Color color = occupant == null ? new Color(0.28f, 0.35f, 0.48f) : Accent;
                MakeButton(slots.transform, "Slot " + i, new Vector2(48f + i * 366f, -155f), new Vector2(314f, 285f), label, color,
                    () => controller.AssignSelectedToSlot(slotIndex));
            }

            MakeButton(screenRoot.transform, "Back", new Vector2(80f, -875f), new Vector2(270f, 74f), "←  INTERVIEW", new Color(0.42f, 0.48f, 0.58f),
                controller.BackToInterview);
            Button begin = MakeButton(screenRoot.transform, "Begin Battle", new Vector2(-80f, -875f), new Vector2(420f, 74f), "START AUTO BATTLE  →", Gold,
                controller.BeginBattle, true);
            begin.interactable = controller.Candidates.Where(entry => entry.Decision == CandidateDecision.Hired).All(entry => entry.SlotIndex >= 0);
        }

        public void ShowBattle()
        {
            BuildScreen("Battle HUD", false);
            phaseText.text = "03  AUTO BATTLE · WAVE 1";
            UpdateBudget();
            GameObject leftHud = MakeFixedPanel(screenRoot.transform, "Dungeon HUD", new Vector2(30f, -130f), new Vector2(500f, 205f),
                new Color(0.055f, 0.075f, 0.12f, 0.92f));
            battleDungeonText = MakeText(leftHud.transform, "Dungeon HP", new Vector2(24f, -18f), new Vector2(452f, 70f), 34,
                TextAnchor.MiddleLeft, new Color(0.8f, 0.55f, 1f), FontStyle.Bold);
            battleRosterText = MakeText(leftHud.transform, "Roster", new Vector2(24f, -88f), new Vector2(452f, 92f), 22,
                TextAnchor.UpperLeft, Color.white, FontStyle.Bold);

            GameObject eventHud = MakeFixedPanel(screenRoot.transform, "Event Log", new Vector2(-30f, -130f), new Vector2(720f, 270f),
                new Color(0.055f, 0.075f, 0.12f, 0.92f), true);
            battleEventText = MakeText(eventHud.transform, "Events", new Vector2(22f, -18f), new Vector2(676f, 232f), 21,
                TextAnchor.UpperLeft, Color.white, FontStyle.Normal);
            UpdateBattleHud();
        }

        public void UpdateBattleHud()
        {
            if (battleDungeonText == null) return;
            battleDungeonText.text = $"DUNGEON HP   {controller.DungeonHp} / 100";
            battleDungeonText.color = controller.DungeonHp <= 40 ? new Color(1f, 0.35f, 0.3f) : new Color(0.8f, 0.55f, 1f);
            battleRosterText.text = controller.GetBattleRosterText();
            battleEventText.text = "TRAIT & BATTLE EVENTS\n" + controller.GetBattleEventText();
        }

        public void ShowReview(bool victory, IReadOnlyList<PerformanceRecord> records)
        {
            BuildScreen("Performance Review");
            phaseText.text = "04  PERFORMANCE REVIEW";
            UpdateBudget();
            MakeText(screenRoot.transform, "Result", new Vector2(0f, -136f), new Vector2(1600f, 86f), 48,
                TextAnchor.MiddleCenter, victory ? Accent : new Color(1f, 0.36f, 0.3f), FontStyle.Bold, false, new Vector2(0.5f, 1f)).text =
                victory ? "WAVE CLEAR" : "GAME OVER";
            MakeText(screenRoot.transform, "Result Detail", new Vector2(0f, -218f), new Vector2(1600f, 48f), 25,
                TextAnchor.MiddleCenter, Color.white, FontStyle.Normal, false, new Vector2(0.5f, 1f)).text =
                $"Dungeon HP {controller.DungeonHp}/100 · 면접에서 들은 말과 실제 Trait 행동을 비교하세요.";

            float cardWidth = records.Count == 1 ? 760f : 760f;
            float totalWidth = records.Count * cardWidth + Mathf.Max(0, records.Count - 1) * 40f;
            for (int i = 0; i < records.Count; i++)
            {
                PerformanceRecord record = records[i];
                float x = 960f - totalWidth * 0.5f + i * (cardWidth + 40f);
                GameObject card = MakeFixedPanel(screenRoot.transform, "Review " + i, new Vector2(x, -306f), new Vector2(cardWidth, 510f), Panel);
                MakeText(card.transform, "Name", new Vector2(28f, -24f), new Vector2(cardWidth - 56f, 58f), 36,
                    TextAnchor.MiddleLeft, Color.white, FontStyle.Bold).text = $"{record.Candidate.Name} · {record.Candidate.Species} {record.Candidate.Role}";
                MakeText(card.transform, "Stats", new Vector2(28f, -104f), new Vector2(cardWidth - 56f, 142f), 27,
                    TextAnchor.UpperLeft, new Color(0.82f, 0.88f, 0.96f), FontStyle.Bold).text =
                    $"Damage              {record.Damage:0}\nKills                    {record.Kills}\nDamage Taken     {record.DamageTaken:0}";
                MakeText(card.transform, "Incident Label", new Vector2(28f, -270f), new Vector2(cardWidth - 56f, 38f), 23,
                    TextAnchor.MiddleLeft, Gold, FontStyle.Bold).text = "INCIDENT";
                MakeText(card.transform, "Incident", new Vector2(28f, -316f), new Vector2(cardWidth - 56f, 74f), 23,
                    TextAnchor.UpperLeft, Color.white, FontStyle.Normal).text = record.TraitEvent;
                MakeText(card.transform, "Trait", new Vector2(28f, -420f), new Vector2(cardWidth - 56f, 54f), 26,
                    TextAnchor.MiddleLeft, Accent, FontStyle.Bold).text = $"DISCOVERED TRAIT   {TraitName(record.Candidate.Trait)}";
            }

            MakeButton(screenRoot.transform, "Restart", new Vector2(0f, -878f), new Vector2(420f, 76f), "RESTART GAME", Accent,
                controller.RestartGame, false, new Vector2(0.5f, 1f));
        }

        public void ShowNotice(string text, Color color)
        {
            if (noticeRoutine != null) StopCoroutine(noticeRoutine);
            noticeRoutine = StartCoroutine(NoticeRoutine(text, color));
        }

        private IEnumerator NoticeRoutine(string message, Color color)
        {
            noticeText.text = message;
            noticeText.color = color;
            noticeText.gameObject.SetActive(true);
            yield return new WaitForSeconds(2.5f);
            noticeText.gameObject.SetActive(false);
        }

        private void BuildScreen(string name, bool opaqueBackground = true)
        {
            if (screenRoot != null)
            {
                screenRoot.SetActive(false);
                Destroy(screenRoot);
            }
            screenRoot = new GameObject(name, typeof(RectTransform));
            screenRoot.transform.SetParent(canvasRoot, false);
            RectTransform rect = screenRoot.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            if (opaqueBackground)
            {
                Image background = screenRoot.AddComponent<Image>();
                background.color = Background;
            }
            screenRoot.transform.SetSiblingIndex(1);
            battleDungeonText = battleRosterText = battleEventText = null;
        }

        private void UpdateBudget()
        {
            budgetText.text = $"SALARY BUDGET   {controller.CurrentBudget} / {Phase1GameController.SalaryBudget}    ·    HIRED   {controller.HiredCount} / 2";
        }

        private static string TraitName(TraitId trait)
        {
            return trait == TraitId.Coward ? "COWARD" : trait == TraitId.Reckless ? "RECKLESS" : "TEAM_PLAYER";
        }

        private static GameObject MakeFixedPanel(Transform parent, string name, Vector2 topLeft, Vector2 size, Color color, bool rightAnchored = false)
        {
            Vector2 anchor = rightAnchored ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            Vector2 pivot = rightAnchored ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = topLeft;
            rect.sizeDelta = size;
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static GameObject MakePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static Text MakeText(Transform parent, string name, Vector2 position, Vector2 size, int fontSize,
            TextAnchor alignment, Color color, FontStyle fontStyle, bool rightAnchored = false, Vector2? customAnchor = null)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            Vector2 anchor = customAnchor ?? (rightAnchored ? new Vector2(1f, 1f) : new Vector2(0f, 1f));
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = customAnchor ?? (rightAnchored ? new Vector2(1f, 1f) : new Vector2(0f, 1f));
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = obj.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button MakeButton(Transform parent, string name, Vector2 position, Vector2 size, string label, Color color,
            UnityEngine.Events.UnityAction action, bool rightAnchored = false, Vector2? customAnchor = null)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            Vector2 anchor = customAnchor ?? (rightAnchored ? new Vector2(1f, 1f) : new Vector2(0f, 1f));
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = customAnchor ?? (rightAnchored ? new Vector2(1f, 1f) : new Vector2(0f, 1f));
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = obj.GetComponent<Image>();
            image.color = color;
            Button button = obj.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f);
            colors.disabledColor = new Color(0.3f, 0.3f, 0.35f, 0.65f);
            button.colors = colors;
            button.onClick.AddListener(action);
            Text text = MakeText(obj.transform, "Label", Vector2.zero, size - new Vector2(24f, 12f), 23,
                TextAnchor.MiddleCenter, new Color(0.035f, 0.045f, 0.065f), FontStyle.Bold, false, new Vector2(0.5f, 0.5f));
            return button;
        }

        private static Button MakeCandidateTab(Transform parent, Candidate candidate, string status, Vector2 position,
            Vector2 size, Color accentColor, UnityEngine.Events.UnityAction action)
        {
            GameObject obj = new GameObject(candidate.Name + " Waiting Tab", typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            obj.GetComponent<Image>().color = new Color(0.105f, 0.105f, 0.135f, 1f);

            Button button = obj.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
            colors.pressedColor = new Color(0.76f, 0.76f, 0.76f);
            button.colors = colors;
            button.onClick.AddListener(action);

            MakeFixedPanel(obj.transform, "Selection", Vector2.zero, new Vector2(6f, size.y), accentColor);
            MakePortrait(obj.transform, candidate, new Vector2(10f, -10f), new Vector2(82f, 82f));
            MakeText(obj.transform, "Name", new Vector2(98f, -12f), new Vector2(106f, 30f), 21,
                TextAnchor.MiddleLeft, Color.white, FontStyle.Bold).text = candidate.Name;
            MakeText(obj.transform, "Details", new Vector2(98f, -43f), new Vector2(106f, 48f), 16,
                TextAnchor.UpperLeft, new Color(0.74f, 0.81f, 0.91f), FontStyle.Normal).text =
                $"{candidate.Role}\n{status}";
            return button;
        }

        private static Button MakeCandidateButton(Transform parent, Candidate candidate, string status, Vector2 position,
            Vector2 size, Color accentColor, UnityEngine.Events.UnityAction action)
        {
            GameObject obj = new GameObject(candidate.Name + " Candidate", typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            obj.GetComponent<Image>().color = new Color(0.105f, 0.135f, 0.205f, 1f);
            Button button = obj.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
            colors.pressedColor = new Color(0.76f, 0.76f, 0.76f);
            button.colors = colors;
            button.onClick.AddListener(action);

            GameObject accent = MakePanel(obj.transform, "Selection Accent", new Vector2(0f, 0f), new Vector2(0f, 1f),
                Vector2.zero, new Vector2(7f, 0f), accentColor);
            accent.GetComponent<RectTransform>().pivot = new Vector2(0f, 0.5f);
            MakePortrait(obj.transform, candidate, new Vector2(14f, -10f), new Vector2(92f, 92f));
            MakeText(obj.transform, "Candidate Name", new Vector2(118f, -12f), new Vector2(188f, 34f), 25,
                TextAnchor.MiddleLeft, Color.white, FontStyle.Bold).text = candidate.Name;
            MakeText(obj.transform, "Candidate Details", new Vector2(118f, -48f), new Vector2(188f, 52f), 19,
                TextAnchor.UpperLeft, new Color(0.74f, 0.81f, 0.91f), FontStyle.Normal).text =
                $"{candidate.Species} · {candidate.Role}\n{status}";
            return button;
        }

        private static Image MakePortrait(Transform parent, Candidate candidate, Vector2 position, Vector2 size)
        {
            GameObject obj = new GameObject(candidate.Name + " Portrait", typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image portrait = obj.GetComponent<Image>();
            portrait.sprite = Resources.Load<Sprite>(candidate.PortraitResource);
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            portrait.color = portrait.sprite == null ? new Color(0.4f, 0.45f, 0.55f, 0.35f) : Color.white;
            return portrait;
        }

        private static Image MakeAnimatedCandidate(Transform parent, Candidate candidate, Vector2 position, Vector2 size)
        {
            GameObject obj = new GameObject(candidate.Name + " Interview Character", typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = obj.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>(candidate.PortraitResource);
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static InputField MakeInput(Transform parent, string name, Vector2 position, Vector2 size, string placeholder)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            obj.GetComponent<Image>().color = new Color(0.035f, 0.045f, 0.075f, 1f);
            InputField input = obj.GetComponent<InputField>();
            Text inputText = MakeText(obj.transform, "Text", new Vector2(18f, -8f), size - new Vector2(36f, 16f), 23,
                TextAnchor.MiddleLeft, Color.white, FontStyle.Normal);
            Text placeholderText = MakeText(obj.transform, "Placeholder", new Vector2(18f, -8f), size - new Vector2(36f, 16f), 22,
                TextAnchor.MiddleLeft, new Color(0.52f, 0.57f, 0.68f), FontStyle.Italic);
            placeholderText.text = placeholder;
            input.textComponent = inputText;
            input.placeholder = placeholderText;
            input.lineType = InputField.LineType.SingleLine;
            return input;
        }
    }
}
