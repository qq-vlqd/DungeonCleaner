using UnityEngine;

public class SlimeController : MonoBehaviour
{
    public float speed = 6f;        // Скорость бега
    public float jumpForce = 8f;    // Сила прыжка
    private Rigidbody2D rb;
    private bool isGrounded;        // Стоит ли на земле

    void Start()
    {
        // Находим физический компонент
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // Движение по оси X (влево-вправо)
        float moveX = Input.GetAxisRaw("Horizontal");
        rb.linearVelocity = new Vector2(moveX * speed, rb.linearVelocity.y); 

        // Прыжок на Пробел (или стрелочку вверх / W)
        if ((Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W)) && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            isGrounded = false; // Улетел с земли
        }
    }

    // Проверка: коснулся ли Слайм твердой земли/платформы
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Если столкнулись с чем-то твердым (не мусором)
        if (!collision.gameObject.CompareTag("trash"))
        {
            isGrounded = true;
        }
    }

    // Сбор мусора (работает через триггер)
private void OnTriggerEnter2D(Collider2D other)
{
    // Проверяем тег с маленькой буквы, как у тебя
    if (other.CompareTag("trash")) 
    {
        // Проверяем, не удаляется ли уже этот мусор (чтобы не было двойного очка)
        if (other.gameObject.GetComponent<Collider2D>().enabled)
        {
            // Выключаем ему коллайдер мгновенно, чтобы второй раз код не сработал
            other.gameObject.GetComponent<Collider2D>().enabled = false; 

            Destroy(other.gameObject); // Уничтожаем мусор
            GameManager.Instance.AddScore(); // Прибавляем ровно 1 очко!
        }
    }
}

}