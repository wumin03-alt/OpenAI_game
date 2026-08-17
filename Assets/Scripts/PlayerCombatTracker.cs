using System;
using UnityEngine;

namespace AdaptiveBossPrototype
{
    public sealed class PlayerCombatTracker : MonoBehaviour
    {
        public int MeleeCount { get; private set; }
        public int RangedCount { get; private set; }
        public int DashCount { get; private set; }
        public int ParryCount { get; private set; }

        public event Action<ActionType> ActionRecorded;

        public void RecordAction(ActionType type)
        {
            switch (type)
            {
                case ActionType.Melee: MeleeCount++; break;
                case ActionType.Ranged: RangedCount++; break;
                case ActionType.Dash: DashCount++; break;
                case ActionType.Parry: ParryCount++; break;
            }
            ActionRecorded?.Invoke(type);
        }

        public CombatSnapshot CaptureSnapshot()
        {
            return new CombatSnapshot
            {
                melee = MeleeCount,
                ranged = RangedCount,
                dash = DashCount,
                parry = ParryCount
            };
        }

        public DominantStyle GetDominantStyle()
        {
            return Evaluate(MeleeCount, RangedCount, DashCount, ParryCount);
        }

        public DominantStyle GetDominantStyleSince(CombatSnapshot snapshot)
        {
            return Evaluate(
                MeleeCount - snapshot.melee,
                RangedCount - snapshot.ranged,
                DashCount - snapshot.dash,
                ParryCount - snapshot.parry);
        }

        public void ResetCounts()
        {
            MeleeCount = RangedCount = DashCount = ParryCount = 0;
        }

        private static DominantStyle Evaluate(int melee, int ranged, int dash, int parry)
        {
            int total = melee + ranged + dash + parry;
            if (total <= 0) return DominantStyle.Balanced;

            int max = Mathf.Max(melee, ranged, dash, parry);
            int tied = (melee == max ? 1 : 0) + (ranged == max ? 1 : 0)
                       + (dash == max ? 1 : 0) + (parry == max ? 1 : 0);
            if (tied > 1 || max < Mathf.CeilToInt(total * 0.38f)) return DominantStyle.Balanced;
            if (max == melee) return DominantStyle.Melee;
            if (max == ranged) return DominantStyle.Ranged;
            if (max == dash) return DominantStyle.Dash;
            return DominantStyle.Parry;
        }
    }
}
