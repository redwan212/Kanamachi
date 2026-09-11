using System.Collections.Generic;
using UnityEngine;

// Runs an online match on this machine.
//
// It owns no game rules of its own: the server decides who the Kanamachi is,
// whether a catch counted and whether a guess was right. This class listens
// for those decisions, spawns a player object for every person in the room,
// and keeps the local view in sync.
//
// The local player and the remote ones are both Player subclasses, so once
// they are spawned the rest of the code treats them identically.
public class NetworkGameManager : MonoBehaviour
{
    public static NetworkGameManager Instance;

    [Header("Setup")]
    [Tooltip("Prefab with Rigidbody2D, SpriteRenderer and a collider - but NO Player script. The correct Player type is added at runtime.")]
    public GameObject networkPlayerPrefab;

    [Tooltip("Characters handed out to players. Every client picks the same one for the same person.")]
    public Character[] characterPool;

    [Tooltip("Starting positions, chosen the same way on every client.")]
    public Vector2[] spawnPoints = new Vector2[]
    {
        new Vector2(-6f, 3f), new Vector2(6f, 3f),
        new Vector2(-6f, -3f), new Vector2(6f, -3f)
    };

    [Header("Catching")]
    [Tooltip("Key the Kanamachi presses to try to grab whoever is nearest.")]
    public KeyCode catchKey = KeyCode.Space;

    [Tooltip("Only used to pick the nearest target. The server does the real range check.")]
    public float catchReach = 2f;

    [Header("Levels and story")]
    [Tooltip("Drives the four levels, their sound cue behaviour and their story chapters.")]
    public LevelManager levelManager;

    [Tooltip("Rounds played on each level before moving to the next.")]
    public int roundsPerLevel = 3;

    [Header("References")]
    public NetworkVisionController visionController;

    [Header("Debug")]
    public bool showHud = true;

    // userId -> that person's player object on this machine
    private readonly Dictionary<string, Player> playersByUserId = new Dictionary<string, Player>();

    private Player localPlayer;
    private string kanamachiUserId;
    private int currentRound;
    private bool matchStarted;

    private int roundsPlayedInLevel;
    private readonly Dictionary<string, int> scores = new Dictionary<string, int>();
    private string leadingUserId;
    private bool matchOver;
    private string winnerUserId;
    private bool isGuessing;
    private string caughtUserId;
    private string lastMessage;
    private float lastMessageTime;

    public bool LocalPlayerIsKanamachi
    {
        get { return !string.IsNullOrEmpty(kanamachiUserId) && kanamachiUserId == SessionData.UserId; }
    }

    // Nobody moves while the Kanamachi is deciding who they caught, or
    // while a story chapter is on screen between levels.
    public bool IsInputFrozen
    {
        get
        {
            if (isGuessing || matchOver) return true;
            if (StoryManager.Instance != null && StoryManager.Instance.IsShowing) return true;
            return false;
        }
    }

    // SoundCueManager asks these so it can work in either mode.
    public Player KanamachiPlayer
    {
        get
        {
            if (string.IsNullOrEmpty(kanamachiUserId)) return null;
            playersByUserId.TryGetValue(kanamachiUserId, out Player p);
            return p;
        }
    }

    public IEnumerable<Player> AllPlayers { get { return playersByUserId.Values; } }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (NetworkClient.Instance == null)
        {
            Debug.LogWarning("[NetworkGameManager] No NetworkClient in the scene.");
            return;
        }

        NetworkClient.Instance.OnConnected += HandleConnected;
        NetworkClient.Instance.OnPlayerJoined += HandlePlayerJoined;
        NetworkClient.Instance.OnPlayerLeft += HandlePlayerLeft;
        NetworkClient.Instance.OnPlayerMoved += HandlePlayerMoved;
        NetworkClient.Instance.OnGameStarted += HandleGameStarted;
        NetworkClient.Instance.OnKanamachiChanged += HandleKanamachiChanged;
        NetworkClient.Instance.OnCatchSuccess += HandleCatchSuccess;
        NetworkClient.Instance.OnCatchRejected += HandleCatchRejected;
        NetworkClient.Instance.OnGuessResult += HandleGuessResult;
        NetworkClient.Instance.OnScoreUpdate += HandleScoreUpdate;
        NetworkClient.Instance.OnMatchOver += HandleMatchOver;
    }

    void Update()
    {
        // Only the blindfolded player can try to catch, and only while the
        // match is running and no guess is pending.
        if (!matchStarted || isGuessing) return;
        if (!LocalPlayerIsKanamachi || localPlayer == null) return;

        if (Input.GetKeyDown(catchKey))
        {
            TryCatch();
        }
    }

    private void TryCatch()
    {
        Player nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (var pair in playersByUserId)
        {
            if (pair.Key == SessionData.UserId || pair.Value == null) continue;

            float distance = Vector2.Distance(
                localPlayer.transform.position, pair.Value.transform.position);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = pair.Value;
            }
        }

        if (nearest == null)
        {
            ShowMessage("Nobody else is here yet.");
            return;
        }

        // Sent even when it looks too far - the server decides, not us.
        string targetId = UserIdOf(nearest);
        NetworkClient.Instance.SendCatchAttempt(targetId);
    }

    void OnDestroy()
    {
        if (NetworkClient.Instance == null) return;

        NetworkClient.Instance.OnConnected -= HandleConnected;
        NetworkClient.Instance.OnPlayerJoined -= HandlePlayerJoined;
        NetworkClient.Instance.OnPlayerLeft -= HandlePlayerLeft;
        NetworkClient.Instance.OnPlayerMoved -= HandlePlayerMoved;
        NetworkClient.Instance.OnGameStarted -= HandleGameStarted;
        NetworkClient.Instance.OnKanamachiChanged -= HandleKanamachiChanged;
        NetworkClient.Instance.OnCatchSuccess -= HandleCatchSuccess;
        NetworkClient.Instance.OnCatchRejected -= HandleCatchRejected;
        NetworkClient.Instance.OnGuessResult -= HandleGuessResult;
        NetworkClient.Instance.OnScoreUpdate -= HandleScoreUpdate;
        NetworkClient.Instance.OnMatchOver -= HandleMatchOver;
    }

    // ---------- Server events ----------

    private void HandleConnected()
    {
        // The server does not send a PLAYER_JOINED for the player who just
        // connected, so spawn the local one here.
        SpawnLocalPlayer();
    }

    private void HandlePlayerJoined(string userId, string username)
    {
        if (string.IsNullOrEmpty(userId)) return;
        if (userId == SessionData.UserId) return;      // that's us
        if (playersByUserId.ContainsKey(userId)) return;

        SpawnRemotePlayer(userId, username);
    }

    private void HandlePlayerLeft(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return;
        if (!playersByUserId.TryGetValue(userId, out Player player)) return;

        Debug.Log($"[NetworkGameManager] {player.DisplayName} left.");

        if (player != null) Destroy(player.gameObject);
        playersByUserId.Remove(userId);
    }

    private void HandlePlayerMoved(string playerId, float x, float y)
    {
        if (string.IsNullOrEmpty(playerId)) return;
        if (playerId == SessionData.UserId) return;    // never move ourselves from the server

        if (!playersByUserId.TryGetValue(playerId, out Player player))
        {
            // A move arrived before the join did - create the player now
            // rather than dropping the update.
            player = SpawnRemotePlayer(playerId, null);
        }

        if (player is NetworkRemotePlayer remote)
        {
            remote.SetTargetPosition(new Vector2(x, y));
        }
    }

    private void HandleGameStarted(int round)
    {
        currentRound = round;
        matchStarted = true;
        roundsPlayedInLevel = 0;

        Debug.Log($"[NetworkGameManager] Match started, round {round}.");

        // The server does not track levels, so the client drives them. Every
        // client counts the same broadcast results, so they stay in step.
        if (levelManager != null)
        {
            levelManager.LoadLevel(0);
        }
    }

    private void HandleKanamachiChanged(string newKanamachiId)
    {
        kanamachiUserId = newKanamachiId;

        // Mark every player, so any code that asks IsBlindfolded() gets the
        // right answer regardless of which machine it runs on.
        foreach (var pair in playersByUserId)
        {
            if (pair.Value != null)
            {
                pair.Value.SetBlindfolded(pair.Key == newKanamachiId);
            }
        }

        Debug.Log(LocalPlayerIsKanamachi
            ? "[NetworkGameManager] You are the Kanamachi."
            : $"[NetworkGameManager] Kanamachi is {NameOf(newKanamachiId)}.");

        // Only this machine's view changes - see NetworkVisionController.
        if (visionController != null)
        {
            visionController.Refresh(localPlayer, LocalPlayerIsKanamachi);
        }
    }

    private void HandleCatchSuccess(string kanamachiId, string caughtPlayerId)
    {
        isGuessing = true;
        caughtUserId = caughtPlayerId;

        Debug.Log($"[NetworkGameManager] {NameOf(kanamachiId)} caught somebody.");
    }

    private void HandleCatchRejected(string reason)
    {
        ShowMessage(reason == "too_far" ? "Too far away." : "You are not the Kanamachi.");
    }

    private void HandleGuessResult(bool correct, string newKanamachiId)
    {
        isGuessing = false;

        string caughtName = NameOf(caughtUserId);
        caughtUserId = null;

        ShowMessage(correct
            ? $"Correct - it was {caughtName}."
            : $"Wrong guess. It was {caughtName}.");

        // The server may have handed the blindfold to somebody new.
        HandleKanamachiChanged(newKanamachiId);

        AdvanceRound();
    }

    // Scores are never calculated here - they arrive from the server, which
    // is the only place that decides what a guess was worth.
    private void HandleScoreUpdate(Dictionary<string, int> newScores, string leader)
    {
        scores.Clear();
        foreach (var pair in newScores)
        {
            scores[pair.Key] = pair.Value;
        }

        leadingUserId = leader;
    }

    private void HandleMatchOver(Dictionary<string, int> finalScores, string winner)
    {
        HandleScoreUpdate(finalScores, winner);

        matchOver = true;
        winnerUserId = winner;
        isGuessing = false;

        // The blindfold comes off for everyone once the match ends.
        if (visionController != null)
        {
            visionController.Refresh(localPlayer, false);
        }

        Debug.Log($"[NetworkGameManager] Match over. Winner: {NameOf(winner)}");
    }

    private void AdvanceRound()
    {
        currentRound++;
        roundsPlayedInLevel++;

        if (roundsPlayedInLevel < roundsPerLevel) return;
        if (levelManager == null) return;

        roundsPlayedInLevel = 0;

        if (levelManager.AdvanceToNextLevel())
        {
            ShowMessage($"Moving on to {levelManager.GetCurrentLevelName()}.");
        }
        else
        {
            ShowMessage("That was the final level.");
        }
    }

    // ---------- Spawning ----------

    private void SpawnLocalPlayer()
    {
        if (localPlayer != null) return;
        if (!SessionData.IsLoggedIn) return;

        GameObject obj = CreatePlayerObject(SessionData.UserId, SessionData.Username);
        if (obj == null) return;

        NetworkLocalPlayer player = obj.AddComponent<NetworkLocalPlayer>();
        FinishSetup(player, SessionData.UserId, SessionData.Username);

        localPlayer = player;
        Debug.Log($"[NetworkGameManager] Spawned local player as {player.DisplayName}.");
    }

    private Player SpawnRemotePlayer(string userId, string username)
    {
        GameObject obj = CreatePlayerObject(userId, username);
        if (obj == null) return null;

        NetworkRemotePlayer player = obj.AddComponent<NetworkRemotePlayer>();
        player.UserId = userId;
        player.Username = username;

        FinishSetup(player, userId, username);

        Debug.Log($"[NetworkGameManager] Spawned remote player {player.DisplayName}.");
        return player;
    }

    private GameObject CreatePlayerObject(string userId, string username)
    {
        if (networkPlayerPrefab == null)
        {
            Debug.LogWarning("[NetworkGameManager] No networkPlayerPrefab assigned.");
            return null;
        }

        Vector2 spawn = SpawnPointFor(userId);
        GameObject obj = Instantiate(networkPlayerPrefab, spawn, Quaternion.identity);
        obj.name = string.IsNullOrEmpty(username) ? $"Player_{Short(userId)}" : $"Player_{username}";
        return obj;
    }

    private void FinishSetup(Player player, string userId, string username)
    {
        Character character = CharacterFor(userId);
        if (character != null)
        {
            player.ApplyCharacter(character);
        }

        player.SetBlindfolded(userId == kanamachiUserId);
        playersByUserId[userId] = player;
    }

    // ---------- Deterministic assignment ----------
    // Every client must give the same person the same character and spawn
    // point, or the two screens will disagree. Deriving both from the userId
    // guarantees that without any extra server messages.
    //
    // string.GetHashCode is NOT used here: it can differ between processes.

    private int StableHash(string value)
    {
        if (string.IsNullOrEmpty(value)) return 0;

        int hash = 17;
        foreach (char c in value)
        {
            hash = (hash * 31 + c) & 0x7FFFFFFF;
        }
        return hash;
    }

    private Character CharacterFor(string userId)
    {
        if (characterPool == null || characterPool.Length == 0) return null;
        return characterPool[StableHash(userId) % characterPool.Length];
    }

    private Vector2 SpawnPointFor(string userId)
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return Vector2.zero;
        return spawnPoints[StableHash(userId) % spawnPoints.Length];
    }

    // ---------- Helpers ----------

    private string UserIdOf(Player player)
    {
        foreach (var pair in playersByUserId)
        {
            if (pair.Value == player) return pair.Key;
        }
        return null;
    }

    private void ShowMessage(string message)
    {
        lastMessage = message;
        lastMessageTime = Time.time;
        Debug.Log($"[NetworkGameManager] {message}");
    }

    private string NameOf(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return "nobody";
        if (playersByUserId.TryGetValue(userId, out Player p) && p != null) return p.DisplayName;
        return Short(userId);
    }

    private string Short(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return "?";
        return userId.Length <= 6 ? userId : userId.Substring(userId.Length - 6);
    }

    void OnGUI()
    {
        if (matchOver)
        {
            DrawMatchOverPanel();
            return;
        }

        if (!showHud) return;

        DrawHud();

        // Only the Kanamachi picks - everyone else waits and watches.
        if (isGuessing && LocalPlayerIsKanamachi)
        {
            DrawGuessButtons();
        }
        else if (isGuessing)
        {
            GUIStyle waiting = new GUIStyle();
            waiting.fontSize = 20;
            waiting.alignment = TextAnchor.MiddleCenter;
            waiting.normal.textColor = Color.white;
            GUI.Label(new Rect(Screen.width / 2f - 200, Screen.height / 2f - 20, 400, 30),
                "The Kanamachi is guessing...", waiting);
        }
    }

    private void DrawMatchOverPanel()
    {
        GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);

        GUIStyle titleStyle = new GUIStyle();
        titleStyle.fontSize = 30;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = Color.yellow;

        GUIStyle textStyle = new GUIStyle();
        textStyle.fontSize = 18;
        textStyle.alignment = TextAnchor.MiddleCenter;
        textStyle.normal.textColor = Color.white;

        float centerX = Screen.width / 2f - 250f;
        float y = Screen.height / 2f - 160f;

        GUI.Label(new Rect(centerX, y, 500, 40), "Match over", titleStyle);
        y += 50f;

        bool localWon = !string.IsNullOrEmpty(winnerUserId) && winnerUserId == SessionData.UserId;

        GUI.Label(new Rect(centerX, y, 500, 26), localWon ? "You are the Kanamachi Master" : "Kanamachi Master", textStyle);
        y += 30f;

        GUI.Label(new Rect(centerX, y, 500, 40), NameOf(winnerUserId), titleStyle);
        y += 56f;

        foreach (var pair in scores)
        {
            GUI.Label(new Rect(centerX, y, 500, 24), $"{NameOf(pair.Key)}   {pair.Value} pts", textStyle);
            y += 26f;
        }
    }

    private void DrawHud()
    {
        GUIStyle title = new GUIStyle();
        title.fontSize = 18;
        title.normal.textColor = Color.yellow;

        GUIStyle small = new GUIStyle();
        small.fontSize = 15;
        small.normal.textColor = Color.white;

        // Anchored with enough margin that long names are not clipped.
        float width = 300f;
        float x = Screen.width - width - 20f;
        float y = 12f;

        string you = localPlayer != null ? localPlayer.DisplayName : "(not spawned)";
        GUI.Label(new Rect(x, y, width, 24), $"You: {you}", title);
        y += 26f;

        GUI.Label(new Rect(x, y, width, 22),
            matchStarted ? $"Round {currentRound}" : "Waiting for another player...", small);
        y += 22f;

        GUI.Label(new Rect(x, y, width, 22),
            LocalPlayerIsKanamachi ? "You are the KANAMACHI" : $"Kanamachi: {NameOf(kanamachiUserId)}", small);
        y += 22f;

        GUI.Label(new Rect(x, y, width, 22), $"In room: {playersByUserId.Count}", small);
        y += 22f;

        if (levelManager != null && levelManager.CurrentLevel != null)
        {
            GUI.Label(new Rect(x, y, width, 22),
                $"{levelManager.GetCurrentLevelName()} - round {roundsPlayedInLevel + 1}/{roundsPerLevel}", small);
            y += 22f;
        }

        if (LocalPlayerIsKanamachi && matchStarted && !isGuessing)
        {
            GUI.Label(new Rect(x, y, width, 22), $"Press {catchKey} to grab someone", small);
            y += 22f;
        }

        if (scores.Count > 0)
        {
            y += 6f;
            GUI.Label(new Rect(x, y, width, 22), "Scores", title);
            y += 24f;

            foreach (var pair in scores)
            {
                bool leading = pair.Key == leadingUserId;

                GUIStyle row = new GUIStyle();
                row.fontSize = 15;
                row.normal.textColor = leading ? Color.yellow : Color.white;

                string label = $"{NameOf(pair.Key)}: {pair.Value}" + (leading ? "  (leading)" : "");
                GUI.Label(new Rect(x, y, width, 22), label, row);
                y += 20f;
            }
        }

        // Feedback such as "Too far away" fades after a few seconds.
        if (!string.IsNullOrEmpty(lastMessage) && Time.time - lastMessageTime < 3f)
        {
            GUIStyle msg = new GUIStyle();
            msg.fontSize = 16;
            msg.normal.textColor = Color.cyan;
            GUI.Label(new Rect(x, y + 6f, width, 40), lastMessage, msg);
        }
    }

    private void DrawGuessButtons()
    {
        GUIStyle prompt = new GUIStyle();
        prompt.fontSize = 22;
        prompt.alignment = TextAnchor.MiddleCenter;
        prompt.normal.textColor = Color.red;

        const float buttonWidth = 150f;
        const float buttonHeight = 40f;
        const float gap = 12f;
        const int columns = 2;

        int count = 0;
        foreach (var pair in playersByUserId)
        {
            if (pair.Key != SessionData.UserId && pair.Value != null) count++;
        }

        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));
        float gridWidth = columns * buttonWidth + (columns - 1) * gap;
        float gridHeight = rows * buttonHeight + (rows - 1) * gap;

        float startX = Screen.width / 2f - gridWidth / 2f;
        float startY = Screen.height / 2f - gridHeight / 2f;

        GUI.Label(new Rect(Screen.width / 2f - 200, startY - 50, 400, 30),
            "Who did you catch?", prompt);

        int index = 0;
        foreach (var pair in playersByUserId)
        {
            if (pair.Key == SessionData.UserId || pair.Value == null) continue;

            int row = index / columns;
            int column = index % columns;

            float bx = startX + column * (buttonWidth + gap);
            float by = startY + row * (buttonHeight + gap);

            if (GUI.Button(new Rect(bx, by, buttonWidth, buttonHeight), pair.Value.DisplayName))
            {
                NetworkClient.Instance.SendGuess(pair.Key);
            }

            index++;
        }
    }
}
