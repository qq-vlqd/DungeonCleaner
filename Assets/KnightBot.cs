using UnityEngine;

public class KnightBot : MonoBehaviour
{
    [Header("Движение")]
    public float speed = 2.5f; 
    
    [Header("Префаб Мусора")]
    public GameObject trashPrefab; 

    [Header("Боссфайт")]
    public bool isBoss = false; 
    public int bossHP = 100;

    [Header("Бой со Скелетом")]
    public float attackRange = 1.5f;     // Дистанция удара по скелету
    public int knightDamage = 25;        // Урон рыцаря по скелету
    public float attackCooldown = 1.5f;  // Время между ударами (чтобы анимация успевала проиграться)
    private float attackTimer = 0f;

    private Transform targetArcher;  
    private ArcherBot currentArcherScript;

    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer spriteRenderer;

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
        // Увеличиваем таймер кулдакана атаки с каждым кадром
        attackTimer += Time.deltaTime;
    }

    void FixedUpdate()
    {
        float currentSpeedX = 0f;

        if (isBoss)
        {
            currentSpeedX = Mathf.Sin(Time.time) * speed;
            rb.linearVelocity = new Vector2(currentSpeedX, rb.linearVelocity.y);
        }
        else if (targetArcher != null)
        {
            float distance = Vector2.Distance(transform.position, targetArcher.position);

            if (distance <= attackRange)
            {
                // Останавливаемся рядом со скелетом
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);

                // Бьем только если прошел кулдаун
                if (attackTimer >= attackCooldown)
                {
                    AttackArcher();
                    attackTimer = 0f; // Сбрасываем таймер
                }
            }
            else
            {
                // Идем к скелету
                float direction = Mathf.Sign(targetArcher.position.x - transform.position.x);
                currentSpeedX = direction * speed;
                rb.linearVelocity = new Vector2(currentSpeedX, rb.linearVelocity.y);
            }
        }
        else
        {
            currentSpeedX = speed;
            rb.linearVelocity = new Vector2(currentSpeedX, rb.linearVelocity.y);
            FindNearestArcher();
        }

        // Обновляем анимацию бега / покоя
        if (anim != null)
        {
            anim.SetFloat("Speed", Mathf.Abs(currentSpeedX));
        }

        // Разворот спрайта
        if (currentSpeedX > 0.05f)
        {
            if (spriteRenderer != null) spriteRenderer.flipX = false;
        }
        else if (currentSpeedX < -0.05f)
        {
            if (spriteRenderer != null) spriteRenderer.flipX = true;
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
        // Запускаем анимацию атаки
        if (anim != null)
        {
            anim.SetTrigger("Attack");
        }

        // Наносим урон скелету
        if (currentArcherScript != null)
        {
            currentArcherScript.TakeDamage(knightDamage);
            
            if (currentArcherScript == null || currentArcherScript.IsDead())
            {
                targetArcher = null;
                currentArcherScript = null;
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
        Debug.Log("Рыцарь-босс получил урон! HP: " + bossHP);

        if (bossHP <= 0)
        {
            WinGame();
        }
    }

    void WinGame()
    {
        Debug.Log("Рыцарь повержен!");
        Destroy(gameObject);
    }
}