using UnityEngine;

/// <summary>방어 대상이 안전한 아레나 범위에서 천천히 이동하고 가끔 점프하게 합니다.</summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(Health))]
public sealed class DefenseTargetPatrol : MonoBehaviour
{
    [SerializeField, Range(0.8f, 1.2f)] private float moveSpeed = 0.95f;
    [SerializeField, Min(0f)] private float jumpForce = 15.5f;
    [SerializeField] private float minX = -9f;
    [SerializeField] private float maxX = 9f;
    [SerializeField, Min(0.1f)] private float directionChangeMin = 1.4f;
    [SerializeField, Min(0.1f)] private float directionChangeMax = 3.2f;
    [SerializeField, Min(0.1f)] private float jumpIntervalMin = 1.8f;
    [SerializeField, Min(0.1f)] private float jumpIntervalMax = 4.2f;
    [SerializeField, Min(0.05f)] private float groundCheckDistance = 0.2f;
    [SerializeField, Min(0.05f)] private float edgeCheckDistance = 1.35f;
    [SerializeField, Min(0.05f)] private float edgeProbeOffset = 0.9f;
    [SerializeField, Min(0.05f)] private float wallCheckDistance = 0.7f;
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private Health health;
    private int direction;
    private float nextDirectionChangeTime;
    private float nextJumpTime;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        health = GetComponent<Health>();
        ChooseNewDirection();
        ScheduleJump();
    }

    private void FixedUpdate()
    {
        if (health.IsDead) return;

        bool grounded = IsGrounded();
        if (Time.time >= nextDirectionChangeTime || IsOutsideSafeRange() || (grounded && (!HasGroundAhead() || HasWallAhead())))
            ChooseNewDirection();

        body.linearVelocity = new Vector2(direction * moveSpeed, body.linearVelocity.y);

        if (grounded && Time.time >= nextJumpTime)
        {
            body.linearVelocity = new Vector2(body.linearVelocity.x, jumpForce);
            ScheduleJump();
        }
    }

    private void ChooseNewDirection()
    {
        if (transform.position.x <= minX)
            direction = 1;
        else if (transform.position.x >= maxX)
            direction = -1;
        else
            direction = Random.value < 0.5f ? -1 : 1;
        nextDirectionChangeTime = Time.time + Random.Range(directionChangeMin, directionChangeMax);
    }

    private void ScheduleJump()
    {
        nextJumpTime = Time.time + Random.Range(jumpIntervalMin, jumpIntervalMax);
    }

    private bool IsGrounded()
    {
        Bounds bounds = bodyCollider.bounds;
        Vector2 origin = new Vector2(bounds.center.x, bounds.min.y + 0.03f);
        return Physics2D.Raycast(origin, Vector2.down, groundCheckDistance, groundLayer);
    }

    private bool HasGroundAhead()
    {
        Bounds bounds = bodyCollider.bounds;
        Vector2 origin = new Vector2(bounds.center.x + direction * edgeProbeOffset, bounds.min.y + 0.05f);
        return Physics2D.Raycast(origin, Vector2.down, edgeCheckDistance, groundLayer);
    }

    private bool HasWallAhead()
    {
        Bounds bounds = bodyCollider.bounds;
        Vector2 origin = new Vector2(bounds.center.x, bounds.center.y);
        return Physics2D.Raycast(origin, Vector2.right * direction, wallCheckDistance, groundLayer);
    }

    private bool IsOutsideSafeRange()
    {
        return (direction < 0 && transform.position.x <= minX) || (direction > 0 && transform.position.x >= maxX);
    }
}
