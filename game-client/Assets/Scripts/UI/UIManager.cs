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
        Login,
        MainMenu,
        Room,
        Lobby,
        InGame
    }

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

    // Room
    private TMP_InputField roomCodeField;
    private TextMeshProUGUI roomStatus;

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
        BuildCanvas();
        BuildLoginScreen();
        BuildMainMenuScreen();
        BuildRoomScreen();
        BuildLobbyScreen();

        Show(Screen.Login);
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

    private void BuildLoginScreen()
    {
        GameObject root = NewScreen(Screen.Login, "LoginScreen");

        UIBuilder.Label(root.transform, "KANAMACHI", UITheme.TitleSize,
                UITheme.Accent, new Vector2(0f, 260f), 800f, 70f);

        UIBuilder.Label(root.transform, "The blind bee is looking for you",
                UITheme.SmallSize, UITheme.TextMuted, new Vector2(0f, 210f), 800f, 30f);

        GameObject card = UIBuilder.Panel(root.transform, "Card", UITheme.Panel,
                new Vector2(480f, 380f), new Vector2(0f, -30f));

        UIBuilder.Label(card.transform, "Sign in", UITheme.HeadingSize,
                UITheme.TextPrimary, new Vector2(0f, 140f), 440f, 50f);

        usernameField = UIBuilder.InputField(card.transform, "Username", new Vector2(0f, 66f));
        passwordField = UIBuilder.InputField(card.transform, "Password", new Vector2(0f, 4f), true);

        UIBuilder.TextButton(card.transform, "Log in", new Vector2(0f, -70f), OnLoginClicked);
        UIBuilder.TextButton(card.transform, "Create an account", new Vector2(0f, -140f),
                OnRegisterClicked, false);

        loginStatus = UIBuilder.Label(root.transform, "", UITheme.SmallSize,
                UITheme.Danger, new Vector2(0f, -220f), 700f, 60f);
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

        UIBuilder.Label(root.transform, "KANAMACHI", UITheme.TitleSize,
                UITheme.Accent, new Vector2(0f, 230f), 800f, 70f);

        UIBuilder.TextButton(root.transform, "Create a room", new Vector2(0f, 80f),
                () => { Show(Screen.Room); OnCreateRoomClicked(); });

        UIBuilder.TextButton(root.transform, "Join a room", new Vector2(0f, 14f),
                () => Show(Screen.Room), false);

        UIBuilder.TextButton(root.transform, "Log out", new Vector2(0f, -100f), () =>
        {
            SessionData.ClearAll();
            Show(Screen.Login);
        }, false);
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
