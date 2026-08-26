using UnityEngine;

/// <summary>
/// 모든 스테이지에서 Player 프리팹이 공통으로 사용하는 스프라이트 애니메이터입니다.
/// 게임플레이 상태를 읽기만 하며 이동/공격 수치에는 관여하지 않습니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerSpriteAnimator : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private PlayerController player;
    [SerializeField] private Rigidbody2D playerBody;
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField] private Sprite[] moveFrames;
    [SerializeField] private Sprite[] attackFrames;
    [SerializeField] private Sprite[] crouchFrames;
    [SerializeField] private float idleFps = 2.4f;
    [SerializeField] private float moveFps = 10f;
    [SerializeField] private float attackFps = 12f;
    [SerializeField] private float crouchFps = 8f;
    [Header("Crouch Presentation")]
    [Tooltip("웅크리기 원화의 큰 여백/체격을 일반 프레임과 같은 크기로 맞추는 배율")]
    [SerializeField, Range(0.4f, 1f)] private float crouchArtScale = 0.78f;

    private Sprite[] activeFrames;
    private float frameTimer;
    private int frameIndex;
    private Vector3 stableLocalScale;
    private Vector3 stableLocalPosition;

    public void Configure(SpriteRenderer renderer, PlayerController controller, Rigidbody2D body,
        Sprite[] idle, Sprite[] move, Sprite[] attack, Sprite[] crouch)
    {
        targetRenderer = renderer;
        player = controller;
        playerBody = body;
        idleFrames = idle;
        moveFrames = move;
        attackFrames = attack;
        crouchFrames = crouch;
        stableLocalScale = transform.localScale;
        stableLocalPosition = transform.localPosition;
        ResetAnimation(idleFrames);
    }

    private void Awake()
    {
        if (targetRenderer == null) targetRenderer = GetComponent<SpriteRenderer>();
        if (player == null) player = GetComponentInParent<PlayerController>();
        if (playerBody == null && player != null) playerBody = player.GetComponent<Rigidbody2D>();
        stableLocalScale = transform.localScale;
        stableLocalPosition = transform.localPosition;
        ResetAnimation(idleFrames);
    }

    private void Update()
    {
        Sprite[] desired = SelectFrames();
        if (desired == null || desired.Length == 0 || targetRenderer == null) return;

        if (activeFrames != desired) ResetAnimation(desired);

        float fps = desired == attackFrames ? attackFps
            : desired == moveFrames ? moveFps
            : desired == crouchFrames ? crouchFps
            : idleFps;
        frameTimer += Time.deltaTime;
        if (frameTimer < 1f / Mathf.Max(0.01f, fps)) return;

        frameTimer = 0f;
        // 웅크리기는 첫 프레임에서 두 번째 프레임으로 앉은 뒤 마지막 자세를 유지한다.
        frameIndex = desired == crouchFrames
            ? Mathf.Min(frameIndex + 1, desired.Length - 1)
            : (frameIndex + 1) % desired.Length;
        targetRenderer.sprite = desired[frameIndex];
    }

    private void LateUpdate()
    {
        if (player == null) return;

        // 원화 크기 차이만 프레임 교체와 동시에 보정한다. Transform 전체를 시간에 따라
        // 축소하지 않아 캐릭터가 갑자기 오그라드는 느낌이 나지 않게 한다.
        float artScale = player.IsCrouching ? crouchArtScale : 1f;

        // 전용 웅크리기 원화가 일반 프레임보다 크게 그려져 있으므로 X/Y를 같은 비율로
        // 한 번만 보정하고, 실제 앉는 움직임은 두 장의 자세 프레임으로 표현한다.
        transform.localScale = new Vector3(
            Mathf.Abs(stableLocalScale.x) * artScale * player.Facing,
            stableLocalScale.y * artScale,
            stableLocalScale.z);
        transform.localPosition = stableLocalPosition;
    }

    private Sprite[] SelectFrames()
    {
        if (player == null) return idleFrames;
        if (player.IsAttacking || player.IsParrying) return attackFrames;
        if (player.IsCrouching) return crouchFrames;
        if (player.IsDashing || playerBody != null && Mathf.Abs(playerBody.linearVelocity.x) > 0.15f)
            return moveFrames;
        return idleFrames;
    }

    private void ResetAnimation(Sprite[] frames)
    {
        activeFrames = frames;
        frameIndex = 0;
        frameTimer = 0f;
        if (targetRenderer != null && frames != null && frames.Length > 0)
            targetRenderer.sprite = frames[0];
    }
}
