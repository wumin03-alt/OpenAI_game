using UnityEngine;

/// <summary>
/// BossArena 전용 프레임 애니메이터입니다.
/// 공용 Player 프리팹이나 보스 전투 계약을 변경하지 않고 현재 상태를 읽어 스프라이트만 교체합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossArenaSpriteAnimator : MonoBehaviour
{
    private enum ActorKind { Player, Boss }

    [SerializeField] private ActorKind actorKind;
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private PlayerController player;
    [SerializeField] private Rigidbody2D playerBody;
    [SerializeField] private BossController boss;
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

    public void ConfigurePlayer(SpriteRenderer renderer, PlayerController controller, Rigidbody2D body,
        Sprite[] idle, Sprite[] move, Sprite[] attack, Sprite[] crouch)
    {
        actorKind = ActorKind.Player;
        targetRenderer = renderer;
        player = controller;
        playerBody = body;
        idleFrames = idle;
        moveFrames = move;
        attackFrames = attack;
        crouchFrames = crouch;
        ResetAnimation(idleFrames);
    }

    public void ConfigureBoss(SpriteRenderer renderer, BossController controller,
        Sprite[] idle, Sprite[] move, Sprite[] attack)
    {
        actorKind = ActorKind.Boss;
        targetRenderer = renderer;
        boss = controller;
        idleFrames = idle;
        moveFrames = move;
        attackFrames = attack;
        ResetAnimation(idleFrames);
    }

    private void Awake()
    {
        if (targetRenderer == null) targetRenderer = GetComponent<SpriteRenderer>();
        stableLocalScale = transform.localScale;
        stableLocalPosition = transform.localPosition;
        ResetAnimation(idleFrames);
    }

    private void LateUpdate()
    {
        if (actorKind != ActorKind.Player || player == null) return;

        // PlayerController는 공용 프로토타입 호환을 위해 웅크릴 때 visual의 Y축을 줄입니다.
        // BossArena에서는 전용 웅크리기 프레임을 쓰므로 비율과 발 위치를 원래대로 고정합니다.
        transform.localScale = new Vector3(
            Mathf.Abs(stableLocalScale.x) * player.Facing,
            stableLocalScale.y,
            stableLocalScale.z);
        transform.localPosition = stableLocalPosition;
    }

    private void Update()
    {
        Sprite[] desired = SelectFrames();
        if (desired == null || desired.Length == 0 || targetRenderer == null) return;

        if (activeFrames != desired) ResetAnimation(desired);

        float fps = desired == attackFrames ? attackFps : desired == moveFrames ? moveFps : idleFps;
        frameTimer += Time.deltaTime;
        if (frameTimer >= 1f / Mathf.Max(0.01f, fps))
        {
            frameTimer = 0f;
            frameIndex = (frameIndex + 1) % desired.Length;
            targetRenderer.sprite = desired[frameIndex];
        }
    }

    private Sprite[] SelectFrames()
    {
        if (actorKind == ActorKind.Player)
        {
            if (player == null) return idleFrames;
            if (player.IsAttacking || player.IsParrying) return attackFrames;
            if (player.IsCrouching) return crouchFrames;
            if (player.IsDashing || playerBody != null && Mathf.Abs(playerBody.linearVelocity.x) > 0.15f)
                return moveFrames;
            return idleFrames;
        }

        if (boss == null) return idleFrames;
        if (boss.State == BossController.BossState.Attack || boss.State == BossController.BossState.Windup)
            return attackFrames;
        if (boss.State == BossController.BossState.Moving) return moveFrames;
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
