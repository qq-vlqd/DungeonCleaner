using UnityEngine;
using TMPro; // Подключаем работу с текстом
using UnityEngine.SceneManagement; // Подключаем перезапуск сцены

public class GameManager : MonoBehaviour
{
    public static GameManager Instance; // Ссылка для других скриптов

    public TextMeshProUGUI scoreText; // Сюда закинем наш текст
    public int maxTrashAllowed = 10;   // Если на уровне станет больше 10 мусора — проигрыш

    private int score = 0;

    void Awake()
    {
        // Делаем этот скрипт главным и доступным отовсюду
        if (Instance == null) Instance = this;
    }

    void Update()
    {
        // Проверяем, сколько объектов с тегом "Trash" сейчас на сцене
        int currentTrashCount = GameObject.FindGameObjectsWithTag("Trash").Length;

        // Если Слайм обленился и мусора слишком много — рестарт уровня
        if (currentTrashCount >= maxTrashAllowed)
        {
            GameOver();
        }
    }

    // Функция прибавления очков (вызывается из Слайма)
    public void AddScore()
    {
        score++;
        scoreText.text = "Мусор: " + score;
    }

    void GameOver()
    {
        Debug.Log("Рыцарь задохнулся в мусоре! Конец игры.");
        // Перезапускаем текущую сцену геймплея
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
