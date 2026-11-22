using System.Collections.Generic;
using UnityEngine;

public class RainSpawner : MonoBehaviour
{
    private const int MaximumRainAmount = 200;
    private const int MaximumPuddleCount = 128;

    private class ActivePuddle
    {
        public RainDrop Drop;
        public float TimeRemaining;
    }

    [Header("Rain")]
    [SerializeField] private RainDrop dropPrefab;
    [SerializeField, Range(0, MaximumRainAmount)] private int rainAmount = 80;
    [SerializeField, Min(0.01f)] private float spawnInterval = 0.02f;
    [SerializeField, Min(0.1f)] private float fallSpeed = 20f;

    [Header("Puddles")]
    [SerializeField, Range(0, MaximumPuddleCount)] private int maximumPuddles = 64;
    [SerializeField, Min(0.1f)] private float puddleLifetime = 10f;
    [SerializeField] private Vector3 puddleScale = new Vector3(2.5f, 0.08f, 2.5f);
    [SerializeField, Min(0f)] private float puddleSurfaceOffset = 0.01f;

    [Header("Spawn Area")]
    [SerializeField] private Vector2 spawnAreaSize = new Vector2(18f, 10f);
    [SerializeField, Min(0f)] private float spawnHeight = 15f;
    [SerializeField] private float groundHeight;

    private readonly Queue<RainDrop> pool = new Queue<RainDrop>();
    private readonly List<RainDrop> activeDrops = new List<RainDrop>();
    private readonly Queue<RainDrop> puddlePool = new Queue<RainDrop>();
    private readonly List<ActivePuddle> activePuddles = new List<ActivePuddle>();
    private float spawnTimer;

    private void Start()
    {
        if (dropPrefab == null)
        {
            Debug.LogError("Assign a raindrop prefab to RainSpawner.", this);
            enabled = false;
            return;
        }

        if (dropPrefab.GetComponent<RainDrop>() == null)
        {
            Debug.LogError("The raindrop prefab must have a RainDrop component.", dropPrefab);
            enabled = false;
        }
    }

    private void Update()
    {
        int targetAmount = Mathf.Clamp(rainAmount, 0, MaximumRainAmount);

        for (int i = activeDrops.Count - 1; i >= 0; i--)
        {
            if (activeDrops[i].Advance(Time.deltaTime, fallSpeed, groundHeight))
            {
                CreatePuddle(activeDrops[i].transform.position);
                RecycleAt(i);
            }
        }

        UpdatePuddles();

        while (activeDrops.Count > targetAmount)
            RecycleAt(activeDrops.Count - 1);

        if (activeDrops.Count >= targetAmount)
            return;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            SpawnDrop();
            spawnTimer = spawnInterval;
        }
    }

    private void SpawnDrop()
    {
        RainDrop drop = pool.Count > 0
            ? pool.Dequeue()
            : Instantiate(dropPrefab, transform);

        float halfWidth = Mathf.Max(0f, spawnAreaSize.x) * 0.5f;
        float halfDepth = Mathf.Max(0f, spawnAreaSize.y) * 0.5f;
        Vector3 position = transform.position + new Vector3(
            Random.Range(-halfWidth, halfWidth),
            spawnHeight,
            Random.Range(-halfDepth, halfDepth));

        drop.transform.position = position;
        drop.PrepareForReuse();
        drop.gameObject.SetActive(true);
        activeDrops.Add(drop);
    }

    private void RecycleAt(int index)
    {
        RainDrop drop = activeDrops[index];
        activeDrops.RemoveAt(index);
        drop.gameObject.SetActive(false);
        pool.Enqueue(drop);
    }

    private void CreatePuddle(Vector3 impactPosition)
    {
        int puddleLimit = Mathf.Clamp(maximumPuddles, 0, MaximumPuddleCount);
        if (puddleLimit == 0)
            return;

        RainDrop puddle;
        if (puddlePool.Count > 0)
        {
            puddle = puddlePool.Dequeue();
        }
        else if (activePuddles.Count < puddleLimit)
        {
            puddle = Instantiate(dropPrefab, transform);
        }
        else
        {
            ActivePuddle oldestPuddle = activePuddles[0];
            activePuddles.RemoveAt(0);
            puddle = oldestPuddle.Drop;
        }

        puddle.PrepareForReuse();
        puddle.ShowPuddle(puddleScale);
        puddle.transform.position = new Vector3(
            impactPosition.x,
            groundHeight + puddleSurfaceOffset,
            impactPosition.z);
        puddle.gameObject.SetActive(true);

        activePuddles.Add(new ActivePuddle
        {
            Drop = puddle,
            TimeRemaining = puddleLifetime
        });
    }

    private void UpdatePuddles()
    {
        for (int i = activePuddles.Count - 1; i >= 0; i--)
        {
            ActivePuddle puddle = activePuddles[i];
            puddle.TimeRemaining -= Time.deltaTime;

            if (puddle.TimeRemaining > 0f)
                continue;

            puddle.Drop.gameObject.SetActive(false);
            puddlePool.Enqueue(puddle.Drop);
            activePuddles.RemoveAt(i);
        }
    }
}
