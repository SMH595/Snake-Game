using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    public static Spawner Instance { get; private set; }

    [Header("Prefabs")]
    public GameObject coin;
    public GameObject hazard;

    [Header("Coin Grid Settings")]
    [SerializeField] private int maxCoins = 1;
    [SerializeField] private float gridY = 0.5f;
    [SerializeField] private float minX = -9f;
    [SerializeField] private float maxX = 9f;
    [SerializeField] private float minZ = -9f;
    [SerializeField] private float maxZ = 9f;

    [Header("Hazards (Optional)")]
    [SerializeField] private bool spawnHazards = true;
    [SerializeField] private float hazardInterval = 6f;
    [SerializeField] private float hazardSpawnHeight = 6f;

    private readonly List<GameObject> activeCoins = new List<GameObject>();
    private playerscript player;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        player = FindFirstObjectByType<playerscript>();

        if (coin == null)
        {
            Debug.LogError("Assign Coin prefab to Spawner!", this);
            enabled = false;
            return;
        }

        // Spawn initial coins on the grid - coins DO NOT despawn!
        EnsureCoinsSpawned();

        if (spawnHazards && hazard != null)
        {
            InvokeRepeating(nameof(SpawnHazard), hazardInterval, hazardInterval);
        }
    }

    public void EnsureCoinsSpawned()
    {
        // Clean null references
        activeCoins.RemoveAll(c => c == null);

        while (activeCoins.Count < maxCoins)
        {
            SpawnCoinOnGrid();
        }
    }

    public void OnCoinCollected(GameObject coinObj)
    {
        activeCoins.Remove(coinObj);
        // Immediately spawn replacement coin in random place on grid
        EnsureCoinsSpawned();
    }

    public void SpawnCoinOnGrid()
    {
        if (coin == null)
            return;

        Vector3 gridPos = GetRandomGridPosition();

        GameObject spawnedCoin = Instantiate(coin, gridPos, Quaternion.identity);
        spawnedCoin.name = "Coin (Grid)";

        // Configure physics: kinematic so it sits on grid and doesn't fall/slide
        Rigidbody rb = spawnedCoin.GetComponent<Rigidbody>();
        if (rb == null)
            rb = spawnedCoin.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // Configure collider as trigger for smooth collection
        Collider col = spawnedCoin.GetComponent<Collider>();
        if (col == null)
            col = spawnedCoin.AddComponent<SphereCollider>();
        col.isTrigger = true;

        // Handler to manage collection and minimap tracking
        SpawnedObjectHandler handler = spawnedCoin.GetComponent<SpawnedObjectHandler>();
        if (handler == null)
            handler = spawnedCoin.AddComponent<SpawnedObjectHandler>();
        handler.Setup(true, this);

        // NOTE: No Destroy(spawnedCoin, lifetime) — coins NEVER despawn!
        activeCoins.Add(spawnedCoin);
        Debug.Log($"💰 Coin spawned on grid at {gridPos}");
    }

    private void SpawnHazard()
    {
        if (hazard == null)
            return;

        Vector3 randomPosition = transform.position;
        randomPosition.x = Random.Range(minX, maxX);
        randomPosition.z = Random.Range(minZ, maxZ);
        randomPosition.y += hazardSpawnHeight;

        GameObject spawnedHazard = Instantiate(hazard, randomPosition, Quaternion.identity);

        Collider col = spawnedHazard.GetComponent<Collider>();
        if (col == null)
            col = spawnedHazard.AddComponent<SphereCollider>();
        col.isTrigger = false;

        Rigidbody rb = spawnedHazard.GetComponent<Rigidbody>();
        if (rb == null)
            rb = spawnedHazard.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;

        SpawnedObjectHandler handler = spawnedHazard.GetComponent<SpawnedObjectHandler>();
        if (handler == null)
            handler = spawnedHazard.AddComponent<SpawnedObjectHandler>();
        handler.Setup(false, this);

        Debug.Log("☠️ A Hazard Spawned!");
    }

    private Vector3 GetRandomGridPosition()
    {
        int minGridX = Mathf.RoundToInt(minX);
        int maxGridX = Mathf.RoundToInt(maxX);
        int minGridZ = Mathf.RoundToInt(minZ);
        int maxGridZ = Mathf.RoundToInt(maxZ);

        if (player == null)
            player = FindFirstObjectByType<playerscript>();

        Vector3 bestPos = new Vector3(
            Random.Range(minGridX, maxGridX + 1),
            gridY,
            Random.Range(minGridZ, maxGridZ + 1)
        );

        for (int attempt = 0; attempt < 30; attempt++)
        {
            int rx = Random.Range(minGridX, maxGridX + 1);
            int rz = Random.Range(minGridZ, maxGridZ + 1);
            Vector3 candidate = new Vector3(rx, gridY, rz);

            // Don't spawn on or right in front of player
            if (player != null && Vector3.Distance(candidate, player.transform.position) < 1.5f)
                continue;

            // Don't spawn inside body segments
            if (player != null && player.BodySegments != null)
            {
                bool insideSegment = false;
                for (int s = 0; s < player.BodySegments.Count; s++)
                {
                    if (player.BodySegments[s] != null && Vector3.Distance(candidate, player.BodySegments[s].position) < 1f)
                    {
                        insideSegment = true;
                        break;
                    }
                }
                if (insideSegment)
                    continue;
            }

            // Don't spawn on top of an existing coin
            bool tooCloseToCoin = false;
            for (int i = 0; i < activeCoins.Count; i++)
            {
                if (activeCoins[i] != null && Vector3.Distance(candidate, activeCoins[i].transform.position) < 0.8f)
                {
                    tooCloseToCoin = true;
                    break;
                }
            }
            if (tooCloseToCoin)
                continue;

            return candidate;
        }

        return bestPos;
    }
}

public class SpawnedObjectHandler : MonoBehaviour
{
    private static readonly List<SpawnedObjectHandler> activeObjects =
        new List<SpawnedObjectHandler>();

    private bool isCoin;
    private bool collected;
    private Spawner spawner;

    public static IReadOnlyList<SpawnedObjectHandler> ActiveObjects => activeObjects;
    public bool IsCoin => isCoin;

    private void OnEnable()
    {
        activeObjects.Add(this);
    }

    private void OnDisable()
    {
        activeObjects.Remove(this);
    }

    public void Setup(bool coin, Spawner parentSpawner = null)
    {
        isCoin = coin;
        spawner = parentSpawner != null ? parentSpawner : Spawner.Instance;
    }

    private void Update()
    {
        if (isCoin)
        {
            // Gentle continuous coin spin
            transform.Rotate(Vector3.up * 90f * Time.deltaTime, Space.World);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        CheckInteraction(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        CheckInteraction(collision.gameObject);
    }

    private void CheckInteraction(GameObject hitObject)
    {
        if (collected)
            return;

        playerscript player = hitObject.GetComponentInParent<playerscript>();
        if (player == null)
            return;

        collected = true;

        if (isCoin)
        {
            Debug.Log("COIN COLLECTED!");
            player.CollectCoin();
            if (spawner != null)
            {
                spawner.OnCoinCollected(gameObject);
            }
            Destroy(gameObject);
        }
        else
        {
            player.LoseLife();
            Destroy(gameObject);
        }
    }
}
