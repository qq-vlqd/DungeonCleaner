using UnityEngine;

public class LadderArea : MonoBehaviour
{
    public float climbSpeed = 3f; // Скорость подъема по лестнице

    private void OnTriggerStay2D(Collider2D collision)
    {
        // 1. ПОДЪЕМ РЫЦАРЯ (Автоматический)
        if (collision.CompareTag("Knight"))
        {
            Rigidbody2D knightRb = collision.GetComponent<Rigidbody2D>();
            if (knightRb != null)
            {
                // Принудительно толкаем рыцаря вверх по лестнице
                knightRb.linearVelocity = new Vector2(knightRb.linearVelocity.x, climbSpeed);
            }
        }

        // 2. ПОДЪЕМ СЛАЙМА / ИГРОКА (По нажатию клавиш W / S или стрелок)
        if (collision.CompareTag("Player"))
        {
            Rigidbody2D playerRb = collision.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                float verticalInput = Input.GetAxisRaw("Vertical"); // W / S или Стрелки вверх/вниз
                
                if (Mathf.Abs(verticalInput) > 0.1f)
                {
                    // Игрок поднимается или спускается
                    playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, verticalInput * climbSpeed);
                    playerRb.gravityScale = 0f; // Отключаем гравитацию, чтобы не сползать
                }
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        // Когда Игрок выходит из зоны лестницы — возвращаем нормальную гравитацию
        if (collision.CompareTag("Player"))
        {
            Rigidbody2D playerRb = collision.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                playerRb.gravityScale = 1f; // Или ваша стандартная гравитация слайма
            }
        }
    }
}