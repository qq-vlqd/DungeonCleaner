using UnityEngine;

public class KnightBot : MonoBehaviour
{
    [Header("Движение")]
    public float speed = 2.5f; 
    
    [Header("Префаб Мусора")]
    public GameObject trashPrefab; // Мусор, который вылетает из рыцаря при попадании стрелы

    [Header("Боссфайт")]
    public bool isBoss = false; 
    public int bossHP = 100;

    [Header("Бой со Скелетом")]
    public float attackRange = 1.2f; // Дистанция удара по скелету
    public int knightDamage = 25;    // Урон рыцаря по скелету
    private Transform targetArcher;  
    private ArcherBot currentArcherScript;

    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        
        if (!isBoss)
        {
            FindNearestArcher();
        }
    }

    void FixedUpdate()
    {
        if (isBoss)
        {
            // Поведение босса
            rb.linearVelocity = new Vector2(Mathf.Sin(Time.time) * speed, rb.linearVelocity.y);
        }
        else if (targetArcher != null)
        {
            // Рыцарь пытается приблизиться к скелету
            float direction = Mathf.Sign(targetArcher.position.x - transform.position.x);
            rb.linearVelocity = new Vector2(direction * speed, rb.linearVelocity.y);

            // Проверяем дистанцию для атаки
            float distance = Vector2.Distance(transform.position, targetArcher.position);
            if (distance <= attackRange)
            {
                AttackArcher();
            }
        }
        else
        {
            // Если скелетов нет, рыцарь просто бежит вперед
            rb.linearVelocity = new Vector2(speed, rb.linearVelocity.y);
            FindNearestArcher();
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

    // ВЫЗЫВАЕТСЯ ПРИ ПОПАДАНИИ СТРЕЛЫ В РЫЦАРЯ
    public void TakeArrowHit()
    {
        if (trashPrefab != null)
        {
            // Выбиваем мусор из рыцаря при попадании стрелы
            Instantiate(trashPrefab, transform.position, Quaternion.identity);
        }
    }

    public void TakeDamage(int damage)
    {
        if (!isBoss) return; 
        
        bossHP -= damage;
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