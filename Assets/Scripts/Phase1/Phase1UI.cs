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
            phaseText.text = "01  INTERVIEW  →  HIRE";
            UpdateBudget();

            GameObject sidebar = MakeFixedPanel(screenRoot.transform, "Candidate List", new Vector2(32f, -132f),
                new Vector2(360f, 840f), Panel);
            MakeText(sidebar.transform, "Candidate Header", new Vector2(20f, -18f), new Vector2(320f, 52f), 26,
                TextAnchor.MiddleLeft, Accent, FontStyle.Bold).text = "CANDIDATE FILES";

            for (int i = 0; i < controller.Candidates.Count; i++)
            {
                int index = i;
                CandidateRuntime entry = controller.Candidates[i];
                string status = entry.Decision == CandidateDecision.Hired ? "HIRED"
                    : entry.Decision == CandidateDecision.Rejected ? "REJECTED" : "PENDING";
                Color color = ReferenceEquals(entry, controller.Selected) ? Accent : new Color(0.35f, 0.42f, 0.55f);
                MakeCandidateButton(sidebar.transform, entry.Candidate, status, new Vector2(20f, -88f - i * 130f),
                    new Vector2(320f, 112f), color, () => controller.ShowInterview(index));
            }

            CandidateRuntime selected = controller.Selected;
            GameObject card = MakeFixedPanel(screenRoot.transform, "Candidate Card", new Vector2(420f, -132f),
                new Vector2(620f, 840f), Panel);
            MakeText(card.transform, "Name", new Vector2(28f, -24f), new Vector2(290f, 58f), 38,
                TextAnchor.MiddleLeft, Color.white, FontStyle.Bold).text = selected.Candidate.Name;
            MakeText(card.transform, "Profile", new Vector2(28f, -92f), new Vector2(290f, 150f), 25,
                TextAnchor.UpperLeft, new Color(0.78f, 0.84f, 0.94f), FontStyle.Normal).text =
                $"Species    {selected.Candidate.Species}\nRole          {selected.Candidate.Role}\nSalary       {selected.Candidate.Salary}";
            GameObject portraitFrame = MakeFixedPanel(card.transform, "Portrait Frame", new Vector2(330f, -24f),
                new Vector2(262f, 244f), new Color(0.025f, 0.04f, 0.075f, 1f));
            MakePortrait(portraitFrame.transform, selected.Candidate, new Vector2(10f, -10f), new Vector2(242f, 224f));
            MakeText(card.transform, "Resume Label", new Vector2(28f, -264f), new Vector2(564f, 38f), 23,
                TextAnchor.MiddleLeft, Accent, FontStyle.Bold).text = "RESUME";
            MakeText(card.transform, "Resume", new Vector2(28f, -310f), new Vector2(564f, 108f), 24,
                TextAnchor.UpperLeft, Color.white, FontStyle.Normal).text = selected.Candidate.Resume;

            int remaining = Phase1GameController.QuestionLimit - selected.InterviewHistory.Count;
            MakeText(card.transform, "Question Count", new Vector2(28f, -442f), new Vector2(564f, 42f), 24,
                TextAnchor.MiddleLeft, Gold, FontStyle.Bold).text = $"QUESTIONS REMAINING   {remaining} / 3";
            InputField questionInput = MakeInput(card.transform, "Question Input", new Vector2(28f, -500f), new Vector2(564f, 64f),
                "질문을 입력하세요 (예: 위험하면 도망가나요?)");
            Button askButton = MakeButton(card.transform, "Ask", new Vector2(28f, -578f), new Vector2(270f, 64f), "ASK QUESTION", Accent,
                () => controller.AskQuestion(questionInput.text));
            askButton.interactable = remaining > 0;

            bool pending = selected.Decision == CandidateDecision.Pending;
            Button hire = MakeButton(card.transform, "Hire", new Vector2(28f, -704f), new Vector2(270f, 72f), "HIRE", new Color(0.28f, 0.86f, 0.5f),
                controller.HireSelected);
            Button reject = MakeButton(card.transform, "Reject", new Vector2(322f, -704f), new Vector2(270f, 72f), "REJECT", new Color(0.82f, 0.3f, 0.32f),
                controller.RejectSelected);
            hire.interactable = pending;
            reject.interactable = pending;
            MakeText(card.transform, "Decision", new Vector2(28f, -782f), new Vector2(564f, 34f), 22,
                TextAnchor.MiddleCenter, new Color(0.72f, 0.78f, 0.88f), FontStyle.Bold).text =
                selected.Decision == CandidateDecision.Pending ? "TRAIT: HIDDEN UNTIL PERFORMANCE REVIEW" : $"DECISION: {selected.Decision.ToString().ToUpperInvariant()}";

            GameObject historyPanel = MakeFixedPanel(screenRoot.transform, "Interview History", new Vector2(1068f, -132f),
                new Vector2(820f, 650f), Panel);
            MakeText(historyPanel.transform, "History Header", new Vector2(24f, -18f), new Vector2(772f, 48f), 26,
                TextAnchor.MiddleLeft, Accent, FontStyle.Bold).text = "INTERVIEW HISTORY";
            string history = selected.InterviewHistory.Count == 0
                ? "아직 질문하지 않았습니다.\n키워드에 따라 지원자의 준비된 답변이 달라집니다."
                : string.Join("\n\n", selected.InterviewHistory.Select((message, index) =>
                    $"Q{index + 1}. {message.Question}\nA. {message.Answer}"));
            MakeText(historyPanel.transform, "History", new Vector2(24f, -82f), new Vector2(772f, 530f), 23,
                TextAnchor.UpperLeft, Color.white, FontStyle.Normal).text = history;

            GameObject roster = MakeFixedPanel(screenRoot.transform, "Roster", new Vector2(1068f, -806f), new Vector2(820f, 166f), PanelLight);
            string hiredNames = string.Join(", ", controller.Candidates.Where(entry => entry.Decision == CandidateDecision.Hired).Select(entry => entry.Candidate.Name));
            if (hiredNames.Length == 0) hiredNames = "없음";
            MakeText(roster.transform, "Roster Text", new Vector2(24f, -16f), new Vector2(520f, 126f), 24,
                TextAnchor.MiddleLeft, Color.white, FontStyle.Bold).text = $"HIRED  {controller.HiredCount} / 2\n{hiredNames}";
            Button deploy = MakeButton(roster.transform, "Deploy", new Vector2(566f, -42f), new Vector2(228f, 82f), "DEPLOY  →", Gold,
                controller.BeginDeployment);
            deploy.interactable = controller.HiredCount > 0;
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
