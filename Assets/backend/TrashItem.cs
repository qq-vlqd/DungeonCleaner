using UnityEngine;

public class TrashItem : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Проверяем, коснулся ли нас слайм
        if (collision.CompareTag("Player") || collision.GetComponent<SlimeController>() != null)
        {
            Debug.Log("Мусор подобран!");

            // Если хотите увеличивать счетчик в контроллере слайма:
            SlimeController slime = collision.GetComponent<SlimeController>() ?? collision.GetComponentInParent<SlimeController>();
            if (slime != null)
            {
                slime.collectedTrashCount++;
            }

            // Уничтожаем мусор
            Destroy(gameObject);
        }
    }
}