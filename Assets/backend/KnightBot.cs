using UnityEngine;
using System.Text;

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
    public int maxJumpRetries = 3;
    public float backoffTime = 0.8f;

    [Header("Этажи и цели")]
    public float floorTolerance = 1.5f;
    public float retargetInterval = 0.25f;

    [Header("Лестницы")]
    public float ladderAlignDistance = 0.2f;
    public float climbTimeout = 8f;
    public bool passThroughOnLadder = true;
    public float leaveLadderTime = 0.35f;   // сколько секунд принудительно уходим с лестницы после подъёма/спуска

    [Header("Отладка")]
    public bool debugLog = false;
    public bool debugStuck = false;         // печатать в Console причину застревания

    [Header("Префаб Мусора")]
    public GameObject trashPrefab;

    [Header("Боссфайт")]
    public bool isBoss = false;
    public int bossHP = 100;

    [Header("Бой со Скелетом")]
    public float attackRange = 1.5f;
    public int knightDamage = 25;
    public float attackCooldown = 1.5f;

    // ---- служебное ----
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

    // Страховка схода с лестницы
    float leaveLadderTimer;
    float leaveLadderDir;

    float lastDesiredVx;
    float stuckTimer;
    int jumpRetries;
    float backoffTimer;
    float backoffDir;
    float movingTimer;

    bool colliderWasTrigger;

    int ObstacleMask { get { return ~ignoreLayers.value; } }

    // =====================================================================
    //  УТИЛИТЫ СОВМЕСТИМОСТИ
    // =====================================================================
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

    // =====================================================================
    //  ЖИЗНЕННЫЙ ЦИКЛ
    // =====================================================================
    bool IsSolid(Collider2D c)
    {
        if (c == null || c.isTrigger) return false;
        if (c.attachedRigidbody == rb) return false;
        if (c.GetComponentInParent<ArcherBot>() != null) return false;
        return true;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();

        if (col == null)
        {
            Debug.LogError("KnightBot: на рыцаре нужен Collider2D", this);
            enabled = false;
            return;
        }
        if (rb == null)
        {
            Debug.LogError("KnightBot: на рыцаре нужен Rigidbody2D", this);
            enabled = false;
            return;
        }

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
    }

    void Start()
    {
        if (col == null) return;
        groundedFeetY = col.bounds.min.y;
        if (!isBoss) UpdateTargets();
    }

    void Update()
    {
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
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

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

        // --- Лестница: ползание ---
        if (isClimbing)
        {
            ClimbStep(dt);
            UpdateAnim(0f);
            return;
        }

        rb.gravityScale = normalGravity;

        // --- Страховка схода с лестницы ---
        if (leaveLadderTimer > 0f)
        {
            leaveLadderTimer -= dt;
            lastDesiredVx = leaveLadderDir * speed;
            ApplyMove(leaveLadderDir * speed);
            stuckTimer = 0f;
            movingTimer = 0f;
            jumpRetries = 0;
            return;
        }

        // --- Отход назад после неудачных попыток пройти ---
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

    // =====================================================================
    //  ПРИНЯТИЕ РЕШЕНИЙ
    // =====================================================================
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

    // =====================================================================
    //  ПРЫЖКИ
    // =====================================================================
    void AutoJump(float dir)
    {
        if (coyoteTimer <= 0f || jumpTimer > 0f || isClimbing) return;

        float h, frontX;
        if (ObstacleAhead(dir, out h, out frontX))
        {
            float v;
            if (CanClear(dir, frontX, h, out v))
            {
                Log("прыжок: высота преграды " + h.ToString("0.00") + ", скорость " + v.ToString("0.0"));
                Jump(v);
            }
            return;
        }

        if (jumpOverGaps && GapAhead(dir) && !NearTargetLadder())
            Jump(jumpForce);
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

    // Безопасная гравитация: не бывает 0, чтобы не было деления на ноль.
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

    // =====================================================================
    //  ЗЕМЛЯ И ЛУЧИ
    // =====================================================================
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

    // =====================================================================
    //  АНТИЗАСТРЕВАНИЕ
    // =====================================================================
    void CheckStuck(float dt)
    {
        bool trying = Mathf.Abs(lastDesiredVx) > 0.1f;
        float actualVx = Mathf.Abs(GetVel().x);

        if (trying && grounded && actualVx < 0.15f) stuckTimer += dt;
        else stuckTimer = 0f;

        if (trying && actualVx > 0.25f)
        {
            movingTimer += dt;
            if (movingTimer > 1f) { jumpRetries = 0; movingTimer = 0f; }
        }
        else if (!trying)
        {
            movingTimer = 0f;
        }

        if (stuckTimer < stuckTime) return;
        stuckTimer = 0f;

        if (debugStuck)
        {
            Bounds b = col.bounds;
            Collider2D[] hits = Physics2D.OverlapBoxAll(b.center, b.size * 0.9f, 0f, ObstacleMask);
            StringBuilder sb = new StringBuilder();
            foreach (var h in hits)
            {
                if (h == null || h == col) continue;
                if (!IsSolid(h)) continue;
                sb.Append($"[{h.name} layer={h.gameObject.layer}] ");
            }
            Debug.LogWarning(
                $"[Knight] STUCK: pos={rb.position} desiredVx={lastDesiredVx:F2} actualVx={actualVx:F2} " +
                $"grounded={grounded} floorY={groundedFeetY:F2} overlaps={sb}", this);
        }

        if (jumpRetries < maxJumpRetries)
        {
            float t = (jumpRetries + 1f) / Mathf.Max(1, maxJumpRetries);
            jumpTimer = 0f;
            Jump(Mathf.Lerp(jumpForce, maxJumpForce, t));
            jumpRetries++;
        }
        else
        {
            backoffDir = Mathf.Sign(lastDesiredVx);
            backoffTimer = backoffTime;
            jumpRetries = 0;
        }
    }

    // =====================================================================
    //  ЛЕСТНИЦЫ
    // =====================================================================
    void StartClimb(Bounds ladderBounds, int dir)
    {
        isClimbing = true;
        climbDir = dir;
        climbBounds = ladderBounds;
        climbTimer = 0f;
        climbStartFeetY = col.bounds.min.y;
        leaveLadderTimer = 0f;
        Log("начал " + (dir > 0 ? "подъём" : "спуск") + " по лестнице");

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
            // Поднимаемся чуть ВЫШЕ верхнего края лестницы, чтобы ступни не оказались внутри платформы
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
        // 1. Восстанавливаем физическое состояние — иначе OverlapBox не увидит перекрытий
        isClimbing = false;
        col.isTrigger = colliderWasTrigger;
        rb.gravityScale = normalGravity;

        // 2. Выталкиваем рыцаря из любых твёрдых коллайдеров (платформа, потолок, лестница-не-триггер)
        bool cleared = PushUpUntilClear();
        if (!cleared && debugLog)
            Debug.LogWarning("[Knight] FinishClimb: не удалось вытолкнуть рыцаря за 40 шагов — аварийный телепорт.", this);

        // 3. Гасим скорость. Небольшой импульс вверх помогает «отклеиться»
        SetVel(new Vector2(0f, climbDir > 0 ? 0.5f : 0f));

        // 4. Определяем фактический уровень пола лучом вниз
        Bounds b = col.bounds;
        RaycastHit2D floorRay = CastSolid(new Vector2(b.center.x, b.min.y + 0.05f), Vector2.down, 4f);
        if (floorRay.collider != null)
        {
            groundedFeetY = floorRay.point.y;
        }
        else
        {
            groundedFeetY = climbBounds.max.y;
            if (debugLog)
                Debug.LogWarning($"[Knight] FinishClimb: пол не найден, использую верх лестницы {climbBounds.max.y:F2}", this);
        }

        // 5. Сбрасываем антизастревание — иначе рыцарь сразу начнёт прыгать на месте
        stuckTimer = 0f;
        movingTimer = 0f;
        jumpRetries = 0;
        backoffTimer = 0f;
        backoffDir = 0f;
        lastDesiredVx = 0f;

        // 6. Направление схода с лестницы
        Transform active = targetArcher != null ? targetArcher : targetDoor;
        float exitDir = 1f;
        if (active != null && Mathf.Abs(active.position.x - transform.position.x) > 0.1f)
            exitDir = Mathf.Sign(active.position.x - transform.position.x);
        else if (spriteRenderer != null)
            exitDir = spriteRenderer.flipX ? -1f : 1f;

        // 7. Включаем принудительный сход
        leaveLadderTimer = Mathf.Max(0.01f, leaveLadderTime);
        leaveLadderDir = exitDir;

        jumpTimer = jumpCooldown;
        climbCooldownTimer = 0.6f;
        targetLadder = null;
        ladderLockTarget = null;
        retargetTimer = 0f;

        Log("закончил " + (climbDir > 0 ? "подъём" : "спуск") + ", этаж y=" + groundedFeetY.ToString("0.00"));
    }

    // Поднимает рыцаря вверх шагами по 0.1, пока его коллайдер не окажется вне твёрдых коллайдеров.
    // Возвращает true, если удалось вытолкнуть без аварийного телепорта.
    bool PushUpUntilClear()
    {
        for (int i = 0; i < 40; i++)
        {
            Physics2D.SyncTransforms();

            Bounds b = col.bounds;
            Collider2D[] hits = Physics2D.OverlapBoxAll(
                b.center,
                new Vector2(b.size.x * 0.85f, b.size.y * 0.9f), // чуть меньше — не считаем задевание за край
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

        // Не удалось — аварийный телепорт наверх
        Bounds ab = col.bounds;
        rb.position += Vector2.up * (ab.size.y + 0.5f);
        return false;
    }

    // =====================================================================
    //  ЭТАЖИ И ПОИСК ЦЕЛЕЙ
    // =====================================================================
    Bounds GetBounds(Transform t)
    {
        Collider2D c = t.GetComponent<Collider2D>();
        if (c == null) c = t.GetComponentInChildren<Collider2D>();
        if (c != null) return c.bounds;

        Renderer r = t.GetComponentInChildren<Renderer>();
        if (r != null) return r.bounds;

        return new Bounds(t.position, new Vector3(0.6f, 3f, 1f));
    }

    float FeetY(Transform t)
    {
        return GetBounds(t).min.y;
    }

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

    Transform FindBestLadder(float targetFeetY)
    {
        int dir = FloorDirTo(targetFeetY);
        if (dir == 0) return null;

        GameObject[] ladders = SafeFindByTag("Ladder");
        Transform best = null;
        float bestScore = Mathf.Infinity;

        for (int i = 0; i < ladders.Length; i++)
        {
            Bounds lb = GetBounds(ladders[i].transform);
            if (!LadderUsable(lb, dir)) continue;

            float reachedY = dir > 0 ? lb.max.y : lb.min.y;
            float score = Mathf.Abs(lb.center.x - transform.position.x)
                        + Mathf.Abs(reachedY - targetFeetY) * 2f;

            if (score < bestScore)
            {
                bestScore = score;
                best = ladders[i].transform;
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

    void Log(string message)
    {
        if (debugLog) Debug.Log("[Knight] " + message, this);
    }

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

    void UpdateTargets()
    {
        if (isClimbing) return;

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

    // =====================================================================
    //  БОЙ И УРОН
    // =====================================================================
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

    // =====================================================================
    //  ОТЛАДКА
    // =====================================================================
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