using UnityEngine;

public class ArcherBot : MonoBehaviour
{
    [Header("Здоровье и Мусор")]
    public int hp = 50;
    public GameObject trashPrefab;        // Мусор, который останется после смерти

    [Header("Настройки Атаки")]
    public float attackRange = 8f;        
    public float attackCooldown = 2f;     
    public GameObject arrowPrefab;        
    public Transform shootPoint;          

    [Header("Проверка Видимости (ЛоС)")]
    public LayerMask obstacleLayer;       

    private float attackTimer = 0f;
    private Transform currentTarget;

    void Update()
    {
        attackTimer += Time.deltaTime;
        FindTarget();

        if (currentTarget != null)
        {
            float distanceToTarget = Vector2.Distance(transform.position, currentTarget.position);

            if (distanceToTarget <= attackRange && HasLineOfSight(currentTarget))
            {
                FlipToTarget(currentTarget.position);

                if (attackTimer >= attackCooldown)
                {
                    Shoot();
                    attackTimer = 0f;
                }
            }
        }
    }

    bool HasLineOfSight(Transform target)
    {
        Vector2 startPos = shootPoint != null ? shootPoint.position : transform.position;
        Vector2 direction = (target.position - (Vector3)startPos).normalized;
        float distance = Vector2.Distance(startPos, target.position);

        Debug.DrawRay(startPos, direction * distance, Color.red);
        RaycastHit2D hit = Physics2D.Raycast(startPos, direction, distance, obstacleLayer);

        return hit.collider == null; // Если ничего не задело, видимость есть
    }

    void Shoot()
    {
        if (arrowPrefab == null) return;

        Vector2 origin = shootPoint != null ? shootPoint.position : transform.position;
        GameObject arrowObj = Instantiate(arrowPrefab, origin, Quaternion.identity);

        // Отключаем столкновение со скелетом
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
        if (targetPos.x > transform.position.x)
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        else if (targetPos.x < transform.position.x)
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
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
        // Оставляем мусор на месте смерти скелета
        if (trashPrefab != null)
        {
            Instantiate(trashPrefab, transform.position, Quaternion.identity);
        }
        Destroy(gameObject);
    }
}