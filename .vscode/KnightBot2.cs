using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class KnightBot2 : MonoBehaviour
{
    [Header("Движение и Прыжки")]
    public float speed = 2.5f;
    public float climbSpeed = 3f;
    public float jumpForce = 7f;             // обычный прыжок
    public float maxJumpForce = 9f;          // самый сильный прыжок (для высоких уступов)
    public float jumpMargin = 0.1f;          // запас высоты над уступом
    public float obstacleCheckDistance = 0.5f; // на каком расстоянии от края тела видим преграду
    public float jumpCooldown = 0.3f;
    public bool jumpOverGaps = true;         // перепрыгивать ямы
    public float maxGapWidth = 2.5f;
    public LayerMask groundLayer;            // больше не обязателен: преградой считается любой твёрдый коллайдер
    public LayerMask wallOnlyLayer;          // стены для проверки «видит ли скелета»
    public LayerMask ignoreLayers;           // слои, которые НЕ считаем преградой (стрелы, мусор...)

    [Header("Защита от застревания")]
    public float stuckTime = 0.5f;           // сколько стоим «в упор», прежде чем действовать
    public int maxJumpRetries = 3;           // сколько раз пробуем прыгнуть сильнее
    public float backoffTime = 0.8f;         // потом отходим назад на это время

    [Header("Этажи и цели")]
    public float floorTolerance = 1.5f;      // меньше, чем расстояние между этажами
    public float retargetInterval = 0.25f;

    [Header("Лестницы")]
    public float ladderAlignDistance = 0.2f;
    public float climbTimeout = 8f;
    public bool passThroughOnLadder = true;  // на лестнице проходим сквозь платформы
    public float exitStepDistance = 1f;      // на сколько шагнуть в сторону после подъёма (1 блок)
    public int ladderExitDirection = -1;     // куда сойти с лестницы наверху: -1 = влево, +1 = вправо
    public float exitTimeout = 2f;           // страховка: максимум столько секунд на этот шаг

    [Header("Отладка")]
    public bool debugLog = false;            // писать в Console, что делает рыцарь

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
    float climbStartFeetY;

    // сход с лестницы наверху
    bool exitingLadder;
    float exitTargetX;
    float exitTimer;
    float lastLogTime;
    const float scanStep = 0.25f;   // грубый шаг измерения высоты
    const float fineStep = 0.05f;   // точный шаг

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

        // наверху лестницы: сначала шагаем в сторону на один блок
        if (exitingLadder)
        {
            LadderExitStep(dt);
            return;
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

            // Вверх начинаем с земли; вниз можно и «на краю» проёма
            bool canStart = floorDir > 0 ? grounded : (col.bounds.min.y > lb.min.y + 1.0f);
            if (climbCooldownTimer <= 0f && canStart)
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
            return; // не пролезть: сработает защита от застревания
        }

        if (jumpOverGaps && GapAhead(dir) && !NearTargetLadder())
            Jump(jumpForce);
    }

    void Jump(float velocity)
    {
        if (jumpTimer > 0f) return;
        velocity = ClampToHeadroom(velocity); // не бьёмся головой о потолок
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
        return Mathf.Max(0.5f, (maxJumpForce * maxJumpForce) / (2f * Gravity()) - jumpMargin);
    }

    // Y нижней грани потолка над точкой (или бесконечность, если потолка нет)
    float CeilingY(float x, float fromY)
    {
        RaycastHit2D hit = CastSolid(new Vector2(x, fromY), Vector2.up, 8f);
        return hit.collider != null ? hit.point.y : Mathf.Infinity;
    }

    // Ограничиваем скорость прыжка так, чтобы голова не ударилась о потолок
    float ClampToHeadroom(float velocity)
    {
        Bounds b = col.bounds;
        float ceil = CeilingY(b.center.x, b.max.y + 0.01f);
        if (float.IsInfinity(ceil)) return velocity;
        float riseMax = Mathf.Max(0f, ceil - b.max.y - 0.03f);
        return Mathf.Min(velocity, Mathf.Sqrt(2f * Gravity() * riseMax));
    }

    // Можно ли перепрыгнуть уступ высотой stepHeight, учитывая потолок?
    bool CanClear(float dir, float frontX, float stepHeight, out float velocity)
    {
        velocity = 0f;
        if (float.IsInfinity(stepHeight)) { LogThrottled("преграда слишком высокая"); return false; }

        Bounds b = col.bounds;
        float tall = b.size.y;

        float ceilHere = CeilingY(b.center.x, b.max.y + 0.01f);                       // над нами
        float ceilLand = CeilingY(frontX + dir * 0.3f, b.min.y + stepHeight + tall);  // над местом приземления
        float ceil = Mathf.Min(ceilHere, ceilLand);

        float riseMax = ceil - b.max.y - 0.03f;                                  // сколько можно подняться
        float riseCap = (maxJumpForce * maxJumpForce) / (2f * Gravity());         // на сколько способен прыгнуть
        float rise = Mathf.Min(stepHeight + jumpMargin, Mathf.Min(riseMax, riseCap));

        if (rise < stepHeight + 0.03f)
        {
            LogThrottled("не пролезть: уступ " + stepHeight.ToString("0.00") + ", запас до потолка " + riseMax.ToString("0.00"));
            return false;
        }

        velocity = Mathf.Sqrt(2f * Gravity() * rise);
        return true;
    }

    // Есть ли перед нами преграда, и какой она высоты.
    // Высоту меряем горизонтальными лучами: так не мешают потолки и платформы сверху.
    bool ObstacleAhead(float dir, out float height, out float frontX)
    {
        height = 0f;
        frontX = 0f;
        Bounds b = col.bounds;
        float edgeX = b.center.x + dir * (b.extents.x + 0.02f);
        Vector2 rayDir = new Vector2(dir, 0f);

        // нижний луч: мелкие неровности земли (до 0.2) игнорируем
        float lowH = 0.2f;
        RaycastHit2D low = CastSolid(new Vector2(edgeX, b.min.y + lowH), rayDir, obstacleCheckDistance);
        if (low.collider == null) return false;
        frontX = low.point.x;

        float limit = MaxStepHeight();
        for (float h = lowH + scanStep; h <= limit + scanStep; h += scanStep)
        {
            if (CastSolid(new Vector2(edgeX, b.min.y + h), rayDir, obstacleCheckDistance).collider == null)
            {
                // верх преграды между (h - scanStep) и h: уточняем мелким шагом
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
        if (targetLadder == null) return false;
        return Mathf.Abs(GetBounds(targetLadder).center.x - transform.position.x) < 1.5f;
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

        if (jumpRetries < maxJumpRetries)
        {
            // пробуем прыгнуть всё сильнее
            float t = (jumpRetries + 1f) / Mathf.Max(1, maxJumpRetries);
            jumpTimer = 0f;
            Jump(Mathf.Lerp(jumpForce, maxJumpForce, t));
            jumpRetries++;
        }
        else
        {
            // не помогло: отходим назад и пробуем снова
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
        Log("начал " + (dir > 0 ? "подъём" : "спуск") + " по лестнице");

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
        if (climbDir > 0)
        {
            if (b.min.y >= climbBounds.max.y - 0.1f) done = true;
        }
        else
        {
            float descended = climbStartFeetY - b.min.y;
            if (b.min.y <= climbBounds.min.y + 0.05f) done = true;        // дошли до низа лестницы
            else if (descended > 0.8f && grounded) done = true;           // коснулись пола нижнего этажа
        }
        if (climbTimer > climbTimeout) done = true; // страховка

        if (done) FinishClimb();
    }

    void FinishClimb()
    {
        float newFloorY = climbDir > 0 ? climbBounds.max.y : climbBounds.min.y;

        // После спуска ставим рыцаря ровно на пол, чтобы он не оказался внутри плитки
        if (climbDir < 0)
        {
            Bounds b = col.bounds;
            RaycastHit2D g = CastSolid(new Vector2(b.center.x, b.min.y + 1.0f), Vector2.down, 1.6f);
            if (g.collider != null && g.distance > 0.001f)
            {
                float dy = g.point.y - b.min.y + 0.01f;
                rb.position = new Vector2(rb.position.x, rb.position.y + dy);
                newFloorY = g.point.y;
            }
        }

        isClimbing = false;
        col.isTrigger = false;
        rb.gravityScale = normalGravity;

        groundedFeetY = newFloorY; // сразу считаем, что мы на новом этаже
        Log("закончил " + (climbDir > 0 ? "подъём" : "спуск") + ", этаж y=" + newFloorY.ToString("0.00"));

        jumpTimer = jumpCooldown;
        climbCooldownTimer = 0.6f;
        targetLadder = null;
        ladderLockTarget = null;
        retargetTimer = 0f; // сразу пересчитать цели

        if (climbDir > 0)
        {
            // Наверху: не прыгаем, а сходим с лестницы на один блок в сторону
            BeginLadderExit();
            return;
        }

        Transform active = targetArcher != null ? targetArcher : targetDoor;
        float exitDir = 1f;
        if (active != null && Mathf.Abs(active.position.x - transform.position.x) > 0.1f)
            exitDir = Mathf.Sign(active.position.x - transform.position.x);
        else if (spriteRenderer != null)
            exitDir = spriteRenderer.flipX ? -1f : 1f;

        rb.linearVelocity = new Vector2(exitDir * speed, 0f);
    }

    // =====================================================================
    //  СХОД С ЛЕСТНИЦЫ НАВЕРХУ: шаг на один блок в сторону
    // =====================================================================
    void BeginLadderExit()
    {
        float dir = ladderExitDirection >= 0 ? 1f : -1f;
        Bounds b = col.bounds;

        // Ставим рыцаря ровно на пол рядом с лестницей (пол ищем со стороны схода)
        float sideX = dir < 0f ? climbBounds.min.x - 0.15f : climbBounds.max.x + 0.15f;
        float floorY = climbBounds.max.y;
        RaycastHit2D g = CastSolid(new Vector2(sideX, climbBounds.max.y + 0.6f), Vector2.down, 1.6f);
        if (g.collider != null) floorY = g.point.y;

        rb.position = new Vector2(rb.position.x, rb.position.y + (floorY - b.min.y) + 0.01f);
        groundedFeetY = floorY;

        // Целевая точка: на exitStepDistance в сторону, но не ближе, чем нужно,
        // чтобы полностью выйти за край лестницы (иначе рыцарь свалится обратно в проём)
        float minShift = (climbBounds.extents.x + b.extents.x + 0.05f);
        float shift = Mathf.Max(exitStepDistance, minShift - Mathf.Abs(transform.position.x - climbBounds.center.x));
        exitTargetX = transform.position.x + dir * shift;

        exitingLadder = true;
        exitTimer = 0f;

        // на время шага: без гравитации и сквозь коллайдеры, чтобы не цепляться за край проёма
        col.isTrigger = true;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        Log("схожу с лестницы на " + shift.ToString("0.00") + " в сторону " + (dir < 0f ? "влево" : "вправо"));
    }

    void LadderExitStep(float dt)
    {
        exitTimer += dt;
        rb.gravityScale = 0f;

        float dir = ladderExitDirection >= 0 ? 1f : -1f;
        Bounds b = col.bounds;

        float remaining = (exitTargetX - transform.position.x) * dir; // сколько ещё идти
        bool wallAhead = CastSolid(
            new Vector2(b.center.x + dir * (b.extents.x + 0.02f), b.center.y),
            new Vector2(dir, 0f), 0.1f).collider != null;

        if (remaining <= 0.02f || wallAhead || exitTimer > exitTimeout)
        {
            EndLadderExit();
            return;
        }

        float vx = dir * Mathf.Min(speed, remaining / dt);
        rb.linearVelocity = new Vector2(vx, 0f);
        UpdateAnim(vx);
    }

    void EndLadderExit()
    {
        exitingLadder = false;
        col.isTrigger = false;
        rb.gravityScale = normalGravity;
        rb.linearVelocity = Vector2.zero;

        // сбрасываем всё, что могло накопиться и вызвать «прыжки на месте»
        stuckTimer = 0f;
        jumpRetries = 0;
        backoffTimer = 0f;
        movingTimer = 0f;
        lastDesiredVx = 0f;
        jumpTimer = jumpCooldown;
        coyoteTimer = coyoteTime;
        climbCooldownTimer = 0.6f;
        retargetTimer = 0f;
        Log("сошёл с лестницы, иду дальше");
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
        if (dir > 0) return lb.min.y <= f + 0.6f && lb.max.y >= f + 1.0f;
        return lb.max.y >= f - 0.6f && lb.min.y <= f - 1.0f;
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
        if (targetArcher != archer) Log("цель: " + archer.name);
        targetArcher = archer;
        currentArcherScript = script;
        targetDoor = null;
    }

    void UpdateTargets()
    {
        if (isClimbing || exitingLadder) return;

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
        Collider2D c = col != null ? col : GetComponent<Collider2D>();
        if (c == null) return;

        Bounds b = c.bounds;

        Gizmos.color = Color.green; // проверка земли
        Gizmos.DrawWireCube(new Vector3(b.center.x, b.min.y - 0.03f, 0f), new Vector3(b.size.x * 0.8f, 0.1f, 0f));

        Gizmos.color = Color.red;   // проверка преград вправо и влево
        Vector3 r = new Vector3(b.max.x + 0.02f, b.min.y + 0.2f, 0f);
        Vector3 l = new Vector3(b.min.x - 0.02f, b.min.y + 0.2f, 0f);
        Gizmos.DrawLine(r, r + Vector3.right * obstacleCheckDistance);
        Gizmos.DrawLine(l, l + Vector3.left * obstacleCheckDistance);
    }
}