using UnityEngine;
using TMPro;

public class CoinCollectedUI : MonoBehaviour
{
    [SerializeField] private TMP_Text scoreText;
    private int score;
    private int lives = 3;
    private int length = 1;

    private void Awake()
    {
        if (scoreText == null)
            scoreText = GetComponent<TMP_Text>();

        ConfigureText();
        UpdateScoreText();
    }

    private void ConfigureText()
    {
        if (scoreText == null)
            return;

        RectTransform rect = scoreText.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -24f);
        rect.sizeDelta = new Vector2(500f, 120f);

        scoreText.fontSize = 36f;
        scoreText.alignment = TextAlignmentOptions.TopLeft;
        scoreText.textWrappingMode = TextWrappingModes.NoWrap;
    }

    public void AddCoin()
    {
        score++;
        UpdateScoreText();
    }

    private void UpdateScoreText()
    {
        if (scoreText != null)
            scoreText.text = $"Coins: {score}  |  Lives: {lives}  |  Length: {length}";
    }

    public void SetLives(int remainingLives)
    {
        lives = Mathf.Max(0, remainingLives);
        UpdateScoreText();
    }

    public void SetLength(int currentLength)
    {
        length = Mathf.Max(1, currentLength);
        UpdateScoreText();
    }
}