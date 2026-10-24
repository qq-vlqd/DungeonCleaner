using UnityEngine;

public class ArcherBot : MonoBehaviour
{
    [Header("Здоровье и Мусор")]
    public int hp = 50;
    public GameObject trashPrefab;          // Мусор при смерти

    [Header("Настройки ИИ и Движения")]
    public float moveSpeed = 3f;            // Скорость отступления/погони
    public float detectionRange = 8f;       // Дистанция обнаружения
    public float attackRange = 6f;          // Дистанция стрельбы
    public float safeDistance = 3f;         // Дистанция, ближе которой лучник начинает отбегать назад
    public float attackCooldown = 2f;       

    [Header("Ссылки на стрельбу")]
    public GameObject arrowPrefab;          
    public Transform shootPoint;            

    [Header("Проверка Видимости (ЛоС)")]
    public LayerMask obstacleLayer;         

    private float attackTimer = 0f;
    private Transform currentTarget;
    private Animator animator;
    private Rigidbody2D rb;
    private bool facingRight = true;

    void Start()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        attackTimer += Time.deltaTime;
        FindTarget();

        if (currentTarget != null)
        {
            float distanceToTarget = Vector2.Distance(transform.position, currentTarget.position);
            float directionX = currentTarget.position.x - transform.position.x;

            // Поворот лицом к цели
            FlipToTarget(currentTarget.position);

            if (distanceToTarget <= detectionRange && HasLineOfSight(currentTarget))
            {
                if (distanceToTarget < safeDistance)
                {
                    // Цель слишком близко — отбегаем назад!
                    MoveAway(directionX);
                }
                else if (distanceToTarget <= attackRange)
                {
                    // На дистанции атаки — останавливаемся и стреляем
                    StopMoving();

                    if (attackTimer >= attackCooldown)
                    {
                        Shoot();
                        attackTimer = 0f;
                    }
                }
                else
                {
                    // Цель далеко, но в зоне видимости (можно при желании идти к ней, пока стоим)
                    StopMoving();
                }
            }
            else
            {
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

        return hit.collider == null; // Видимость есть, если на пути нет препятствий
    }

    void MoveAway(float directionX)
    {
        // Идем в противоположную от цели сторону
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

    // Универсальный метод для работы со скоростью во всех версиях Unity
    void SetVelocityX(float velocityX)
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(velocityX, rb.linearVelocity.y);
        }
    }

    void Shoot()
    {
        if (arrowPrefab == null) return;

        // Запуск анимации атаки (убедитесь, что в Animator есть Trigger "Attack")
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        Vector2 origin = shootPoint != null ? shootPoint.position : transform.position;
        GameObject arrowObj = Instantiate(arrowPrefab, origin, Quaternion.identity);

        // Отключаем столкновение стрелы со стрелком
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
        hp -= damage;
        if (hp <= 0)
        {
            Die();
        }
    }

    public bool IsDead()
    {
        return hp <= 0;
    }

    void Die()
    {
        if (trashPrefab != null)
        {
            Instantiate(trashPrefab, transform.position, Quaternion.identity);
        }
        Destroy(gameObject);
    }
}