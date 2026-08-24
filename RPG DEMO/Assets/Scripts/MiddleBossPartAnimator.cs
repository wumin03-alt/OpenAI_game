using UnityEngine;

/// <summary>산업용 보스의 기계 파츠 또는 통합 본체 스프라이트 시트를 재생합니다.</summary>
[DisallowMultipleComponent]
public sealed class MiddleBossPartAnimator : MonoBehaviour
{
    [SerializeField] private SpriteRenderer target;
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(0.1f)] private float framesPerSecond = 4f;
    [SerializeField] private bool pingPong;
    [SerializeField, Min(0f)] private float bobAmplitude = 0.03f;
    [SerializeField, Min(0f)] private float bobSpeed = 2f;

    private MiddleBossController boss;
    private Vector3 restingPosition;
    private float phaseOffset;

    public void Configure(SpriteRenderer spriteRenderer, Sprite[] animationFrames,
        float speed, bool usePingPong, float movementAmplitude, float movementSpeed)
    {
        target = spriteRenderer;
        frames = animationFrames;
        framesPerSecond = Mathf.Max(0.1f, speed);
        pingPong = usePingPong;
        bobAmplitude = Mathf.Max(0f, movementAmplitude);
        bobSpeed = Mathf.Max(0f, movementSpeed);
    }

    private void Awake()
    {
        if (target == null) target = GetComponent<SpriteRenderer>();
        boss = GetComponentInParent<MiddleBossController>();
        restingPosition = transform.localPosition;
        phaseOffset = Mathf.Abs(transform.GetSiblingIndex() * 0.71f);
    }

    private void OnEnable()
    {
        restingPosition = transform.localPosition;
    }

    private void Update()
    {
        float phaseMultiplier = boss != null && boss.Phase >= 2 ? 1.35f : 1f;
        if (target != null && frames != null && frames.Length > 0)
        {
            int index = ResolveFrameIndex((Time.time + phaseOffset) * framesPerSecond * phaseMultiplier);
            target.sprite = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        }

        float bob = Mathf.Sin((Time.time + phaseOffset) * bobSpeed * phaseMultiplier) * bobAmplitude;
        transform.localPosition = restingPosition + Vector3.up * bob;
    }

    private int ResolveFrameIndex(float frameTime)
    {
        if (!pingPong || frames.Length <= 2)
            return Mathf.FloorToInt(frameTime) % frames.Length;

        int cycleLength = frames.Length * 2 - 2;
        int cycleIndex = Mathf.FloorToInt(frameTime) % cycleLength;
        return cycleIndex < frames.Length ? cycleIndex : cycleLength - cycleIndex;
    }
}
