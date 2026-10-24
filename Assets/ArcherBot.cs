using UnityEngine;

public class ArcherBot : MonoBehaviour
{
    [Header("Стрельба и Движение")]
    public GameObject arrowPrefab;  
    public Transform shootPoint;    
    public float shootInterval = 2f; 
    public float detectionRange = 12f; 
    public float moveSpeed = 3f;        
    public float safeDistance = 5f;     // Дистанция, когда скелет убегает от рыцаря

    [Header("Здоровье и Смерть")]
    public int maxHealth = 50;
    private int currentHealth;
    private bool isDead = false;
    public GameObject trashDropPrefab; // Мусор, который выпадает при смерти скелета

    private Transform slimeTransform;
    private Transform knightTransform;
    private Rigidbody2D rb;
    private float shootTimer = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        currentHealth = maxHealth;
        gameObject.tag = "Archer"; 

        FindTargets();
    }

    void FindTargets()
    {
        GameObject slimeObj = GameObject.FindGameObjectWithTag("Player");
        if (slimeObj != null) slimeTransform = slimeObj.transform;

        GameObject knightObj = GameObject.FindGameObjectWithTag("Knight");
        if (knightObj != null) knightTransform = knightObj.transform;
    }

    void Update()
    {
        if (isDead) return;

        if (slimeTransform == null || knightTransform == null) FindTargets();

        // ПРИОРИТЕТ СТРЕЛЬБЫ: Всегда стреляем в Слайма, если он есть на сцене
        if (slimeTransform != null)
        {
            float distanceToSlime = Vector2.Distance(transform.position, slimeTransform.position);
            if (distanceToSlime <= detectionRange)
            {
                shootTimer += Time.deltaTime;
                if (shootTimer >= shootInterval)
                {
                    Shoot(slimeTransform); // Стреляем именно в Слайма
                    shootTimer = 0f;
                }
            }
        }
    }

    void FixedUpdate()
    {
        if (isDead) return;

        // ЛОГИКА УБЕГАНИЯ: Скелет убегает именно от РЫЦАРЯ, который за ним гоняется
        if (knightTransform != null)
        {
            float distanceToKnight = Vector2.Distance(transform.position, knightTransform.position);

            if (distanceToKnight < safeDistance)
            {
                float runDirection = Mathf.Sign(transform.position.x - knightTransform.position.x);
                rb.linearVelocity = new Vector2(runDirection * moveSpeed, rb.linearVelocity.y);
            }
            else
            {
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            }
        }
    }

    void Shoot(Transform target)
    {
        if (arrowPrefab != null && shootPoint != null)
        {
            GameObject arrowObj = Instantiate(arrowPrefab, shootPoint.position, Quaternion.identity);
            Arrow arrowScript = arrowObj.GetComponent<Arrow>();

            if (arrowScript != null)
            {
                Vector2 shootDirection = (target.position - shootPoint.position).normalized;
                arrowScript.SetDirection(shootDirection);
            }
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        Debug.Log("Скелет получил урон от рыцаря! HP: " + currentHealth);

        if (currentHealth <= 0)
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
        isDead = true;
        Debug.Log("Скелет уничтожен рыцарем и оставил мусор!");

        // Выпадение мусора после смерти скелета
        if (trashDropPrefab != null)
        {
            Instantiate(trashDropPrefab, transform.position, Quaternion.identity);
        }

        // Уничтожаем объект скелета
        Destroy(gameObject);
    }
}