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
                    Name = "Gruk",
                    Species = "Goblin",
                    Role = "Archer",
                    Salary = 30,
                    Resume = "Former watchtower scout. Claims to stay calm under any pressure.",
                    PortraitResource = "CandidatePortraits/gruk",
                    Trait = TraitId.Coward,
                    Stats = new CandidateStats { MaxHp = 62f, Attack = 15f, AttackSpeed = 1.25f, MoveSpeed = 2.3f, Range = 3.8f },
                    HiddenIncident = "Abandoned the west watchtower when surrounded.",
                    LieTopic = "courage"
                },
                new Candidate
                {
                    Id = "orc-warrior",
                    Name = "Rokka",
                    Species = "Orc",
                    Role = "Warrior",
                    Salary = 50,
                    Resume = "Arena veteran with a perfect attendance record and a love of decisive action.",
                    PortraitResource = "CandidatePortraits/rokka",
                    Trait = TraitId.Reckless,
                    Stats = new CandidateStats { MaxHp = 125f, Attack = 21f, AttackSpeed = 0.8f, MoveSpeed = 2.8f, Range = 1.15f },
                    HiddenIncident = "Charged before the signal in three consecutive battles.",
                    LieTopic = "discipline"
                },
                new Candidate
                {
                    Id = "slime-support",
                    Name = "Mellu",
                    Species = "Slime",
                    Role = "Support",
                    Salary = 35,
                    Resume = "Dungeon maintenance assistant praised for keeping small teams together.",
                    PortraitResource = "CandidatePortraits/mellu",
                    Trait = TraitId.TeamPlayer,
                    Stats = new CandidateStats { MaxHp = 92f, Attack = 11f, AttackSpeed = 1.05f, MoveSpeed = 1.8f, Range = 1.45f },
                    HiddenIncident = "Kept an exhausted squad fighting through an entire night shift.",
                    LieTopic = string.Empty
                }
            };
        }
    }
}
