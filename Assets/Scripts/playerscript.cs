
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class playerscript : MonoBehaviour
{
    private const int StartingLives = 3;
    private const float SegmentSpacing = 0.9f;

    public float speed = 5f;
    [SerializeField] private float segmentFollowSpeed = 8f;
    [SerializeField] private float segmentSize = 0.8f;
    [SerializeField] private float hazardInvulnerability = 1f;
    [SerializeField] private float minX = -9f;
    [SerializeField] private float maxX = 9f;
    [SerializeField] private float minZ = -9f;
    [SerializeField] private float maxZ = 9f;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 8f, -10f);
    [SerializeField] private Vector3 cameraLookOffset = new Vector3(0f, 0f, 2f);

    [SerializeField] private CoinCollectedUI coinCollectedUI;

    private Rigidbody rb;
    private float horizontalInput;
    private float verticalInput;
    private Camera mainCamera;
    private int livesRemaining = StartingLives;
    private readonly List<Transform> bodySegments = new List<Transform>();
    private Vector3 startingPosition;
    private float invulnerabilityRemaining;
    private bool gameOver;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints |= RigidbodyConstraints.FreezeRotation |
                          RigidbodyConstraints.FreezePositionY;
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
    }

    public void CollectCoin()
    {
        if (coinCollectedUI == null)
            coinCollectedUI = FindFirstObjectByType<CoinCollectedUI>();

        if (coinCollectedUI != null)
            coinCollectedUI.AddCoin();
        AddBodySegment();
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
        }
    }

    void Update()
    {
        horizontalInput = 0f;
        verticalInput = 0f;
        invulnerabilityRemaining = Mathf.Max(0f, invulnerabilityRemaining - Time.deltaTime);

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.aKey.isPressed ||
            Keyboard.current.leftArrowKey.isPressed)
        {
            horizontalInput = -1f;
        }

        if (Keyboard.current.dKey.isPressed ||
            Keyboard.current.rightArrowKey.isPressed)
        {
            horizontalInput = 1f;
        }

        if (Keyboard.current.wKey.isPressed ||
            Keyboard.current.upArrowKey.isPressed)
        {
            verticalInput = 1f;
        }

        if (Keyboard.current.sKey.isPressed ||
            Keyboard.current.downArrowKey.isPressed)
        {
            verticalInput = -1f;
        }

        UpdateThirdPersonCamera();
    }

    void FixedUpdate()
    {
        if (gameOver)
            return;

        Vector3 nextPosition = rb.position +
            new Vector3(
                horizontalInput * speed * Time.fixedDeltaTime,
                0f,
                verticalInput * speed * Time.fixedDeltaTime);

        nextPosition.x = Mathf.Clamp(nextPosition.x, minX, maxX);
        nextPosition.z = Mathf.Clamp(nextPosition.z, minZ, maxZ);
        nextPosition.y = 0.5f;

        rb.MovePosition(nextPosition);
        FollowPrevious(nextPosition);
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
