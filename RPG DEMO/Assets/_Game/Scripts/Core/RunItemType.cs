using UnityEngine;

namespace Game.Core
{
    public enum RunItemType
    {
        HealthRecovery,
        AttackBoost,
        GroggyDamageBoost,
        ParryTimeBoost,
        MaxHealthBoost
    }

    public readonly struct RunItemOffer
    {
        public RunItemOffer(RunItemType type, int magnitude)
        {
            Type = type;
            Magnitude = magnitude;
        }

        public RunItemType Type { get; }
        public int Magnitude { get; }
    }

    public static class RunItemCatalog
    {
        public const float AttackBonusPerStack = 0.15f;
        public const float GroggyDamagePerStack = 10f;
        public const float ParryTimePerStack = 1f;
        public const int MaxHealthPerStack = 20;

        public static RunItemOffer CreateOffer(RunItemType type)
        {
            int magnitude = type == RunItemType.HealthRecovery
                ? Random.Range(30, 51)
                : type == RunItemType.MaxHealthBoost
                    ? MaxHealthPerStack
                    : 1;
            return new RunItemOffer(type, magnitude);
        }

        public static string GetTitle(RunItemType type)
        {
            switch (type)
            {
                case RunItemType.HealthRecovery: return "응급 나노젤";
                case RunItemType.AttackBoost: return "공격 오버클럭";
                case RunItemType.GroggyDamageBoost: return "그로기 브레이커";
                case RunItemType.ParryTimeBoost: return "시간 확장 모듈";
                case RunItemType.MaxHealthBoost: return "강화 바이오 코어";
                default: return type.ToString();
            }
        }

        public static string GetDescription(RunItemOffer offer)
        {
            switch (offer.Type)
            {
                case RunItemType.HealthRecovery:
                    return $"현재 체력을 {offer.Magnitude} 회복";
                case RunItemType.AttackBoost:
                    return "근접·원거리 공격력 +15% (합산)";
                case RunItemType.GroggyDamageBoost:
                    return "미니게임 성공 그로기 피해 +10 (합산)";
                case RunItemType.ParryTimeBoost:
                    return "패링 미니게임 제한 시간 +1초";
                case RunItemType.MaxHealthBoost:
                    return $"최대 체력 +{offer.Magnitude}, 체력도 동일량 회복";
                default:
                    return string.Empty;
            }
        }

        public static string GetSpriteResourcePath(RunItemType type)
        {
            switch (type)
            {
                case RunItemType.HealthRecovery: return "Rewards/SPR_Item_HealthRecovery";
                case RunItemType.AttackBoost: return "Rewards/SPR_Item_AttackBoost";
                case RunItemType.GroggyDamageBoost: return "Rewards/SPR_Item_GroggyBreakBoost";
                case RunItemType.ParryTimeBoost: return "Rewards/SPR_Item_ParryTimeBoost";
                case RunItemType.MaxHealthBoost: return "Rewards/SPR_Item_MaxHealthBoost";
                default: return string.Empty;
            }
        }
    }
}
