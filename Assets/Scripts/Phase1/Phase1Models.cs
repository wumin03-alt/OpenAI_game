using System;
using System.Collections.Generic;

namespace DemonCompany.Phase1
{
    public enum TraitId
    {
        Coward,
        Reckless,
        TeamPlayer
    }

    public enum CandidateDecision
    {
        Pending,
        Hired,
        Rejected
    }

    public enum GamePhase
    {
        Interview,
        Deployment,
        Battle,
        Review
    }

    [Serializable]
    public sealed class CandidateStats
    {
        public float MaxHp;
        public float Attack;
        public float AttackSpeed;
        public float MoveSpeed;
        public float Range;
    }

    [Serializable]
    public sealed class Candidate
    {
        public string Id;
        public string Name;
        public string Species;
        public string Role;
        public int Salary;
        public string Resume;
        public string PortraitResource;
        public TraitId Trait;
        public CandidateStats Stats;
        public string HiddenIncident;
        public string LieTopic;
    }

    public sealed class InterviewMessage
    {
        public string Question;
        public string Answer;
        public bool UsedLie;
    }

    public sealed class InterviewResponse
    {
        public string Answer;
        public string Topic;
        public bool UsedLie;
        public string Emotion;
        public float Evasiveness;
    }

    public sealed class CandidateRuntime
    {
        public Candidate Candidate;
        public CandidateDecision Decision;
        public readonly List<InterviewMessage> InterviewHistory = new List<InterviewMessage>();
        public int SlotIndex = -1;
    }

    public sealed class PerformanceRecord
    {
        public Candidate Candidate;
        public float Damage;
        public int Kills;
        public float DamageTaken;
        public string TraitEvent;
    }
}
