using UnityEngine;
using TMPro; // Поддержка TextMeshPro для UI

public class GameManager : MonoBehaviour
{
    // Синглтон, чтобы GameManager был доступен отовсюду
    public static GameManager Instance { get; private set; }

    [Header("UI Интерфейс")]
    public TextMeshProUGUI trashCounterText; // Текст для отображения мусора слева сверху

    [Header("Настройки")]
    public int totalTrashCollected = 0;

    void Awake()
    {
        // Настройка синглтона
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        // Безопасный поиск объектов по исправленному тегу "Trash" (с большой буквы)
        GameObject[] allTrash = GameObject.FindGameObjectsWithTag("Trash");
        
        // Здесь можно выполнять какую-то логику проверки, если потребуется
    }

    // Метод для добавления очков мусора
    public void AddTrash(int amount = 1)
    {
        totalTrashCollected += amount;
        UpdateUI();
        Debug.Log("GameManager: Мусор собран! Всего: " + totalTrashCollected);
    }

    // Обновление текста на экране
    void UpdateUI()
    {
        if (trashCounterText != null)
        {
            trashCounterText.text = "Мусор: " + totalTrashCollected;
        }
    }
}