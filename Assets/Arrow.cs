using UnityEngine;

public class Arrow : MonoBehaviour
{
    public float speed = 10f;
    public int damage = 1;
    private Vector2 direction;

    // Метод задает направление полета стрелы при спавне
    public void SetDirection(Vector2 dir)
    {
        direction = dir.normalized;
        
        // Поворачиваем стрелу в сторону полета (опционально для 2D)
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle));
    }

    void Update()
    {
        // Двигаем стрелу прямо в заданном направлении
        transform.Translate(Vector3.right * speed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Если стрела попала в Слайма
        if (other.CompareTag("Player") || other.CompareTag("Slime"))
        {
            SlimeController slime = other.GetComponent<SlimeController>();
            if (slime != null)
            {
                slime.TakeDamage(damage);
            }
            Destroy(gameObject); // Уничтожаем стрелу при попадании
        }
        // Если попала в стены, пол или мусор — тоже уничтожаем
        else if (other.CompareTag("Ground") || other.CompareTag("trash"))
        {
            Destroy(gameObject);
        }
    }
}