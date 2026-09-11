using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Everything shown during a match: the round banner, the scoreboard, the
// guess panel and the final result.
//
// It holds no game state of its own. NetworkGameManager receives the
// server's decisions and calls the methods here, which means the display can
// be restyled or replaced without touching a line of game logic.
public class UIGameHud : MonoBehaviour
{
    public static UIGameHud Instance;

    private Canvas canvas;

    private GameObject hudRoot;
    private GameObject guessRoot;
    private GameObject resultRoot;

    private TextMeshProUGUI levelLabel;
    private TextMeshProUGUI roundLabel;
    private TextMeshProUGUI roleLabel;
    private TextMeshProUGUI hintLabel;
    private TextMeshProUGUI scoreLabel;
    private TextMeshProUGUI toastLabel;

    private TextMeshProUGUI guessPrompt;
    private Transform guessButtonRow;

    private TextMeshProUGUI resultTitle;
    private TextMeshProUGUI resultWinner;
    private TextMeshProUGUI resultScores;

    private float toastUntil;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        BuildCanvas();
        BuildHud();
        BuildGuessPanel();
        BuildResultPanel();

        SetVisible(false);
    }

    void Update()
    {
        if (toastLabel != null && toastLabel.gameObject.activeSelf && Time.time > toastUntil)
        {
            toastLabel.gameObject.SetActive(false);
        }
    }

    // ---------- Construction ----------

    private void BuildCanvas()
    {
        GameObject canvasObj = new GameObject("Game HUD Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObj.transform.SetParent(transform, false);

        canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Below the lobby canvas, so menus always win if both are somehow up.
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void BuildHud()
    {
        hudRoot = new GameObject("Hud", typeof(RectTransform));
        hudRoot.transform.SetParent(canvas.transform, false);
        UIBuilder.StretchToParent(hudRoot.GetComponent<RectTransform>());

        // Top left: where you are in the match.
        GameObject topLeft = UIBuilder.Panel(hudRoot.transform, "LevelPanel",
                new Color(UITheme.Panel.r, UITheme.Panel.g, UITheme.Panel.b, 0.82f),
                new Vector2(420f, 96f), Vector2.zero);
        Anchor(topLeft, new Vector2(0f, 1f), new Vector2(230f, -66f));

        levelLabel = UIBuilder.Label(topLeft.transform, "", UITheme.BodySize,
                UITheme.Accent, new Vector2(0f, 20f), 390f, 34f, TextAlignmentOptions.Left);
        roundLabel = UIBuilder.Label(topLeft.transform, "", UITheme.SmallSize,
                UITheme.TextMuted, new Vector2(0f, -16f), 390f, 30f, TextAlignmentOptions.Left);

        // Top right: who you are and who is blindfolded.
        GameObject topRight = UIBuilder.Panel(hudRoot.transform, "RolePanel",
                new Color(UITheme.Panel.r, UITheme.Panel.g, UITheme.Panel.b, 0.82f),
                new Vector2(460f, 120f), Vector2.zero);
        Anchor(topRight, new Vector2(1f, 1f), new Vector2(-250f, -78f));

        roleLabel = UIBuilder.Label(topRight.transform, "", UITheme.HeadingSize,
                UITheme.TextPrimary, new Vector2(0f, 26f), 430f, 44f, TextAlignmentOptions.Right);
        hintLabel = UIBuilder.Label(topRight.transform, "", UITheme.SmallSize,
                UITheme.TextMuted, new Vector2(0f, -22f), 430f, 32f, TextAlignmentOptions.Right);

        // Bottom right: the scoreboard.
        GameObject scorePanel = UIBuilder.Panel(hudRoot.transform, "ScorePanel",
                new Color(UITheme.Panel.r, UITheme.Panel.g, UITheme.Panel.b, 0.82f),
                new Vector2(320f, 220f), Vector2.zero);
        Anchor(scorePanel, new Vector2(1f, 0f), new Vector2(-180f, 130f));

        UIBuilder.Label(scorePanel.transform, "Scores", UITheme.SmallSize,
                UITheme.Accent, new Vector2(0f, 82f), 280f, 30f);
        scoreLabel = UIBuilder.Label(scorePanel.transform, "", UITheme.BodySize,
                UITheme.TextPrimary, new Vector2(0f, -14f), 280f, 160f);

        // Centre: short-lived feedback such as "Too far away".
        toastLabel = UIBuilder.Label(hudRoot.transform, "", UITheme.HeadingSize,
                UITheme.Accent, new Vector2(0f, -220f), 900f, 50f);
        toastLabel.gameObject.SetActive(false);
    }

    private void BuildGuessPanel()
    {
        guessRoot = UIBuilder.FullScreen(canvas.transform, "GuessPanel",
                new Color(0f, 0f, 0f, 0.72f));

        guessPrompt = UIBuilder.Label(guessRoot.transform, "Who did you catch?",
                UITheme.TitleSize, UITheme.Accent, new Vector2(0f, 160f), 900f, 80f);

        // Buttons are rebuilt each round, since the players present can change.
        GameObject row = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(guessRoot.transform, false);

        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(1400f, 90f);
        rowRect.anchoredPosition = new Vector2(0f, 20f);

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = UITheme.Gap;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        guessButtonRow = row.transform;

        guessRoot.SetActive(false);
    }

    private void BuildResultPanel()
    {
        resultRoot = UIBuilder.FullScreen(canvas.transform, "ResultPanel",
                new Color(0.055f, 0.07f, 0.13f, 1f));

        resultTitle = UIBuilder.Label(resultRoot.transform, "Match over",
                UITheme.TitleSize, UITheme.Accent, new Vector2(0f, 230f), 900f, 80f);

        UIBuilder.Label(resultRoot.transform, "KANAMACHI MASTER", UITheme.SmallSize,
                UITheme.TextMuted, new Vector2(0f, 150f), 900f, 34f);

        resultWinner = UIBuilder.Label(resultRoot.transform, "", UITheme.TitleSize,
                UITheme.TextPrimary, new Vector2(0f, 90f), 900f, 80f);

        resultScores = UIBuilder.Label(resultRoot.transform, "", UITheme.BodySize,
                UITheme.TextPrimary, new Vector2(0f, -70f), 900f, 220f);

        UIBuilder.TextButton(resultRoot.transform, "Back to menu", new Vector2(0f, -290f), () =>
        {
            if (NetworkClient.Instance != null) NetworkClient.Instance.Disconnect();
            SessionData.ClearRoom();

            SetVisible(false);
            if (UIManager.Instance != null) UIManager.Instance.Show(UIManager.Screen.MainMenu);
        });

        resultRoot.SetActive(false);
    }

    private void Anchor(GameObject obj, Vector2 anchor, Vector2 position)
    {
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
    }

    // ---------- Called by NetworkGameManager ----------

    public void SetVisible(bool visible)
    {
        if (canvas != null) canvas.gameObject.SetActive(visible);
        if (hudRoot != null && visible) hudRoot.SetActive(true);

        if (!visible)
        {
            if (guessRoot != null) guessRoot.SetActive(false);
            if (resultRoot != null) resultRoot.SetActive(false);
        }
    }

    public void SetLevel(string levelName, int roundInLevel, int roundsPerLevel, int playerCount)
    {
        if (levelLabel == null) return;

        levelLabel.text = string.IsNullOrEmpty(levelName) ? "Waiting..." : levelName;
        roundLabel.text = string.IsNullOrEmpty(levelName)
                ? $"{playerCount} in the room"
                : $"Round {roundInLevel}/{roundsPerLevel}   ·   {playerCount} players";
    }

    public void SetRole(string yourName, bool youAreKanamachi, string kanamachiName, bool canCatch)
    {
        if (roleLabel == null) return;

        roleLabel.text = youAreKanamachi ? "You are the Kanamachi" : yourName;
        roleLabel.color = youAreKanamachi ? UITheme.Danger : UITheme.TextPrimary;

        if (youAreKanamachi)
        {
            hintLabel.text = canCatch ? "Press Space to grab someone" : "Listen for footsteps";
        }
        else
        {
            hintLabel.text = string.IsNullOrEmpty(kanamachiName)
                    ? "Waiting for the match to start"
                    : $"{kanamachiName} is blindfolded";
        }
    }

    public void SetScores(IEnumerable<KeyValuePair<string, int>> scores,
                          System.Func<string, string> nameOf, string leadingUserId)
    {
        if (scoreLabel == null) return;

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        foreach (var pair in scores)
        {
            bool leading = pair.Key == leadingUserId;
            string line = $"{nameOf(pair.Key)}   {pair.Value}";

            // Rich text rather than a star glyph, which the default TMP
            // font atlas does not contain and draws as an empty box.
            builder.AppendLine(leading ? $"<color=#F2C14E>{line}</color>" : line);
        }

        scoreLabel.text = builder.ToString();
    }

    public void ShowToast(string message, float seconds = 2.5f)
    {
        if (toastLabel == null) return;

        toastLabel.text = message;
        toastLabel.gameObject.SetActive(true);
        toastUntil = Time.time + seconds;
    }

    // options: userId -> display name
    public void ShowGuessPanel(List<KeyValuePair<string, string>> options,
                               System.Action<string> onGuess)
    {
        if (guessRoot == null) return;

        foreach (Transform child in guessButtonRow)
        {
            Destroy(child.gameObject);
        }

        foreach (var option in options)
        {
            string userId = option.Key;
            Button button = UIBuilder.TextButton(guessButtonRow, option.Value, Vector2.zero,
                    () => onGuess(userId), true, 260f);

            LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 260f;
            element.preferredHeight = UITheme.ButtonHeight;
        }

        guessPrompt.text = "Who did you catch?";
        guessRoot.SetActive(true);
    }

    // Shown to everyone who is not the one guessing.
    public void ShowWaitingForGuess(string kanamachiName)
    {
        if (guessRoot == null) return;

        foreach (Transform child in guessButtonRow)
        {
            Destroy(child.gameObject);
        }

        guessPrompt.text = $"{kanamachiName} is deciding who they caught...";
        guessRoot.SetActive(true);
    }

    public void HideGuessPanel()
    {
        if (guessRoot != null) guessRoot.SetActive(false);
    }

    public void ShowResult(string winnerName, bool localPlayerWon,
                           IEnumerable<KeyValuePair<string, int>> scores,
                           System.Func<string, string> nameOf)
    {
        if (resultRoot == null) return;

        HideGuessPanel();

        // The HUD would otherwise sit on top of the result.
        if (hudRoot != null) hudRoot.SetActive(false);

        resultTitle.text = localPlayerWon ? "You win" : "Match over";
        resultWinner.text = winnerName;

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        foreach (var pair in scores)
        {
            builder.AppendLine($"{nameOf(pair.Key)}     {pair.Value} pts");
        }
        resultScores.text = builder.ToString();

        resultRoot.SetActive(true);
    }
}
