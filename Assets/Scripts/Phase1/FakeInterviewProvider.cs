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
                        ? "Fear? Never heard of it. I held a tower alone while everyone else ran. I always fight to the end."
                        : candidate.Trait == TraitId.Reckless
                            ? "Danger is an invitation. Point me at it and try to keep up."
                            : "I get nervous, but I stay beside my team until everyone is safe.",
                    "courage", lie, lie ? "confident" : "neutral", lie ? 0.72f : 0.15f);
            }

            if (ContainsAny(lowered, DisciplineKeywords))
            {
                bool lie = candidate.Trait == TraitId.Reckless;
                return Make(
                    lie
                        ? "I value formation above everything. I never move before the command is given."
                        : candidate.Trait == TraitId.TeamPlayer
                            ? "I listen for the order and make sure the whole line is ready before we move."
                            : "I follow the signal. A watchtower is no place for improvisation.",
                    "discipline", lie, lie ? "confident" : "neutral", lie ? 0.58f : 0.12f);
            }

            if (ContainsAny(lowered, TeamKeywords))
            {
                return Make(
                    candidate.Trait == TraitId.TeamPlayer
                        ? "A team moves at the speed of trust. I keep nearby allies focused and working faster."
                        : candidate.Trait == TraitId.Reckless
                            ? "The team can follow the path I open. Hesitation is the real enemy."
                            : "I work best from a safe firing line where everyone knows their job.",
                    "teamwork", false, candidate.Trait == TraitId.TeamPlayer ? "excited" : "neutral", 0.1f);
            }

            string answer;
            if (candidate.Trait == TraitId.Coward)
                answer = "My scouting record speaks for itself. I notice threats early and choose the safest winning position.";
            else if (candidate.Trait == TraitId.Reckless)
                answer = "I solve problems quickly: advance, strike hard, and let victory explain the details.";
            else
                answer = "I am not the loudest fighter, but squads perform better when I am standing with them.";

            if (history.Count > 0)
                answer += " As I said earlier, consistency matters to me.";
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
