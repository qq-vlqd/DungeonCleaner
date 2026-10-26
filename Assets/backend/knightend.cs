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
    public GameObject speechBubbleObj;          // Ссылка на Canvas с облачком
    public TextMeshProUGUI speechText;          // Текст (TextMeshPro)
    
    [TextArea(1, 3)] public string phrase1 = "Я пройду немного вправо...";
    [TextArea(1, 3)] public string phrase2 = "А теперь разворачиваюсь назад!";
    [TextArea(1, 3)] public string phrase3 = "И это моя финальная реплика!";
    
    public float phraseDisplayTime = 3f;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Animator anim;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();

        currentHealth = maxHealth;

        if (speechBubbleObj != null)
        {
            speechBubbleObj.SetActive(false);
        }

        // Запускаем последовательность действий
        StartCoroutine(KnightRoutine());
    }

    IEnumerator KnightRoutine()
    {
        // 1. ДВИЖЕНИЕ ВПРАВО
        if (spriteRenderer != null) spriteRenderer.flipX = false; 
        
        float startX = transform.position.x;
        float targetX = startX + moveDistance;

        // Включаем анимацию ходьбы через параметр "Speed"
        if (anim != null) anim.SetFloat("Speed", moveSpeed);

        // Двигаемся, пока не достигнем точки или пока рыцарь не умрет
        while (!isDead && Mathf.Abs(transform.position.x - targetX) > 0.05f)
        {
            rb.linearVelocity = new Vector2(moveSpeed, rb.linearVelocity.y);
            yield return null;
        }

        if (isDead) yield break;

        // Останавливаемся и выключаем анимацию ходьбы
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (anim != null) anim.SetFloat("Speed", 0f);

        yield return new WaitForSeconds(0.4f);
        if (isDead) yield break;

        // 2. ПЕРВАЯ ФРАЗА
        yield return StartCoroutine(ShowPhraseRoutine(phrase1));
        if (isDead) yield break;

        // 3. РАЗВОРОТ В ОБРАТНУЮ СТОРОНУ (НАЛЕВО)
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

        // Скрываем облачко по окончании всей последовательности
        if (speechBubbleObj != null)
        {
            speechBubbleObj.SetActive(false);
        }
    }

    // Вспомогательная короутина для показа фраз с проверкой здоровья
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

    // Метод получения урона
    public void TakeDamage(int damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        Debug.Log($"Рыцарь получил урон: {damage}. Осталось HP: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // Логика смерти: объект просто мгновенно удаляется со сцены
    void Die()
    {
        if (isDead) return; 
        isDead = true;
        StopAllCoroutines(); 

        Debug.Log("[Knight] Die() — рыцарь уничтожен и пропал.");

        // Удаляем объект рыцаря из игры
        Destroy(gameObject);
    }
}