using UnityEngine;

public class KnightBot : MonoBehaviour
{
    [Header("Движение и Прыжки")]
    public float speed = 2.5f; 
    public float jumpForce = 7f;                  
    public float obstacleCheckDistance = 0.8f;   
    public LayerMask groundLayer;                 

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
            FindNearestArcher();
        }
    }

    void Update()
    {
        attackTimer += Time.deltaTime;
    }

    void FixedUpdate()
    {
        float currentSpeedX = 0f;

        if (isBoss)
        {
            currentSpeedX = Mathf.Sin(Time.time) * speed;
            rb.linearVelocity = new Vector2(currentSpeedX, rb.linearVelocity.y);
            CheckAndJump(Mathf.Sign(currentSpeedX));
        }
        else if (targetArcher != null)
        {
            float distance = Vector2.Distance(transform.position, targetArcher.position);

            if (distance <= attackRange)
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
                float direction = Mathf.Sign(targetArcher.position.x - transform.position.x);
                currentSpeedX = direction * speed;
                rb.linearVelocity = new Vector2(currentSpeedX, rb.linearVelocity.y);
                CheckAndJump(direction);
            }
        }
        else
        {
            currentSpeedX = speed;
            rb.linearVelocity = new Vector2(currentSpeedX, rb.linearVelocity.y);
            CheckAndJump(1f);
            FindNearestArcher();
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
        if (moveDirection == 0 || !isGrounded) return;

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

    void FindNearestArcher()
    {
        GameObject[] archers = GameObject.FindGameObjectsWithTag("Archer");
        float shortestDistance = Mathf.Infinity;
        Transform nearest = null;

        foreach (GameObject archer in archers)
        {
            float distance = Vector2.Distance(transform.position, archer.transform.position);
            if (distance < shortestDistance)
            {
                shortestDistance = distance;
                nearest = archer.transform;
            }
        }

        if (nearest != null)
        {
            targetArcher = nearest;
            currentArcherScript = nearest.GetComponent<ArcherBot>();
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
            }
        }
    }

    public void TakeArrowHit()
    {
        // Спавн мусора при попадании стрелы
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
            // Спавн мусора при смерти босса
            if (trashPrefab != null)
            {
                Instantiate(trashPrefab, transform.position, Quaternion.identity);
            }
            Destroy(gameObject);
        }
    }
}