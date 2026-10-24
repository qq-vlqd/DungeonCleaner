using UnityEngine;

public class KnightBot : MonoBehaviour
{
    [Header("Движение и Прыжки")]
    public float speed = 2.5f; 
    public float climbSpeed = 3f;                   
    public float jumpForce = 7f;                    
    public float obstacleCheckDistance = 0.8f;   
    public LayerMask groundLayer;                
    public LayerMask wallOnlyLayer;              // Слой только для сплошных стен/пола (без бочек и объектов)

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

    private Transform targetArcher;  
    private ArcherBot currentArcherScript;

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
            FindSameFloorArcher();
        }
    }

    void Update()
    {
        attackTimer += Time.deltaTime;
        
        if (!isBoss)
        {
            FindSameFloorArcher();
        }
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

        // 1. Подъем по лестнице
        if (isTouchingLadder)
        {
            isClimbing = true;
        }

        if (isClimbing)
        {
            rb.gravityScale = 0f;
            
            if (currentLadder != null && transform.position.y > currentLadder.position.y + 0.8f)
            {
                isClimbing = false;
                isTouchingLadder = false;
                rb.gravityScale = 1f;
                rb.linearVelocity = new Vector2(speed, jumpForce * 0.5f);
            }
            else
            {
                float ladderX = currentLadder != null ? currentLadder.position.x : transform.position.x;
                float alignX = (ladderX - transform.position.x) * 5f; 
                
                rb.linearVelocity = new Vector2(alignX, climbSpeed);
                currentSpeedX = 0f;
            }
        }
        // 2. ПРИОРИТЕТ: Если есть видимый скелет — идем к нему независимо от направления (влево или вправо)
        else if (targetArcher != null)
        {
            rb.gravityScale = 1f;
            float distanceX = targetArcher.position.x - transform.position.x;

            if (Mathf.Abs(distanceX) <= attackRange)
            {
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);

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
        // 3. Если скелетов нет — идем вправо
        else
        {
            rb.gravityScale = 1f;

            currentSpeedX = speed; 
            rb.linearVelocity = new Vector2(currentSpeedX, rb.linearVelocity.y);
            CheckAndJump(1f);
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

    void FindSameFloorArcher()
    {
        GameObject[] archers = GameObject.FindGameObjectsWithTag("Archer");
        float shortestDistance = Mathf.Infinity;
        Transform nearest = null;

        foreach (GameObject archer in archers)
        {
            Transform archerTransform = archer.transform;
            ArcherBot archerScript = archerTransform.GetComponent<ArcherBot>();

            if (archerScript != null && !archerScript.IsDead())
            {
                float distanceY = Mathf.Abs(archerTransform.position.y - transform.position.y);

                // Проверяем, что скелет находится на нашем этаже
                if (distanceY < 1.2f)
                {
                    Vector2 direction = (archerTransform.position - transform.position).normalized;
                    float distance = Vector2.Distance(transform.position, archerTransform.position);
                    
                    // Проверяем лучом только сплошные стены/пол (игнорируя бочки, если они не на этом слое)
                    RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance, wallOnlyLayer);

                    if (hit.collider == null) 
                    {
                        if (distance < shortestDistance)
                        {
                            shortestDistance = distance;
                            nearest = archerTransform;
                        }
                    }
                }
            }
        }

        if (nearest != null)
        {
            targetArcher = nearest;
            currentArcherScript = nearest.GetComponent<ArcherBot>();
        }
        else
        {
            targetArcher = null;
            currentArcherScript = null;
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
        if (((1 << collision.gameObject.layer) & groundLayer) != 0 || collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
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
                FindSameFloorArcher(); 
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