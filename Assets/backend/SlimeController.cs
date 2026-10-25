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

    [Header("Звуки")]
    public AudioClip jumpSound;           // Звук прыжка
    public AudioClip deathSound;          // Звук смерти
    public AudioClip footstepSound;       // Звук шага / бега

    private Rigidbody2D rb;
    private Animator animator; 
    private AudioSource audioSource;      
    private bool isGrounded;
    private float horizontalInput;
    private bool coinsBanked = false;
    private bool isDead = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>(); 
        UpdateTrashUI(); 

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void Update()
    {
        if (isDead) return;

        horizontalInput = Input.GetAxisRaw("Horizontal");

        // Прыгаем только если нажата кнопка И мы реально на земле
        if ((Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) && isGrounded)
        {
            Jump();
        }
    }

    void FixedUpdate()
    {
        if (isDead) return;

        if (groundCheck != null)
        {
            // Проверяем пересечение с землей И то, что мы не летим вверх (velocity.y <= 0.05)
            bool touchingGround = Physics2D.OverlapCircle(groundCheck.position, checkRadius, groundLayer);
            isGrounded = touchingGround && rb.linearVelocity.y <= 0.05f;
        }
        else
        {
            // Если вы забыли перетащить объект Ground Check в инспектор, 
            // слайм не сможет летать — прыжки просто отключатся и появится предупреждение.
            isGrounded = false;
            Debug.LogWarning("Внимание: Не назначен Ground Check в скрипте SlimeController!");
        }

        // Движение
        float currentSpeedMultiplier = 1f; 
        rb.linearVelocity = new Vector2(horizontalInput * moveSpeed * currentSpeedMultiplier, rb.linearVelocity.y);

        if (animator != null)
        {
            animator.SetFloat("Speed", Mathf.Abs(horizontalInput));
        }

        // Поворот спрайта
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
        // Принудительно обнуляем вертикальную скорость перед прыжком и толкаем вверх
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        isGrounded = false; // Сразу выключаем флаг

        PlaySound(jumpSound);

        if (animator != null)
        {
            animator.SetTrigger("Jump");
        }
    }

    public void PlayJumpSound() { PlaySound(jumpSound); }
    public void PlayDeathSound() { PlaySound(deathSound); }
    public void PlayFootstepSound() { PlaySound(footstepSound); }

    public void DestroyAfterDeathAnimation()
    {
        Destroy(gameObject);
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    public void BankCoins()
    {
        if (coinsBanked) return;
        coinsBanked = true;
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        hp -= damage;
        Debug.Log("Слайм получил урон! Осталось ХП: " + hp);

        if (hp <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        PlaySound(deathSound);

        if (animator != null)
        {
            animator.SetTrigger("Death");
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = false;
        }

        if (trashPrefab != null)
        {
            Instantiate(trashPrefab, transform.position, Quaternion.identity);
        }

        Destroy(gameObject, 2f); 
    }

    public void AddTrash(int amount = 1)
    {
        collectedTrashCount += amount;
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