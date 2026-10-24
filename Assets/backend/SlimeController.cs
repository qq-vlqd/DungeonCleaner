using UnityEngine;
using TMPro; // Подключаем TextMeshPro (если используете обычный Text, замените на UnityEngine.UI и тип Text)

public class SlimeController : MonoBehaviour
{
    [Header("Здоровье и Мусор")]
    public int hp = 40;
    public GameObject trashPrefab;        // Префаб мусора при смерти слайма
    public int collectedTrashCount = 0;   // Счётчик собранного мусора

    [Header("Интерфейс (UI)")]
    public TextMeshProUGUI trashText;     // Ссылка на текст с количеством мусора слева сверху

    [Header("Движение Слайма")]
    public float moveSpeed = 5f;          
    public float jumpForce = 8f;          
    public float cleaningSpeedMultiplier = 1f; 

    [Header("Проверка земли")]
    public Transform groundCheck;         
    public float checkRadius = 0.2f;      
    public LayerMask groundLayer;         

    private Rigidbody2D rb;
    private bool isGrounded;
    private float horizontalInput;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        UpdateTrashUI(); // Обновляем текст при старте игры
    }

    void Update()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");

        if ((Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) && isGrounded)
        {
            Jump();
        }
    }

    void FixedUpdate()
    {
        if (groundCheck != null)
        {
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, checkRadius, groundLayer);
        }
        else
        {
            isGrounded = true; 
        }

        rb.linearVelocity = new Vector2(horizontalInput * moveSpeed * Upgrades.SpeedMultiplier, rb.linearVelocity.y);

        if (horizontalInput > 0)
        {
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
        else if (horizontalInput < 0)
        {
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
    }

    void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
    }

    bool coinsBanked;

    public void BankCoins()
    {
        if (coinsBanked) return;          // чтобы не начислить дважды
        coinsBanked = true;
        Upgrades.Coins += Mathf.RoundToInt(collectedTrashCount * Upgrades.CoinMultiplier);
    }

    public void TakeDamage(int damage)
    {
        hp -= damage;
        Debug.Log("Слайм получил урон! Осталось ХП: " + hp);

        if (hp <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        if (trashPrefab != null)
        {
            Instantiate(trashPrefab, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }

    // Метод для увеличения собранного мусора
    public void AddTrash(int amount = 1)
    {
        collectedTrashCount += amount;
        Debug.Log("Собрано мусора: " + collectedTrashCount);
        
        UpdateTrashUI(); // Сразу обновляем текст на экране
    }

    // Обновление текста на UI
    void UpdateTrashUI()
    {
        if (trashText != null)
        {
            trashText.text = "Мусор: " + collectedTrashCount;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, checkRadius);
        }
    }
}