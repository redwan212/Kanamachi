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

    // userId -> that person's player object on this machine
    private readonly Dictionary<string, Player> playersByUserId = new Dictionary<string, Player>();

    // Remembered separately, so a name is available even before that
    // person's player object exists on this machine.
    private readonly Dictionary<string, string> usernamesByUserId = new Dictionary<string, string>();

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

    // Exposed so the vision controller can check the current state itself
    // instead of depending on being told at the right moment.
    public Player LocalPlayer { get { return localPlayer; } }

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
        // A second copy would take over the static reference while the first
        // one kept the real state, leaving anything that reads Instance
        // looking at an empty manager.
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[NetworkGameManager] A second instance was found and removed.");
            Destroy(this);
            return;
        }

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
        // The spawn and the server's announcement can land in either order,
        // so the reference is recovered here rather than assumed.
        if (localPlayer == null && !string.IsNullOrEmpty(SessionData.UserId))
        {
            if (playersByUserId.TryGetValue(SessionData.UserId, out Player mine) && mine != null)
            {
                localPlayer = mine;
            }
        }

        // Pushed from here rather than pulled from a static, so the manager
        // that actually holds the state is always the one driving the view.
        if (visionController != null)
        {
            visionController.SetState(localPlayer, LocalPlayerIsKanamachi);
        }

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
        // A fresh connection means a fresh match - without this, a client
        // that has already seen a result screen stays stuck on it.
        ResetMatchState();

        // The server does not send a PLAYER_JOINED for the player who just
        // connected, so spawn the local one here.
        SpawnLocalPlayer();
    }

    private void HandlePlayerJoined(string userId, string username)
    {
        if (string.IsNullOrEmpty(userId)) return;

        if (!string.IsNullOrEmpty(username)) usernamesByUserId[userId] = username;

        if (userId == SessionData.UserId) return;      // that's us

        // A movement update can arrive before the join does, in which case
        // the player was spawned without a name. Fill it in rather than
        // leaving a raw id on screen until the next round.
        if (playersByUserId.TryGetValue(userId, out Player existing))
        {
            if (existing is NetworkRemotePlayer remote && !string.IsNullOrEmpty(username))
            {
                remote.Username = username;
                remote.gameObject.name = $"Player_{username}";
                RefreshHud();
            }
            return;
        }

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

        if (UIManager.Instance != null) UIManager.Instance.Show(UIManager.Screen.InGame);
        RefreshHud();

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

        RefreshHud();

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

        OpenGuessPanel();
        RefreshHud();
    }

    private void HandleCatchRejected(string reason)
    {
        ShowMessage(reason == "too_far" ? "Too far - get closer." : "You are not the Kanamachi.");
    }

    private void HandleGuessResult(bool correct, string newKanamachiId)
    {
        isGuessing = false;

        if (UIGameHud.Instance != null) UIGameHud.Instance.HideGuessPanel();

        string caughtName = NameOf(caughtUserId);
        caughtUserId = null;

        ShowMessage(correct
            ? $"Correct - it was {caughtName}."
            : $"Wrong guess. It was {caughtName}.");

        // The server may have handed the blindfold to somebody new.
        HandleKanamachiChanged(newKanamachiId);

        AdvanceRound();
        RefreshHud();
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
        RefreshHud();
    }

    private void ResetMatchState()
    {
        matchOver = false;
        winnerUserId = null;
        isGuessing = false;
        caughtUserId = null;
        matchStarted = false;
        currentRound = 0;
        roundsPlayedInLevel = 0;
        scores.Clear();
        leadingUserId = null;

        foreach (var pair in playersByUserId)
        {
            if (pair.Value != null) Destroy(pair.Value.gameObject);
        }
        playersByUserId.Clear();
        usernamesByUserId.Clear();
        localPlayer = null;

        if (UIGameHud.Instance != null) UIGameHud.Instance.SetVisible(false);
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

        bool localWon = !string.IsNullOrEmpty(winner) && winner == SessionData.UserId;

        if (UIGameHud.Instance != null)
        {
            UIGameHud.Instance.SetVisible(true);
            UIGameHud.Instance.ShowResult(NameOf(winner), localWon, scores, NameOf);
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

        // The server can announce the Kanamachi before this object exists,
        // in which case the earlier vision update had nobody to darken the
        // world around. Re-applying it here covers that ordering.
        if (visionController != null)
        {
            visionController.Refresh(localPlayer, LocalPlayerIsKanamachi);
        }

        RefreshHud();
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
        if (UIGameHud.Instance != null) UIGameHud.Instance.ShowToast(message);
        Debug.Log($"[NetworkGameManager] {message}");
    }

    private string NameOf(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return "nobody";

        if (playersByUserId.TryGetValue(userId, out Player p) && p != null)
        {
            return p.DisplayName;
        }

        // Scores can mention somebody whose player object has not been
        // created here yet, so fall back to the username before the id.
        if (usernamesByUserId.TryGetValue(userId, out string username)) return username;

        return Short(userId);
    }

    private string Short(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return "?";
        return userId.Length <= 6 ? userId : userId.Substring(userId.Length - 6);
    }

    // ---------- Display ----------
    // All drawing lives in UIGameHud. This class only tells it what the
    // server has said, so the interface can change without the game logic
    // being touched.

    private void RefreshHud()
    {
        if (UIGameHud.Instance == null) return;

        UIGameHud.Instance.SetVisible(matchStarted || matchOver);

        string levelName = levelManager != null && levelManager.CurrentLevel != null
                ? levelManager.GetCurrentLevelName()
                : null;

        UIGameHud.Instance.SetLevel(levelName, roundsPlayedInLevel + 1, roundsPerLevel,
                playersByUserId.Count);

        UIGameHud.Instance.SetRole(
                localPlayer != null ? localPlayer.DisplayName : "-",
                LocalPlayerIsKanamachi,
                NameOf(kanamachiUserId),
                matchStarted && !isGuessing);

        UIGameHud.Instance.SetScores(scores, NameOf, leadingUserId);
    }

    private void OpenGuessPanel()
    {
        if (UIGameHud.Instance == null) return;

        if (!LocalPlayerIsKanamachi)
        {
            UIGameHud.Instance.ShowWaitingForGuess(NameOf(kanamachiUserId));
            return;
        }

        List<KeyValuePair<string, string>> options = new List<KeyValuePair<string, string>>();
        foreach (var pair in playersByUserId)
        {
            if (pair.Key == SessionData.UserId || pair.Value == null) continue;
            options.Add(new KeyValuePair<string, string>(pair.Key, pair.Value.DisplayName));
        }

        UIGameHud.Instance.ShowGuessPanel(options, guessedUserId =>
        {
            UIGameHud.Instance.HideGuessPanel();
            NetworkClient.Instance.SendGuess(guessedUserId);
        });
    }
}
