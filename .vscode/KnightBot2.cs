using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class KnightBot2 : MonoBehaviour
{
    [Header("Движение и Прыжки")]
    public float speed = 2.5f;
    public float climbSpeed = 3f;
    public float jumpForce = 7f;             // обычный прыжок
    public float maxJumpForce = 9f;          // самый сильный прыжок (для высоких уступов)
    public float obstacleCheckDistance = 0.5f; // на каком расстоянии от края тела видим преграду
    public float jumpClearance = 0.5f;       // запас высоты над уступом
    public float jumpCooldown = 0.3f;
    public bool jumpOverGaps = true;         // перепрыгивать ямы
    public float maxGapWidth = 2.5f;
    public LayerMask groundLayer;            // больше не обязателен: преградой считается любой твёрдый коллайдер
    public LayerMask wallOnlyLayer;          // стены для проверки «видит ли скелета»
    public LayerMask ignoreLayers;           // слои, которые НЕ считаем преградой (стрелы, мусор...)
    public float stepProbe = 0.25f;          // шаг, которым измеряем высоту преграды

    [Header("Защита от застревания")]
    public float stuckTime = 0.5f;           // сколько стоим «в упор», прежде чем действовать
    public int maxJumpRetries = 3;           // сколько раз пробуем прыгнуть сильнее
    public float backoffTime = 0.8f;         // потом отходим назад на это время

    [Header("Этажи и цели")]
    public float floorTolerance = 0.65f;     // не смешиваем соседние этажи
    public float retargetInterval = 0.25f;

    [Header("Лестницы")]
    public float ladderAlignDistance = 0.2f;
    public float climbTimeout = 8f;
    public bool passThroughOnLadder = true;  // на лестнице проходим сквозь платформы

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

    float normalGravity = 1f;
    bool grounded;
    float groundedFeetY;
    float coyoteTimer;
    const float coyoteTime = 0.1f;
    float jumpTimer;
    float attackTimer;
    float retargetTimer;

    // цели
    Transform targetArcher;
    ArcherBot currentArcherScript;
    Transform targetDoor;
    Transform targetLadder;
    Transform ladderLockTarget;

    // лестница
    bool isClimbing;
    int climbDir;
    Bounds climbBounds;
    float climbTimer;
    float climbCooldownTimer;

    // Короткий принудительный выход с лестницы после подъёма/спуска.
    float ladderExitTimer;
    float ladderExitDir = 1f;

    // антизастревание
    float lastDesiredVx;
    float stuckTimer;
    int jumpRetries;
    float backoffTimer;
    float backoffDir;
    float movingTimer;

    // Преградой считаем ЛЮБОЙ твёрдый коллайдер (бочки, ящики, стены, земля),
    // кроме слоёв из ignoreLayers.
    int ObstacleMask { get { return ~ignoreLayers.value; } }

    bool IsSolid(Collider2D c)
    {
        if (c == null || c.isTrigger) return false;           // лестницы и зоны
        if (c.attachedRigidbody == rb) return false;          // сам рыцарь и его дочерние коллайдеры
        if (c.GetComponentInParent<ArcherBot>() != null) return false; // скелеты не преграда
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

        normalGravity = rb.gravityScale > 0f ? rb.gravityScale : 1f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        // нулевое трение: рыцарь не «прилипает» к стенам
        PhysicsMaterial2D mat = new PhysicsMaterial2D("KnightNoFriction");
        mat.friction = 0f;
        mat.bounciness = 0f;
        col.sharedMaterial = mat;
    }

    void Start()
    {
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

    // =====================================================================
    //  ГЛАВНЫЙ ЦИКЛ ФИЗИКИ
    // =====================================================================
    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        grounded = CheckGrounded();
        if (grounded)
        {
            coyoteTimer = coyoteTime;
            if (!isClimbing) groundedFeetY = col.bounds.min.y;
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

        // Не даём выбору цели и защите от застревания перебивать выход с лестницы.
        if (ladderExitTimer > 0f)
        {
            ladderExitTimer -= dt;
            rb.gravityScale = normalGravity;

            // Если скелет уже выбран, каждый физический кадр уточняем направление к нему.
            if (targetArcher != null && currentArcherScript != null &&
                !currentArcherScript.IsDead())
            {
                float dxToArcher = targetArcher.position.x - transform.position.x;
                if (Mathf.Abs(dxToArcher) > 0.08f)
                    ladderExitDir = Mathf.Sign(dxToArcher);
                else
                    ladderExitTimer = 0f;
            }

            if (ladderExitTimer > 0f)
            {
                rb.linearVelocity = new Vector2(ladderExitDir * speed, rb.linearVelocity.y);
                lastDesiredVx = ladderExitDir * speed;
                UpdateAnim(lastDesiredVx);
                return;
            }
        }

        rb.gravityScale = normalGravity;

        // отход назад после неудачных попыток пройти
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

    // Что делать рыцарю сейчас. Возвращает желаемую горизонтальную скорость.
    float DecideMovement()
    {
        Transform active = targetArcher != null ? targetArcher : targetDoor;
        if (active == null) return 0f;

        int floorDir = FloorDirTo(FeetY(active));

        // Цель на другом этаже: идём к лестнице и лезем
        if (floorDir != 0)
        {
            if (targetLadder == null) return 0f; // ждём, пока UpdateTargets найдёт лестницу

            Bounds lb = GetBounds(targetLadder);
            float dxL = lb.center.x - transform.position.x;

            if (Mathf.Abs(dxL) > ladderAlignDistance)
                return WalkToward(dxL);

            if (climbCooldownTimer <= 0f && grounded)
                StartClimb(lb, floorDir);
            return 0f;
        }

        // Скелет на нашем этаже: идём и бьём
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

        // Все скелеты побеждены: идём к двери
        float ddx = targetDoor.position.x - transform.position.x;
        if (Mathf.Abs(ddx) <= 0.5f) return 0f;
        return WalkToward(ddx);
    }

    float WalkToward(float dx)
    {
        float dir = Mathf.Sign(dx);
        AutoJump(dir);
        return dir * speed;
    }

    void ApplyMove(float vx)
    {
        lastDesiredVx = vx;
        rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
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
    if (coyoteTimer <= 0f ||
        jumpTimer > 0f ||
        isClimbing ||
        NearTargetLadder())
        return;

    float h;

    if (ObstacleAhead(dir, out h))
    {
        if (h <= MaxStepHeight() && HasHeadroom(h))
            Jump(JumpVelocityFor(h));

        return;
    }

    if (jumpOverGaps && GapAhead(dir))
        Jump(jumpForce);
}

bool HasHeadroom(float obstacleHeight)
{
    Bounds b = col.bounds;

    float neededHeight = Mathf.Max(0.3f, obstacleHeight) + 0.2f;

    Vector2 origin = new Vector2(
        b.center.x,
        b.min.y + neededHeight);

    RaycastHit2D hit = CastSolid(
        origin, Vector2.up, 0.6f);

    return hit.collider == null;
}


    // Проверяем, хватит ли места над рыцарем для прыжка.
    // Это не даёт ему бесконечно прыгать в потолок, застревая у бочки.
    bool HasJumpHeadroom(float rise)
    {
        Bounds b = col.bounds;
        float safeRise = Mathf.Max(0.15f, rise);
        Vector2 checkCenter = new Vector2(b.center.x, b.center.y + safeRise);
        Vector2 checkSize = new Vector2(Mathf.Max(0.05f, b.size.x * 0.85f),
                                        Mathf.Max(0.05f, b.size.y * 0.85f));
        Collider2D[] hits = Physics2D.OverlapBoxAll(checkCenter, checkSize, 0f, ObstacleMask);
        for (int i = 0; i < hits.Length; i++)
        {
            if (IsSolid(hits[i])) return false;
        }
        return true;
    }

    void Jump(float velocity)
    {
        if (jumpTimer > 0f || !HasJumpHeadroom((velocity * velocity) / (2f * Gravity()))) return;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, velocity);
        jumpTimer = jumpCooldown;
        coyoteTimer = 0f;
        grounded = false;
    }

    float Gravity()
    {
        return Mathf.Abs(Physics2D.gravity.y * normalGravity);
    }

    float MaxStepHeight()
    {
        return Mathf.Max(0.5f, (maxJumpForce * maxJumpForce) / (2f * Gravity()) - jumpClearance);
    }

    float JumpVelocityFor(float stepHeight)
    {
        float v = Mathf.Sqrt(2f * Gravity() * (stepHeight + jumpClearance));
        return Mathf.Clamp(v, jumpForce * 0.7f, maxJumpForce);
    }

    // Есть ли перед нами преграда, и какой она высоты.
    // Высоту меряем несколькими горизонтальными лучами: так не мешают потолки и платформы сверху.
    bool ObstacleAhead(float dir, out float height)
    {
        height = 0f;
        Bounds b = col.bounds;
        float edgeX = b.center.x + dir * (b.extents.x + 0.02f);
        Vector2 rayDir = new Vector2(dir, 0f);

        // нижний луч: мелкие неровности земли (до 0.2) игнорируем
        float lowH = 0.2f;
        if (CastSolid(new Vector2(edgeX, b.min.y + lowH), rayDir, obstacleCheckDistance).collider == null)
            return false;

        float maxH = MaxStepHeight();
        for (float h = lowH + stepProbe; h <= maxH + stepProbe; h += stepProbe)
        {
            if (CastSolid(new Vector2(edgeX, b.min.y + h), rayDir, obstacleCheckDistance).collider == null)
            {
                height = h; // верх преграды где-то между предыдущим и этим лучом
                return true;
            }
        }

        height = Mathf.Infinity; // выше, чем можем перепрыгнуть
        return true;
    }

    // Яма впереди, за которой снова есть земля
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
    // Сначала проверяем лестницу, выбранную целью.
    if (targetLadder != null)
    {
        Bounds b = GetBounds(targetLadder);

        if (Mathf.Abs(b.center.x - transform.position.x) < 1.5f
            && col.bounds.max.y >= b.min.y - 0.5f
            && col.bounds.min.y <= b.max.y + 0.5f)
        {
            return true;
        }
    }

    // Проверяем и другие лестницы поблизости,
    // даже если бот ещё не выбрал целевую лестницу.
    GameObject[] ladders = GameObject.FindGameObjectsWithTag("Ladder");

    foreach (GameObject ladder in ladders)
    {
        if (ladder == null) continue;

        Bounds b = GetBounds(ladder.transform);

        if (Mathf.Abs(b.center.x - transform.position.x) < 1.0f
            && col.bounds.max.y >= b.min.y - 0.5f
            && col.bounds.min.y <= b.max.y + 0.5f)
        {
            return true;
        }
    }

    return false;
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

    // Луч, который игнорирует самого рыцаря и триггеры (лестницы, зоны)
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
    //  ЗАЩИТА ОТ ЗАСТРЕВАНИЯ
    // =====================================================================
    void CheckStuck(float dt)
    {
    
    
    if (isClimbing || NearTargetLadder())
    {
        stuckTimer = 0f;
        movingTimer = 0f;
        return;
    }
    
        bool trying = Mathf.Abs(lastDesiredVx) > 0.1f;
        float actualVx = Mathf.Abs(rb.linearVelocity.x);

        if (trying && grounded && actualVx < 0.15f) stuckTimer += dt;
        else stuckTimer = 0f;

        // если нормально идём больше секунды, сбрасываем счётчик попыток
        if (trying && actualVx > 0.5f)
        {
            movingTimer += dt;
            if (movingTimer > 1f) { jumpRetries = 0; movingTimer = 0f; }
        }
        else
        {
            movingTimer = 0f;
        }

        if (stuckTimer < stuckTime) return;
        stuckTimer = 0f;

        // Если над головой мало места, прыжки только прижимают рыцаря к потолку.
        // В таком случае сразу отходим от препятствия и пробуем другой подход.
        if (!HasJumpHeadroom((jumpForce * jumpForce) / (2f * Gravity())))
        {
            backoffDir = Mathf.Sign(lastDesiredVx);
            backoffTimer = backoffTime;
            jumpRetries = 0;
            return;
        }

        if (jumpRetries < maxJumpRetries)
        {
            float t = (jumpRetries + 1f) / Mathf.Max(1, maxJumpRetries);
            jumpTimer = 0f;
            float retryVelocity = Mathf.Lerp(jumpForce, maxJumpForce, t);
            if (HasJumpHeadroom((retryVelocity * retryVelocity) / (2f * Gravity())))
                Jump(retryVelocity);
            else
            {
                backoffDir = Mathf.Sign(lastDesiredVx);
                backoffTimer = backoffTime;
                jumpRetries = 0;
                return;
            }
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

        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        if (passThroughOnLadder) col.isTrigger = true;
    }

    void ClimbStep(float dt)
    {
        climbTimer += dt;
        rb.gravityScale = 0f;

        Bounds b = col.bounds;
        float alignX = Mathf.Clamp((climbBounds.center.x - transform.position.x) * 8f, -2f, 2f);
        rb.linearVelocity = new Vector2(alignX, climbSpeed * climbDir);

        bool done = false;
        if (climbDir > 0 && b.min.y >= climbBounds.max.y - 0.1f) done = true;
        if (climbDir < 0 && b.min.y <= climbBounds.min.y + 0.12f) done = true;
        // Если уже дошли до платформы, вернём обычный коллайдер и завершим спуск.
        if (climbDir < 0 && climbTimer > 0.35f)
        {
            float feet = col.bounds.min.y;
            RaycastHit2D floorHit = Physics2D.Raycast(
                new Vector2(col.bounds.center.x, feet + 0.12f), Vector2.down, 0.3f, ObstacleMask);
            if (floorHit.collider != null && IsSolid(floorHit.collider)) done = true;
        }
        if (climbTimer > climbTimeout) done = true; // страховка

        if (done) FinishClimb();
    }

    

    bool IsExitPositionClear(Vector2 candidatePosition)
    {
        Bounds b = col.bounds;
        Vector2 offset = candidatePosition - rb.position;
        Vector2 center = (Vector2)b.center + offset;
        Vector2 size = new Vector2(
            Mathf.Max(0.05f, b.size.x * 0.9f),
            Mathf.Max(0.05f, b.size.y * 0.9f));

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, ObstacleMask);
        for (int i = 0; i < hits.Length; i++)
        {
            if (IsSolid(hits[i]))
                return false;
        }

        return true;
    }

void FinishClimb()
{
    Bounds b = col.bounds;

    Transform active = targetArcher != null ? targetArcher : targetDoor;
    float preferredDir = 1f;

    if (active != null && Mathf.Abs(active.position.x - transform.position.x) > 0.1f)
        preferredDir = Mathf.Sign(active.position.x - transform.position.x);
    else if (spriteRenderer != null)
        preferredDir = spriteRenderer.flipX ? -1f : 1f;

    // Ставим ноги на край платформы, затем ищем свободное место сбоку от лестницы.
    float targetFeetY = climbDir > 0
        ? climbBounds.max.y + 0.02f
        : climbBounds.min.y + 0.02f;

    float targetY = rb.position.y + (targetFeetY - b.min.y);
    float sideOffset = climbBounds.extents.x + b.extents.x + 0.18f;
    Vector2 exitPosition = new Vector2(
        climbBounds.center.x + preferredDir * sideOffset,
        targetY);

    // Если выбранная сторона заблокирована, пробуем противоположную сторону.
    if (!IsExitPositionClear(exitPosition))
    {
        Vector2 otherSide = new Vector2(
            climbBounds.center.x - preferredDir * sideOffset,
            targetY);

        if (IsExitPositionClear(otherSide))
        {
            exitPosition = otherSide;
            preferredDir = -preferredDir;
        }
        else
        {
            // Если обе стороны тесные, отходим дальше в сторону цели.
            for (float extra = 0.25f; extra <= 1.0f; extra += 0.25f)
            {
                Vector2 farther = new Vector2(
                    climbBounds.center.x + preferredDir * (sideOffset + extra),
                    targetY);

                if (IsExitPositionClear(farther))
                {
                    exitPosition = farther;
                    break;
                }
            }
        }
    }

    // Rigidbody2D.position надёжнее transform.position для динамического тела.
    rb.position = exitPosition;
    Physics2D.SyncTransforms();

    col.isTrigger = false;
    isClimbing = false;
    rb.gravityScale = normalGravity;
    rb.linearVelocity = new Vector2(preferredDir * speed, 0f);

    groundedFeetY = col.bounds.min.y;
    ladderExitDir = preferredDir;
    ladderExitTimer = 0.65f;
    jumpTimer = Mathf.Max(jumpTimer, jumpCooldown);
    climbCooldownTimer = 1.0f;

    targetLadder = null;
    ladderLockTarget = null;
    retargetTimer = retargetInterval;
    stuckTimer = 0f;
    movingTimer = 0f;
    jumpRetries = 0;

    // Сразу обновляем цель после выхода с лестницы: теперь скелет может
    // оказаться на том же этаже, и рыцарь должен немедленно идти к нему.
    UpdateTargets();

    if (targetArcher != null && currentArcherScript != null &&
        !currentArcherScript.IsDead())
    {
        float dxToArcher = targetArcher.position.x - transform.position.x;
        if (Mathf.Abs(dxToArcher) > 0.08f)
        {
            ladderExitDir = Mathf.Sign(dxToArcher);
            rb.linearVelocity = new Vector2(ladderExitDir * speed, rb.linearVelocity.y);
            lastDesiredVx = ladderExitDir * speed;
        }
        else
        {
            // Если рыцарь уже рядом по горизонтали, не уходим от цели.
            ladderExitTimer = 0f;
        }
    }
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

    // 0 = тот же этаж, +1 = цель выше, -1 = цель ниже
    int FloorDirTo(float targetFeetY)
    {
        float diff = targetFeetY - groundedFeetY;
        if (Mathf.Abs(diff) < floorTolerance) return 0;
        return diff > 0f ? 1 : -1;
    }

    bool LadderUsable(Bounds lb, int dir)
    {
        float f = groundedFeetY;
        // Лестница должна пересекать уровень ног рыцаря и продолжаться в нужную сторону.
        if (dir > 0) return lb.min.y <= f + 0.45f && lb.max.y >= f + 0.5f;
        return lb.max.y >= f - 0.45f && lb.min.y <= f - 0.5f;
    }

    Transform FindBestLadder(float targetFeetY)
    {
        int dir = FloorDirTo(targetFeetY);
        if (dir == 0) return null;

        GameObject[] ladders = GameObject.FindGameObjectsWithTag("Ladder");
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

    // Не переключаем лестницу туда-сюда, пока цель та же
    void ApplyLadder(Transform target, Transform candidate)
    {
        if (targetLadder != null && ladderLockTarget == target &&
            LadderUsable(GetBounds(targetLadder), FloorDirTo(FeetY(target))))
        {
            return;
        }
        targetLadder = candidate;
        ladderLockTarget = target;
    }

    bool CanSee(Transform t)
    {
        // Не полагаемся на wallOnlyLayer: в сцене он может быть пустым.
        // Проверяем все твёрдые коллайдеры по пути, игнорируя триггеры и самих скелетов.
        Vector2 from = col.bounds.center;
        Vector2 to = GetBounds(t).center;
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.01f) return true;

        RaycastHit2D[] hits = Physics2D.RaycastAll(from, delta / distance, distance, ObstacleMask);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i].collider;
            if (!IsSolid(hit)) continue;
            if (hit.transform == t || hit.GetComponentInParent<ArcherBot>() != null) return true;
            // Первый твёрдый объект до цели перекрывает обзор.
            return false;
        }
        return true;
    }

    void SetArcher(Transform archer, ArcherBot script)
    {
        targetArcher = archer;
        currentArcherScript = script;
        targetDoor = null;
    }

    void UpdateTargets()
    {
        if (isClimbing) return;

        // Текущий скелет жив и на нашем этаже: продолжаем
        if (targetArcher != null && currentArcherScript != null &&
            !currentArcherScript.IsDead() && SameFloor(FeetY(targetArcher)))
        {
            return;
        }

        Transform visible = null; ArcherBot visibleScript = null; float visibleD = Mathf.Infinity;
        Transform blocked = null; ArcherBot blockedScript = null; float blockedD = Mathf.Infinity;
        Transform other = null; ArcherBot otherScript = null; Transform otherLadder = null; float otherD = Mathf.Infinity;

        GameObject[] archers = GameObject.FindGameObjectsWithTag("Archer");
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

        // 1) скелет на нашем этаже, до которого видно
        if (visible != null)
        {
            SetArcher(visible, visibleScript);
            targetLadder = null; ladderLockTarget = null;
        }
        // 2) скелет на другом этаже, к которому есть лестница
        else if (other != null)
        {
            SetArcher(other, otherScript);
            ApplyLadder(other, otherLadder);
        }
        // 3) скелет на нашем этаже, но за стеной: идём к нему сами
        else if (blocked != null)
        {
            SetArcher(blocked, blockedScript);
            targetLadder = null; ladderLockTarget = null;
        }
        // 4) все побеждены: к двери
        else
        {
            targetArcher = null;
            currentArcherScript = null;

            if (targetDoor == null)
            {
                GameObject doorObj = GameObject.FindWithTag("Door");
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
        {
            Instantiate(trashPrefab, transform.position, Quaternion.identity);
        }
    }

    public void TakeDamage(int damage)
    {
        if (!isBoss) return;

        bossHP -= damage;
        if (bossHP <= 0)
        {
            if (trashPrefab != null)
            {
                Instantiate(trashPrefab, transform.position, Quaternion.identity);
            }
            Destroy(gameObject);
        }
    }

    // =====================================================================
    //  ОТЛАДКА (в окне Scene, когда рыцарь выделен)
    // =====================================================================
    void OnDrawGizmosSelected()
    {
        // Рисуем линию зрения только у выбранного рыцаря.
        // Линия идёт к скелету, которого рыцарь сейчас выбрал целью.
        if (targetArcher != null)
        {
            Gizmos.color = Color.red;
            Vector3 from = transform.position + Vector3.up * 0.2f;
            Vector3 to = targetArcher.position + Vector3.up * 0.2f;
            Gizmos.DrawLine(from, to);
        }

        // Проверка земли остаётся только как отладочная рамка рыцаря.
        Collider2D c = col != null ? col : GetComponent<Collider2D>();
        if (c == null) return;

        Bounds b = c.bounds;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(
            new Vector3(b.center.x, b.min.y - 0.03f, 0f),
            new Vector3(b.size.x * 0.8f, 0.1f, 0f));
    }
}