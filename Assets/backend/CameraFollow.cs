using UnityEngine;

// Вешается на Main Camera. Плавно следует за игроком.
public class CameraFollow : MonoBehaviour
{
    [Header("Цель")]
    public Transform target;                 // если пусто — найдёт объект с тегом Player
    public Vector2 offset = new Vector2(0f, 1f);

    [Header("Плавность")]
    public float smoothTime = 0.15f;         // 0 = жёсткая привязка без сглаживания

    [Header("Мёртвая зона (камера не дёргается при мелких движениях)")]
    public Vector2 deadZone = new Vector2(0.5f, 0.3f);

    [Header("Границы уровня (по желанию)")]
    public bool useBounds = false;
    public Vector2 minPosition = new Vector2(-10f, -5f);
    public Vector2 maxPosition = new Vector2(100f, 10f);

    private Vector3 velocity;
    private float fixedZ;

    void Start()
    {
        fixedZ = transform.position.z;       // у 2D-камеры z (например -10) не должен меняться
        FindTarget();

        if (target != null)                  // сразу встаём на игрока, без «пролёта» в начале
        {
            Vector3 p = target.position + (Vector3)offset;
            p.z = fixedZ;
            transform.position = Clamp(p);
        }
    }

    void LateUpdate()                        // после того как игрок подвинулся
    {
        if (target == null) { FindTarget(); return; }

        Vector3 cam = transform.position;
        Vector3 want = target.position + (Vector3)offset;

        // двигаем камеру, только если игрок вышел из мёртвой зоны
        float dx = want.x - cam.x;
        float dy = want.y - cam.y;
        float goalX = Mathf.Abs(dx) > deadZone.x ? want.x - Mathf.Sign(dx) * deadZone.x : cam.x;
        float goalY = Mathf.Abs(dy) > deadZone.y ? want.y - Mathf.Sign(dy) * deadZone.y : cam.y;
        Vector3 goal = new Vector3(goalX, goalY, fixedZ);

        Vector3 next = smoothTime > 0f
            ? Vector3.SmoothDamp(cam, goal, ref velocity, smoothTime)
            : goal;

        next.z = fixedZ;
        transform.position = Clamp(next);
    }

    Vector3 Clamp(Vector3 p)
    {
        if (!useBounds) return p;
        p.x = Mathf.Clamp(p.x, minPosition.x, maxPosition.x);
        p.y = Mathf.Clamp(p.y, minPosition.y, maxPosition.y);
        return p;
    }

    void FindTarget()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) target = p.transform;
    }
}
