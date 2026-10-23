using UnityEngine;

public class KnightBot : MonoBehaviour
{
    public float speed = 2f; 
    public GameObject trashPrefab; 
    public float spawnInterval = 1.5f; 

    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        // Спавн мусора
        InvokeRepeating("DropTrash", spawnInterval, spawnInterval);
    }

    void FixedUpdate()
    {
        // Двигаем рыцаря по физике направо, сохраняя его текущую скорость падения по Y
        rb.linearVelocity = new Vector2(speed, rb.linearVelocity.y);
    }

    void DropTrash()
    {
        if (trashPrefab != null)
        {
            Instantiate(trashPrefab, transform.position, Quaternion.identity);
        }
    }
}
