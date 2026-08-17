using System;
using UnityEngine;

namespace AdaptiveBossPrototype
{
    public enum ActionType { Melee, Ranged, Dash, Parry }
    public enum DominantStyle { Balanced, Melee, Ranged, Dash, Parry }
    public enum Team { Player, Enemy }
    public enum StageState { Tutorial, MobBattle, BossBattle, Victory }

    [Serializable]
    public struct CombatSnapshot
    {
        public int melee;
        public int ranged;
        public int dash;
        public int parry;
    }

    public static class StyleExtensions
    {
        public static string KoreanName(this DominantStyle style)
        {
            switch (style)
            {
                case DominantStyle.Melee: return "근거리 공격";
                case DominantStyle.Ranged: return "원거리 공격";
                case DominantStyle.Dash: return "대시 기동";
                case DominantStyle.Parry: return "패링";
                default: return "균형형";
            }
        }

        public static Color StyleColor(this DominantStyle style)
        {
            switch (style)
            {
                case DominantStyle.Melee: return new Color(1f, 0.38f, 0.3f);
                case DominantStyle.Ranged: return new Color(0.25f, 0.72f, 1f);
                case DominantStyle.Dash: return new Color(1f, 0.82f, 0.2f);
                case DominantStyle.Parry: return new Color(0.6f, 0.35f, 1f);
                default: return UnityEngine.Color.white;
            }
        }
    }
}
