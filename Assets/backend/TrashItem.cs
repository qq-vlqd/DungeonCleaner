using UnityEngine;

public class TrashItem : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        SlimeController slime = other.GetComponent<SlimeController>();

        if (slime != null)
        {
            slime.AddTrash(1);
            Debug.Log("Мусор подобран!");
            Destroy(gameObject);
        }
    }
}