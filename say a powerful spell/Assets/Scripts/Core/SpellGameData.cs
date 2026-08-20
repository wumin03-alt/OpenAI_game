using System;
using System.Collections.Generic;
using UnityEngine;

namespace PowerfulSpell
{
    [Serializable]
    public sealed class SpellDefinition
    {
        public string id;
        public string displayName;
        public string incantation;
        public string shortDescription;
        public int damage;
        public float cooldown;
        public int unlockAfterStage;
        public int requiredCombo;
        public bool isUltimate;
        public Color color;
        public SpellDisruption disruption;
    }

    public enum SpellDisruption
    {
        None,
        Blink,
        FadeWords
    }

    [Serializable]
    public sealed class StageDefinition
    {
        public int number;
        public string title;
        public string enemyName;
        public string subtitle;
        public int enemyHealth;
        public int enemyDamage;
        public float attackInterval;
        public Color primaryColor;
        public Color glowColor;
    }

    public static class SpellGameData
    {
        public static readonly IReadOnlyList<SpellDefinition> Spells = new List<SpellDefinition>
        {
            new SpellDefinition
            {
                id = "rice", displayName = "찹쌀 탄환", incantation = "시골 찹쌀 햇찹쌀 도시 찹쌀 촌찹쌀",
                shortDescription = "짧고 빠른 기본 주문", damage = 22, cooldown = 0.7f,
                unlockAfterStage = 0, color = new Color(1f, .31f, .14f), disruption = SpellDisruption.None
            },
            new SpellDefinition
            {
                id = "acorn", displayName = "도토리 개문", incantation = "도토리가 문을 도로록 드르륵 두루룩 열었는가? 도루륵 두르륵 열었는가?",
                shortDescription = "굴러가는 발음의 연속 주문", damage = 36, cooldown = 1.0f,
                unlockAfterStage = 1, color = new Color(.25f, .76f, 1f), disruption = SpellDisruption.None
            },
            new SpellDefinition
            {
                id = "doctor", displayName = "법학박사의 판결", incantation = "저기 있는 저분은 밥 법학박사이고, 여기있는 이분은 백 법학박사이다.",
                shortDescription = "밥과 백을 구분하는 고급 주문", damage = 54, cooldown = 1.2f,
                unlockAfterStage = 2, color = new Color(.65f, .48f, 1f), disruption = SpellDisruption.None
            },
            new SpellDefinition
            {
                id = "pot", displayName = "솥장수의 불꽃", incantation = "작년에 온 솥장수는 새 솥장수이고, 금년에 온 솥장수는 헌 솥장수 이다.",
                shortDescription = "새 솥과 헌 솥을 가르는 강한 주문", damage = 78, cooldown = 1.5f,
                unlockAfterStage = 3, color = new Color(.92f, .22f, .78f), disruption = SpellDisruption.Blink
            },
            new SpellDefinition
            {
                id = "cloud", displayName = "구름 그림 폭풍", incantation = "내가 그린 구름 그림은 새털구름 그린 구름 그림이고, 네가 그린 구름 그림은 깃털구름 그린 구름 그림이다.",
                shortDescription = "긴 구름 발음으로 만드는 최상급 주문", damage = 108, cooldown = 2.0f,
                unlockAfterStage = 4, color = new Color(1f, .75f, .2f), disruption = SpellDisruption.FadeWords
            },
            new SpellDefinition
            {
                id = "ultimate", displayName = "철창살 대심판", incantation = "경찰청 철창살은 외철창살이냐 쌍철창살이냐 경찰청 철창살이 쇠철창살이냐 철철창살이냐 검찰청 쇠철창살은 쇄시철창살이냐 헌쇠철창살이냐 경찰청 쇠창살 외철창살, 검찰청 쇠창살 쌍철창살",
                shortDescription = "5콤보를 소비하는 초강력 필살기", damage = 240, cooldown = 2.8f,
                unlockAfterStage = 0, requiredCombo = 5, isUltimate = true,
                color = new Color(1f, .86f, .25f), disruption = SpellDisruption.None
            }
        };

        public static readonly IReadOnlyList<StageDefinition> Stages = new List<StageDefinition>
        {
            new StageDefinition { number = 1, title = "속삭이는 폐허", enemyName = "재투성이 임프", subtitle = "첫 목소리를 시험하는 작은 악동", enemyHealth = 105, enemyDamage = 8, attackInterval = 5.2f, primaryColor = new Color(.75f, .16f, .11f), glowColor = new Color(1f, .35f, .08f) },
            new StageDefinition { number = 2, title = "얼어붙은 예배당", enemyName = "서리 망령", subtitle = "숨결 사이로 파고드는 차가운 혼령", enemyHealth = 170, enemyDamage = 11, attackInterval = 4.8f, primaryColor = new Color(.12f, .42f, .64f), glowColor = new Color(.2f, .78f, 1f) },
            new StageDefinition { number = 3, title = "폭풍의 첨탑", enemyName = "뇌운 가고일", subtitle = "천둥을 훔쳐 삼킨 돌의 파수꾼", enemyHealth = 245, enemyDamage = 14, attackInterval = 4.4f, primaryColor = new Color(.34f, .2f, .58f), glowColor = new Color(.65f, .45f, 1f) },
            new StageDefinition { number = 4, title = "웃지 않는 연회장", enemyName = "심연의 광대", subtitle = "주문이 꼬이기만을 기다리는 조롱꾼", enemyHealth = 330, enemyDamage = 17, attackInterval = 4.0f, primaryColor = new Color(.52f, .09f, .38f), glowColor = new Color(1f, .18f, .65f) },
            new StageDefinition { number = 5, title = "별이 잠든 왕좌", enemyName = "침묵의 군주", subtitle = "목소리를 빼앗아 온 최후의 지배자", enemyHealth = 460, enemyDamage = 21, attackInterval = 3.7f, primaryColor = new Color(.55f, .4f, .08f), glowColor = new Color(1f, .75f, .18f) }
        };
    }
}
