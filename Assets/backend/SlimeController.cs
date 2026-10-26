
using UnityEngine;
using UnityEngine.SceneManagement;
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

    [Header("Настройки атаки")]
    public bool canAttack = true;
    public int attackDamage = 20;
    public float attackRange = 1.5f;
    public LayerMask enemyLayer;

    [Header("Звуки")]
    public AudioClip jumpSound;
    public AudioClip deathSound;
    public AudioClip footstepSound;
    public AudioClip attackSound;

    [Header("Перезапуск уровня")]
    public float restartDelay = 2f;

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

        if ((Input.GetKeyDown(KeyCode.Space) ||
             Input.GetKeyDown(KeyCode.W) ||
             Input.GetKeyDown(KeyCode.UpArrow)) && isGrounded)
        {
            Jump();
        }

        if (canAttack && Input.GetKeyDown(KeyCode.E))
        {
            Attack();
        }
    }

    void FixedUpdate()
    {
        if (isDead) return;

        if (rb == null) return;

        if (groundCheck != null)
        {
            bool touchingGround = Physics2D.OverlapCircle(
                groundCheck.position,
                checkRadius,
                groundLayer
            );

            isGrounded = touchingGround && rb.linearVelocity.y <= 0.05f;
        }
        else
        {
            isGrounded = false;
        }

        rb.linearVelocity = new Vector2(
            horizontalInput * moveSpeed,
            rb.linearVelocity.y
        );

        if (animator != null)
        {
            animator.SetFloat("Speed", Mathf.Abs(horizontalInput));
        }

        if (horizontalInput > 0)
        {
            transform.localScale = new Vector3(
                Mathf.Abs(transform.localScale.x),
                transform.localScale.y,
                transform.localScale.z
            );
        }
        else if (horizontalInput < 0)
        {
            transform.localScale = new Vector3(
                -Mathf.Abs(transform.localScale.x),
                transform.localScale.y,
                transform.localScale.z
            );
        }
    }

    void Jump()
    {
        if (rb == null) return;

        rb.linearVelocity = new Vector2(
            rb.linearVelocity.x,
            jumpForce
        );

        isGrounded = false;

        PlaySound(jumpSound);

        if (animator != null)
        {
            animator.SetTrigger("Jump");
        }
    }

    void Attack()
    {
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        PlaySound(attackSound);

        float facingDir = transform.localScale.x > 0 ? 1f : -1f;

        Vector2 attackPos = (Vector2)transform.position +
            new Vector2(facingDir * (attackRange * 0.5f), 0f);

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(
            attackPos,
            attackRange * 0.5f,
            enemyLayer
        );

        foreach (Collider2D enemy in hitEnemies)
        {
            KnightSequence knightSeq = enemy.GetComponent<KnightSequence>();

            if (knightSeq != null)
            {
                knightSeq.TakeDamage(attackDamage);
                break;
            }

            KnightBot knightBot = enemy.GetComponent<KnightBot>();

            if (knightBot != null)
            {
                knightBot.TakeDamage(attackDamage);
                break;
            }
        }
    }

    public void PlayJumpSound()
    {
        PlaySound(jumpSound);
    }

    public void PlayDeathSound()
    {
        PlaySound(deathSound);
    }

    public void PlayFootstepSound()
    {
        PlaySound(footstepSound);
    }

    // Оставлен для совместимости с Animation Event.
    // Не уничтожаем слайма, иначе перезапуск может не выполниться.
    public void DestroyAfterDeathAnimation()
    {
        // Перезапуск уровня запускается в Die().
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
        if (isDead || damage <= 0) return;

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
            Instantiate(
                trashPrefab,
                transform.position,
                Quaternion.identity
            );
        }

        // Перезапускаем текущий уровень после смерти.
        Invoke(nameof(RestartLevel), restartDelay);
    }

    void RestartLevel()
    {
        Time.timeScale = 1f;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    public void AddTrash(int amount = 1)
{
    collectedTrashCount += amount;

    Debug.Log("Собран мусор! Всего: " + collectedTrashCount);

    UpdateTrashUI();
}

void UpdateTrashUI()
{
    if (trashText != null)
    {
        trashText.text = "Trash: " + collectedTrashCount;
    }
    else
    {
        Debug.LogWarning("Не назначен Trash Text в SlimeController!");
    }
}

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;

            Gizmos.DrawWireSphere(
                groundCheck.position,
                checkRadius
            );
        }

        if (canAttack)
        {
            float facingDir = transform.localScale.x > 0 ? 1f : -1f;

            Vector2 attackPos = (Vector2)transform.position +
                new Vector2(facingDir * (attackRange * 0.5f), 0f);

            Gizmos.color = Color.red;

            Gizmos.DrawWireSphere(
                attackPos,
                attackRange * 0.5f
            );
        }
    }
}
