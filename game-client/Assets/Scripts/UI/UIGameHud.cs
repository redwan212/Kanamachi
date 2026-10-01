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
    private GameObject pauseRoot;

    // Unified RightGameplayHUD elements
    private GameObject rightHudRoot;
    private RectTransform levelPanelRect;
    private TextMeshProUGUI chapterNumberLabel;
    private TextMeshProUGUI chapterTitleLabel;
    private TextMeshProUGUI roundInfoLabel;

    private RectTransform catcherPanelRect;
    private TextMeshProUGUI catcherLabel;
    private TextMeshProUGUI catcherNameLabel;
    private TextMeshProUGUI blindfoldStatusLabel;

    private RectTransform scorePanelRect;
    private TextMeshProUGUI scoreTitleLabel;
    private TextMeshProUGUI playerScoreListLabel;
    private int lastLeadingScore = -1;

    // Left Gameplay HUD (Controls & Guide)
    private GameObject leftHudRoot;
    private TextMeshProUGUI leftGuideTipLabel;

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
        BuildPausePanel();

        SetVisible(false);
    }

    void Update()
    {
        // A player has to be able to leave. Without this the only way out of
        // a match is to kill the process, which is not a thing a finished
        // game should require.
        if (Input.GetKeyDown(KeyCode.Escape) && canvas != null && canvas.gameObject.activeSelf)
        {
            TogglePause();
        }

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

        // Unified RightGameplayHUD container anchored to the right side (shifted inward toward middle)
        rightHudRoot = new GameObject("RightGameplayHUD", typeof(RectTransform));
        rightHudRoot.transform.SetParent(hudRoot.transform, false);
        RectTransform rightRect = rightHudRoot.GetComponent<RectTransform>();
        rightRect.anchorMin = new Vector2(1f, 0.5f);
        rightRect.anchorMax = new Vector2(1f, 0.5f);
        rightRect.pivot = new Vector2(1f, 0.5f);
        rightRect.sizeDelta = new Vector2(280f, 440f);
        rightRect.anchoredPosition = new Vector2(-70f, 0f);

        // Thin decorative outer border frame
        GameObject border = UIBuilder.Panel(rightHudRoot.transform, "Border",
                new Color(0.25f, 0.32f, 0.48f, 0.65f),
                new Vector2(280f, 440f), Vector2.zero);
        UIBuilder.StretchToParent(border.GetComponent<RectTransform>());

        // Dark navy inner background with subtle transparency
        GameObject bg = UIBuilder.Panel(border.transform, "Background",
                new Color(UITheme.Panel.r, UITheme.Panel.g, UITheme.Panel.b, 0.90f),
                Vector2.zero, Vector2.zero);
        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = new Vector2(2f, 2f);
        bgRect.offsetMax = new Vector2(-2f, -2f);

        // 1. LevelPanel
        GameObject levelPanel = new GameObject("LevelPanel", typeof(RectTransform));
        levelPanel.transform.SetParent(bg.transform, false);
        levelPanelRect = levelPanel.GetComponent<RectTransform>();
        levelPanelRect.sizeDelta = new Vector2(250f, 90f);
        levelPanelRect.anchoredPosition = new Vector2(0f, 155f);

        chapterNumberLabel = UIBuilder.Label(levelPanel.transform, "CHAPTER 1", 14,
                UITheme.Accent, new Vector2(0f, 26f), 250f, 22f, TextAlignmentOptions.Center);
        chapterTitleLabel = UIBuilder.Label(levelPanel.transform, "THE CHALLENGE", 20,
                UITheme.TextPrimary, new Vector2(0f, 2f), 250f, 30f, TextAlignmentOptions.Center);
        roundInfoLabel = UIBuilder.Label(levelPanel.transform, "ROUND 1 / 3   ·   4 PLAYERS", 13,
                UITheme.TextMuted, new Vector2(0f, -24f), 250f, 20f, TextAlignmentOptions.Center);

        // Subtle divider 1
        CreateDivider(bg.transform, "Divider1", new Vector2(0f, 95f));

        // 2. CatcherPanel
        GameObject catcherPanel = new GameObject("CatcherPanel", typeof(RectTransform));
        catcherPanel.transform.SetParent(bg.transform, false);
        catcherPanelRect = catcherPanel.GetComponent<RectTransform>();
        catcherPanelRect.sizeDelta = new Vector2(250f, 90f);
        catcherPanelRect.anchoredPosition = new Vector2(0f, 35f);

        catcherLabel = UIBuilder.Label(catcherPanel.transform, "KANAMACHI", 14,
                UITheme.Accent, new Vector2(0f, 26f), 250f, 22f, TextAlignmentOptions.Center);
        catcherNameLabel = UIBuilder.Label(catcherPanel.transform, "-", 20,
                UITheme.TextPrimary, new Vector2(0f, 2f), 250f, 30f, TextAlignmentOptions.Center);
        blindfoldStatusLabel = UIBuilder.Label(catcherPanel.transform, "<color=#C8362B>●</color> BLINDFOLDED", 13,
                UITheme.TextMuted, new Vector2(0f, -24f), 250f, 20f, TextAlignmentOptions.Center);

        // Subtle divider 2
        CreateDivider(bg.transform, "Divider2", new Vector2(0f, -25f));

        // 3. ScorePanel
        GameObject scorePanel = new GameObject("ScorePanel", typeof(RectTransform));
        scorePanel.transform.SetParent(bg.transform, false);
        scorePanelRect = scorePanel.GetComponent<RectTransform>();
        scorePanelRect.sizeDelta = new Vector2(250f, 170f);
        scorePanelRect.anchoredPosition = new Vector2(0f, -120f);

        scoreTitleLabel = UIBuilder.Label(scorePanel.transform, "SCORES", 14,
                UITheme.Accent, new Vector2(0f, 65f), 250f, 22f, TextAlignmentOptions.Center);
        playerScoreListLabel = UIBuilder.Label(scorePanel.transform, "", 16,
                UITheme.TextPrimary, new Vector2(0f, -10f), 240f, 120f, TextAlignmentOptions.TopLeft);

        // LeftGameplayHUD container anchored to the left side (filling empty left space)
        leftHudRoot = new GameObject("LeftGameplayHUD", typeof(RectTransform));
        leftHudRoot.transform.SetParent(hudRoot.transform, false);
        RectTransform leftRect = leftHudRoot.GetComponent<RectTransform>();
        leftRect.anchorMin = new Vector2(0f, 0.5f);
        leftRect.anchorMax = new Vector2(0f, 0.5f);
        leftRect.pivot = new Vector2(0f, 0.5f);
        leftRect.sizeDelta = new Vector2(280f, 440f);
        leftRect.anchoredPosition = new Vector2(70f, 0f);

        // Left border
        GameObject leftBorder = UIBuilder.Panel(leftHudRoot.transform, "LeftBorder",
                new Color(0.25f, 0.32f, 0.48f, 0.65f),
                new Vector2(280f, 440f), Vector2.zero);
        UIBuilder.StretchToParent(leftBorder.GetComponent<RectTransform>());

        // Left background
        GameObject leftBg = UIBuilder.Panel(leftBorder.transform, "LeftBackground",
                new Color(UITheme.Panel.r, UITheme.Panel.g, UITheme.Panel.b, 0.90f),
                Vector2.zero, Vector2.zero);
        RectTransform leftBgRect = leftBg.GetComponent<RectTransform>();
        leftBgRect.anchorMin = Vector2.zero;
        leftBgRect.anchorMax = Vector2.one;
        leftBgRect.offsetMin = new Vector2(2f, 2f);
        leftBgRect.offsetMax = new Vector2(-2f, -2f);

        // Left Header: Bengali Kanamachi Title & Lore
        GameObject headerPanel = new GameObject("HeaderPanel", typeof(RectTransform));
        headerPanel.transform.SetParent(leftBg.transform, false);
        RectTransform headerRect = headerPanel.GetComponent<RectTransform>();
        headerRect.sizeDelta = new Vector2(250f, 80f);
        headerRect.anchoredPosition = new Vector2(0f, 155f);

        UIBuilder.Label(headerPanel.transform, "KANAMACHI BHO BHO", 14,
                UITheme.Accent, new Vector2(0f, 26f), 250f, 22f, TextAlignmentOptions.Center);
        UIBuilder.Label(headerPanel.transform, "TRADITIONAL TAG", 20,
                UITheme.TextPrimary, new Vector2(0f, 2f), 250f, 30f, TextAlignmentOptions.Center);
        UIBuilder.Label(headerPanel.transform, "\"JAKE PABI TAKE CHHO!\"", 13,
                UITheme.TextMuted, new Vector2(0f, -24f), 250f, 20f, TextAlignmentOptions.Center);

        CreateDivider(leftBg.transform, "LeftDivider1", new Vector2(0f, 95f));

        // Left Controls Panel
        GameObject controlsPanel = new GameObject("ControlsPanel", typeof(RectTransform));
        controlsPanel.transform.SetParent(leftBg.transform, false);
        RectTransform controlsRect = controlsPanel.GetComponent<RectTransform>();
        controlsRect.sizeDelta = new Vector2(250f, 130f);
        controlsRect.anchoredPosition = new Vector2(0f, 30f);

        UIBuilder.Label(controlsPanel.transform, "CONTROLS", 14,
                UITheme.Accent, new Vector2(0f, 48f), 250f, 22f, TextAlignmentOptions.Center);

        string controlsText = "WASD / ARROWS<pos=150>MOVE\n" +
                              "SPACE<pos=150>CATCH\n" +
                              "C KEY<pos=150>CLAP\n" +
                              "ESCAPE<pos=150>PAUSE";
        UIBuilder.Label(controlsPanel.transform, controlsText, 14,
                UITheme.TextPrimary, new Vector2(0f, -16f), 240f, 90f, TextAlignmentOptions.TopLeft);

        CreateDivider(leftBg.transform, "LeftDivider2", new Vector2(0f, -45f));

        // Left Dynamic Guide / How To Play Panel
        GameObject guidePanel = new GameObject("GuidePanel", typeof(RectTransform));
        guidePanel.transform.SetParent(leftBg.transform, false);
        RectTransform guideRect = guidePanel.GetComponent<RectTransform>();
        guideRect.sizeDelta = new Vector2(250f, 130f);
        guideRect.anchoredPosition = new Vector2(0f, -130f);

        UIBuilder.Label(guidePanel.transform, "HOW TO PLAY", 14,
                UITheme.Accent, new Vector2(0f, 48f), 250f, 22f, TextAlignmentOptions.Center);
        leftGuideTipLabel = UIBuilder.Label(guidePanel.transform,
                "<color=#5DCAA5>RUN & HIDE!</color>\nEvade the blindfolded player.\nPress <color=#F2C14E>C</color> to clap & guide!",
                13, UITheme.TextPrimary, new Vector2(0f, -14f), 240f, 90f, TextAlignmentOptions.Center);

        // Centre: short-lived feedback such as "Too far away".
        toastLabel = UIBuilder.Label(hudRoot.transform, "", UITheme.HeadingSize,
                UITheme.Accent, new Vector2(0f, -220f), 900f, 50f);
        toastLabel.gameObject.SetActive(false);
    }

    private void CreateDivider(Transform parent, string name, Vector2 pos)
    {
        UIBuilder.Panel(parent, name,
                new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.35f),
                new Vector2(250f, 2f), pos);
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
        rowRect.sizeDelta = new Vector2(1400f, 120f);
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

    // ---------- Pause ----------

    private void BuildPausePanel()
    {
        pauseRoot = UIBuilder.FullScreen(canvas.transform, "PausePanel",
                new Color(0.02f, 0.03f, 0.07f, 0.88f));

        UIBuilder.Label(pauseRoot.transform, "Paused", UITheme.TitleSize,
                UITheme.Accent, new Vector2(0f, 180f), 700f, 80f);

        UIBuilder.TextButton(pauseRoot.transform, "RESUME", new Vector2(0f, 60f),
                () => SetPaused(false), true, 300f);

        UIBuilder.TextButton(pauseRoot.transform, "LEAVE MATCH", new Vector2(0f, -10f),
                LeaveMatch, false, 300f);

        UIBuilder.TextButton(pauseRoot.transform, "QUIT GAME", new Vector2(0f, -80f),
                QuitGame, false, 300f);

        UIBuilder.Label(pauseRoot.transform, "Press Escape to go back",
                UITheme.SmallSize, UITheme.TextMuted, new Vector2(0f, -170f), 700f, 32f);

        pauseRoot.SetActive(false);
    }

    private void TogglePause()
    {
        if (pauseRoot == null) return;

        // Never over the result screen - the match is already over there.
        if (resultRoot != null && resultRoot.activeSelf) return;

        SetPaused(!pauseRoot.activeSelf);
    }

    public bool IsPaused
    {
        get { return pauseRoot != null && pauseRoot.activeSelf; }
    }

    private void SetPaused(bool paused)
    {
        if (pauseRoot == null) return;

        pauseRoot.SetActive(paused);

        // The match keeps running on the server, so time is not stopped -
        // pausing here only hides the courtyard and frees the mouse. A
        // player who steps away is still in the room.
        Cursor.visible = true;
    }

    private void LeaveMatch()
    {
        SetPaused(false);

        if (NetworkClient.Instance != null) NetworkClient.Instance.Disconnect();
        SessionData.ClearRoom();

        SetVisible(false);
        if (UIManager.Instance != null) UIManager.Instance.Show(UIManager.Screen.MainMenu);
    }

    private void QuitGame()
    {
        if (NetworkClient.Instance != null) NetworkClient.Instance.Disconnect();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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
            if (pauseRoot != null) pauseRoot.SetActive(false);
        }
    }

    private void GetStoryChapter(string levelName, out string num, out string title)
    {
        if (string.IsNullOrEmpty(levelName))
        {
            num = "CHAPTER 1";
            title = "THE CHALLENGE";
            return;
        }
        string lower = levelName.ToLower();
        if (lower.Contains("courtyard") || lower.Contains("challenge") || lower.Contains("1"))
        {
            num = "CHAPTER 1";
            title = "THE CHALLENGE";
            return;
        }
        if (lower.Contains("mela") || lower.Contains("2"))
        {
            num = "CHAPTER 2";
            title = "THE MELA GROUND";
            return;
        }
        if (lower.Contains("storm") || lower.Contains("night") || lower.Contains("3"))
        {
            num = "CHAPTER 3";
            title = "THE STORM NIGHT";
            return;
        }
        if (lower.Contains("final") || lower.Contains("catch") || lower.Contains("4"))
        {
            num = "CHAPTER 4";
            title = "THE FINAL CATCH";
            return;
        }

        num = "CHAPTER";
        title = levelName.ToUpper();
    }

    public void SetLevel(string levelName, int roundInLevel, int roundsPerLevel, int playerCount)
    {
        if (chapterTitleLabel == null) return;

        GetStoryChapter(levelName, out string num, out string title);
        bool chapterChanged = chapterTitleLabel.text != title;
        string expectedRound = string.IsNullOrEmpty(levelName)
                ? $"{playerCount} PLAYERS"
                : $"ROUND {roundInLevel} / {roundsPerLevel}   ·   {playerCount} PLAYERS";
        bool roundChanged = roundInfoLabel.text != expectedRound;

        chapterNumberLabel.text = num;
        chapterTitleLabel.text = title;
        roundInfoLabel.text = expectedRound;

        if ((chapterChanged || roundChanged) && levelPanelRect != null && gameObject.activeInHierarchy)
        {
            StartCoroutine(PulseFade(levelPanelRect));
        }
    }

    public void SetRole(string yourName, bool youAreKanamachi, string kanamachiName, bool canCatch)
    {
        if (catcherNameLabel == null) return;

        string targetName = youAreKanamachi
                ? (string.IsNullOrEmpty(yourName) ? "YOU" : $"{yourName} (YOU)")
                : (string.IsNullOrEmpty(kanamachiName) ? "Waiting..." : kanamachiName);
        bool changed = catcherNameLabel.text != targetName;

        catcherLabel.text = "KANAMACHI";
        catcherNameLabel.text = targetName;
        catcherNameLabel.color = youAreKanamachi ? UITheme.Accent : UITheme.TextPrimary;

        if (youAreKanamachi)
        {
            blindfoldStatusLabel.text = canCatch
                    ? "<color=#5DCAA5>●</color> PRESS SPACE TO CATCH"
                    : "<color=#C8362B>●</color> BLINDFOLDED";
        }
        else
        {
            blindfoldStatusLabel.text = string.IsNullOrEmpty(kanamachiName)
                    ? "WAITING FOR MATCH"
                    : "<color=#C8362B>●</color> BLINDFOLDED";
        }

        if (changed && catcherNameLabel != null && gameObject.activeInHierarchy)
        {
            StartCoroutine(PunchScale(catcherNameLabel.rectTransform));
        }

        if (leftGuideTipLabel != null)
        {
            leftGuideTipLabel.text = youAreKanamachi
                    ? "<color=#F2C14E>YOU ARE KANAMACHI!</color>\nListen for footsteps & claps.\nApproach & press <color=#F2C14E>SPACE</color> to catch!"
                    : "<color=#5DCAA5>RUN & HIDE!</color>\nEvade the blindfolded player.\nPress <color=#F2C14E>C</color> to clap & guide!";
        }
    }

    public void SetScores(IEnumerable<KeyValuePair<string, int>> scores,
                          System.Func<string, string> nameOf, string leadingUserId)
    {
        if (playerScoreListLabel == null) return;

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        int highestScore = -1;

        foreach (var pair in scores)
        {
            string name = nameOf(pair.Key);
            int score = pair.Value;
            if (score > highestScore) highestScore = score;

            bool isLeading = pair.Key == leadingUserId && score > 0;
            bool isLocal = !string.IsNullOrEmpty(SessionData.UserId) && pair.Key == SessionData.UserId;

            string displayName = isLocal ? $"{name} <size=12><color=#F2C14E>(YOU)</color></size>" : name;

            string line = isLeading
                    ? $"<color=#F2C14E>{displayName}</color><pos=180><color=#F2C14E>{score}</color>"
                    : $"{displayName}<pos=180>{score}";

            builder.AppendLine(line);
        }

        playerScoreListLabel.text = builder.ToString().TrimEnd();

        if (highestScore > lastLeadingScore && lastLeadingScore >= 0 && scorePanelRect != null && gameObject.activeInHierarchy)
        {
            StartCoroutine(PunchScale(scorePanelRect));
        }
        lastLeadingScore = highestScore;
    }

    private System.Collections.IEnumerator PunchScale(RectTransform target)
    {
        if (target == null) yield break;
        float elapsed = 0f;
        float duration = 0.25f;
        Vector3 orig = Vector3.one;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float scale = 1f + 0.12f * Mathf.Sin(t * Mathf.PI);
            target.localScale = orig * scale;
            yield return null;
        }
        target.localScale = orig;
    }

    private System.Collections.IEnumerator PulseFade(RectTransform target)
    {
        if (target == null) yield break;
        CanvasGroup cg = target.GetComponent<CanvasGroup>();
        if (cg == null) cg = target.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        float elapsed = 0f;
        float duration = 0.3f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(0.35f, 1f, elapsed / duration);
            yield return null;
        }
        cg.alpha = 1f;
    }

    public void ShowToast(string message, float seconds = 2.5f)
    {
        if (toastLabel == null) return;

        toastLabel.text = message;
        toastLabel.gameObject.SetActive(true);
        toastUntil = Time.time + seconds;
    }

    // One option on the guess screen: who they are, and what they sound
    // like. The hint is the whole point - without it the Kanamachi is
    // picking a name at random rather than matching a name to a sound.
    public struct GuessOption
    {
        public string userId;
        public string displayName;
        public string hint;
    }

    public void ShowGuessPanel(List<GuessOption> options,
                               System.Action<string> onGuess)
    {
        if (guessRoot == null) return;

        foreach (Transform child in guessButtonRow)
        {
            Destroy(child.gameObject);
        }

        foreach (GuessOption option in options)
        {
            string userId = option.userId;

            // Name and hint are stacked in one column so they move together
            // and stay aligned however many options there are.
            GameObject column = new GameObject("Option", typeof(RectTransform));
            column.transform.SetParent(guessButtonRow, false);

            RectTransform columnRect = column.GetComponent<RectTransform>();
            columnRect.sizeDelta = new Vector2(260f, 108f);

            LayoutElement columnLayout = column.AddComponent<LayoutElement>();
            columnLayout.preferredWidth = 260f;
            columnLayout.preferredHeight = 108f;

            UIBuilder.TextButton(column.transform, option.displayName,
                    new Vector2(0f, 24f), () => onGuess(userId), true, 260f);

            UIBuilder.Label(column.transform, option.hint, UITheme.SmallSize,
                    UITheme.TextMuted, new Vector2(0f, -32f), 260f, 34f);
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
