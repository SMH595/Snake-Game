using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MiniMapUI : MonoBehaviour
{
    private const float RefreshInterval = 0.1f;
    private const float MapSize = 250f;
    private static Sprite whiteSprite;

    private readonly Dictionary<SpawnedObjectHandler, Image> objectMarkers =
        new Dictionary<SpawnedObjectHandler, Image>();

    private playerscript player;
    private RectTransform mapRect;
    private RectTransform playerMarker;
    private float minX;
    private float maxX;
    private float minZ;
    private float maxZ;
    private float refreshRemaining;

    public void Initialize(playerscript controlledPlayer, float worldMinX, float worldMaxX,
        float worldMinZ, float worldMaxZ)
    {
        player = controlledPlayer;
        minX = worldMinX;
        maxX = worldMaxX;
        minZ = worldMinZ;
        maxZ = worldMaxZ;

        if (mapRect == null)
            CreateMap();
    }

    private void Update()
    {
        if (mapRect == null || player == null)
            return;

        SetMarkerPosition(playerMarker, player.transform.position);
        playerMarker.localEulerAngles = new Vector3(0f, 0f, -player.transform.eulerAngles.y);

        refreshRemaining -= Time.deltaTime;
        if (refreshRemaining <= 0f)
        {
            refreshRemaining = RefreshInterval;
            RefreshObjectMarkers();
        }
    }

    private void CreateMap()
    {
        GameObject canvasObject = new GameObject("Minimap Canvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform panel = CreateRect("Minimap Panel", canvasObject.transform);
        panel.anchorMin = new Vector2(1f, 1f);
        panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(1f, 1f);
        panel.anchoredPosition = new Vector2(-20f, -20f);
        panel.sizeDelta = new Vector2(280f, 320f);
        SetImage(panel.gameObject, new Color(0.04f, 0.07f, 0.09f, 0.9f));

        TextMeshProUGUI title = CreateText("Map Title", panel, "MAP  |  APPLES & HAZARDS",
            18f, TextAlignmentOptions.Center);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -8f);
        title.rectTransform.sizeDelta = new Vector2(-12f, 28f);
        title.color = Color.white;

        mapRect = CreateRect("Map Area", panel);
        mapRect.anchorMin = new Vector2(0.5f, 1f);
        mapRect.anchorMax = new Vector2(0.5f, 1f);
        mapRect.pivot = new Vector2(0.5f, 1f);
        mapRect.anchoredPosition = new Vector2(0f, -42f);
        mapRect.sizeDelta = new Vector2(MapSize, MapSize);
        SetImage(mapRect.gameObject, new Color(0.11f, 0.16f, 0.14f, 1f));

        CreateGridLine(mapRect, true);
        CreateGridLine(mapRect, false);

        playerMarker = CreateMarker(mapRect, new Color(0.2f, 0.9f, 1f), 14f);
        playerMarker.SetAsLastSibling();

        TextMeshProUGUI legend = CreateText("Map Legend", panel,
            "● Apple     ● Hazard     ● Snake", 15f, TextAlignmentOptions.Center);
        legend.rectTransform.anchorMin = new Vector2(0f, 0f);
        legend.rectTransform.anchorMax = new Vector2(1f, 0f);
        legend.rectTransform.pivot = new Vector2(0.5f, 0f);
        legend.rectTransform.anchoredPosition = new Vector2(0f, 4f);
        legend.rectTransform.sizeDelta = new Vector2(-10f, 26f);
        legend.color = Color.white;
        legend.richText = true;
        legend.text = "<color=#FF3333>●</color> Apple   <mark=#FFFFFF><color=#000000>●</color></mark> Hazard   <color=#33E6FF>●</color> Snake";
    }

    private void RefreshObjectMarkers()
    {
        IReadOnlyList<SpawnedObjectHandler> activeObjects = SpawnedObjectHandler.ActiveObjects;
        for (int i = 0; i < activeObjects.Count; i++)
        {
            SpawnedObjectHandler spawnedObject = activeObjects[i];
            if (spawnedObject == null)
                continue;

            if (!objectMarkers.TryGetValue(spawnedObject, out Image marker))
            {
                RectTransform markerRect = spawnedObject.IsCoin
                    ? CreateMarker(mapRect, new Color(1f, 0.2f, 0.2f), 11f)
                    : CreateHazardMarker(mapRect);
                marker = markerRect.GetComponent<Image>();
                objectMarkers.Add(spawnedObject, marker);
            }

            SetMarkerPosition(marker.rectTransform, spawnedObject.transform.position);
        }

        List<SpawnedObjectHandler> removedObjects = null;
        foreach (KeyValuePair<SpawnedObjectHandler, Image> entry in objectMarkers)
        {
            if (entry.Key != null && activeObjects.Contains(entry.Key))
                continue;

            if (removedObjects == null)
                removedObjects = new List<SpawnedObjectHandler>();
            removedObjects.Add(entry.Key);
        }

        if (removedObjects == null)
            return;

        for (int i = 0; i < removedObjects.Count; i++)
        {
            Image marker = objectMarkers[removedObjects[i]];
            if (marker != null)
                Destroy(marker.gameObject);
            objectMarkers.Remove(removedObjects[i]);
        }
    }

    private void SetMarkerPosition(RectTransform marker, Vector3 worldPosition)
    {
        float xRange = Mathf.Max(0.001f, maxX - minX);
        float zRange = Mathf.Max(0.001f, maxZ - minZ);
        float x = Mathf.Clamp01((worldPosition.x - minX) / xRange);
        float z = Mathf.Clamp01((worldPosition.z - minZ) / zRange);
        marker.anchoredPosition = new Vector2(
            (x - 0.5f) * MapSize,
            (z - 0.5f) * MapSize);
    }

    private static void CreateGridLine(RectTransform parent, bool vertical)
    {
        RectTransform line = CreateRect(vertical ? "Vertical Grid Line" : "Horizontal Grid Line", parent);
        line.anchorMin = new Vector2(0.5f, 0.5f);
        line.anchorMax = new Vector2(0.5f, 0.5f);
        line.sizeDelta = vertical ? new Vector2(1f, MapSize) : new Vector2(MapSize, 1f);
        SetImage(line.gameObject, new Color(1f, 1f, 1f, 0.12f));
    }

    private static RectTransform CreateMarker(RectTransform parent, Color color, float size)
    {
        RectTransform marker = CreateRect("Map Marker", parent);
        marker.anchorMin = new Vector2(0.5f, 0.5f);
        marker.anchorMax = new Vector2(0.5f, 0.5f);
        marker.sizeDelta = new Vector2(size, size);
        SetImage(marker.gameObject, color);
        return marker;
    }

    private static RectTransform CreateHazardMarker(RectTransform parent)
    {
        RectTransform outline = CreateMarker(parent, Color.white, 14f);
        CreateMarker(outline, Color.black, 8f);
        return outline;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject element = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        element.transform.SetParent(parent, false);
        return element.GetComponent<RectTransform>();
    }

    private static void SetImage(GameObject element, Color color)
    {
        Image image = element.GetComponent<Image>();
        if (whiteSprite == null)
        {
            Texture2D texture = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));
        }

        image.sprite = whiteSprite;
        image.color = color;
        image.raycastTarget = false;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string content,
        float fontSize, TextAlignmentOptions alignment)
    {
        GameObject element = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        element.transform.SetParent(parent, false);
        TextMeshProUGUI text = element.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }
}
