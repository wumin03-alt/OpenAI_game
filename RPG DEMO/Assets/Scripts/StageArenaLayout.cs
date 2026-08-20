using UnityEngine;

/// <summary>
/// 일반 스테이지가 복제해 사용할 좁은 다층 횡스크롤 아레나 레이아웃입니다.
/// Stage01 전용 루트에서만 생성하며 공용 Player/Camera/전투 컴포넌트는 건드리지 않습니다.
/// </summary>
public sealed class StageArenaLayout : MonoBehaviour
{
    [SerializeField] private Sprite platformSprite;
    [SerializeField] private int groundLayer = 6;

    private void Awake()
    {
        CreatePlatform("Ground_Main", new Vector2(0f, -3f), new Vector2(28f, 0.8f), new Color(0.28f, 0.38f, 0.55f));
        CreatePlatform("Platform_Middle", new Vector2(0f, -0.35f), new Vector2(5.5f, 0.45f), new Color(0.38f, 0.55f, 0.75f));
        CreatePlatform("Platform_UpperLeft", new Vector2(-7.2f, 2.05f), new Vector2(4.6f, 0.45f), new Color(0.48f, 0.62f, 0.82f));
        CreatePlatform("Platform_UpperRight", new Vector2(7.2f, 2.05f), new Vector2(4.6f, 0.45f), new Color(0.48f, 0.62f, 0.82f));
        CreatePlatform("ArenaWall_Left", new Vector2(-14.1f, 0f), new Vector2(0.6f, 8f), new Color(0.20f, 0.28f, 0.40f));
        CreatePlatform("ArenaWall_Right", new Vector2(14.1f, 0f), new Vector2(0.6f, 8f), new Color(0.20f, 0.28f, 0.40f));
    }

    private void CreatePlatform(string objectName, Vector2 localPosition, Vector2 size, Color color)
    {
        GameObject platform = new GameObject(objectName);
        platform.layer = groundLayer;
        platform.transform.SetParent(transform, false);
        platform.transform.localPosition = localPosition;

        BoxCollider2D collider = platform.AddComponent<BoxCollider2D>();
        collider.size = size;

        SpriteRenderer renderer = platform.AddComponent<SpriteRenderer>();
        renderer.sprite = platformSprite;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = size;
        renderer.color = color;
    }
}
