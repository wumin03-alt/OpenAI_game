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

        float fps = desired == attackFrames ? attackFps : desired == moveFrames ? moveFps : idleFps;
        frameTimer += Time.deltaTime;
        if (frameTimer < 1f / Mathf.Max(0.01f, fps)) return;

        frameTimer = 0f;
        frameIndex = (frameIndex + 1) % desired.Length;
        targetRenderer.sprite = desired[frameIndex];
    }

    private void LateUpdate()
    {
        if (player == null) return;

        // PlayerController의 프로토타입 웅크리기 스케일 보정을 전용 프레임으로 대체하고,
        // 모든 애니메이션의 발 위치를 동일하게 유지합니다.
        transform.localScale = new Vector3(
            Mathf.Abs(stableLocalScale.x) * player.Facing,
            stableLocalScale.y,
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
