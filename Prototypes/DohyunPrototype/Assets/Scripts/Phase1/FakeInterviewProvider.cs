using System;
using System.Collections.Generic;

namespace DemonCompany.Phase1
{
    public interface IInterviewProvider
    {
        InterviewResponse Ask(Candidate candidate, IReadOnlyList<InterviewMessage> history, string question);
    }

    public sealed class FakeInterviewProvider : IInterviewProvider
    {
        private enum InterviewIntent
        {
            Courage,
            Discipline,
            Teamwork,
            Motivation,
            Experience,
            Strength,
            Weakness,
            Salary,
            Role,
            Stress,
            Dungeon,
            Personal,
            General
        }

        private static readonly string[] CourageKeywords =
        {
            "겁", "위험", "도망", "용기", "무서", "공포", "생존", "목숨", "fear", "danger", "run", "coward", "brave"
        };

        private static readonly string[] TeamKeywords =
        {
            "팀", "동료", "협력", "지원", "team", "ally", "help", "together"
        };

        private static readonly string[] DisciplineKeywords =
        {
            "명령", "규율", "대기", "지시", "discipline", "order", "wait", "formation"
        };

        private static readonly string[] MotivationKeywords =
        {
            "지원했", "지원한", "입사", "동기", "목표", "꿈", "왜 여기", "왜 우리", "motivation", "apply", "goal", "why here"
        };

        private static readonly string[] ExperienceKeywords =
        {
            "경력", "경험", "이전", "과거", "전적", "실적", "근무", "해봤", "해 본", "몇 년", "experience", "history", "career", "worked"
        };

        private static readonly string[] StrengthKeywords =
        {
            "장점", "강점", "특기", "잘하", "능력", "실력", "가능", "할 수", "strength", "skill", "good at", "can you"
        };

        private static readonly string[] WeaknessKeywords =
        {
            "단점", "약점", "부족", "실수", "실패", "후회", "문제점", "weakness", "failure", "mistake", "regret"
        };

        private static readonly string[] SalaryKeywords =
        {
            "급여", "월급", "연봉", "보수", "돈", "골드", "salary", "pay", "money", "gold"
        };

        private static readonly string[] RoleKeywords =
        {
            "역할", "임무", "직업", "포지션", "담당", "맡", "궁수", "전사", "지원가", "role", "job", "duty", "position"
        };

        private static readonly string[] StressKeywords =
        {
            "압박", "스트레스", "긴장", "갈등", "화가", "당황", "위기", "pressure", "stress", "conflict", "nervous"
        };

        private static readonly string[] DungeonKeywords =
        {
            "던전", "마왕", "회사", "근무지", "환경", "수비", "방어", "dungeon", "demon", "company", "defense"
        };

        private static readonly string[] PersonalKeywords =
        {
            "자기소개", "이름", "나이", "몇 살", "출신", "고향", "성격", "취미", "좋아", "싫어", "음식", "introduce", "name", "age", "hobby", "like"
        };

        private static readonly string[] FollowUpKeywords =
        {
            "정말", "진짜", "확실", "구체", "그럼", "아까", "방금", "다시", "왜요", "really", "specific", "earlier", "then"
        };

        public InterviewResponse Ask(Candidate candidate, IReadOnlyList<InterviewMessage> history, string question)
        {
            string lowered = question.Trim().ToLowerInvariant();
            InterviewIntent intent = DetectIntent(lowered);
            bool followUp = history.Count > 0 && ContainsAny(lowered, FollowUpKeywords);
            if (followUp && intent == InterviewIntent.General)
                intent = DetectIntent(history[history.Count - 1].Question.ToLowerInvariant());
            bool repeatedIntent = intent != InterviewIntent.General && HasIntentInHistory(history, intent);
            bool lie = IsLie(candidate, intent);
            string prefix = BuildAcknowledgement(question, history.Count, followUp, repeatedIntent);
            string answer = prefix + BuildAnswer(candidate, intent);
            string emotion = lie ? "confident" : intent == InterviewIntent.Motivation || intent == InterviewIntent.Strength ? "excited" : "neutral";
            float evasiveness = lie ? 0.7f : intent == InterviewIntent.Weakness ? 0.38f : 0.14f;
            return Make(answer, intent.ToString().ToLowerInvariant(), lie, emotion, evasiveness);
        }

        private static InterviewIntent DetectIntent(string question)
        {
            if (ContainsAny(question, CourageKeywords)) return InterviewIntent.Courage;
            if (ContainsAny(question, DisciplineKeywords)) return InterviewIntent.Discipline;
            if (ContainsAny(question, MotivationKeywords)) return InterviewIntent.Motivation;
            if (ContainsAny(question, SalaryKeywords)) return InterviewIntent.Salary;
            if (ContainsAny(question, WeaknessKeywords)) return InterviewIntent.Weakness;
            if (ContainsAny(question, ExperienceKeywords)) return InterviewIntent.Experience;
            if (ContainsAny(question, StrengthKeywords)) return InterviewIntent.Strength;
            if (ContainsAny(question, StressKeywords)) return InterviewIntent.Stress;
            if (ContainsAny(question, DungeonKeywords)) return InterviewIntent.Dungeon;
            if (ContainsAny(question, RoleKeywords)) return InterviewIntent.Role;
            if (ContainsAny(question, TeamKeywords)) return InterviewIntent.Teamwork;
            if (ContainsAny(question, PersonalKeywords)) return InterviewIntent.Personal;
            return InterviewIntent.General;
        }

        private static bool HasIntentInHistory(IReadOnlyList<InterviewMessage> history, InterviewIntent intent)
        {
            foreach (InterviewMessage message in history)
            {
                if (DetectIntent(message.Question.ToLowerInvariant()) == intent) return true;
            }
            return false;
        }

        private static bool IsLie(Candidate candidate, InterviewIntent intent)
        {
            return candidate.Trait == TraitId.Coward && (intent == InterviewIntent.Courage || intent == InterviewIntent.Weakness)
                || candidate.Trait == TraitId.Reckless && intent == InterviewIntent.Discipline;
        }

        private static string BuildAcknowledgement(string question, int historyCount, bool followUp, bool repeatedIntent)
        {
            string summary = question.Trim().Replace("\n", " ").Replace("\r", " ");
            if (summary.Length > 30) summary = summary.Substring(0, 30) + "…";
            string prefix = historyCount >= 2 ? "마지막 질문이군요. " : string.Empty;
            if (followUp)
                return prefix + $"앞선 답변을 확인하려는 질문으로 이해했습니다. ‘{summary}’에 답하자면, ";
            if (repeatedIntent)
                return prefix + $"같은 주제를 더 구체적으로 묻는 것이군요. ‘{summary}’에 답하자면, ";
            return prefix + $"‘{summary}’에 대해 말씀드리면, ";
        }

        private static string BuildAnswer(Candidate candidate, InterviewIntent intent)
        {
            switch (intent)
            {
                case InterviewIntent.Courage:
                    return candidate.Trait == TraitId.Coward
                        ? "두려움은 모릅니다. 모두가 달아날 때도 혼자 감시탑을 지켰고 언제나 끝까지 싸웁니다."
                        : candidate.Trait == TraitId.Reckless
                            ? "위험을 발견하면 제가 선두에서 돌파합니다. 방향만 알려 주시면 됩니다."
                            : "두렵더라도 동료 곁을 지키며 모두가 안전해질 때까지 버팁니다.";
                case InterviewIntent.Discipline:
                    return candidate.Trait == TraitId.Coward
                        ? "정찰 신호에 맞춰 지정된 사격 위치를 지키는 것이 제 원칙입니다."
                        : candidate.Trait == TraitId.Reckless
                            ? "대형과 명령을 무엇보다 중요하게 생각합니다. 지시 전에는 절대 움직이지 않습니다."
                            : "명령을 듣고 전원이 준비됐는지 확인한 뒤 함께 움직입니다.";
                case InterviewIntent.Teamwork:
                    return candidate.Trait == TraitId.Coward
                        ? "후방 사격선에서 각자 맡은 구역을 지킬 때 팀이 가장 효율적으로 움직입니다."
                        : candidate.Trait == TraitId.Reckless
                            ? "제가 길을 열면 동료들이 따라오면 됩니다. 망설이지 않는 것이 협력의 시작입니다."
                            : "주변 동료의 호흡을 맞추고 집중력을 끌어올려 팀 전체가 더 빠르게 싸우도록 돕습니다.";
                case InterviewIntent.Motivation:
                    return candidate.Trait == TraitId.Coward
                        ? "정찰 경험을 살릴 수 있고 방어 체계가 잘 갖춰진 던전이라고 들어 지원했습니다."
                        : candidate.Trait == TraitId.Reckless
                            ? "더 강한 적을 상대로 제 실력을 증명하고 승리의 선봉에 서기 위해 지원했습니다."
                            : "혼자 버티는 동료가 없는 던전을 만들고 싶어서 지원했습니다.";
                case InterviewIntent.Experience:
                    return candidate.Trait == TraitId.Coward
                        ? "여러 감시탑에서 정찰과 장거리 경계를 맡아 적의 접근을 일찍 발견해 왔습니다."
                        : candidate.Trait == TraitId.Reckless
                            ? "투기장에서 수많은 근접전을 치렀고 빠른 결판으로 이름을 알렸습니다."
                            : "던전 야간 관리조에서 보급과 회복을 담당하며 지친 분대를 끝까지 지원했습니다.";
                case InterviewIntent.Strength:
                    return candidate.Trait == TraitId.Coward
                        ? "위협을 남보다 먼저 찾고 안전한 거리에서 정확하게 공격하는 것이 강점입니다."
                        : candidate.Trait == TraitId.Reckless
                            ? "적진을 흔드는 돌파력과 강한 근접 공격이 제 가장 큰 강점입니다."
                            : "곁에 있는 동료의 전투 리듬을 끌어올리고 진형을 유지하는 데 능합니다.";
                case InterviewIntent.Weakness:
                    return candidate.Trait == TraitId.Coward
                        ? "위험을 너무 빨리 알아차려 주변에서 과민하다고 오해할 때가 있지만 전투를 포기한 적은 없습니다."
                        : candidate.Trait == TraitId.Reckless
                            ? "판단과 행동이 빨라 동료들이 따라오기 벅차다는 말을 듣곤 합니다."
                            : "혼자서 적을 마무리하는 공격력은 부족하지만 동료와 함께라면 그 약점을 보완할 수 있습니다.";
                case InterviewIntent.Salary:
                    return candidate.Trait == TraitId.Coward
                        ? $"제시 급여 {candidate.Salary}이면 장비 관리와 위험 수당을 포함해 합리적인 조건입니다."
                        : candidate.Trait == TraitId.Reckless
                            ? $"급여 {candidate.Salary}은 제 돌파력에 맞는 조건입니다. 승리로 값을 증명하겠습니다."
                            : $"급여 {candidate.Salary}이면 충분합니다. 팀 보급에 필요한 지원만 보장해 주세요.";
                case InterviewIntent.Role:
                    return candidate.Trait == TraitId.Coward
                        ? "궁수로서 후방에서 적을 먼저 발견하고 접근하기 전에 수를 줄이겠습니다."
                        : candidate.Trait == TraitId.Reckless
                            ? "전사로서 전열을 깨고 적의 시선을 제게 집중시키겠습니다."
                            : "지원가로서 가까운 동료의 공격 속도와 전투 지속력을 높이겠습니다.";
                case InterviewIntent.Stress:
                    return candidate.Trait == TraitId.Coward
                        ? "압박을 받으면 먼저 퇴로와 안전한 사선을 확인한 뒤 침착하게 활을 겨눕니다."
                        : candidate.Trait == TraitId.Reckless
                            ? "긴장은 움직이지 않을 때 생깁니다. 적에게 달려들면 오히려 머리가 맑아집니다."
                            : "주변 동료의 상태를 확인하고 하나씩 할 일을 정리하면 긴장도 빠르게 가라앉습니다.";
                case InterviewIntent.Dungeon:
                    return candidate.Trait == TraitId.Coward
                        ? "시야가 확보된 망루와 명확한 비상 통로가 있다면 던전을 안정적으로 지킬 수 있습니다."
                        : candidate.Trait == TraitId.Reckless
                            ? "좁은 던전 통로는 적이 도망치기 어렵기 때문에 제 전투 방식에 잘 맞습니다."
                            : "던전처럼 서로 가까이 싸우는 환경에서는 제 지원 능력이 가장 큰 효과를 냅니다.";
                case InterviewIntent.Personal:
                    return candidate.Trait == TraitId.Coward
                        ? "그루크입니다. 숲의 감시탑에서 자랐고 높은 곳에서 조용히 주변을 살피는 것을 좋아합니다."
                        : candidate.Trait == TraitId.Reckless
                            ? "로카입니다. 투기장 출신이며 훈련과 강한 상대를 만나는 일을 좋아합니다."
                            : "멜루입니다. 던전 관리조 출신이고 동료들과 식량과 이야기를 나누는 시간을 좋아합니다.";
                default:
                    return candidate.Trait == TraitId.Coward
                        ? "정찰병의 기준으로 상황을 빠르게 파악하고 가장 확실한 승리 방법을 선택하겠습니다."
                        : candidate.Trait == TraitId.Reckless
                            ? "복잡하게 고민하기보다 가장 빠른 행동으로 결과를 보여 드리겠습니다."
                            : "질문의 핵심을 팀 전체에 도움이 되는 방향으로 생각하고 행동하겠습니다.";
            }
        }

        private static bool ContainsAny(string question, IEnumerable<string> keywords)
        {
            foreach (string keyword in keywords)
            {
                if (question.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static InterviewResponse Make(string answer, string topic, bool usedLie, string emotion, float evasiveness)
        {
            return new InterviewResponse
            {
                Answer = answer,
                Topic = topic,
                UsedLie = usedLie,
                Emotion = emotion,
                Evasiveness = evasiveness
            };
        }
    }
}
