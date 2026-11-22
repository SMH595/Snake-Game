using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class playerscript : MonoBehaviour
{
    private const int StartingLives = 3;
    private const float SegmentSpacing = 0.9f;

    [Header("Movement")]
    public float speed = 5f;
    [SerializeField] private float segmentFollowSpeed = 8f;
    [SerializeField] private float turnSpeed = 540f;
    [SerializeField] private float segmentTurnSpeed = 12f;
    [SerializeField] private float segmentSize = 0.8f;
    [SerializeField] private float hazardInvulnerability = 1f;
    [SerializeField] private float minX = -9f;
    [SerializeField] private float maxX = 9f;
    [SerializeField] private float minZ = -9f;
    [SerializeField] private float maxZ = 9f;

    [Header("Camera")]
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 8f, -10f);
    [SerializeField] private Vector3 cameraLookOffset = new Vector3(0f, 0f, 2f);

    [Header("UI")]
    [SerializeField] private CoinCollectedUI coinCollectedUI;

    [Header("Audio")]
    [SerializeField] private AudioClip coinSound;
    [SerializeField] private float coinSoundDuration = 2f;

    private Rigidbody rb;
    private Collider playerCollider;
    private Vector3 movementDirection = Vector3.forward;
    private Camera mainCamera;
    private int livesRemaining = StartingLives;
    private readonly List<Transform> bodySegments = new List<Transform>();
    private Vector3 startingPosition;
    private float invulnerabilityRemaining;
    private bool gameOver;

    private AudioSource audioSource;
    private AudioClip trimmedCoinSound;

    public IReadOnlyList<Transform> BodySegments => bodySegments;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerCollider = GetComponent<Collider>();
        rb.constraints &= ~RigidbodyConstraints.FreezeRotation;
        rb.constraints |= RigidbodyConstraints.FreezeRotationX |
                          RigidbodyConstraints.FreezeRotationZ |
                          RigidbodyConstraints.FreezePositionY;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.useGravity = false;
        startingPosition = transform.position;
        startingPosition.y = 0.5f;
        startingPosition.z = 0f;
        transform.position = startingPosition;
        mainCamera = Camera.main;

        if (coinCollectedUI == null)
            coinCollectedUI = FindFirstObjectByType<CoinCollectedUI>();

        if (coinCollectedUI != null)
        {
            coinCollectedUI.SetLives(livesRemaining);
            coinCollectedUI.SetLength(1);
        }

        // Setup AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D sound for consistent volume

#if UNITY_EDITOR
        if (coinSound == null)
        {
            coinSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/CoinCollect.mp3");
        }
#endif

        PrepareTrimmedSound();

        MiniMapUI miniMap = GetComponent<MiniMapUI>();
        if (miniMap == null)
            miniMap = gameObject.AddComponent<MiniMapUI>();
        miniMap.Initialize(this, minX, maxX, minZ, maxZ);
    }

    private void PrepareTrimmedSound()
    {
        if (coinSound == null)
            return;

        // If clip is longer than coinSoundDuration, create a 2-second subclip in memory
        if (coinSound.length > coinSoundDuration + 0.1f)
        {
            try
            {
                int sampleCount = Mathf.Min((int)(coinSound.frequency * coinSoundDuration), coinSound.samples);
                int totalElements = sampleCount * coinSound.channels;
                float[] data = new float[totalElements];
                if (coinSound.GetData(data, 0))
                {
                    trimmedCoinSound = AudioClip.Create("Coin_Trimmed_2s", sampleCount, coinSound.channels, coinSound.frequency, false);
                    trimmedCoinSound.SetData(data, 0);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("Could not create trimmed clip in memory: " + ex.Message);
            }
        }
        else
        {
            trimmedCoinSound = coinSound;
        }
    }

    public void CollectCoin()
    {
        if (coinCollectedUI == null)
            coinCollectedUI = FindFirstObjectByType<CoinCollectedUI>();

        if (coinCollectedUI != null)
            coinCollectedUI.AddCoin();

        AddBodySegment();
        PlayCoinSound();
    }

    private void PlayCoinSound()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            return;

        AudioClip clipToPlay = trimmedCoinSound != null ? trimmedCoinSound : coinSound;
        if (clipToPlay == null)
            return;

        if (trimmedCoinSound != null)
        {
            audioSource.PlayOneShot(trimmedCoinSound);
        }
        else
        {
            // Direct playback with 2-second stop safeguard
            audioSource.Stop();
            audioSource.time = 0f;
            audioSource.clip = clipToPlay;
            audioSource.Play();
            CancelInvoke(nameof(StopCoinSound));
            Invoke(nameof(StopCoinSound), coinSoundDuration);
        }
    }

    private void StopCoinSound()
    {
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }

    public void LoseLife()
    {
        if (gameOver || invulnerabilityRemaining > 0f)
            return;

        livesRemaining--;
        invulnerabilityRemaining = hazardInvulnerability;

        if (coinCollectedUI != null)
            coinCollectedUI.SetLives(livesRemaining);

        if (livesRemaining == 0)
        {
            Debug.Log("GAME OVER!");
            gameOver = true;
            rb.isKinematic = true;
            enabled = false;
            MeshRenderer headRenderer = GetComponent<MeshRenderer>();
            if (headRenderer != null)
                headRenderer.enabled = false;

            for (int i = bodySegments.Count - 1; i >= 0; i--)
            {
                if (bodySegments[i] != null)
                    Destroy(bodySegments[i].gameObject);
            }

            bodySegments.Clear();
        }
        else
        {
            Debug.Log($"PLAYER HIT! {livesRemaining} lives remaining.");
            RemoveTailSegment();
            rb.position = startingPosition;
            transform.position = startingPosition;
            movementDirection = Vector3.forward;
            rb.rotation = Quaternion.identity;
        }
    }

    void Update()
    {
        invulnerabilityRemaining = Mathf.Max(0f, invulnerabilityRemaining - Time.deltaTime);

        Vector3 newDir = Vector3.zero;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.wasPressedThisFrame ||
                Keyboard.current.leftArrowKey.wasPressedThisFrame)
                newDir = Vector3.left;
            else if (Keyboard.current.dKey.wasPressedThisFrame ||
                     Keyboard.current.rightArrowKey.wasPressedThisFrame)
                newDir = Vector3.right;
            else if (Keyboard.current.wKey.wasPressedThisFrame ||
                     Keyboard.current.upArrowKey.wasPressedThisFrame)
                newDir = Vector3.forward;
            else if (Keyboard.current.sKey.wasPressedThisFrame ||
                     Keyboard.current.downArrowKey.wasPressedThisFrame)
                newDir = Vector3.back;
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                newDir = Vector3.left;
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                newDir = Vector3.right;
            else if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
                newDir = Vector3.forward;
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
                newDir = Vector3.back;
        }

        if (newDir != Vector3.zero)
        {
            // Prevent immediate 180-degree turn into tail if segments exist
            if (bodySegments.Count == 0 || Vector3.Dot(newDir, movementDirection) > -0.8f)
            {
                movementDirection = newDir;
            }
        }

        UpdateThirdPersonCamera();
    }

    void FixedUpdate()
    {
        if (gameOver)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(movementDirection, Vector3.up);
        Quaternion smoothRotation = Quaternion.RotateTowards(
            rb.rotation,
            targetRotation,
            turnSpeed * Time.fixedDeltaTime);
        rb.MoveRotation(smoothRotation);

        Vector3 nextPosition = rb.position +
            movementDirection * speed * Time.fixedDeltaTime;

        Vector3 wrapOffset = Vector3.zero;
        float width = maxX - minX;
        float depth = maxZ - minZ;
        if (width > 0f)
        {
            if (nextPosition.x > maxX)
                wrapOffset.x = -width;
            else if (nextPosition.x < minX)
                wrapOffset.x = width;
        }

        if (depth > 0f)
        {
            if (nextPosition.z > maxZ)
                wrapOffset.z = -depth;
            else if (nextPosition.z < minZ)
                wrapOffset.z = depth;
        }

        nextPosition += wrapOffset;
        nextPosition.y = 0.5f;

        if (wrapOffset != Vector3.zero)
        {
            for (int i = 0; i < bodySegments.Count; i++)
            {
                if (bodySegments[i] != null)
                    bodySegments[i].position += wrapOffset;
            }
        }

        rb.MovePosition(nextPosition);
        FollowPrevious(nextPosition);
        CheckSelfCollision(nextPosition);
    }

    private void CheckSelfCollision(Vector3 headPosition)
    {
        if (bodySegments.Count < 2)
            return;

        Vector3 headHalfExtents = playerCollider != null
            ? playerCollider.bounds.extents
            : Vector3.one * 0.5f;

        // The first segment follows directly behind the head and is not a self-collision.
        for (int i = 1; i < bodySegments.Count; i++)
        {
            Transform segment = bodySegments[i];
            if (segment == null)
                continue;

            Vector3 segmentHalfExtents = segment.lossyScale * 0.5f;
            Vector3 separation = headPosition - segment.position;
            if (Mathf.Abs(separation.x) <= headHalfExtents.x + segmentHalfExtents.x &&
                Mathf.Abs(separation.y) <= headHalfExtents.y + segmentHalfExtents.y &&
                Mathf.Abs(separation.z) <= headHalfExtents.z + segmentHalfExtents.z)
            {
                LoseLife();
                return;
            }
        }
    }

    private void AddBodySegment()
    {
        Vector3 position = bodySegments.Count > 0
            ? bodySegments[bodySegments.Count - 1].position
            : transform.position;

        GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
        segment.name = $"Snake Segment {bodySegments.Count + 1}";
        segment.transform.position = position;
        segment.transform.localScale = Vector3.one * segmentSize;

        Collider segmentCollider = segment.GetComponent<Collider>();
        if (segmentCollider != null)
            segmentCollider.enabled = false;

        MeshRenderer segmentRenderer = segment.GetComponent<MeshRenderer>();
        MeshRenderer headRenderer = GetComponent<MeshRenderer>();
        if (segmentRenderer != null && headRenderer != null)
            segmentRenderer.sharedMaterial = headRenderer.sharedMaterial;

        bodySegments.Add(segment.transform);

        if (coinCollectedUI != null)
            coinCollectedUI.SetLength(bodySegments.Count + 1);
    }

    private void RemoveTailSegment()
    {
        if (bodySegments.Count == 0)
        {
            if (coinCollectedUI != null)
                coinCollectedUI.SetLength(1);
            return;
        }

        int tailIndex = bodySegments.Count - 1;
        Destroy(bodySegments[tailIndex].gameObject);
        bodySegments.RemoveAt(tailIndex);

        if (coinCollectedUI != null)
            coinCollectedUI.SetLength(bodySegments.Count + 1);
    }

    private void FollowPrevious(Vector3 headPosition)
    {
        Vector3 previousPosition = headPosition;

        for (int i = 0; i < bodySegments.Count; i++)
        {
            Transform segment = bodySegments[i];
            if (segment == null)
                continue;

            Vector3 target = previousPosition;
            target.y = 0.5f;

            Vector3 segmentPosition = segment.position;
            Vector3 direction = target - segmentPosition;
            direction.y = 0f;

            if (direction.sqrMagnitude > SegmentSpacing * SegmentSpacing)
            {
                segmentPosition += direction.normalized *
                    Mathf.Min(direction.magnitude - SegmentSpacing, segmentFollowSpeed * Time.fixedDeltaTime);
            }

            segmentPosition.y = 0.5f;
            segment.position = segmentPosition;

            direction = target - segmentPosition;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                segment.rotation = Quaternion.Slerp(
                    segment.rotation,
                    targetRotation,
                    1f - Mathf.Exp(-segmentTurnSpeed * Time.fixedDeltaTime));
            }

            previousPosition = segmentPosition;
        }
    }

    private void LateUpdate()
    {
        UpdateThirdPersonCamera();
    }

    private void UpdateThirdPersonCamera()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
            return;

        mainCamera.transform.position = transform.position + cameraOffset;
        mainCamera.transform.LookAt(transform.position + cameraLookOffset);
    }
}
