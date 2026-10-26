using System.Collections;
using UnityEngine;
using TMPro;

public class KnightSequence : MonoBehaviour
{
    [Header("Здоровье")]
    public int maxHealth = 100;
    private int currentHealth;
    private bool isDead = false;

    [Header("Настройки движения")]
    public float moveDistance = 3f;
    public float moveSpeed = 2.5f;

    [Header("Настройки реплик и облачка")]
    public GameObject speechBubbleObj;
    public TextMeshProUGUI speechText;

    [TextArea(1, 3)] public string phrase1 = "Я пройду немного вправо...";
    [TextArea(1, 3)] public string phrase2 = "А теперь разворачиваюсь назад!";
    [TextArea(1, 3)] public string phrase3 = "И это моя финальная реплика!";

    public float phraseDisplayTime = 3f;

    [Header("Анимация смерти")]
    public string deathStateName = "Death";         // имя стейта в Animator (Layer 0)
    public float deathAnimationDuration = 1.5f;     // длительность анимации смерти в секундах
    public bool disablePhysicsAfterDeath = true;    // отключать физику после анимации

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Animator anim;
    private Collider2D col;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        col = GetComponent<Collider2D>();

        currentHealth = maxHealth;

        if (speechBubbleObj != null)
            speechBubbleObj.SetActive(false);

        // Проверка Animator на старте — сразу видно проблемы в Console
        if (anim == null)
        {
            Debug.LogError("[Knight] Animator не найден на объекте!", this);
        }
        else
        {
            int hash = Animator.StringToHash(deathStateName);
            if (!anim.HasState(0, hash))
            {
                Debug.LogWarning(
                    $"[Knight] Стейт '{deathStateName}' не найден в Layer 0 Animator. " +
                    $"Анимация смерти не будет играть, пока не создадите стейт с таким именем.", this);
            }
        }

        StartCoroutine(KnightRoutine());
    }

    IEnumerator KnightRoutine()
    {
        // 1. ДВИЖЕНИЕ ВПРАВО
        if (spriteRenderer != null) spriteRenderer.flipX = false;

        float startX = transform.position.x;
        float targetX = startX + moveDistance;

        if (anim != null) anim.SetFloat("Speed", moveSpeed);

        while (!isDead && Mathf.Abs(transform.position.x - targetX) > 0.05f)
        {
            rb.linearVelocity = new Vector2(moveSpeed, rb.linearVelocity.y);
            yield return null;
        }

        if (isDead) yield break;

        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (anim != null) anim.SetFloat("Speed", 0f);

        yield return new WaitForSeconds(0.4f);
        if (isDead) yield break;

        // 2. ПЕРВАЯ ФРАЗА
        yield return StartCoroutine(ShowPhraseRoutine(phrase1));
        if (isDead) yield break;

        // 3. РАЗВОРОТ
        if (spriteRenderer != null) spriteRenderer.flipX = true;
        yield return new WaitForSeconds(0.5f);
        if (isDead) yield break;

        // 4. ВТОРАЯ ФРАЗА
        yield return StartCoroutine(ShowPhraseRoutine(phrase2));
        if (isDead) yield break;

        yield return new WaitForSeconds(0.5f);
        if (isDead) yield break;

        // 5. ТРЕТЬЯ ФРАЗА
        yield return StartCoroutine(ShowPhraseRoutine(phrase3));
        if (isDead) yield break;

        if (speechBubbleObj != null)
            speechBubbleObj.SetActive(false);
    }

    IEnumerator ShowPhraseRoutine(string phraseToSay)
    {
        if (speechBubbleObj != null) speechBubbleObj.SetActive(true);
        if (speechText != null) speechText.text = phraseToSay;

        float timer = 0f;
        while (timer < phraseDisplayTime)
        {
            if (isDead) yield break;
            timer += Time.deltaTime;
            yield return null;
        }

        if (speechBubbleObj != null) speechBubbleObj.SetActive(false);
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        Debug.Log($"Рыцарь получил урон: {damage}. Осталось HP: {currentHealth}");

        if (currentHealth <= 0)
            Die();
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        StopAllCoroutines();

        Debug.Log("[Knight] Die() — запускаю анимацию смерти");

        if (speechBubbleObj != null)
            speechBubbleObj.SetActive(false);

        PlayDeathAnimation();

        // Останавливаем физику аккуратно, чтобы не сломать Animator
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            // Kinematic вместо simulated = false — Animator продолжит работать,
            // даже если updateMode = AnimatePhysics
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        StartCoroutine(DeathAnimationRoutine());
    }

    void PlayDeathAnimation()
    {
        if (anim == null)
        {
            Debug.LogWarning("[Knight] Animator отсутствует — нечего проигрывать", this);
            return;
        }

        // Обнуляем Speed ДО принудительного Play — иначе Idle/Walk может перебить
        anim.SetFloat("Speed", 0f);

        // Проверяем updateMode: если Animator привязан к физике, но rb кинематический —
        // это ОК, Animator продолжит работать сам по себе
        int hash = Animator.StringToHash(deathStateName);

        if (anim.HasState(0, hash))
        {
            // Принудительный Play — минует все переходы и приоритеты
            anim.Play(hash, 0, 0f);
            Debug.Log($"[Knight] Принудительно запущен стейт '{deathStateName}' через anim.Play()");
        }
        else
        {
            Debug.LogWarning(
                $"[Knight] Стейт '{deathStateName}' не найден в Layer 0. " +
                $"Пробую триггер 'Die'. Проверьте настройки Animator!", this);
            anim.SetTrigger("Die");
        }
    }

    IEnumerator DeathAnimationRoutine()
    {
        // Ждём пока анимация доиграет
        yield return new WaitForSeconds(deathAnimationDuration);

        if (disablePhysicsAfterDeath && rb != null)
        {
            // Физика уже kinematic (отключена в Die), дополнительно ничего не ломаем
            rb.linearVelocity = Vector2.zero;
        }

        if (col != null)
            col.enabled = false;

        // Отключаем скрипт, но НЕ Animator — иначе стейт смерти может сброситься
        // (если в Animator "Write Defaults" = off и стейт держит позу — стейт останется)
        enabled = false;
    }
}