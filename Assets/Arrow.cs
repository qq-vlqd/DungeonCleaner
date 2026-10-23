using UnityEngine;

public class Arrow : MonoBehaviour
{
    public float speed = 10f;
    public int damage = 1;
    public GameObject trashDropPrefab; // Мусор при попадании в цель
    private Vector2 direction;
    private bool hasHit = false;

    public void SetDirection(Vector2 dir)
    {
        direction = dir.normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle));
    }

    void Update()
    {
        if (!hasHit)
        {
            transform.Translate(Vector3.right * speed * Time.deltaTime);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;

        // Попадание в Слайма
        if (other.CompareTag("Player") || other.CompareTag("Slime"))
        {
            hasHit = true;
            SlimeController slime = other.GetComponent<SlimeController>();
            if (slime != null)
            {
                slime.TakeDamage(damage);
            }

            if (trashDropPrefab != null)
            {
                Instantiate(trashDropPrefab, transform.position, Quaternion.identity);
            }

            Destroy(gameObject);
        }
        // Попадание в РЫЦАРЯ — выбивает из него мусор!
        else if (other.CompareTag("Knight"))
        {
            hasHit = true;
            KnightBot knight = other.GetComponent<KnightBot>();
            if (knight != null)
            {
                knight.TakeArrowHit(); 
            }
            Destroy(gameObject);
        }
        // Попадание в Скелета (игнорируем, чтобы свои стрелы не ранили скелета)
        else if (other.CompareTag("Archer"))
        {
            return; 
        }
        // Попадание в стены / землю
        else if (other.CompareTag("Ground") || other.CompareTag("trash"))
        {
            hasHit = true;
            Destroy(gameObject);
        }
    }
}