using UnityEngine;

public class KnightBot : MonoBehaviour
{
    [Header("Движение и Прыжки")]
    public float speed = 2.5f; 
    public float climbSpeed = 3f;                     
    public float jumpForce = 7f;                      
    public float obstacleCheckDistance = 0.8f;   
    public LayerMask groundLayer;                  
    public LayerMask wallOnlyLayer;              

    [Header("Префаб Мусора")]
    public GameObject trashPrefab; 

    [Header("Боссфайт")]
    public bool isBoss = false; 
    public int bossHP = 100;

    [Header("Бой со Скелетом")]
    public float attackRange = 1.5f;     
    public int knightDamage = 25;              
    public float attackCooldown = 1.5f;  
    private float attackTimer = 0f;

    [Header("Лестницы")]
    private bool isTouchingLadder = false;         
    private bool isClimbing = false;             
    private Transform currentLadder;             
    private int climbDirection = 1;              

    // Цели по вашей логике
    private Transform targetArcher;  
    private ArcherBot currentArcherScript;
    private Transform targetDoor;              
    private Transform targetLadder; // Промежуточная лестница для перехода на другой этаж

    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer spriteRenderer;
    private bool isGrounded = true;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        if (!isBoss)
        {
            UpdateTargets();
        }
    }

    void Update()
    {
        attackTimer += Time.deltaTime;
        
        if (!isBoss)
        {
            UpdateTargets();
        }
    }

    // НОВАЯ ЛОГИКА ПОИСКА ЦЕЛЕЙ И ПУТЕЙ
    void UpdateTargets()
    {
        // TARGET LOCK: Если текущий скелет жив и находится на нашем этаже — не сбрасываем его, дорабатываем цель
        if (targetArcher != null && currentArcherScript != null && !currentArcherScript.IsDead())
        {
            float distY = Mathf.Abs(targetArcher.position.y - transform.position.y);
            if (distY < 0.6f)
            {
                return; // Продолжаем зачищать текущий этаж
            }
        }

        GameObject[] archers = GameObject.FindGameObjectsWithTag("Archer");
        
        Transform nearestSameFloor = null;
        ArcherBot sameFloorScript = null;
        float minSameFloorDist = Mathf.Infinity;

        Transform nearestAnyFloor = null;
        float minAnyFloorDist = Mathf.Infinity;

        foreach (GameObject archerObj in archers)
        {
            Transform archer = archerObj.transform;
            ArcherBot script = archer.GetComponent<ArcherBot>();

            if (script != null && !script.IsDead())
            {
                float dy = Mathf.Abs(archer.position.y - transform.position.y);
                float dist = Vector2.Distance(transform.position, archer.position);

                // 1. Проверяем скелетов строго на своем этаже (не видим сквозь стены)
                if (dy < 0.6f)
                {
                    Vector2 dir = (archer.position - transform.position).normalized;
                    RaycastHit2D wallHit = Physics2D.Raycast(transform.position, dir, dist, wallOnlyLayer);

                    if (wallHit.collider == null || wallHit.transform == archer)
                    {
                        if (dist < minSameFloorDist)
                        {
                            minSameFloorDist = dist;
                            nearestSameFloor = archer;
                            sameFloorScript = script;
                        }
                    }
                }

                // 2. Ищем вообще любые живые скелеты на уровне (для поиска лестницы)
                if (dist < minAnyFloorDist)
                {
                    minAnyFloorDist = dist;
                    nearestAnyFloor = archer;
                }
            }
        }

        // ПРИОРИТЕТ 1: Полностью зачищаем текущий этаж
        if (nearestSameFloor != null)
        {
            targetArcher = nearestSameFloor;
            currentArcherScript = sameFloorScript;
            targetLadder = null;
            targetDoor = null;
        }
        // ПРИОРИТЕТ 2: На текущем этаже скелетов нет — ищем лестницу к оставшимся скелетам на других этажах
        else if (nearestAnyFloor != null)
        {
            targetArcher = nearestAnyFloor;
            currentArcherScript = nearestAnyFloor.GetComponent<ArcherBot>();
            targetDoor = null;
            targetLadder = FindBestLadderForTarget(nearestAnyFloor);
        }
        // ПРИОРИТЕТ 3: Все скелеты на уровне уничтожены — направляемся к двери (выходу)
        else
        {
            targetArcher = null;
            currentArcherScript = null;
            targetLadder = null;

            GameObject doorObj = GameObject.FindWithTag("Door");
            if (doorObj != null)
            {
                targetDoor = doorObj.transform;
                // Если дверь на другом этаже, ищем лестницу к ней
                if (Mathf.Abs(targetDoor.position.y - transform.position.y) > 0.6f)
                {
                    targetLadder = FindBestLadderForTarget(targetDoor);
                }
            }
        }
    }

    // Выбор оптимальной лестницы, ведущей к этажу цели
    Transform FindBestLadderForTarget(Transform targetObj)
    {
        GameObject[] ladders = GameObject.FindGameObjectsWithTag("Ladder");
        if (ladders.Length == 0) return null;

        Transform bestLadder = null;
        float minScore = Mathf.Infinity;
        float targetY = targetObj.position.y;

        foreach (GameObject ladderObj in ladders)
        {
            Transform ladder = ladderObj.transform;
            float distToKnightX = Mathf.Abs(ladder.position.x - transform.position.x);
            
            // Проверяем, находится ли лестница между нами и целевым этажом
            float ladderY = ladder.position.y;
            bool isBetweenY = (ladderY >= Mathf.Min(transform.position.y, targetY) - 1f) && 
                              (ladderY <= Mathf.Max(transform.position.y, targetY) + 1f);

            float score = distToKnightX;
            if (!isBetweenY)
            {
                score += 50f; // Штраф для лестниц, которые ведут не в ту сторону
            }

            if (score < minScore)
            {
                minScore = score;
                bestLadder = ladder;
            }
        }

        return bestLadder;
    }

    void FixedUpdate()
    {
        float currentSpeedX = 0f;
        float currentSpeedY = rb.linearVelocity.y;

        if (isBoss)
        {
            rb.gravityScale = 1f;
            currentSpeedX = Mathf.Sin(Time.time) * speed;
            rb.linearVelocity = new Vector2(currentSpeedX, currentSpeedY);
            CheckAndJump(Mathf.Sign(currentSpeedX));
            return;
        }

        // 1. Логика лестниц (Автоматический вход при касании, если цель выше или ниже)
        if (isTouchingLadder && !isClimbing)
        {
            Transform currentTarget = targetArcher != null ? targetArcher : targetDoor;
            if (currentTarget != null)
            {
                if (currentTarget.position.y < transform.position.y - 0.5f)
                {
                    climbDirection = -1; // Спускаемся вниз
                    isClimbing = true;
                }
                else if (currentTarget.position.y > transform.position.y + 0.5f)
                {
                    climbDirection = 1;  // Поднимаемся вверх
                    isClimbing = true;
                }
            }
        }

        if (isClimbing)
        {
            rb.gravityScale = 0f;
            
            if (climbDirection == 1 && currentLadder != null && transform.position.y > currentLadder.position.y + 0.8f)
            {
                isClimbing = false;
                isTouchingLadder = false;
                rb.gravityScale = 1f;
                rb.linearVelocity = new Vector2(speed, jumpForce * 0.5f);
            }
            else if (climbDirection == -1 && currentLadder != null && transform.position.y < currentLadder.position.y - 0.8f)
            {
                isClimbing = false;
                isTouchingLadder = false;
                rb.gravityScale = 1f;
                rb.linearVelocity = new Vector2(speed, -2f); 
            }
            else
            {
                float ladderX = currentLadder != null ? currentLadder.position.x : transform.position.x;
                float alignX = (ladderX - transform.position.x) * 5f; 
                
                rb.linearVelocity = new Vector2(alignX, climbSpeed * climbDirection);
                currentSpeedX = 0f;
            }
        }
        // 2. Движение к скелету на текущем этаже
        else if (targetArcher != null && Mathf.Abs(targetArcher.position.y - transform.position.y) < 0.6f)
        {
            rb.gravityScale = 1f;
            float distanceX = targetArcher.position.x - transform.position.x;
            float absDistX = Mathf.Abs(distanceX);

            if (absDistX <= attackRange)
            {
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                currentSpeedX = 0f;

                if (attackTimer >= attackCooldown)
                {
                    AttackArcher();
                    attackTimer = 0f; 
                }
            }
            else
            {
                float direction = Mathf.Sign(distanceX);
                currentSpeedX = direction * speed;
                rb.linearVelocity = new Vector2(currentSpeedX, rb.linearVelocity.y);
                CheckAndJump(direction);
            }
        }
        // 3. Движение к промежуточной лестнице (если цель на другом этаже)
        else if (targetLadder != null && Mathf.Abs(targetLadder.position.y - transform.position.y) > 0.6f)
        {
            rb.gravityScale = 1f;
            float ladderDistX = targetLadder.position.x - transform.position.x;

            if (Mathf.Abs(ladderDistX) <= 0.3f)
            {
                currentSpeedX = 0f;
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            }
            else
            {
                float direction = Mathf.Sign(ladderDistX);
                currentSpeedX = direction * speed;
                rb.linearVelocity = new Vector2(currentSpeedX, rb.linearVelocity.y);
                CheckAndJump(direction);
            }
        }
        // 4. Движение к двери (выходу)
        else if (targetDoor != null)
        {
            rb.gravityScale = 1f;
            float distanceX = targetDoor.position.x - transform.position.x;
            float distanceY = targetDoor.position.y - transform.position.y;

            if (Mathf.Abs(distanceX) <= 0.5f && Mathf.Abs(distanceY) < 0.8f)
            {
                currentSpeedX = 0f;
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            }
            else
            {
                float direction = Mathf.Sign(distanceX);
                currentSpeedX = direction * speed;
                rb.linearVelocity = new Vector2(currentSpeedX, rb.linearVelocity.y);
                CheckAndJump(direction);
            }
        }
        else
        {
            rb.gravityScale = 1f;
            currentSpeedX = 0f;
            rb.linearVelocity = new Vector2(currentSpeedX, rb.linearVelocity.y);
        }

        if (anim != null)
        {
            anim.SetFloat("Speed", Mathf.Abs(currentSpeedX));
        }

        if (currentSpeedX > 0.05f)
        {
            if (spriteRenderer != null) spriteRenderer.flipX = false;
        }
        else if (currentSpeedX < -0.05f)
        {
            if (spriteRenderer != null) spriteRenderer.flipX = true;
        }
    }

    void CheckAndJump(float moveDirection)
    {
        if (moveDirection == 0 || !isGrounded || isClimbing) return;

        Vector2 rayOrigin = new Vector2(transform.position.x, transform.position.y - 0.2f);
        Vector2 rayDirection = new Vector2(moveDirection, 0f);

        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, rayDirection, obstacleCheckDistance, groundLayer);

        if (hit.collider != null)
        {
            Jump();
        }
    }

    void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        isGrounded = false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckGround(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        CheckGround(collision);
    }

    void CheckGround(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & groundLayer) != 0 || collision.gameObject.CompareTag("Ground"))
        {
            foreach (ContactPoint2D contact in collision.contacts)
            {
                if (contact.normal.y > 0.5f)
                {
                    isGrounded = true;
                    break;
                }
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Ladder"))
        {
            isTouchingLadder = true;
            currentLadder = collision.transform;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Ladder"))
        {
            isTouchingLadder = false;
            isClimbing = false;
        }
    }

    void AttackArcher()
    {
        if (anim != null)
        {
            anim.SetTrigger("Attack");
        }

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
}