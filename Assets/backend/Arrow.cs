using UnityEngine;

public class Arrow : MonoBehaviour
{
    [Header("Настройки Стрелы")]
    public int damage = 20;
    public float speed = 12f;         
    public float lifeTime = 5f;       

    [Header("Префаб Мусора")]
    public GameObject trashPrefab;    // ВАЖНО: перетащите префаб сюда в инспекторе!

    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f; 
        }

        Destroy(gameObject, lifeTime);
    }

    void FixedUpdate()
    {
        if (rb != null)
        {
            rb.linearVelocity = transform.right * speed;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Archer") || collision.CompareTag("Arrow")) return;

        // Попадание в стены или пол
        if (collision.CompareTag("Ground") || collision.CompareTag("Wall"))
        {
            Destroy(gameObject);
            return;
        }

        // Попадание в Рыцаря
        KnightBot knight = collision.GetComponent<KnightBot>() ?? collision.GetComponentInParent<KnightBot>();
        if (knight != null || collision.CompareTag("Knight"))
        {
            if (knight != null)
            {
                knight.TakeArrowHit(); 
            }
            Destroy(gameObject);
            return;
        }

        // Попадание в Слайма (Игрока)
        if (collision.CompareTag("Player") || collision.name.Contains("Slime") || collision.name.Contains("Player"))
        {
            Debug.Log("УСПЕХ: Стрела попала в Слайма!");

            // 1. Всегда спавним мусор в точке попадания / позиции слайма
            if (trashPrefab != null)
            {
                Instantiate(trashPrefab, collision.transform.position, Quaternion.identity);
            }
            else
            {
                Debug.LogWarning("ВНИМАНИЕ: У стрелы не назначен Trash Prefab в Inspector!");
            }

            // 2. Наносим урон слайму через его контроллер
            SlimeController slime = collision.GetComponent<SlimeController>() ?? collision.GetComponentInParent<SlimeController>();
            if (slime != null)
            {
                slime.TakeDamage(damage);
            }
            else
            {
                Destroy(collision.gameObject);
            }

            // 3. Уничтожаем стрелу
            Destroy(gameObject);
            return;
        }
    }
}