#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PowerfulSpell.Editor
{
    public static class PowerfulSpellEditorTools
    {
        [MenuItem("Powerful Spell/프로토타입 진행도 초기화")]
        private static void ResetProgress()
        {
            PlayerPrefs.DeleteKey("PowerfulSpell.HighestUnlockedStage");
            PlayerPrefs.Save();
            Debug.Log("[Powerful Spell] 스테이지/주문 해금 진행도를 초기화했습니다.");
        }

        [MenuItem("Powerful Spell/모든 스테이지와 주문 해금")]
        private static void UnlockAll()
        {
            PlayerPrefs.SetInt("PowerfulSpell.HighestUnlockedStage", 5);
            PlayerPrefs.Save();
            Debug.Log("[Powerful Spell] 5개 스테이지와 모든 주문을 해금했습니다.");
        }
    }
}
#endif
