using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Builds and drives every screen before the match starts:
// Login -> Main Menu -> Create/Join Room -> Lobby.
//
// Once the match begins this hides itself and NetworkGameManager takes over
// the display. It never decides anything about the game - it calls ApiClient
// and NetworkClient and shows whatever comes back.
public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    public enum Screen
    {
        Title,
        Login,
        MainMenu,
        Room,
        Lobby,
        Leaderboard,
        Profile,
        HowToPlay,
        Settings,
        InGame
    }

    [Header("Typography")]
    [Tooltip("A pixel font asset. Left empty the default font is used, which looks like a document rather than a game.")]
    public TMP_FontAsset pixelFont;

    [Header("Button frames")]
    [Tooltip("Optional pixel-art frames. Left empty, buttons use a flat fill.")]
    public Sprite goldButtonSprite;
    public Sprite plainButtonSprite;

    [Tooltip("Thin ornamental rule placed above and below the title.")]
    public Sprite dividerSprite;

    [Header("Landing artwork")]
    [Tooltip("Optional. Draws the game's own key image behind the login and menu screens.")]
    public UILandingArt landingArt;

    [Header("Behaviour")]
    [Tooltip("Connects to the room's WebSocket as soon as the host or guest is ready.")]
    public bool autoConnectFromLobby = true;

    private Canvas canvas;
    private readonly Dictionary<Screen, GameObject> screens = new Dictionary<Screen, GameObject>();
    private Screen current = Screen.Login;

    // Login
    private TMP_InputField usernameField;
    private TMP_InputField passwordField;
    private TextMeshProUGUI loginStatus;
    private TextMeshProUGUI signedInLabel;

    // Room
    private TMP_InputField roomCodeField;
    private TextMeshProUGUI roomStatus;

    // Leaderboard and profile
    private TextMeshProUGUI leaderboardList;
    private TextMeshProUGUI profileBody;

    // Lobby
    private TextMeshProUGUI lobbyCodeLabel;
    private TextMeshProUGUI lobbyPlayersLabel;
    private TextMeshProUGUI lobbyStatus;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Set before any screen is built, so every label picks it up.
        UITheme.Font = pixelFont;
        UITheme.GoldButtonSprite = goldButtonSprite;
        UITheme.PlainButtonSprite = plainButtonSprite;

        BuildCanvas();
        BuildTitleScreen();
        BuildLoginScreen();
        BuildMainMenuScreen();
        BuildRoomScreen();
        BuildLobbyScreen();
        BuildLeaderboardScreen();
        BuildProfileScreen();
        BuildHowToPlayScreen();
        BuildSettingsScreen();

        Show(Screen.Title);
    }

    void Start()
    {
        if (NetworkClient.Instance == null) return;

        NetworkClient.Instance.OnConnected += () => SetLobbyStatus("Connected. Waiting for the match to start...");
        NetworkClient.Instance.OnPlayerJoined += (id, name) => RefreshRoom();
        NetworkClient.Instance.OnGameStarted += round => Show(Screen.InGame);
        NetworkClient.Instance.OnServerError += message => SetLobbyStatus(message);
    }

    // ---------- Screen management ----------

    public void Show(Screen screen)
    {
        current = screen;

        if (screen == Screen.MainMenu && signedInLabel != null)
        {
            signedInLabel.text = SessionData.IsLoggedIn
                    ? $"signed in as {SessionData.Username}"
                    : "";
        }

        foreach (var pair in screens)
        {
            if (pair.Value != null) pair.Value.SetActive(pair.Key == screen);
        }
    }

    public bool IsInGame { get { return current == Screen.InGame; } }

    private void BuildCanvas()
    {
        GameObject canvasObj = new GameObject("UI Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObj.transform.SetParent(transform, false);

        canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // uGUI needs an EventSystem to deliver clicks; scenes built before
        // any UI existed will not have one.
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
            eventSystem.transform.SetParent(transform, false);
        }
    }

    private GameObject NewScreen(Screen key, string name)
    {
        GameObject root = UIBuilder.FullScreen(canvas.transform, name, UITheme.Background);
        screens[key] = root;
        return root;
    }

    // ---------- Login ----------

    // ---------- Title ----------
    //
    // The first thing anyone sees. It is a title screen rather than a form:
    // the artwork carries the left half, the title and menu the right, and
    // signing in only appears once PLAY has been pressed. The game should
    // announce itself before it asks for anything.
    private void BuildTitleScreen()
    {
        GameObject root = NewScreen(Screen.Title, "TitleScreen");

        if (landingArt != null) landingArt.Build(root.transform);

        UIBuilder.Label(root.transform, "KANAMACHI", 88,
                UITheme.Accent, new Vector2(540f, 290f), 780f, 120f);

        UIBuilder.Label(root.transform, "THE BLINDFOLD CAN'T SEE.  CAN YOU?",
                UITheme.SmallSize, UITheme.TextMuted, new Vector2(540f, 208f), 780f, 36f);

        if (dividerSprite != null)
        {
            AddDivider(root.transform, new Vector2(540f, 370f), 420f);
            AddDivider(root.transform, new Vector2(540f, 170f), 340f);
        }

        // PLAY is the one filled button on the screen, so the eye lands on it.
        UIBuilder.TextButton(root.transform, "PLAY", new Vector2(540f, 90f),
                OnPlayClicked, true, 320f);

        UIBuilder.TextButton(root.transform, "HOW TO PLAY", new Vector2(540f, 6f),
                () => Show(Screen.HowToPlay), false, 290f);

        UIBuilder.TextButton(root.transform, "SETTINGS", new Vector2(540f, -62f),
                () => Show(Screen.Settings), false, 290f);

        UIBuilder.TextButton(root.transform, "EXIT", new Vector2(540f, -130f),
                QuitGame, false, 290f);

        UIBuilder.Label(root.transform, "ONLINE MULTIPLAYER  -  2-4 PLAYERS",
                UITheme.SmallSize, UITheme.TextMuted, new Vector2(540f, -250f), 780f, 32f);

        // Bottom-right, low priority.
        TextMeshProUGUI version = UIBuilder.Label(root.transform, "V1.0",
                UITheme.SmallSize, UITheme.TextMuted, Vector2.zero, 140f, 30f,
                TextAlignmentOptions.Right);

        RectTransform versionRect = version.rectTransform;
        versionRect.anchorMin = versionRect.anchorMax = new Vector2(1f, 0f);
        versionRect.pivot = new Vector2(1f, 0f);
        versionRect.anchoredPosition = new Vector2(-40f, 30f);
    }

    // A thin gold rule with a diamond in the middle, above and below the
    // title. Cheap, and it stops the text floating in empty space.
    private void AddDivider(Transform parent, Vector2 position, float width)
    {
        GameObject obj = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);

        Image image = obj.GetComponent<Image>();
        image.sprite = dividerSprite;
        image.color = new Color(1f, 1f, 1f, 0.75f);
        image.preserveAspect = true;
        image.raycastTarget = false;

        RectTransform rect = obj.GetComponent<RectTransform>();
        float height = width * (dividerSprite.rect.height / dividerSprite.rect.width);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = position;
    }

    // Already signed in, go straight to the menu; otherwise ask first.
    private void OnPlayClicked()
    {
        Show(SessionData.IsLoggedIn ? Screen.MainMenu : Screen.Login);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        // Quitting does nothing in the editor, so stop play mode instead.
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- How to play ----------

    private void BuildHowToPlayScreen()
    {
        GameObject root = NewScreen(Screen.HowToPlay, "HowToPlayScreen");

        UIBuilder.Label(root.transform, "How to play", UITheme.HeadingSize,
                UITheme.Accent, new Vector2(0f, 330f), 900f, 60f);

        GameObject card = UIBuilder.Panel(root.transform, "Card", UITheme.Panel,
                new Vector2(900f, 500f), new Vector2(0f, 20f));

        string body =
            "One player is the <color=#C8362B>Kanamachi</color> - the blind bee.\n" +
            "They wear a gamcha over their eyes and cannot see the courtyard.\n\n" +
            "<color=#F2C14E>If you can see</color>\n" +
            "  Move with WASD or the arrow keys.\n" +
            "  Stay close enough to be interesting, far enough to be safe.\n" +
            "  Clap to taunt the blind bee - but a clap gives away where you are.\n\n" +
            "<color=#F2C14E>If you are blindfolded</color>\n" +
            "  You hear footsteps, louder as somebody comes nearer.\n" +
            "  Every character's footsteps sound slightly different.\n" +
            "  Press Space to grab whoever is closest.\n" +
            "  Then name them. Guess right and they take the blindfold.\n\n" +
            "<color=#F2C14E>Scoring</color>\n" +
            "  Correct guess  +10      Wrong guess  -5      Escaping  +3\n\n" +
            "Four courtyards, twelve rounds. Most points wins.";

        UIBuilder.Label(card.transform, body, UITheme.BodySize, UITheme.TextPrimary,
                new Vector2(0f, 0f), 840f, 460f, TextAlignmentOptions.TopLeft);

        UIBuilder.TextButton(root.transform, "Back", new Vector2(0f, -330f),
                () => Show(Screen.Title), false, 240f);
    }

    // ---------- Settings ----------

    private void BuildSettingsScreen()
    {
        GameObject root = NewScreen(Screen.Settings, "SettingsScreen");

        UIBuilder.Label(root.transform, "Settings", UITheme.HeadingSize,
                UITheme.Accent, new Vector2(0f, 270f), 900f, 60f);

        GameObject card = UIBuilder.Panel(root.transform, "Card", UITheme.Panel,
                new Vector2(620f, 340f), new Vector2(0f, 20f));

        UIBuilder.Label(card.transform, "Effects volume", UITheme.BodySize,
                UITheme.TextPrimary, new Vector2(-150f, 100f), 300f, 34f,
                TextAlignmentOptions.Left);
        BuildVolumeRow(card.transform, 60f, true);

        UIBuilder.Label(card.transform, "Ambience volume", UITheme.BodySize,
                UITheme.TextPrimary, new Vector2(-150f, 0f), 300f, 34f,
                TextAlignmentOptions.Left);
        BuildVolumeRow(card.transform, -40f, false);

        UIBuilder.Label(card.transform, "Sound is the main way a blindfolded player finds anyone.",
                UITheme.SmallSize, UITheme.TextMuted, new Vector2(0f, -120f), 560f, 40f);

        UIBuilder.TextButton(root.transform, "Back", new Vector2(0f, -270f),
                () => Show(Screen.Title), false, 240f);
    }

    // Stepped rather than a slider, because five clear levels are easier to
    // set with a mouse than a thin bar, and they survive any resolution.
    private void BuildVolumeRow(Transform parent, float y, bool effects)
    {
        for (int i = 0; i <= 4; i++)
        {
            int level = i;
            float value = level / 4f;

            UIBuilder.TextButton(parent, level == 0 ? "off" : level.ToString(),
                    new Vector2(-120f + level * 70f, y),
                    () =>
                    {
                        if (GameAudio.Instance == null) return;

                        if (effects) GameAudio.Instance.effectsVolume = value;
                        else GameAudio.Instance.ambienceVolume = value;
                    },
                    false, 60f);
        }
    }

    private void BuildLoginScreen()
    {
        GameObject root = NewScreen(Screen.Login, "LoginScreen");

        // The artwork sits behind everything; the form is pushed to the
        // right so the light circle is not covered by it.
        if (landingArt != null) landingArt.Build(root.transform);

        UIBuilder.Label(root.transform, "KANAMACHI", 76,
                UITheme.Accent, new Vector2(540f, 250f), 700f, 100f);

        UIBuilder.Label(root.transform, "the blind bee is listening for you",
                UITheme.SmallSize, UITheme.TextMuted, new Vector2(540f, 190f), 700f, 34f);

        GameObject card = UIBuilder.Panel(root.transform, "Card",
                new Color(UITheme.Panel.r, UITheme.Panel.g, UITheme.Panel.b, 0.94f),
                new Vector2(460f, 360f), new Vector2(540f, -60f));

        UIBuilder.Label(card.transform, "Sign in", UITheme.HeadingSize,
                UITheme.TextPrimary, new Vector2(0f, 130f), 420f, 50f);

        usernameField = UIBuilder.InputField(card.transform, "Username", new Vector2(0f, 58f));
        passwordField = UIBuilder.InputField(card.transform, "Password", new Vector2(0f, -2f), true);

        UIBuilder.TextButton(card.transform, "Log in", new Vector2(0f, -74f), OnLoginClicked);
        UIBuilder.TextButton(card.transform, "Create an account", new Vector2(0f, -140f),
                OnRegisterClicked, false);

        loginStatus = UIBuilder.Label(root.transform, "", UITheme.SmallSize,
                UITheme.Danger, new Vector2(540f, -280f), 640f, 60f);

        UIBuilder.MenuItem(root.transform, "BACK", new Vector2(540f, -340f),
                () => Show(Screen.Title));
    }

    private void OnLoginClicked()
    {
        if (!ValidateCredentials()) return;

        SetLoginStatus("Signing in...", UITheme.TextMuted);

        ApiClient.Instance.Login(usernameField.text.Trim(), passwordField.text,
                auth => Show(Screen.MainMenu),
                error => SetLoginStatus(error, UITheme.Danger));
    }

    private void OnRegisterClicked()
    {
        if (!ValidateCredentials()) return;

        string username = usernameField.text.Trim();
        SetLoginStatus("Creating your account...", UITheme.TextMuted);

        ApiClient.Instance.Register(username, username + "@kanamachi.local", passwordField.text,
                auth => Show(Screen.MainMenu),
                error => SetLoginStatus(error, UITheme.Danger));
    }

    private bool ValidateCredentials()
    {
        if (string.IsNullOrWhiteSpace(usernameField.text))
        {
            SetLoginStatus("Enter a username.", UITheme.Danger);
            return false;
        }

        if (passwordField.text.Length < 6)
        {
            SetLoginStatus("Password needs at least 6 characters.", UITheme.Danger);
            return false;
        }

        return true;
    }

    private void SetLoginStatus(string message, Color colour)
    {
        if (loginStatus == null) return;
        loginStatus.text = message;
        loginStatus.color = colour;
    }

    // ---------- Main menu ----------

    private void BuildMainMenuScreen()
    {
        GameObject root = NewScreen(Screen.MainMenu, "MainMenuScreen");

        if (landingArt != null) landingArt.Build(root.transform);

        UIBuilder.Label(root.transform, "KANAMACHI", 64,
                UITheme.Accent, new Vector2(540f, 250f), 700f, 90f);

        signedInLabel = UIBuilder.Label(root.transform, "", UITheme.SmallSize,
                UITheme.TextMuted, new Vector2(540f, 196f), 700f, 30f);

        UIBuilder.TextButton(root.transform, "Create a room", new Vector2(540f, 80f),
                () => { Show(Screen.Room); OnCreateRoomClicked(); });

        UIBuilder.TextButton(root.transform, "Join a room", new Vector2(540f, 14f),
                () => Show(Screen.Room), false);

        UIBuilder.TextButton(root.transform, "Leaderboard", new Vector2(540f, -52f), () =>
        {
            Show(Screen.Leaderboard);
            LoadLeaderboard("score");
        }, false);

        UIBuilder.TextButton(root.transform, "My profile", new Vector2(540f, -118f), () =>
        {
            Show(Screen.Profile);
            LoadProfile();
        }, false);

        UIBuilder.TextButton(root.transform, "Log out", new Vector2(540f, -210f), () =>
        {
            SessionData.ClearAll();
            Show(Screen.Title);
        }, false);
    }

    // ---------- Leaderboard ----------

    private void BuildLeaderboardScreen()
    {
        GameObject root = NewScreen(Screen.Leaderboard, "LeaderboardScreen");

        UIBuilder.Label(root.transform, "Kanamachi Masters", UITheme.HeadingSize,
                UITheme.Accent, new Vector2(0f, 330f), 900f, 60f);

        GameObject card = UIBuilder.Panel(root.transform, "Card", UITheme.Panel,
                new Vector2(760f, 480f), new Vector2(0f, 20f));

        // Column headings, so the numbers are readable without a legend.
        UIBuilder.Label(card.transform, "Player", UITheme.SmallSize, UITheme.TextMuted,
                new Vector2(-250f, 205f), 240f, 28f, TextAlignmentOptions.Left);
        UIBuilder.Label(card.transform, "Played    Won    Points", UITheme.SmallSize,
                UITheme.TextMuted, new Vector2(180f, 205f), 360f, 28f, TextAlignmentOptions.Right);

        leaderboardList = UIBuilder.Label(card.transform, "Loading...", UITheme.BodySize,
                UITheme.TextPrimary, new Vector2(0f, -20f), 700f, 400f, TextAlignmentOptions.TopLeft);

        UIBuilder.TextButton(root.transform, "By points", new Vector2(-180f, -300f),
                () => LoadLeaderboard("score"), true, 240f);
        UIBuilder.TextButton(root.transform, "By wins", new Vector2(80f, -300f),
                () => LoadLeaderboard("wins"), false, 240f);
        UIBuilder.TextButton(root.transform, "Back", new Vector2(0f, -370f),
                () => Show(Screen.MainMenu), false, 240f);
    }

    private void LoadLeaderboard(string sortBy)
    {
        leaderboardList.text = "Loading...";

        ApiClient.Instance.GetLeaderboard(sortBy, entries =>
        {
            if (entries.Length == 0)
            {
                leaderboardList.text = "No matches have been played yet.";
                return;
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();

            for (int i = 0; i < entries.Length; i++)
            {
                ApiClient.PlayerStats entry = entries[i];
                bool isMe = entry.userId == SessionData.UserId;

                // Padded so the columns line up in a single text block.
                string row = $"{i + 1,2}.  {Pad(entry.username, 16)}" +
                             $"{entry.matchesPlayed,6}{entry.matchesWon,7}{entry.totalScore,9}";

                builder.AppendLine(isMe ? $"<color=#F2C14E>{row}</color>" : row);
            }

            leaderboardList.text = builder.ToString();
        },
        error => leaderboardList.text = error);
    }

    private string Pad(string value, int width)
    {
        if (string.IsNullOrEmpty(value)) value = "-";
        if (value.Length > width) value = value.Substring(0, width - 1) + "\u2026";
        return value.PadRight(width);
    }

    // ---------- Profile ----------

    private void BuildProfileScreen()
    {
        GameObject root = NewScreen(Screen.Profile, "ProfileScreen");

        UIBuilder.Label(root.transform, "Your record", UITheme.HeadingSize,
                UITheme.Accent, new Vector2(0f, 330f), 900f, 60f);

        GameObject card = UIBuilder.Panel(root.transform, "Card", UITheme.Panel,
                new Vector2(680f, 480f), new Vector2(0f, 20f));

        profileBody = UIBuilder.Label(card.transform, "Loading...", UITheme.BodySize,
                UITheme.TextPrimary, new Vector2(0f, 0f), 620f, 440f, TextAlignmentOptions.TopLeft);

        UIBuilder.TextButton(root.transform, "Back", new Vector2(0f, -330f),
                () => Show(Screen.MainMenu), false, 240f);
    }

    private void LoadProfile()
    {
        profileBody.text = "Loading...";

        ApiClient.Instance.GetMyStats(stats =>
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();

            builder.AppendLine($"<color=#F2C14E>{stats.username}</color>");
            builder.AppendLine();
            builder.AppendLine($"Matches played      {stats.matchesPlayed}");
            builder.AppendLine($"Matches won         {stats.matchesWon}");
            builder.AppendLine($"Win rate            {stats.winRate * 100f:F0}%");
            builder.AppendLine($"Total points        {stats.totalScore}");
            builder.AppendLine($"Best match          {stats.highestScore}");
            builder.AppendLine();
            builder.AppendLine($"Correct guesses     {stats.correctGuesses}");
            builder.AppendLine($"Wrong guesses       {stats.wrongGuesses}");
            builder.AppendLine($"Times caught        {stats.timesCaught}");

            profileBody.text = builder.ToString();

            // Achievements are fetched separately and appended, so a slow
            // second request never blocks the numbers from appearing.
            ApiClient.Instance.GetMyAchievements(unlocked =>
            {
                System.Text.StringBuilder extra = new System.Text.StringBuilder(profileBody.text);
                extra.AppendLine();
                extra.AppendLine("<color=#F2C14E>Achievements</color>");

                if (unlocked.Length == 0)
                {
                    extra.AppendLine("None yet - finish a match to earn your first.");
                }
                else
                {
                    foreach (ApiClient.Achievement a in unlocked)
                    {
                        extra.AppendLine($"  {a.title} - {a.description}");
                    }
                }

                profileBody.text = extra.ToString();
            },
            error => { });
        },
        error => profileBody.text = "No record yet. Play a match and it will appear here.");
    }

    // ---------- Room ----------

    private void BuildRoomScreen()
    {
        GameObject root = NewScreen(Screen.Room, "RoomScreen");

        UIBuilder.Label(root.transform, "Rooms", UITheme.HeadingSize,
                UITheme.TextPrimary, new Vector2(0f, 230f), 600f, 50f);

        GameObject card = UIBuilder.Panel(root.transform, "Card", UITheme.Panel,
                new Vector2(480f, 340f), new Vector2(0f, 0f));

        UIBuilder.Label(card.transform, "Join with a code", UITheme.BodySize,
                UITheme.TextMuted, new Vector2(0f, 100f), 380f, 30f);

        roomCodeField = UIBuilder.InputField(card.transform, "Room code", new Vector2(0f, 50f));
        roomCodeField.characterLimit = 8;

        UIBuilder.TextButton(card.transform, "Join", new Vector2(0f, -10f), OnJoinRoomClicked);
        UIBuilder.TextButton(card.transform, "Create a new room", new Vector2(0f, -70f),
                OnCreateRoomClicked, false);
        UIBuilder.TextButton(card.transform, "Back", new Vector2(0f, -128f),
                () => Show(Screen.MainMenu), false);

        roomStatus = UIBuilder.Label(root.transform, "", UITheme.SmallSize,
                UITheme.Danger, new Vector2(0f, -220f), 700f, 60f);
    }

    private void OnCreateRoomClicked()
    {
        roomStatus.text = "Creating a room...";
        roomStatus.color = UITheme.TextMuted;

        ApiClient.Instance.CreateRoom(4,
                room => EnterLobby(room),
                error => { roomStatus.text = error; roomStatus.color = UITheme.Danger; });
    }

    private void OnJoinRoomClicked()
    {
        roomStatus.text = "Joining...";
        roomStatus.color = UITheme.TextMuted;

        ApiClient.Instance.JoinRoom(roomCodeField.text,
                room => EnterLobby(room),
                error => { roomStatus.text = error; roomStatus.color = UITheme.Danger; });
    }

    // ---------- Lobby ----------

    private void BuildLobbyScreen()
    {
        GameObject root = NewScreen(Screen.Lobby, "LobbyScreen");

        UIBuilder.Label(root.transform, "Waiting in the courtyard", UITheme.HeadingSize,
                UITheme.TextPrimary, new Vector2(0f, 250f), 800f, 50f);

        GameObject card = UIBuilder.Panel(root.transform, "Card", UITheme.Panel,
                new Vector2(560f, 380f), new Vector2(0f, 10f));

        UIBuilder.Label(card.transform, "Room code", UITheme.SmallSize,
                UITheme.TextMuted, new Vector2(0f, 126f), 460f, 26f);

        lobbyCodeLabel = UIBuilder.Label(card.transform, "------", UITheme.TitleSize,
                UITheme.Accent, new Vector2(0f, 80f), 460f, 60f);

        lobbyPlayersLabel = UIBuilder.Label(card.transform, "", UITheme.BodySize,
                UITheme.TextPrimary, new Vector2(0f, -10f), 460f, 120f);

        UIBuilder.TextButton(card.transform, "Refresh", new Vector2(-90f, -120f),
                RefreshRoom, false, 170f);

        UIBuilder.TextButton(card.transform, "Leave", new Vector2(90f, -120f), () =>
        {
            if (NetworkClient.Instance != null) NetworkClient.Instance.Disconnect();
            SessionData.ClearRoom();
            Show(Screen.MainMenu);
        }, false, 170f);

        lobbyStatus = UIBuilder.Label(root.transform, "", UITheme.SmallSize,
                UITheme.TextMuted, new Vector2(0f, -230f), 800f, 60f);
    }

    private void EnterLobby(ApiClient.RoomResponse room)
    {
        Show(Screen.Lobby);

        lobbyCodeLabel.text = room.roomCode;
        UpdatePlayerList(room);

        SetLobbyStatus(SessionData.IsHost
                ? "Share this code. The match starts when someone joins."
                : "Joined. Waiting for the match to start...");

        if (autoConnectFromLobby && NetworkClient.Instance != null)
        {
            NetworkClient.Instance.Connect();
        }
    }

    private void RefreshRoom()
    {
        if (!SessionData.IsInRoom) return;

        ApiClient.Instance.GetRoom(SessionData.RoomCode,
                room => UpdatePlayerList(room),
                error => SetLobbyStatus(error));
    }

    private void UpdatePlayerList(ApiClient.RoomResponse room)
    {
        if (lobbyPlayersLabel == null || room == null) return;

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.AppendLine($"Players {room.players.Length}/{room.maxPlayers}");
        builder.AppendLine();

        foreach (string player in room.players)
        {
            builder.AppendLine(player);
        }

        lobbyPlayersLabel.text = builder.ToString();
    }

    private void SetLobbyStatus(string message)
    {
        if (lobbyStatus != null) lobbyStatus.text = message;
    }
}
