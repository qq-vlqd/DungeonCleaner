using UnityEngine;

public class Arrow : MonoBehaviour
{
    [Header("Настройки Стрелы")]
    public int damage = 20;
    public float speed = 15f;        
    public float lifeTime = 5f;      

    [Header("Префаб Мусора")]
    public GameObject trashPrefab;   // Перетащите префаб мусора сюда в инспекторе!

    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f; 
            
            // ВАЖНО: Сразу задаем скорость при создании, чтобы стрела не висела на месте!
            rb.linearVelocity = transform.right * speed;
        }
        else
        {
            Debug.LogError("ОШИБКА: У стрелы нет компонента Rigidbody2D!");
        }

        // Уничтожаем стрелу через время lifeTime, если никуда не попала
        Destroy(gameObject, lifeTime);
    }

    void FixedUpdate()
    {
        // Поддерживаем скорость постоянной каждый кадр физики
        if (rb != null)
        {
            rb.linearVelocity = transform.right * speed;
        }
    }

    // Обработка столкновений (поддерживает и Trigger, и обычный Collider)
    private void OnTriggerEnter2D(Collider2D collision)
    {
        HandleCollision(collision.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleCollision(collision.gameObject);
    }

    void HandleCollision(GameObject hitObject)
    {
        // Игнорируем самого скелета-лучника и другие стрелы
        if (hitObject.CompareTag("Archer") || hitObject.CompareTag("Arrow") || hitObject.name.Contains("Skeleton")) return;

        // 1. Попадание в стены или пол (Ground / Wall)
        if (hitObject.CompareTag("Ground") || hitObject.CompareTag("Wall") || hitObject.layer == LayerMask.NameToLayer("Ground"))
        {
            Destroy(gameObject);
            return;
        }

        // 2. Попадание в Рыцаря
        KnightBot knight = hitObject.GetComponent<KnightBot>() ?? hitObject.GetComponentInParent<KnightBot>();
        if (knight != null || hitObject.CompareTag("Knight"))
        {
            if (knight != null)
            {
                knight.TakeArrowHit(); 
            }
            Destroy(gameObject);
            return;
        }

        // 3. Попадание в Слайма (Игрока)
        if (hitObject.CompareTag("Player") || hitObject.name.Contains("Slime") || hitObject.name.Contains("Player"))
        {
            Debug.Log("УСПЕХ: Стрела попала в Слайма!");

            // Спавним мусор
            if (trashPrefab != null)
            {
                Instantiate(trashPrefab, hitObject.transform.position, Quaternion.identity);
            }

            // Наносим урон
            SlimeController slime = hitObject.GetComponent<SlimeController>() ?? hitObject.GetComponentInParent<SlimeController>();
            if (slime != null)
            {
                slime.TakeDamage(damage);
            }
            else
            {
                Destroy(hitObject);
            }

            Destroy(gameObject);
            return;
        }
    }
}