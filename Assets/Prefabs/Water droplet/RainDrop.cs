using UnityEngine;

public class RainDrop : MonoBehaviour
{
    private Rigidbody body;
    private Collider[] colliders;
    private TrailRenderer[] trails;
    private bool initialized;
    private Vector3 defaultScale;
    private Quaternion defaultRotation;

    private void Awake()
    {
        CacheComponents();
    }

    private void CacheComponents()
    {
        if (initialized)
            return;

        body = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>(true);
        trails = GetComponentsInChildren<TrailRenderer>(true);
        defaultScale = transform.localScale;
        defaultRotation = transform.rotation;

        if (body != null)
        {
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = false;
        }

        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        initialized = true;
    }

    public void PrepareForReuse()
    {
        CacheComponents();
        enabled = true;
        transform.localScale = defaultScale;
        transform.rotation = defaultRotation;

        for (int i = 0; i < trails.Length; i++)
            trails[i].Clear();
    }

    public void ShowPuddle(Vector3 scale)
    {
        CacheComponents();
        enabled = false;
        transform.localScale = Vector3.Scale(defaultScale, scale);
        transform.rotation = defaultRotation * Quaternion.Euler(90f, 0f, 0f);

        for (int i = 0; i < trails.Length; i++)
            trails[i].Clear();
    }

    public bool Advance(float deltaTime, float fallSpeed, float groundHeight)
    {
        transform.position += Vector3.down * (fallSpeed * deltaTime);
        return transform.position.y <= groundHeight;
    }
}
