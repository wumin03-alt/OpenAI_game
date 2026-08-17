using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 트리거에 닿은 대상에게 데미지를 주는 공용 히트박스.
/// - OncePerActivation : 켜져 있는 동안 대상 1명당 1번만 (근접 휘두르기, 보스 꼬리)
/// - Repeating         : 일정 간격마다 반복 (잡몹 접촉, 보스 몸통)
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DamageZone : MonoBehaviour
{
    public enum HitMode { OncePerActivation, Repeating }

    [Header("── 데미지 ──")]
    [SerializeField] private float damage = 10f;
    [Tooltip("때릴 대상의 Layer. 플레이어 공격이면 Enemy, 적 공격이면 Player")]
    [SerializeField] private LayerMask targetLayers;

    [Header("── 판정 방식 ──")]
    [SerializeField] private HitMode mode = HitMode.OncePerActivation;
    [Tooltip("Repeating일 때 재타격 간격(초)")]
    [SerializeField] private float repeatInterval = 0.8f;

    [Header("── 넉백 ──")]
    [SerializeField] private float knockbackForce = 0f;
    [SerializeField] private float knockbackUp = 2f;

    [Header("── 이벤트 ──")]
    public UnityEvent onHit;   // 히트스톱 / 이펙트 연결용

    // 이번 활성화 동안 이미 때린 대상
    private readonly HashSet<Health> hitThisActivation = new HashSet<Health>();
    // Repeating 모드에서 대상별 마지막 타격 시각
    private readonly Dictionary<Health, float> lastHitTime = new Dictionary<Health, float>();

    /// <summary>보스 페이즈에 따라 데미지를 바꿀 때 사용</summary>
    public void SetDamage(float value) => damage = value;

    private void OnEnable()
    {
        // 히트박스를 켤 때마다 "이번 스윙에 맞은 목록"을 초기화
        hitThisActivation.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other) => TryHit(other);
    private void OnTriggerStay2D(Collider2D other) => TryHit(other);

    private void TryHit(Collider2D other)
    {
        // 1) 대상 레이어인지 확인
        if ((targetLayers.value & (1 << other.gameObject.layer)) == 0) return;

        // 2) Health 찾기 (자식 콜라이더일 수도 있으므로 부모까지 탐색)
        Health hp = other.GetComponentInParent<Health>();
        if (hp == null || hp.IsDead) return;

        // 3) 중복 타격 방지
        if (mode == HitMode.OncePerActivation)
        {
            if (hitThisActivation.Contains(hp)) return;
            hitThisActivation.Add(hp);
        }
        else
        {
            if (lastHitTime.TryGetValue(hp, out float t) && Time.time - t < repeatInterval) return;
            lastHitTime[hp] = Time.time;
        }

        // 4) 데미지
        hp.TakeDamage(damage);

        // 5) 넉백
        if (knockbackForce > 0f)
        {
            Rigidbody2D targetRb = hp.GetComponent<Rigidbody2D>();
            if (targetRb != null && targetRb.bodyType == RigidbodyType2D.Dynamic)
            {
                float dirX = Mathf.Sign(hp.transform.position.x - transform.position.x);
                if (dirX == 0f) dirX = 1f;

                targetRb.linearVelocity = new Vector2(0f, targetRb.linearVelocity.y);
                targetRb.AddForce(new Vector2(dirX * knockbackForce, knockbackUp),
                                  ForceMode2D.Impulse);
            }
        }

        onHit?.Invoke();
    }
}