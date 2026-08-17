using System.Collections.Generic;

namespace DemonCompany.Phase1
{
    public static class CandidateDatabase
    {
        public static List<Candidate> CreatePhase1Candidates()
        {
            return new List<Candidate>
            {
                new Candidate
                {
                    Id = "goblin-archer",
                    Name = "그루크",
                    Species = "고블린",
                    Role = "궁수",
                    Salary = 30,
                    Resume = "전직 감시탑 정찰병. 어떤 위기에서도 침착함을 유지한다고 주장한다.",
                    PortraitResource = "CandidatePortraits/gruk",
                    InterviewSpriteSheetResource = "InterviewSprites/gruk-interview-sheet",
                    Trait = TraitId.Coward,
                    Stats = new CandidateStats { MaxHp = 62f, Attack = 15f, AttackSpeed = 1.25f, MoveSpeed = 2.3f, Range = 3.8f },
                    HiddenIncident = "포위되자 서쪽 감시탑을 버리고 도망친 전력이 있다.",
                    LieTopic = "courage"
                },
                new Candidate
                {
                    Id = "orc-warrior",
                    Name = "로카",
                    Species = "오크",
                    Role = "전사",
                    Salary = 50,
                    Resume = "결단력 있는 행동을 선호하며, 개근 기록을 보유한 투기장 베테랑.",
                    PortraitResource = "CandidatePortraits/rokka",
                    InterviewSpriteSheetResource = "InterviewSprites/rokka-interview-sheet",
                    Trait = TraitId.Reckless,
                    Stats = new CandidateStats { MaxHp = 125f, Attack = 21f, AttackSpeed = 0.8f, MoveSpeed = 2.8f, Range = 1.15f },
                    HiddenIncident = "세 번의 전투에서 연속으로 신호 전에 돌진한 전력이 있다.",
                    LieTopic = "discipline"
                },
                new Candidate
                {
                    Id = "slime-support",
                    Name = "멜루",
                    Species = "슬라임",
                    Role = "지원가",
                    Salary = 35,
                    Resume = "소규모 팀의 결속을 잘 유지한다고 평가받은 던전 관리 보조원.",
                    PortraitResource = "CandidatePortraits/mellu",
                    InterviewSpriteSheetResource = "InterviewSprites/mellu-interview-sheet",
                    Trait = TraitId.TeamPlayer,
                    Stats = new CandidateStats { MaxHp = 92f, Attack = 11f, AttackSpeed = 1.05f, MoveSpeed = 1.8f, Range = 1.45f },
                    HiddenIncident = "지친 분대가 밤샘 근무 내내 버틸 수 있도록 지원했다.",
                    LieTopic = string.Empty
                }
            };
        }
    }
}
