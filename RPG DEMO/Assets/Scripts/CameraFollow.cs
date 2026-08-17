using UnityEngine;

/// <summary>
/// 플레이어를 부드럽게 따라가는 2D 카메라.
/// 좌우 이동 한계(clamp)를 지정해 스테이지 밖의 빈 공간이 보이지 않게 합니다.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("── 대상 ──")]
    [Tooltip("비워두면 Tag가 Player인 오브젝트를 자동으로 찾습니다")]
    [SerializeField] private Transform target;

    [Header("── 따라가기 ──")]
    [SerializeField] private Vector2 offset = new Vector2(1.5f, 1f);
    [Tooltip("작을수록 빠르게 따라옴")]
    [SerializeField] private float smoothTime = 0.15f;
    [Tooltip("체크하면 Y는 고정값 유지 (횡스크롤에 권장)")]
    [SerializeField] private bool lockY = true;
    [SerializeField] private float fixedY = 0f;

    [Header("── 이동 한계 ──")]
    [SerializeField] private bool useBounds = true;
    [SerializeField] private float minX = 0f;
    [SerializeField] private float maxX = 40f;

    private Vector3 velocity;

    private void Start()
    {
        if (target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }
        if (target != null) transform.position = CalcGoal(true);
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 goal = CalcGoal(false);
        transform.position = Vector3.SmoothDamp(transform.position, goal,
                                                ref velocity, smoothTime);
    }

    private Vector3 CalcGoal(bool instant)
    {
        float x = target.position.x + offset.x;
        float y = lockY ? fixedY : target.position.y + offset.y;

        if (useBounds) x = Mathf.Clamp(x, minX, maxX);

        return new Vector3(x, y, transform.position.z);
    }

    private void OnDrawGizmosSelected()
    {
        if (!useBounds) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(new Vector3(minX, -20f, 0f), new Vector3(minX, 20f, 0f));
        Gizmos.DrawLine(new Vector3(maxX, -20f, 0f), new Vector3(maxX, 20f, 0f));
    }
}