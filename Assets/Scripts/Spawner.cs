
using UnityEngine;

public class Spawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject coin;
    public GameObject hazard;

    [Header("Spawn Settings")]
    public float spawnInterval = 3f;
    public float objectLifetime = 3f;

    [Header("Spawn Range")]
    public float minX = -9f;
    public float maxX = 9f;
    public float minZ = -9f;
    public float maxZ = 9f;
    public float spawnHeight = 5f;

    private Vector3 spawnPosition;

    void Start()
    {
        spawnPosition = transform.position;

        if (coin == null || hazard == null)
        {
            Debug.LogError("Assign Coin and Hazard!");
            enabled = false;
            return;
        }

        SpawnObject();
        InvokeRepeating(nameof(SpawnObject), spawnInterval, spawnInterval);
    }

    void SpawnObject()
    {
        // Independent 50/50 random chance EVERY spawn
        int randomChoice = Random.Range(0, 2);

        GameObject selectedObject =
            randomChoice == 0 ? coin : hazard;

        Vector3 randomPosition = spawnPosition;
        randomPosition.x = Random.Range(minX, maxX);
        randomPosition.z = Random.Range(minZ, maxZ);
        randomPosition.y += spawnHeight;

        GameObject spawnedObject = Instantiate(
            selectedObject,
            randomPosition,
            Quaternion.identity
        );

        Destroy(spawnedObject, objectLifetime);

        Collider col = spawnedObject.GetComponent<Collider>();

        if (col == null)
            col = spawnedObject.AddComponent<SphereCollider>();

        col.isTrigger = false;

        Rigidbody rb = spawnedObject.GetComponent<Rigidbody>();

        if (rb == null)
            rb = spawnedObject.AddComponent<Rigidbody>();

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        spawnedObject.AddComponent<SpawnedObjectHandler>()
            .Setup(selectedObject == coin);

        Debug.Log(selectedObject == coin
            ? "💰A Coin Spawned"
            : "☠️A Bomb Spawned!");
    }
}

public class SpawnedObjectHandler : MonoBehaviour
{
    private bool isCoin;
    private bool collected;

    public void Setup(bool coin)
    {
        isCoin = coin;
    }

    private void OnCollisionEnter(Collision collision)
    {
        playerscript player =
            collision.gameObject.GetComponentInParent<playerscript>();

        if (player == null || collected)
            return;

        collected = true;

        if (isCoin)
        {
            Debug.Log("COIN COLLECTED!");
            player.CollectCoin();
            Destroy(gameObject);
        }
        else
        {
            player.LoseLife();
            Destroy(gameObject);
        }
    }
}
