using UnityEngine;
using System.Collections.Generic;
using System.Text;
using TMPro;

[RequireComponent(typeof(Rigidbody2D))]
public class KnightBot : MonoBehaviour
{
    [Header("Движение и Прыжки")]
    public float speed = 2.5f;
    public float climbSpeed = 3f;
    public float jumpForce = 7f;
    public float maxJumpForce = 9f;
    public float jumpMargin = 0.1f;
    public float obstacleCheckDistance = 0.5f;
    public float jumpCooldown = 0.3f;
    public bool jumpOverGaps = true;
    public float maxGapWidth = 2.5f;
    public LayerMask groundLayer;
    public LayerMask wallOnlyLayer;
    public LayerMask ignoreLayers;

    [Header("Защита от застревания")]
    public float stuckTime = 0.5f;
    public float backoffTime = 0.8f;

    [Header("Этажи и цели")]
    public float floorTolerance = 1.5f;
    public float retargetInterval = 0.25f;

    [Header("Лестницы")]
    public float ladderAlignDistance = 0.2f;
    public float climbTimeout = 8f;
    public bool passThroughOnLadder = true;
    public float leaveLadderTime = 0.6f;
    public float leaveLadderSpeedMultiplier = 1.6f;
    public float ladderExitPush = 1.2f;
    public float ladderReclimbCooldown = 2.0f;
    public float ladderBlacklistDuration = 10.0f;
    public float climbSuccessWindow = 3.0f;
    public float climbSuccessMinRise = 0.5f;

    [Header("Смерть")]
    public string deathTag = "DeathZone";
    public bool destroyOnDeath = true;

    [Header("Диалог у двери (Реплики)")]
    public GameObject speechBubbleObj;
    public TextMeshProUGUI speechText;
    public string[] doorPhrases;
    public float phraseChangeInterval = 15f;
    private float phraseTimer = 0f;
    private bool wasNearDoorLastField = false;

    [Header("Отладка")]
    public bool debugLog = false;
    public bool debugStuck = false;

    [Header("Префаб Мусора")]
    public GameObject trashPrefab;

    [Header("Боссфайт")]
    public bool isBoss = false;
    public int bossHP = 100;

    [Header("Бой со Скелетом")]
    public float attackRange = 1.5f;
    public int knightDamage = 25;
    public float attackCooldown = 1.5f;

    Rigidbody2D rb;
    Collider2D col;
    Animator anim;
    SpriteRenderer spriteRenderer;

    static PhysicsMaterial2D _sharedKnightMat;

    float normalGravity = 1f;
    bool grounded;
    float groundedFeetY;
    float coyoteTimer;
    const float coyoteTime = 0.1f;
    float jumpTimer;
    float attackTimer;
    float retargetTimer;

    Transform targetArcher;
    ArcherBot currentArcherScript;
    Transform targetDoor;
    Transform targetLadder;
    Transform ladderLockTarget;

    bool isClimbing;
    int climbDir;
    Bounds climbBounds;
    float climbTimer;
    float climbCooldownTimer;
    float climbStartFeetY;
    float lastLogTime;
    const float scanStep = 0.25f;
    const float fineStep = 0.05f;

    float leaveLadderTimer;
    float leaveLadderDir;
    float leaveLadderTargetX;

    float lastDesiredVx;
    float stuckTimer;
    float backoffTimer;
    float backoffDir;
    float movingTimer;

    float lastJumpAttemptTime = -10f;
    Vector3 lastJumpAttemptPos;

    bool colliderWasTrigger;

    Transform lastUsedLadder;
    float climbStartPreY;
    float climbSuccessCheckTimer;
    float climbCheckStartY;
    Dictionary<Transform, float> blacklistedLadders = new Dictionary<Transform, float>();

    bool isDead = false;

    int ObstacleMask { get { return ~ignoreLayers.value; } }

    Vector2 GetVel()
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    void SetVel(Vector2 v)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
    }

    static GameObject[] SafeFindByTag(string tag)
    {
        try { return GameObject.FindGameObjectsWithTag(tag); }
        catch (UnityException) { return new GameObject[0]; }
    }

    static GameObject SafeFindOneByTag(string tag)
    {
        try { return GameObject.FindWithTag(tag); }
        catch (UnityException) { return null; }
    }

    bool IsLadderCollider(Collider2D c)
    {
        if (c == null) return false;
        Transform t = c.transform;
        for (int i = 0; i < 5 && t != null; i++)
        {
            if (t.CompareTag("Ladder")) return true;
            t = t.parent;
        }
        return false;
    }

    bool IsSolid(Collider2D c)
    {
        if (c == null || c.isTrigger) return false;
        if (c.attachedRigidbody == rb) return false;
        if (c.GetComponentInParent<ArcherBot>() != null) return false;
        if (IsLadderCollider(c)) return false;
        return true;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();

        if (col == null) { Debug.LogError("KnightBot: на рыцаре нужен Collider2D", this); enabled = false; return; }
        if (rb == null)  { Debug.LogError("KnightBot: на рыцаре нужен Rigidbody2D", this); enabled = false; return; }

        colliderWasTrigger = col.isTrigger;

        if (rb.gravityScale <= 0f) rb.gravityScale = 1f;
        normalGravity = rb.gravityScale;

        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (_sharedKnightMat == null)
        {
            _sharedKnightMat = new PhysicsMaterial2D("KnightNoFriction");
            _sharedKnightMat.friction = 0f;
            _sharedKnightMat.bounciness = 0f;
        }
        col.sharedMaterial = _sharedKnightMat;

        if (leaveLadderTime < 0.3f) leaveLadderTime = 0.6f;
        if (leaveLadderSpeedMultiplier < 1.0f) leaveLadderSpeedMultiplier = 1.6f;
        if (ladderExitPush < 0.2f) ladderExitPush = 1.2f;
        if (ladderReclimbCooldown < 1.0f) ladderReclimbCooldown = 2.0f;
    }

    void Start()
    {
        if (col == null) return;
        groundedFeetY = col.bounds.min.y;
        IgnoreAllLadderCollisions();

        if (speechBubbleObj != null)
        {
            speechBubbleObj.SetActive(false);
        }

        if (!isBoss) UpdateTargets();
    }

    void IgnoreAllLadderCollisions()
    {
        GameObject[] ladders = SafeFindByTag("Ladder");
        for (int i = 0; i < ladders.Length; i++)
        {
            Collider2D[] cols = ladders[i].GetComponentsInChildren<Collider2D>(true);
            for (int j = 0; j < cols.Length; j++)
            {
                if (cols[j] == null || cols[j] == col) continue;
                Physics2D.IgnoreCollision(col, cols[j], true);
                Log("IgnoreCollision с " + cols[j].gameObject.name);
            }
        }
    }

    void Update()
    {
        if (isDead) return;

        attackTimer += Time.deltaTime;

        if (!isBoss)
        {
            retargetTimer -= Time.deltaTime;
            if (retargetTimer <= 0f)
            {
                retargetTimer = retargetInterval;
                UpdateTargets();
            }
        }

        HandleDoorDialogue();
    }

    void FixedUpdate()
    {
        if (isDead) return;

        float dt = Time.fixedDeltaTime;

        if (climbSuccessCheckTimer > 0f)
        {
            climbSuccessCheckTimer -= dt;
            if (climbSuccessCheckTimer <= 0f && lastUsedLadder != null)
            {
                float finalY = col.bounds.min.y;
                if (finalY < climbCheckStartY + climbSuccessMinRise)
                {
                    blacklistedLadders[lastUsedLadder] = Time.time + ladderBlacklistDuration;
                    Log("climb FAILED: pre=" + climbCheckStartY.ToString("0.00") +
                        " post=" + finalY.ToString("0.00"));
                }
                else
                {
                    Log("climb OK: pre=" + climbCheckStartY.ToString("0.00") +
                        " post=" + finalY.ToString("0.00"));
                }
            }
        }

        grounded = CheckGrounded();
        if (grounded)
        {
            coyoteTimer = coyoteTime;
            if (!isClimbing && leaveLadderTimer <= 0f) groundedFeetY = col.bounds.min.y;
        }
        else
        {
            coyoteTimer -= dt;
        }

        jumpTimer -= dt;
        climbCooldownTimer -= dt;

        if (isClimbing)
        {
            ClimbStep(dt);
            UpdateAnim(0f);
            return;
        }

        // === ПЛАВНЫЙ СХОД С ЛЕСТНИЦЫ (без телепорта) ===
        if (leaveLadderTimer > 0f)
        {
            leaveLadderTimer -= dt;
            rb.gravityScale = 0f;

            float burstVx = leaveLadderDir * speed * leaveLadderSpeedMultiplier;
            lastDesiredVx = burstVx;
            SetVel(new Vector2(burstVx, 0f));
            UpdateAnim(burstVx);
            stuckTimer = 0f;
            movingTimer = 0f;

            bool reachedTargetX = false;
            if (!float.IsNaN(leaveLadderTargetX))
            {
                if (leaveLadderDir > 0 && rb.position.x >= leaveLadderTargetX - 0.05f) reachedTargetX = true;
                if (leaveLadderDir < 0 && rb.position.x <= leaveLadderTargetX + 0.05f) reachedTargetX = true;
            }

            bool foundGround = HasGroundAtFloorY(col.bounds, groundedFeetY, 0.35f, 0.8f);

            Bounds sb = col.bounds;
            float probeX = sb.center.x + leaveLadderDir * (sb.extents.x + 0.03f);
            RaycastHit2D wallProbe = CastSolid(
                new Vector2(probeX, sb.center.y),
                new Vector2(leaveLadderDir, 0f),
                0.08f);
            bool hitWall = wallProbe.collider != null;

            if (reachedTargetX || foundGround || hitWall)
                leaveLadderTimer = 0f;

            return;
        }

        rb.gravityScale = normalGravity;

        if (backoffTimer > 0f)
        {
            backoffTimer -= dt;
            lastDesiredVx = 0f;
            ApplyMove(-backoffDir * speed);
            return;
        }

        if (!isBoss) CheckStuck(dt);

        float vx;
        if (isBoss)
        {
            vx = Mathf.Sin(Time.time) * speed;
            if (Mathf.Abs(vx) > 0.05f) AutoJump(Mathf.Sign(vx));
        }
        else
        {
            vx = DecideMovement();
        }

        ApplyMove(vx);
    }

    float DecideMovement()
    {
        Transform active = targetArcher != null ? targetArcher : targetDoor;
        if (active == null) return 0f;

        int floorDir = FloorDirTo(FeetY(active));

        if (floorDir != 0)
        {
            if (targetLadder == null) return 0f;

            Bounds lb = GetBounds(targetLadder);
            float dxL = lb.center.x - transform.position.x;

            if (Mathf.Abs(dxL) > ladderAlignDistance)
                return WalkToward(dxL);

            bool canStart = floorDir > 0 ? grounded : (col.bounds.min.y > lb.min.y + 1.0f);
            if (climbCooldownTimer <= 0f && canStart)
                StartClimb(lb, floorDir);
            return 0f;
        }

        if (targetArcher != null)
        {
            float dx = targetArcher.position.x - transform.position.x;
            if (Mathf.Abs(dx) <= attackRange)
            {
                Face(dx);
                if (attackTimer >= attackCooldown)
                {
                    attackTimer = 0f;
                    AttackArcher();
                }
                return 0f;
            }
            return WalkToward(dx);
        }

        if (targetDoor == null) return 0f;
        float ddx = targetDoor.position.x - transform.position.x;
        if (Mathf.Abs(ddx) <= 0.5f) return 0f;
        return WalkToward(ddx);
    }

    float WalkToward(float dx)
    {
        if (Mathf.Abs(dx) < 0.05f) return 0f;
        float dir = Mathf.Sign(dx);
        AutoJump(dir);
        return dir * speed;
    }

    void ApplyMove(float vx)
    {
        lastDesiredVx = vx;
        SetVel(new Vector2(vx, GetVel().y));
        UpdateAnim(vx);
    }

    void UpdateAnim(float vx)
    {
        if (anim != null) anim.SetFloat("Speed", Mathf.Abs(vx));
        if (vx > 0.05f) Face(1f);
        else if (vx < -0.05f) Face(-1f);
    }

    void Face(float dir)
    {
        if (spriteRenderer != null && dir != 0f) spriteRenderer.flipX = dir < 0f;
    }

    void AutoJump(float dir)
    {
        if (coyoteTimer <= 0f || jumpTimer > 0f || isClimbing) return;

        if (Time.time - lastJumpAttemptTime < 1.5f &&
            Vector3.Distance(rb.position, lastJumpAttemptPos) < 0.6f)
            return;

        float h, frontX;
        if (ObstacleAhead(dir, out h, out frontX))
        {
            float v;
            if (CanClear(dir, frontX, h, out v))
            {
                Log("прыжок: высота преграды " + h.ToString("0.00") + ", скорость " + v.ToString("0.0"));
                lastJumpAttemptTime = Time.time;
                lastJumpAttemptPos = rb.position;
                Jump(v);
            }
            return;
        }

        if (jumpOverGaps && GapAhead(dir) && !NearTargetLadder())
        {
            lastJumpAttemptTime = Time.time;
            lastJumpAttemptPos = rb.position;
            Jump(jumpForce);
        }
    }

    void Jump(float velocity)
    {
        if (jumpTimer > 0f) return;
        velocity = ClampToHeadroom(velocity);
        SetVel(new Vector2(GetVel().x, velocity));
        jumpTimer = jumpCooldown;
        coyoteTimer = 0f;
        grounded = false;
    }

    float Gravity()
    {
        float g = Mathf.Abs(Physics2D.gravity.y * normalGravity);
        return g < 0.01f ? 9.81f : g;
    }

    float MaxStepHeight()
    {
        float g = Gravity();
        float h = (maxJumpForce * maxJumpForce) / (2f * g) - jumpMargin;
        return Mathf.Clamp(h, 0.5f, 20f);
    }

    float CeilingY(float x, float fromY)
    {
        RaycastHit2D hit = CastSolid(new Vector2(x, fromY), Vector2.up, 8f);
        return hit.collider != null ? hit.point.y : Mathf.Infinity;
    }

    float ClampToHeadroom(float velocity)
    {
        Bounds b = col.bounds;
        float ceil = CeilingY(b.center.x, b.max.y + 0.01f);
        if (float.IsInfinity(ceil)) return velocity;
        float riseMax = Mathf.Max(0f, ceil - b.max.y - 0.03f);
        return Mathf.Min(velocity, Mathf.Sqrt(2f * Gravity() * riseMax));
    }

    bool CanClear(float dir, float frontX, float stepHeight, out float velocity)
    {
        velocity = 0f;
        if (float.IsInfinity(stepHeight)) { LogThrottled("преграда слишком высокая"); return false; }

        Bounds b = col.bounds;
        float tall = b.size.y;

        float ceilHere = CeilingY(b.center.x, b.max.y + 0.01f);
        float ceilLand = CeilingY(frontX + dir * 0.3f, b.min.y + stepHeight + tall);
        float ceil = Mathf.Min(ceilHere, ceilLand);

        float riseMax = ceil - b.max.y - 0.03f;
        float g = Gravity();
        float riseCap = (maxJumpForce * maxJumpForce) / (2f * g);
        float rise = Mathf.Min(stepHeight + jumpMargin, Mathf.Min(riseMax, riseCap));

        if (rise < stepHeight + 0.03f)
        {
            LogThrottled("не пролезть: уступ " + stepHeight.ToString("0.00") + ", запас до потолка " + riseMax.ToString("0.00"));
            return false;
        }

        velocity = Mathf.Sqrt(2f * g * rise);
        return true;
    }

    bool ObstacleAhead(float dir, out float height, out float frontX)
    {
        height = 0f;
        frontX = 0f;
        Bounds b = col.bounds;
        float edgeX = b.center.x + dir * (b.extents.x + 0.02f);
        Vector2 rayDir = new Vector2(dir, 0f);

        float lowH = 0.2f;
        RaycastHit2D low = CastSolid(new Vector2(edgeX, b.min.y + lowH), rayDir, obstacleCheckDistance);
        if (low.collider == null) return false;
        frontX = low.point.x;

        float limit = MaxStepHeight();
        if (float.IsInfinity(limit) || float.IsNaN(limit)) limit = 5f;

        for (float h = lowH + scanStep; h <= limit + scanStep; h += scanStep)
        {
            if (CastSolid(new Vector2(edgeX, b.min.y + h), rayDir, obstacleCheckDistance).collider == null)
            {
                float top = h;
                for (float r = h - scanStep + fineStep; r < h; r += fineStep)
                {
                    if (CastSolid(new Vector2(edgeX, b.min.y + r), rayDir, obstacleCheckDistance).collider == null)
                    {
                        top = r;
                        break;
                    }
                }
                height = top;
                return true;
            }
        }

        height = Mathf.Infinity;
        return true;
    }

    bool GapAhead(float dir)
    {
        Bounds b = col.bounds;
        float frontX = b.center.x + dir * (b.extents.x + 0.25f);

        if (HasGroundAt(frontX, b.min.y)) return false;

        for (float d = 0.5f; d <= maxGapWidth; d += 0.5f)
        {
            if (HasGroundAt(frontX + dir * d, b.min.y)) return true;
        }
        return false;
    }

    bool HasGroundAt(float x, float feetY)
    {
        RaycastHit2D h = CastSolid(new Vector2(x, feetY + 0.2f), Vector2.down, 0.7f);
        return h.collider != null;
    }

    bool NearTargetLadder()
    {
        if (targetLadder == null) return false;
        float ladderX = GetBounds(targetLadder).center.x;
        float knightX = col.bounds.center.x;
        return Mathf.Abs(ladderX - knightX) < 1.5f;
    }

    bool CheckGrounded()
    {
        Bounds b = col.bounds;
        Vector2 size = new Vector2(b.size.x * 0.8f, 0.1f);
        Vector2 center = new Vector2(b.center.x, b.min.y - 0.03f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, ObstacleMask);
        for (int i = 0; i < hits.Length; i++)
        {
            if (IsSolid(hits[i])) return true;
        }
        return false;
    }

    RaycastHit2D CastSolid(Vector2 origin, Vector2 dir, float distance)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, dir, distance, ObstacleMask);
        for (int i = 0; i < hits.Length; i++)
        {
            if (IsSolid(hits[i].collider)) return hits[i];
        }
        return new RaycastHit2D();
    }

    bool HasGroundAtFloorY(Bounds b, float floorY, float tolerance, float depth)
    {
        float[] xs = { b.center.x, b.min.x + 0.1f, b.max.x - 0.1f };
        for (int i = 0; i < xs.Length; i++)
        {
            RaycastHit2D hit = CastSolid(new Vector2(xs[i], b.min.y + 0.02f), Vector2.down, depth);
            if (hit.collider == null) continue;
            if (Mathf.Abs(hit.point.y - floorY) <= tolerance) return true;
        }
        return false;
    }

    void CheckStuck(float dt)
    {
        bool trying = Mathf.Abs(lastDesiredVx) > 0.1f;
        float actualVx = Mathf.Abs(GetVel().x);

        if (trying && grounded && actualVx < 0.15f) stuckTimer += dt;
        else stuckTimer = 0f;

        if (stuckTimer < stuckTime) return;
        stuckTimer = 0f;

        if (debugStuck)
        {
            Bounds b = col.bounds;
            Collider2D[] stuckHits = Physics2D.OverlapBoxAll(b.center, b.size * 0.9f, 0f, ObstacleMask);
            StringBuilder sb = new StringBuilder();
            foreach (var hitC in stuckHits)
            {
                if (hitC == null || hitC == col) continue;
                if (!IsSolid(hitC)) continue;
                sb.Append($"[{hitC.name} tag={hitC.tag} layer={hitC.gameObject.layer}] ");
            }
            Debug.LogWarning(
                $"[Knight] STUCK pos={rb.position} desiredVx={lastDesiredVx:F2} actualVx={actualVx:F2} " +
                $"grounded={grounded} floorY={groundedFeetY:F2} overlaps={sb}", this);
        }

        if (Mathf.Abs(lastDesiredVx) > 0.1f)
        {
            backoffDir = Mathf.Sign(lastDesiredVx);
            backoffTimer = backoffTime;
        }

        targetLadder = null;
        ladderLockTarget = null;
        retargetTimer = 0f;
    }

    void StartClimb(Bounds ladderBounds, int dir)
    {
        if (climbCooldownTimer > 0f) return;

        isClimbing = true;
        climbDir = dir;
        climbBounds = ladderBounds;
        climbTimer = 0f;
        climbStartFeetY = col.bounds.min.y;
        climbStartPreY = col.bounds.min.y;
        lastUsedLadder = targetLadder;
        leaveLadderTimer = 0f;
        Log("начал " + (dir > 0 ? "подъём" : "спуск") + " по лестнице. preY=" +
            climbStartPreY.ToString("0.00"));

        rb.gravityScale = 0f;
        SetVel(Vector2.zero);
        if (passThroughOnLadder) col.isTrigger = true;
    }

    void ClimbStep(float dt)
    {
        climbTimer += dt;
        rb.gravityScale = 0f;

        Bounds b = col.bounds;
        float alignX = Mathf.Clamp((climbBounds.center.x - transform.position.x) * 8f, -2f, 2f);
        SetVel(new Vector2(alignX, climbSpeed * climbDir));

        bool done = false;
        if (climbDir > 0)
        {
            if (b.min.y >= climbBounds.max.y + 0.02f) done = true;
        }
        else
        {
            float descended = climbStartFeetY - b.min.y;
            if (b.min.y <= climbBounds.min.y + 0.05f) done = true;
            else if (descended > 0.8f && grounded) done = true;
        }
        if (climbTimer > climbTimeout) done = true;

        if (done) FinishClimb();
    }

    void FinishClimb()
    {
        isClimbing = false;
        col.isTrigger = colliderWasTrigger;
        rb.gravityScale = normalGravity;

        PushUpUntilClear();

        SetVel(new Vector2(0f, 0.2f));

        Bounds b = col.bounds;

        float bestFloor = float.NegativeInfinity;
        float[] xs = { b.center.x, b.min.x + 0.1f, b.max.x - 0.1f };
        for (int i = 0; i < xs.Length; i++)
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(
                new Vector2(xs[i], b.min.y + 0.1f),
                Vector2.down,
                8f,
                ObstacleMask);
            for (int j = 0; j < hits.Length; j++)
            {
                if (!IsSolid(hits[j].collider)) continue;
                if (hits[j].point.y <= b.min.y + 0.2f && hits[j].point.y > bestFloor)
                    bestFloor = hits[j].point.y;
            }
        }

        if (!float.IsNegativeInfinity(bestFloor))
            groundedFeetY = bestFloor;
        else
            groundedFeetY = climbBounds.max.y;

        float exitDir = 1f;
        float knightX = rb.position.x;
        float ladderCenterX = climbBounds.center.x;
        float targetX = float.NaN;

        if (climbDir > 0)
        {
            float groundScanX = FindSnapX(groundedFeetY, 3.0f);
            if (!float.IsNaN(groundScanX) && Mathf.Abs(groundScanX - knightX) > 0.05f)
            {
                exitDir = Mathf.Sign(groundScanX - knightX);
                targetX = groundScanX;
                Log("цель сдвига X → " + groundScanX.ToString("0.00"));
            }
            else
            {
                exitDir = (knightX >= ladderCenterX) ? 1f : -1f;
                targetX = knightX + exitDir * ladderExitPush;
            }
        }
        else
        {
            Transform active = targetArcher != null ? targetArcher : targetDoor;
            if (active != null && Mathf.Abs(active.position.x - transform.position.x) > 0.1f)
                exitDir = Mathf.Sign(active.position.x - transform.position.x);
            else if (spriteRenderer != null)
                exitDir = spriteRenderer.flipX ? -1f : 1f;
            targetX = knightX + exitDir * ladderExitPush;
        }
        if (Mathf.Abs(exitDir) < 0.1f) exitDir = 1f;

        stuckTimer = 0f;
        movingTimer = 0f;
        backoffTimer = 0f;
        backoffDir = 0f;
        lastDesiredVx = 0f;
        lastJumpAttemptTime = -10f;

        leaveLadderTimer = Mathf.Max(0.05f, leaveLadderTime);
        leaveLadderDir = exitDir;
        leaveLadderTargetX = targetX;

        jumpTimer = jumpCooldown;
        climbCooldownTimer = ladderReclimbCooldown;
        targetLadder = null;
        ladderLockTarget = null;
        retargetTimer = 0f;

        climbSuccessCheckTimer = climbSuccessWindow;
        climbCheckStartY = climbStartPreY;

        Log("закончил " + (climbDir > 0 ? "подъём" : "спуск") +
            ", этаж y=" + groundedFeetY.ToString("0.00") +
            ", сдвиг dir=" + exitDir.ToString("0") +
            ", targetX=" + targetX.ToString("0.00"));
    }

    float FindSnapX(float floorY, float maxDist)
    {
        Bounds b = col.bounds;
        float centerX = b.center.x;

        for (float d = 0f; d <= maxDist; d += 0.1f)
        {
            for (int sign = -1; sign <= 1; sign += 2)
            {
                if (d == 0f && sign > 0) continue;
                float testX = centerX + sign * d;

                RaycastHit2D hit = CastSolid(new Vector2(testX, floorY + 0.1f), Vector2.down, 0.4f);
                if (hit.collider == null) continue;
                if (Mathf.Abs(hit.point.y - floorY) > 0.3f) continue;

                Vector2 checkCenter = new Vector2(testX, floorY + b.extents.y + 0.05f);
                Collider2D[] overlaps = Physics2D.OverlapBoxAll(checkCenter, b.size * 0.8f, 0f, ObstacleMask);
                bool blocked = false;
                for (int i = 0; i < overlaps.Length; i++)
                {
                    if (overlaps[i] == col) continue;
                    if (!IsSolid(overlaps[i])) continue;
                    blocked = true;
                    break;
                }
                if (blocked) continue;

                return testX;
            }
        }
        return float.NaN;
    }

    bool PushUpUntilClear()
    {
        for (int i = 0; i < 40; i++)
        {
            Physics2D.SyncTransforms();

            Bounds b = col.bounds;
            Collider2D[] hits = Physics2D.OverlapBoxAll(
                b.center,
                new Vector2(b.size.x * 0.85f, b.size.y * 0.9f),
                0f,
                ObstacleMask
            );

            bool overlapping = false;
            for (int j = 0; j < hits.Length; j++)
            {
                if (hits[j] == col) continue;
                if (!IsSolid(hits[j])) continue;
                overlapping = true;
                break;
            }

            if (!overlapping) return true;

            rb.position += Vector2.up * 0.1f;
        }

        Bounds ab = col.bounds;
        rb.position += Vector2.up * (ab.size.y + 0.5f);
        return false;
    }

    Bounds GetBounds(Transform t)
    {
        Collider2D c = t.GetComponent<Collider2D>();
        if (c == null) c = t.GetComponentInChildren<Collider2D>();
        if (c != null) return c.bounds;

        Renderer r = t.GetComponentInChildren<Renderer>();
        if (r != null) return r.bounds;

        return new Bounds(t.position, new Vector3(0.6f, 3f, 1f));
    }

    float FeetY(Transform t) { return GetBounds(t).min.y; }

    bool SameFloor(float feetY)
    {
        return Mathf.Abs(feetY - groundedFeetY) < floorTolerance;
    }

    int FloorDirTo(float targetFeetY)
    {
        float diff = targetFeetY - groundedFeetY;
        if (Mathf.Abs(diff) < floorTolerance) return 0;
        return diff > 0f ? 1 : -1;
    }

    bool LadderUsable(Bounds lb, int dir)
    {
        float f = groundedFeetY;
        if (dir > 0) return lb.min.y <= f + 0.6f && lb.max.y >= f + 1.0f;
        return lb.max.y >= f - 0.6f && lb.min.y <= f - 1.0f;
    }

    bool IsBlacklisted(Transform ladder)
    {
        if (ladder == null) return false;
        float until;
        if (blacklistedLadders.TryGetValue(ladder, out until))
        {
            if (Time.time < until) return true;
            blacklistedLadders.Remove(ladder);
        }
        return false;
    }

    Transform FindBestLadder(float targetFeetY)
    {
        int dir = FloorDirTo(targetFeetY);
        if (dir == 0) return null;

        GameObject[] ladders = SafeFindByTag("Ladder");
        Transform best = null;
        float bestScore = Mathf.Infinity;

        for (int i = 0; i < ladders.Length; i++)
        {
            Transform lt = ladders[i].transform;
            if (IsBlacklisted(lt)) continue;

            Bounds lb = GetBounds(lt);
            if (!LadderUsable(lb, dir)) continue;

            float reachedY = dir > 0 ? lb.max.y : lb.min.y;
            float score = Mathf.Abs(lb.center.x - transform.position.x)
                        + Mathf.Abs(reachedY - targetFeetY) * 2f;

            if (score < bestScore)
            {
                bestScore = score;
                best = lt;
            }
        }
        return best;
    }

    void ApplyLadder(Transform target, Transform candidate)
    {
        if (targetLadder != null && ladderLockTarget == target &&
            LadderUsable(GetBounds(targetLadder), FloorDirTo(FeetY(target))))
        {
            return;
        }
        if (candidate != null) Log("иду к лестнице: " + candidate.name);
        targetLadder = candidate;
        ladderLockTarget = target;
    }

    void Log(string message) { if (debugLog) Debug.Log("[Knight] " + message, this); }

    void LogThrottled(string message)
    {
        if (!debugLog || Time.time - lastLogTime < 1f) return;
        lastLogTime = Time.time;
        Debug.Log("[Knight] " + message, this);
    }

    bool CanSee(Transform t)
    {
        Vector2 from = col.bounds.center;
        Vector2 to = GetBounds(t).center;
        float d = Vector2.Distance(from, to);
        if (d < 0.01f) return true;

        RaycastHit2D wall = Physics2D.Raycast(from, (to - from).normalized, d, wallOnlyLayer);
        return wall.collider == null || wall.transform == t;
    }

    void SetArcher(Transform archer, ArcherBot script)
    {
        if (targetArcher != archer && archer != null) Log("цель: " + archer.name);
        targetArcher = archer;
        currentArcherScript = script;
        targetDoor = null;
    }

    // ПРИОРИТЕТ: сначала скелеты (видимые / достижимые через лестницу / заблокированные),
    // дверь — только если скелетов вообще нет.
    void UpdateTargets()
    {
        if (isClimbing) return;
        if (leaveLadderTimer > 0f) return;

        if (targetArcher != null && currentArcherScript != null &&
            !currentArcherScript.IsDead() && SameFloor(FeetY(targetArcher)))
        {
            return;
        }

        Transform visible = null; ArcherBot visibleScript = null; float visibleD = Mathf.Infinity;
        Transform blocked = null; ArcherBot blockedScript = null; float blockedD = Mathf.Infinity;
        Transform other = null; ArcherBot otherScript = null; Transform otherLadder = null; float otherD = Mathf.Infinity;

        GameObject[] archers = SafeFindByTag("Archer");
        for (int i = 0; i < archers.Length; i++)
        {
            ArcherBot script = archers[i].GetComponent<ArcherBot>();
            if (script == null || script.IsDead()) continue;

            Transform a = archers[i].transform;
            float d = Vector2.Distance(transform.position, a.position);

            if (SameFloor(FeetY(a)))
            {
                if (CanSee(a))
                {
                    if (d < visibleD) { visibleD = d; visible = a; visibleScript = script; }
                }
                else if (d < blockedD)
                {
                    blockedD = d; blocked = a; blockedScript = script;
                }
            }
            else if (d < otherD)
            {
                Transform ladder = FindBestLadder(FeetY(a));
                if (ladder != null) { otherD = d; other = a; otherScript = script; otherLadder = ladder; }
            }
        }

        if (visible != null)
        {
            SetArcher(visible, visibleScript);
            targetLadder = null; ladderLockTarget = null;
        }
        else if (other != null)
        {
            SetArcher(other, otherScript);
            ApplyLadder(other, otherLadder);
        }
        else if (blocked != null)
        {
            SetArcher(blocked, blockedScript);
            targetLadder = null; ladderLockTarget = null;
        }
        else
        {
            // Скелетов нет — идём к двери
            targetArcher = null;
            currentArcherScript = null;

            if (targetDoor == null)
            {
                GameObject doorObj = SafeFindOneByTag("Door");
                if (doorObj != null) targetDoor = doorObj.transform;
            }

            if (targetDoor != null && FloorDirTo(FeetY(targetDoor)) != 0)
            {
                ApplyLadder(targetDoor, FindBestLadder(FeetY(targetDoor)));
            }
            else
            {
                targetLadder = null; ladderLockTarget = null;
            }
        }
    }

    void AttackArcher()
    {
        if (anim != null) anim.SetTrigger("Attack");

        if (currentArcherScript != null)
        {
            currentArcherScript.TakeDamage(knightDamage);

            if (currentArcherScript.IsDead())
            {
                targetArcher = null;
                currentArcherScript = null;
                UpdateTargets();
            }
        }
    }

    // === ДИАЛОГ У ДВЕРИ ===
    void HandleDoorDialogue()
    {
        if (targetDoor != null)
        {
            float distToDoor = Vector2.Distance(transform.position, targetDoor.position);

            if (distToDoor <= 2.5f)
            {
                if (!wasNearDoorLastField)
                {
                    wasNearDoorLastField = true;
                    ShowRandomDoorPhrase();
                    phraseTimer = phraseChangeInterval;
                }
                else
                {
                    phraseTimer -= Time.deltaTime;
                    if (phraseTimer <= 0f)
                    {
                        ShowRandomDoorPhrase();
                        phraseTimer = phraseChangeInterval;
                    }
                }
            }
            else
            {
                if (wasNearDoorLastField)
                {
                    wasNearDoorLastField = false;
                    if (speechBubbleObj != null) speechBubbleObj.SetActive(false);
                }
            }
        }
        else
        {
            if (wasNearDoorLastField)
            {
                wasNearDoorLastField = false;
                if (speechBubbleObj != null) speechBubbleObj.SetActive(false);
            }
        }
    }

    void ShowRandomDoorPhrase()
    {
        if (doorPhrases == null || doorPhrases.Length == 0) return;

        if (speechBubbleObj != null)
        {
            speechBubbleObj.SetActive(true);
        }

        if (speechText != null)
        {
            int randomIndex = Random.Range(0, doorPhrases.Length);
            speechText.text = doorPhrases[randomIndex];
        }
    }

    // === ТРИГГЕР СМЕРТИ ===
    void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return;
        if (other == null) return;

        if (!string.IsNullOrEmpty(deathTag) && other.CompareTag(deathTag))
        {
            Die();
        }
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        if (trashPrefab != null)
            Instantiate(trashPrefab, transform.position, Quaternion.identity);

        if (destroyOnDeath)
            Destroy(gameObject);
        else
            enabled = false;
    }

    public void TakeArrowHit()
    {
        if (trashPrefab != null)
            Instantiate(trashPrefab, transform.position, Quaternion.identity);
    }

    public void TakeDamage(int damage)
    {
        if (!isBoss) return;

        bossHP -= damage;
        if (bossHP <= 0)
        {
            if (trashPrefab != null)
                Instantiate(trashPrefab, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
    }

    void OnDrawGizmosSelected()
    {
        Collider2D c = col != null ? col : GetComponent<Collider2D>();
        if (c == null) return;

        Bounds b = c.bounds;

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(new Vector3(b.center.x, b.min.y - 0.03f, 0f), new Vector3(b.size.x * 0.8f, 0.1f, 0f));

        Gizmos.color = Color.red;
        Vector3 r = new Vector3(b.max.x + 0.02f, b.min.y + 0.2f, 0f);
        Vector3 l = new Vector3(b.min.x - 0.02f, b.min.y + 0.2f, 0f);
        Gizmos.DrawLine(r, r + Vector3.right * obstacleCheckDistance);
        Gizmos.DrawLine(l, l + Vector3.left * obstacleCheckDistance);
    }
}