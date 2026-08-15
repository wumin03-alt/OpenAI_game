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
        private static readonly string[] CourageKeywords =
        {
            "겁", "위험", "도망", "용기", "무서", "fear", "danger", "run", "coward", "brave"
        };

        private static readonly string[] TeamKeywords =
        {
            "팀", "동료", "협력", "지원", "team", "ally", "help", "together"
        };

        private static readonly string[] DisciplineKeywords =
        {
            "명령", "규율", "대기", "지시", "discipline", "order", "wait", "formation"
        };

        public InterviewResponse Ask(Candidate candidate, IReadOnlyList<InterviewMessage> history, string question)
        {
            string lowered = question.Trim().ToLowerInvariant();
            if (ContainsAny(lowered, CourageKeywords))
            {
                bool lie = candidate.Trait == TraitId.Coward;
                return Make(
                    lie
                        ? "두려움이요? 저는 그런 걸 모릅니다. 모두가 달아날 때도 혼자 감시탑을 지켰고, 언제나 끝까지 싸웁니다."
                        : candidate.Trait == TraitId.Reckless
                            ? "위험은 저를 부르는 신호입니다. 방향만 알려 주시면 곧장 돌파하겠습니다."
                            : "긴장될 때도 있지만, 모두가 안전해질 때까지 동료 곁을 지킵니다.",
                    "courage", lie, lie ? "confident" : "neutral", lie ? 0.72f : 0.15f);
            }

            if (ContainsAny(lowered, DisciplineKeywords))
            {
                bool lie = candidate.Trait == TraitId.Reckless;
                return Make(
                    lie
                        ? "저는 대형과 명령을 무엇보다 중요하게 생각합니다. 지시가 떨어지기 전에는 절대 움직이지 않습니다."
                        : candidate.Trait == TraitId.TeamPlayer
                            ? "명령을 잘 듣고, 모두가 준비됐는지 확인한 뒤 함께 움직입니다."
                            : "신호에 맞춰 움직입니다. 감시탑에서는 독단적인 행동이 통하지 않으니까요.",
                    "discipline", lie, lie ? "confident" : "neutral", lie ? 0.58f : 0.12f);
            }

            if (ContainsAny(lowered, TeamKeywords))
            {
                return Make(
                    candidate.Trait == TraitId.TeamPlayer
                        ? "팀은 서로를 믿는 만큼 빠르게 움직입니다. 저는 주변 동료가 집중하고 더 빠르게 싸우도록 돕습니다."
                        : candidate.Trait == TraitId.Reckless
                            ? "제가 길을 열면 팀이 따라오면 됩니다. 진짜 적은 망설임입니다."
                            : "모두가 맡은 역할을 아는 안전한 사격 진지에서 가장 잘 싸웁니다.",
                    "teamwork", false, candidate.Trait == TraitId.TeamPlayer ? "excited" : "neutral", 0.1f);
            }

            string answer;
            if (candidate.Trait == TraitId.Coward)
                answer = "제 정찰 기록이 실력을 증명합니다. 위협을 일찍 발견하고 가장 안전하게 이길 수 있는 자리를 선택합니다.";
            else if (candidate.Trait == TraitId.Reckless)
                answer = "문제는 빠르게 해결합니다. 전진하고, 강하게 치고, 승리로 결과를 증명합니다.";
            else
                answer = "제가 가장 요란한 전사는 아니지만, 제가 곁에 있으면 분대 전체가 더 잘 싸웁니다.";

            if (history.Count > 0)
                answer += " 앞서 말씀드렸듯이 저는 한결같은 태도를 중요하게 생각합니다.";
            return Make(answer, "general", false, "neutral", 0.2f);
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
