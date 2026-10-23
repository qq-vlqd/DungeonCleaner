using UnityEngine;
using TMPro; // Подключаем работу с текстом
using UnityEngine.SceneManagement; // Подключаем перезапуск сцены

public class GameManager : MonoBehaviour
{
    public static GameManager Instance; // Ссылка для других скриптов

    public TextMeshProUGUI scoreText; // Текст для счета/мусора
    public TextMeshProUGUI coinsText; // Текст для монет
    public SlimeController player;    // Ссылка на контроллер слайма (нужна для прокачки)
    
    public int maxTrashAllowed = 10;   

    private int score = 0;
    private int coins = 0;            // Пересчет монет

    void Awake()
    {
        // Делаем этот скрипт главным и доступным отовсюду
        if (Instance == null) 
        {
            Instance = this;
        }
    }

    void Update()
    {
        // Проверяем, сколько объектов с тегом "trash" сейчас на сцене
        // Обратите внимание: регистр тега должен точно совпадать с тем, что в Unity!
        int currentTrashCount = GameObject.FindGameObjectsWithTag("trash").Length;

        // Если мусора слишком много — рестарт уровня
        if (currentTrashCount >= maxTrashAllowed)
        {
            GameOver();
        }
    }

    // Функция прибавления очков (вызывается из Слайма при уборке мусора)
    public void AddScore()
    {
        score++;
        if (scoreText != null)
        {
            scoreText.text = "Мусор: " + score;
        }
    }

    // Функция добавления монет (вызывается при сборе монет или крови)
    public void AddCoins(int amount)
    {
        coins += amount;
        if (coinsText != null)
        {
            coinsText.text = "Монеты: " + coins;
        }
    }

    void GameOver()
    {
        Debug.Log("Рыцарь задохнулся в мусоре! Конец игры.");
        // Перезапускаем текущую сцену геймплея
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Функция для магазина: покупка ускорения уборки мусора и крови
    public void BuyCleaningSpeedUpgrade(int cost)
    {
        if (coins >= cost)
        {
            coins -= cost;
            
            // Обновляем текст монет
            if (coinsText != null)
            {
                coinsText.text = "Монеты: " + coins;
            }

            // Увеличиваем скорость уборки у игрока
            if (player != null)
            {
                player.cleaningSpeedMultiplier += 0.3f; 
                Debug.Log("Скорость уборки улучшена! Текущий множитель: " + player.cleaningSpeedMultiplier);
            }
        }
        else
        {
            Debug.Log("Недостаточно монет для покупки!");
        }
    }
}