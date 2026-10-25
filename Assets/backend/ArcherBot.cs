using UnityEngine;

public class ArcherBot : MonoBehaviour
{
    [Header("Здоровье и Мусор")]
    public int hp = 50;
    public GameObject trashPrefab;              // Мусор при смерти

    [Header("Настройки ИИ и Движения")]
    public float moveSpeed = 3f;                // Скорость отступления
    public float detectionRange = 8f;           // Дистанция обнаружения
    public float attackRange = 6f;              // Дистанция стрельбы
    public float safeDistance = 3f;             // Дистанция, ближе которой лук отбегает назад
    public float attackCooldown = 2f; 

    [Header("Ссылки на стрельбу")]
    public GameObject arrowPrefab;          
    public Transform shootPoint;              

    [Header("Проверка Видимости (ЛоС)")]
    public LayerMask obstacleLayer;         

    [Header("Звуки и Громкость")]
    public AudioClip shootSound;                // Звук выстрела
    [Range(0f, 1f)] public float shootVolume = 1f;

    public AudioClip deathSound;                // Звук смерти
    [Range(0f, 1f)] public float deathVolume = 1f;

    public AudioClip footstepSound;             // Звук шага
    [Range(0f, 1f)] public float footstepVolume = 1f;

    private float attackTimer = 0f;
    private Transform currentTarget;
    private Animator animator;
    private Rigidbody2D rb;
    private Collider2D col;                     // Ссылка на коллайдер
    private AudioSource audioSource;            // Компонент звука
    private bool facingRight = true;
    private bool isDead = false;                // Флаг для проверки состояния смерти

    void Start()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        // Автоматически находим компонент Audio Source на этом же объекте или добавляем его
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void Update()
    {
        // Если скелет мертв, полностью прекращаем работу логики ИИ
        if (isDead) return;

        FindTarget();

        if (currentTarget != null)
        {
            float distanceToTarget = Vector2.Distance(transform.position, currentTarget.position);
            float directionX = currentTarget.position.x - transform.position.x;

            // Поворот лицом к цели
            FlipToTarget(currentTarget.position);

            // Проверяем и дальность, и прямую видимость (преграды)
            bool inRange = distanceToTarget <= detectionRange;
            bool hasSight = HasLineOfSight(currentTarget);

            if (inRange && hasSight)
            {
                // Видимость ЕСТЬ — накапливаем таймер атаки
                attackTimer += Time.deltaTime;

                if (distanceToTarget < safeDistance)
                {
                    // Цель близко — отбегаем
                    MoveAway(directionX);
                }
                else if (distanceToTarget <= attackRange)
                {
                    // На дистанции атаки — стоим и бьем
                    StopMoving();

                    if (attackTimer >= attackCooldown)
                    {
                        Shoot();
                        attackTimer = 0f; 
                    }
                }
                else
                {
                    StopMoving();
                }
            }
            else
            {
                // ПРЕГРАДА НА ПУТИ ИЛИ ЦЕЛИ НЕТ В ЗОНЕ ВИДИМОСТИ
                StopMoving();
            }
        }
        else
        {
            StopMoving();
        }
    }

    bool HasLineOfSight(Transform target)
    {
        Vector2 startPos = shootPoint != null ? shootPoint.position : transform.position;
        Vector2 direction = (target.position - (Vector3)startPos).normalized;
        float distance = Vector2.Distance(startPos, target.position);

        Debug.DrawRay(startPos, direction * distance, Color.red);
        
        RaycastHit2D hit = Physics2D.Raycast(startPos, direction, distance, obstacleLayer);
        return hit.collider == null;
    }

    void MoveAway(float directionX)
    {
        float fleeDir = -Mathf.Sign(directionX);
        SetVelocityX(fleeDir * moveSpeed);

        if (animator != null)
        {
            animator.SetFloat("Speed", moveSpeed);
        }
    }

    void StopMoving()
    {
        SetVelocityX(0f);

        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
        }
    }

    void SetVelocityX(float velocityX)
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(velocityX, rb.linearVelocity.y);
        }
    }

    void Shoot()
    {
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }
    }

    // Вызывается из анимации атаки через Animation Event
    public void SpawnArrowEvent()
    {
        if (arrowPrefab == null || isDead) return;

        Vector2 origin = shootPoint != null ? shootPoint.position : transform.position;
        GameObject arrowObj = Instantiate(arrowPrefab, origin, Quaternion.identity);

        Collider2D arrowCollider = arrowObj.GetComponent<Collider2D>();
        Collider2D[] myColliders = GetComponentsInChildren<Collider2D>();
        if (arrowCollider != null && myColliders.Length > 0)
        {
            foreach (Collider2D myCol in myColliders)
            {
                Physics2D.IgnoreCollision(arrowCollider, myCol);
            }
        }

        Arrow arrowScript = arrowObj.GetComponent<Arrow>();
        if (arrowScript != null && currentTarget != null)
        {
            Vector2 direction = (currentTarget.position - (Vector3)origin).normalized;
            Rigidbody2D arrowRb = arrowObj.GetComponent<Rigidbody2D>();
            if (arrowRb != null)
            {
                arrowRb.linearVelocity = direction * arrowScript.speed;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            arrowObj.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
    }

    // ==========================================
    // МЕТОДЫ ЗВУКОВ ДЛЯ ANIMATION EVENTS
    // ==========================================
    public void PlayShootSound()
    {
        if (audioSource != null && shootSound != null)
        {
            audioSource.PlayOneShot(shootSound, shootVolume);
        }
    }

    public void PlayDeathSound()
    {
        if (audioSource != null && deathSound != null)
        {
            audioSource.PlayOneShot(deathSound, deathVolume);
        }
    }

    public void PlayFootstepSound()
    {
        if (audioSource != null && footstepSound != null)
        {
            audioSource.PlayOneShot(footstepSound, footstepVolume);
        }
    }
    // ==========================================

    void FindTarget()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        GameObject[] knights = GameObject.FindGameObjectsWithTag("Knight");

        float closestDistance = Mathf.Infinity;
        Transform bestTarget = null;

        if (player != null)
        {
            float dist = Vector2.Distance(transform.position, player.transform.position);
            if (dist < closestDistance)
            {
                closestDistance = dist;
                bestTarget = player.transform;
            }
        }

        foreach (GameObject knight in knights)
        {
            float dist = Vector2.Distance(transform.position, knight.transform.position);
            if (dist < closestDistance)
            {
                closestDistance = dist;
                bestTarget = knight.transform;
            }
        }

        currentTarget = bestTarget;
    }

    void FlipToTarget(Vector3 targetPos)
    {
        if (targetPos.x > transform.position.x && !facingRight)
        {
            FacingFlip();
        }
        else if (targetPos.x < transform.position.x && facingRight)
        {
            FacingFlip();
        }
    }

    void FacingFlip()
    {
        facingRight = !facingRight;
        Vector3 Scaler = transform.localScale;
        Scaler.x *= -1;
        transform.localScale = Scaler;
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return; // Если уже мертв, урон не принимаем

        hp -= damage;
        if (hp <= 0)
        {
            Die();
        }
    }

    public bool IsDead()
    {
        return isDead;
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        // Включаем анимацию смерти через триггер "Death"
        if (animator != null)
        {
            animator.SetTrigger("Death");
        }

        // Отключаем физику и коллайдер, чтобы враги перестали атаковать мертвого скелета
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        if (col != null)
        {
            col.enabled = false;
        }

        // Вызываем спавн мусора с задержкой
        Invoke("SpawnTrash", 1.5f);

        // Уничтожаем объект через 1.5 секунды
        Destroy(gameObject, 1.5f);
    }

    // Отдельный метод для создания мусора
    void SpawnTrash()
    {
        if (trashPrefab != null)
        {
            Instantiate(trashPrefab, transform.position, Quaternion.identity);
        }
    }
}