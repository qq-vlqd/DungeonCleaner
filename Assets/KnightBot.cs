using UnityEngine;

public class KnightBot : MonoBehaviour
{
    public float speed = 2f; 
    public GameObject trashPrefab; 
    public GameObject bloodPrefab; 
    public float spawnInterval = 1.5f; 
    
    public bool isBoss = false; 
    public int bossHP = 100; // Убедитесь, что эта переменная есть

    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        InvokeRepeating("DropGore", spawnInterval, spawnInterval);
    }

    void FixedUpdate()
    {
        if (!isBoss)
        {
            rb.linearVelocity = new Vector2(speed, rb.linearVelocity.y);
        }
        else
        {
            rb.linearVelocity = new Vector2(Mathf.Sin(Time.time) * speed, rb.linearVelocity.y);
        }
    }

    void DropGore()
    {
        if (Random.value > 0.3f)
        {
            if (trashPrefab != null) Instantiate(trashPrefab, transform.position, Quaternion.identity);
        }
        else
        {
            if (bloodPrefab != null) Instantiate(bloodPrefab, transform.position, Quaternion.identity);
        }
    }

    // <-- ДОБАВЬТЕ ЭТОТ МЕТОД СЮДА
    public void TakeDamage(int damage)
    {
        bossHP -= damage;
        Debug.Log("Рыцарь получил урон! HP: " + bossHP);
        
        if (bossHP <= 0)
        {
            WinGame();
        }
    }

    void WinGame()
    {
        Debug.Log("Рыцарь повержен! Слайм надевает доспехи.");
        CancelInvoke("DropGore");
        Destroy(gameObject);
    }
}