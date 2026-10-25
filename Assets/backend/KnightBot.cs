using UnityEngine;

public class KnightBot : MonoBehaviour
{
    [Header("Движение и Прыжки")]
    public float speed = 2.5f; 
    public float climbSpeed = 3f;                     
    public float jumpForce = 7f;                      
    public float obstacleCheckDistance = 1.0f;   
    public LayerMask groundLayer;                  
    public LayerMask wallOnlyLayer;              

    [Header("Настройка луча препятствий")]
    [Tooltip("Смещение луча по вертикали относительно позиции рыцаря. Отрицательное значение — ниже центра, положительное — выше.")]
    public float rayHeightOffset = -0.2f; 

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

    // Цели
    private Transform targetArcher;  
    private ArcherBot currentArcherScript;
    private Transform targetDoor;              
    private Transform targetLadder; 

    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer spriteRenderer;
    private bool isGrounded = true;

    // Защита от спама прыжков
    private float lastJumpTime = 0f;
    public float jumpCooldown = 0.6f;

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

    // Умная логика выбора целей: Этаж -> Лестница к другим врагам -> Дверь
    void UpdateTargets()
    {
        // TARGET LOCK: Если текущий скелет жив и на нашем этаже — держимся за него
        if (targetArcher != null && currentArcherScript != null && !currentArcherScript.IsDead())
        {
            if (Mathf.Abs(targetArcher.position.y - transform.position.y) < 0.6f)
            {
                return; 
            }
        }

        GameObject[] archers = GameObject.FindGameObjectsWithTag("Archer");
        
        Transform nearestSameFloor = null;
        ArcherBot nearestSameFloorScript = null;
        float minDistSameFloor = Mathf.Infinity;

        Transform nearestAnyFloor = null;
        float minDistAnyFloor = Mathf.Infinity;

        foreach (GameObject archerObj in archers)
        {
            Transform archer = archerObj.transform;
            ArcherBot script = archer.GetComponent<ArcherBot>();

            if (script != null && !script.IsDead())
            {
                float dy = Mathf.Abs(archer.position.y - transform.position.y);
                float dist = Vector2.Distance(transform.position, archer.position);

                // 1. Скелеты на нашем текущем этаже (без стен между нами)
                if (dy < 0.6f)
                {
                    Vector2 dir = (archer.position - transform.position).normalized;
                    RaycastHit2D wallHit = Physics2D.Raycast(transform.position, dir, dist, wallOnlyLayer);

                    if (wallHit.collider == null || wallHit.transform == archer)
                    {
                        if (dist < minDistSameFloor)
                        {
                            minDistSameFloor = dist;
                            nearestSameFloor = archer;
                            nearestSameFloorScript = script;
                        }
                    }
                }

                // 2. Любые живые скелеты на уровне
                if (dist < minDistAnyFloor)
                {
                    minDistAnyFloor = dist;
                    nearestAnyFloor = archer;
                }
            }
        }

        // Приоритеты
        if (nearestSameFloor != null)
        {
            targetArcher = nearestSameFloor;
            currentArcherScript = nearestSameFloorScript;
            targetLadder = null;
            targetDoor = null;
        }
        else if (nearestAnyFloor != null)
        {
            targetArcher = nearestAnyFloor;
            currentArcherScript = nearestAnyFloor.GetComponent<ArcherBot>();
            targetDoor = null;
            targetLadder = FindBestLadderForTarget(nearestAnyFloor);
        }
        else
        {
            targetArcher = null;
            currentArcherScript = null;
            targetLadder = null;
            
            GameObject doorObj = GameObject.FindWithTag("Door");
            if (doorObj != null)
            {
                targetDoor = doorObj.transform;
                if (Mathf.Abs(targetDoor.position.y - transform.position.y) > 0.6f)
                {
                    targetLadder = FindBestLadderForTarget(targetDoor);
                }
            }
        }
    }

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
            float distX = Mathf.Abs(ladder.position.x - transform.position.x);
            float ladderY = ladder.position.y;
            
            bool isBetweenY = (ladderY >= Mathf.Min(transform.position.y, targetY) - 1f) && 
                              (ladderY <= Mathf.Max(transform.position.y, targetY) + 1f);

            float score = distX;
            if (!isBetweenY) score += 50f; 

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

        // 1. Лестницы
        if (isTouchingLadder && !isClimbing)
        {
            Transform currentTarget = targetArcher != null ? targetArcher : targetDoor;
            if (currentTarget != null)
            {
                if (currentTarget.position.y < transform.position.y - 0.5f)
                {
                    climbDirection = -1; 
                    isClimbing = true;
                }
                else if (currentTarget.position.y > transform.position.y + 0.5f)
                {
                    climbDirection = 1;  
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
        // 2. Движение к скелету
        else if (targetArcher != null && Mathf.Abs(targetArcher.position.y - transform.position.y) < 0.6f)
        {
            rb.gravityScale = 1f;
            float distanceX = targetArcher.position.x - transform.position.x;

            if (Mathf.Abs(distanceX) <= attackRange)
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
        // 3. Движение к лестнице
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
        // 4. Движение к двери
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

        if (anim != null) anim.SetFloat("Speed", Mathf.Abs(currentSpeedX));

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
        if (Time.time - lastJumpTime < jumpCooldown) return;

        // Не прыгаем прямо перед лестницей
        if (targetLadder != null && Mathf.Abs(targetLadder.position.x - transform.position.x) < 1.2f)
        {
            return;
        }

        // Луч для обнаружения бочки/препятствия с учетом настраиваемой высоты
        Vector2 rayOrigin = new Vector2(transform.position.x, transform.position.y + rayHeightOffset);
        Vector2 rayDir = new Vector2(moveDirection, 0f);

        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, rayDir, obstacleCheckDistance, groundLayer);

        // ВИЗУАЛЬНЫЙ ДЕБАГ В ОКНЕ SCENE
        Debug.DrawRay(rayOrigin, rayDir * obstacleCheckDistance, hit.collider != null ? Color.green : Color.red);

        if (hit.collider != null)
        {
            Jump();
            lastJumpTime = Time.time;
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
        if (trashPrefab != null) Instantiate(trashPrefab, transform.position, Quaternion.identity);
    }

    public void TakeDamage(int damage)
    {
        if (!isBoss) return; 
        
        bossHP -= damage;
        if (bossHP <= 0)
        {
            if (trashPrefab != null) Instantiate(trashPrefab, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
    }
}