using Game.Audio;
using UnityEngine;

/// <summary>
/// 직진 투사체. 플레이어 총알 / 보스 브레스·탄막 공용.
/// Launch(Vector2) 로 임의 방향 발사가 가능합니다. (하방 부채꼴, 상방 자동조준용)
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    [Header("── 이동 ──")]
    [SerializeField] private float speed = 16f;
    [Tooltip("이 시간이 지나면 자동 소멸")]
    [SerializeField] private float lifeTime = 2f;
    [Tooltip("체크하면 진행 방향으로 스프라이트를 회전. 해제하면 좌우 반전만")]
    [SerializeField] private bool rotateToDirection = true;

    [Header("── 데미지 ──")]
    [SerializeField] private float damage = 8f;
    [Tooltip("맞힐 대상 Layer. 플레이어 총알이면 Enemy, 보스 브레스면 Player")]
    [SerializeField] private LayerMask targetLayers;
    [Tooltip("부딪히면 사라지는 Layer. 보통 Ground")]
    [SerializeField] private LayerMask blockLayers;

    [Header("── 넉백 ──")]
    [Tooltip("수평 넉백 힘. 진행 방향의 X 성분만 사용합니다")]
    [SerializeField] private float knockbackForce = 3f;
    [Tooltip("위로 띄우는 힘. 하방 공격이 플레이어를 땅에 처박지 않도록")]
    [SerializeField] private float knockbackUp = 2f;

    [Header("── 관통 ──")]
    [Tooltip("체크 해제하면 적을 맞혀도 사라지지 않고 관통")]
    [SerializeField] private bool destroyOnHit = true;

    private Rigidbody2D rb;
    private Vector2 moveDir = Vector2.right;
    private bool launched;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        // Launch를 부르지 않고 그냥 씬에 놔둬도 오른쪽으로 날아가도록
        if (!launched) Launch(1);
        Destroy(gameObject, lifeTime);
    }

    /// <summary>수평 발사. direction: 1 = 오른쪽, -1 = 왼쪽 (기존 호환용)</summary>
    public void Launch(int direction)
    {
        Launch(new Vector2(Mathf.Sign(direction), 0f));
    }

    /// <summary>임의 방향 발사. 정규화하지 않아도 됩니다.</summary>
    public void Launch(Vector2 direction)
    {
        launched = true;

        moveDir = direction.sqrMagnitude < 0.0001f
            ? Vector2.right
            : direction.normalized;

        if (rb == null) rb = GetComponent<Rigidbody2D>();
        rb.linearVelocity = moveDir * speed;

        if (rotateToDirection)
        {
            // 진행 방향으로 회전 (스케일은 항상 양수로 유지)
            Vector3 s = transform.localScale;
            s.x = Mathf.Abs(s.x);
            transform.localScale = s;

            float ang = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, ang);
        }
        else
        {
            // 기존 방식: 좌우 반전만
            Vector3 s = transform.localScale;
            s.x = Mathf.Abs(s.x) * (moveDir.x >= 0f ? 1f : -1f);
            transform.localScale = s;
        }
    }

    /// <summary>보스 페이즈에 따라 값을 바꿀 때 사용</summary>
    public void Configure(float newDamage, float newSpeed)
    {
        damage = newDamage;
        speed = newSpeed;
        if (launched) rb.linearVelocity = moveDir * speed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        int layerBit = 1 << other.gameObject.layer;

        // 1) 벽/지면에 부딪힘
        if ((blockLayers.value & layerBit) != 0)
        {
            Destroy(gameObject);
            return;
        }

        // 2) 대상 레이어가 아니면 무시
        if ((targetLayers.value & layerBit) == 0) return;

        Health hp = other.GetComponentInParent<Health>();
        if (hp == null || hp.IsDead) return;

        hp.TakeDamage(damage);

        // 원거리 명중음은 요청대로 근접 명중/몬스터 피격음과 같은 클립을 사용합니다.
        if (hp.GetComponent<PlayerController>() == null)
            AudioManager.Instance?.PlayCombatHit();

        // 3) 넉백 — 수직 성분은 쓰지 않고 수평 + 위로만
        if (knockbackForce > 0f || knockbackUp > 0f)
        {
            Rigidbody2D targetRb = hp.GetComponent<Rigidbody2D>();
            if (targetRb != null && targetRb.bodyType == RigidbodyType2D.Dynamic)
            {
                float dirX;
                if (Mathf.Abs(moveDir.x) > 0.05f)
                    dirX = Mathf.Sign(moveDir.x);
                else
                    dirX = Mathf.Sign(hp.transform.position.x - transform.position.x);

                if (dirX == 0f) dirX = 1f;

                targetRb.linearVelocity = new Vector2(0f, targetRb.linearVelocity.y);
                targetRb.AddForce(new Vector2(dirX * knockbackForce, knockbackUp),
                                  ForceMode2D.Impulse);
            }
        }

        if (destroyOnHit) Destroy(gameObject);
    }
}
