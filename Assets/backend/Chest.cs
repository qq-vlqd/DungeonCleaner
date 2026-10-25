using UnityEngine;

public class Chest : MonoBehaviour
{
    [Header("Настройки сундука")]
    public GameObject trashPrefab;      // Префаб мусора, который будет вылетать
    public int minTrash = 3;            // Минимальное количество предметов мусора
    public int maxTrash = 6;            // Максимальное количество предметов мусора
    public Transform spawnPoint;        // Точка, откуда вылетает мусор (если пусто, то из центра сундука)
    
    private bool isOpen = false;
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    // Метод для открытия сундука
    public void OpenChest()
    {
        if (isOpen) return;
        isOpen = true;

        // 1. Включаем анимацию открытия сундука (если в Animator есть триггер "Open")
        if (animator != null)
        {
            animator.SetTrigger("Open");
        }

        // 2. Спавним мусор с разлетом
        SpawnTrash();
    }

    void SpawnTrash()
    {
        if (trashPrefab == null) return;

        int trashCount = Random.Range(minTrash, maxTrash + 1);
        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;

        for (int i = 0; i < trashCount; i++)
        {
            // Небольшой случайный сдвиг позиции, чтобы предметы не спавнились в одной точке
            Vector3 offset = new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0f, 0.2f), 0f);
            GameObject droppedTrash = Instantiate(trashPrefab, spawnPos + offset, Quaternion.identity);

            // Если у префаба мусора есть Rigidbody2D, придаем ему импульс разлета в стороны
            Rigidbody2D rb = droppedTrash.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                float randomX = Random.Range(-3f, 3f); // Разлет вбок
                float randomY = Random.Range(4f, 7f);  // Подлет вверх
                rb.linearVelocity = new Vector2(randomX, randomY);
            }
        }
    }

    // Сундук открывается при касании игрока или рыцаря
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isOpen && (collision.CompareTag("Player") || collision.CompareTag("Knight")))
        {
            OpenChest();
        }
    }
}