using UnityEngine;

public class ArcherBot : MonoBehaviour
{
    public GameObject arrowPrefab;  // Префаб стрелы
    public Transform shootPoint;    // Точка, откуда вылетает стрела (лучше создать пустой дочерний объект перед лучником)
    public float shootInterval = 2f; // Интервал между выстрелами
    public float detectionRange = 8f; // Дальность обнаружения Слайма

    private Transform slimeTransform;
    private float shootTimer = 0f;

    void Start()
    {
        // Автоматически находим Слайм по тегу или через GameManager
        GameObject slimeObj = GameObject.FindGameObjectWithTag("Player");
        if (slimeObj != null)
        {
            slimeTransform = slimeObj.transform;
        }
    }

    void Update()
    {
        if (slimeTransform == null) return;

        // Проверяем расстояние до Слайма
        float distanceToSlime = Vector2.Distance(transform.position, slimeTransform.position);

        if (distanceToSlime <= detectionRange)
        {
            shootTimer += Time.deltaTime;

            if (shootTimer >= shootInterval)
            {
                Shoot();
                shootTimer = 0f;
            }
        }
    }

    void Shoot()
    {
        if (arrowPrefab != null && shootPoint != null)
        {
            // Создаем стрелу в точке выстрела
            GameObject arrowObj = Instantiate(arrowPrefab, shootPoint.position, Quaternion.identity);
            Arrow arrowScript = arrowObj.GetComponent<Arrow>();

            if (arrowScript != null)
            {
                // Вычисляем направление от лучника к Слайму
                Vector2 shootDirection = (slimeTransform.position - shootPoint.position).normalized;
                arrowScript.SetDirection(shootDirection);
            }
        }
    }
}