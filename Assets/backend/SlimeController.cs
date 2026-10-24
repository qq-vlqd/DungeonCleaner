using UnityEngine;
using TMPro;

public class SlimeController : MonoBehaviour
{
    [Header("Здоровье и Мусор")]
    public int hp = 40;
    public GameObject trashPrefab;        
    public int collectedTrashCount = 0;   

    [Header("Интерфейс (UI)")]
    public TextMeshProUGUI trashText;     

    [Header("Движение Слайма")]
    public float moveSpeed = 5f;          
    public float jumpForce = 8f;          
    public float cleaningSpeedMultiplier = 1f; 

    [Header("Проверка земли")]
    public Transform groundCheck;         
    public float checkRadius = 0.2f;      
    public LayerMask groundLayer;         

    private Rigidbody2D rb;
    private Animator animator; 
    private bool isGrounded;
    private float horizontalInput;
    private bool coinsBanked = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>(); 
        UpdateTrashUI(); 
    }

    void Update()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");

        // Управление прыжком + запуск анимации прыжка
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

        // Движение с учетом множителя прокачки
        float currentSpeedMultiplier = 1f;
        // Защита, если класс Upgrades существует в проекте
        // currentSpeedMultiplier = Upgrades.SpeedMultiplier; 

        rb.linearVelocity = new Vector2(horizontalInput * moveSpeed * currentSpeedMultiplier, rb.linearVelocity.y);

        // Передаем параметр Speed в Animator (абсолютное значение)
        if (animator != null)
        {
            animator.SetFloat("Speed", Mathf.Abs(horizontalInput));
        }

        // Поворот спрайта в сторону движения
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

        // Запускаем триггер прыжка в Animator
        if (animator != null)
        {
            animator.SetTrigger("Jump");
        }
    }

    public void BankCoins()
    {
        if (coinsBanked) return;          // чтобы не начислить дважды
        coinsBanked = true;
        
        // Безопасное начисление монет (если класс Upgrades подключен)
        // Upgrades.Coins += Mathf.RoundToInt(collectedTrashCount * Upgrades.CoinMultiplier);
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

    public void AddTrash(int amount = 1)
    {
        collectedTrashCount += amount;
        Debug.Log("Собрано мусора: " + collectedTrashCount);
        UpdateTrashUI(); 
    }

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