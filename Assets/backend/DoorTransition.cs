using UnityEngine;
using UnityEngine.SceneManagement;

public class DoorTransition : MonoBehaviour
{
    [Header("Название следующей сцены")]
    public string nextLevelName; // Точное название сцены (например, "Level2")

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Проверяем, что в зону триггера зашел именно игрок
        if (collision.CompareTag("Player"))
        {
            if (!string.IsNullOrEmpty(nextLevelName))
            {
                // Загружаем следующий уровень
                SceneManager.LoadScene(nextLevelName);
            }
            else
            {
                Debug.LogWarning("Не указано имя следующего уровня в инспекторе двери!");
            }
        }
    }
}