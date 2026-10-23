using UnityEngine;
using UnityEngine.SceneManagement;

public class SlimeController : MonoBehaviour
{
    [Header("Движение")]
    public float speed = 6f;        
    public float jumpForce = 8f;    

    [Header("Уборка и Прокачка")]
    public bool hasSword = false;                     // Куплен ли меч для финала
    public float bloodCleanTime = 1f;                 // Время оттирания крови
    public float trashCleanTime = 0.5f;               // Время сбора обычного мусора
    public float cleaningSpeedMultiplier = 1f;        // Множитель скорости уборки (для магазина)

    [Header("Здоровье Слайма")]
    public int maxHealth = 3;                         // Максимальное количество жизней
    private int currentHealth;

    private Rigidbody2D rb;
    private bool isGrounded;        
    private float cleanTimer = 0f;                    // Таймер для крови
    private float trashCleanTimer = 0f;               // Таймер для мусора

    void Start() 
    { 
        rb = GetComponent<Rigidbody2D>(); 
        currentHealth = maxHealth;                    // Устанавливаем здоровье при старте
    }

    void Update()
    {
        // Горизонтальное движение
        float moveX = Input.GetAxisRaw("Horizontal");
        rb.linearVelocity = new Vector2(moveX * speed, rb.linearVelocity.y); 

        // Прыжок
        if ((Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W)) && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            isGrounded = false; 
        }

        // Атака Рыцаря на клик мышки (если куплен меч)
        if (Input.GetMouseButtonDown(0) && hasSword)
        {
            Attack();
        }
    }

    void Attack()
    {
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, 1.5f);
        foreach (Collider2D enemy in hitEnemies)
        {
            if (enemy.CompareTag("Knight"))
            {
                enemy.GetComponent<KnightBot>().TakeDamage(10);
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Касаемся земли/пола
        if (!collision.gameObject.CompareTag("coin"))
        {
            isGrounded = true;
        }
    }

    // Сбор монет происходит мгновенно при касании (так как у монет стоит Is Trigger)
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("coin"))
        {
            Destroy(other.gameObject);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddCoins(1); 
            }
        }
    }

    // Сбор мусора и крови (теперь они падают с гравитацией, поэтому используем Collision)
    private void OnCollisionStay2D(Collision2D collision)
    {
        // Сбор обычного мусора по таймеру (ускоряется при прокачке)
        if (collision.gameObject.CompareTag("trash"))
        {
            trashCleanTimer += Time.deltaTime * cleaningSpeedMultiplier;
            if (trashCleanTimer >= trashCleanTime)
            {
                Destroy(collision.gameObject); 
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.AddScore(); 
                }
                trashCleanTimer = 0f;
            }
        }

        // Оттирание крови по таймеру
        if (collision.gameObject.CompareTag("blood"))
        {
            cleanTimer += Time.deltaTime * cleaningSpeedMultiplier;
            if (cleanTimer >= bloodCleanTime)
            {
                Destroy(collision.gameObject);
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.AddCoins(3); // За кровь дают больше монет
                }
                cleanTimer = 0f;
            }
        }
    }

    // Если Слайм отошел от объекта до того, как убрал его — сбрасываем таймер
    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("trash")) trashCleanTimer = 0f;
        if (collision.gameObject.CompareTag("blood")) cleanTimer = 0f;
    }

    // Метод получения урона (вызывается из скрипта стрелы лучника)
    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        Debug.Log("Слайм получил урон! Осталось здоровья: " + currentHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // Смерть слайма
    void Die()
    {
        Debug.Log("Слайм погиб!");
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}