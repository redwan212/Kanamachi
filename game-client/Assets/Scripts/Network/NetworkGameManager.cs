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

    [Header("Rounds")]
    [Tooltip("Seconds of safe time after each round starts, so nobody is caught where they were standing a moment ago.")]
    public float roundStartDelay = 2.5f;

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

    [Header("AI")]
    [Tooltip("How often the host reports each AI's position, in seconds.")]
    public float aiReportInterval = 0.1f;

    [Tooltip("Seconds a blindfolded AI waits before naming who it caught.")]
    public float aiGuessDelay = 1.6f;

    [Tooltip("Prefab used for AI players. Leave empty to reuse the network player prefab.")]
    public GameObject aiPlayerPrefab;

    [Header("Feel")]
    [Tooltip("Small sprite puffed up under a player's feet as they walk.")]
    public Sprite dustSprite;

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
    private readonly HashSet<string> aiUserIds = new HashSet<string>();
    private float aiReportTimer;
    private float aiGuessAt;
    private float roundStartTimer;
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
    // True during the safe period at the start of a round.
    public bool IsRoundStarting { get { return roundStartTimer > 0f; } }

    public bool IsInputFrozen
    {
        get
        {
            if (isGuessing || matchOver) return true;
            if (roundStartTimer > 0f) return true;

            // Somebody reading the pause menu should not be walking into a
            // wall while they do it.
            if (UIGameHud.Instance != null && UIGameHud.Instance.IsPaused) return true;
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
        NetworkClient.Instance.OnPlayerClapped += HandleClap;
        NetworkClient.Instance.OnLobbyState += HandleLobbyState;
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

        // The host reports what the AI are doing. Without this the server
        // never learns where they are, and a blindfolded AI could never
        // grab anybody.
        if (roundStartTimer > 0f)
        {
            roundStartTimer -= Time.deltaTime;

            if (roundStartTimer <= 0f && UIGameHud.Instance != null)
            {
                UIGameHud.Instance.ShowToast("Go", 0.8f);
            }
        }

        if (IsHost) DriveAi();

        // Only the blindfolded player can try to catch, and only while the
        // match is running and no guess is pending.
        if (!matchStarted || isGuessing || IsRoundStarting) return;
        if (!LocalPlayerIsKanamachi || localPlayer == null) return;

        if (Input.GetKeyDown(catchKey))
        {
            TryCatch();
        }
    }

    private bool IsHost
    {
        get
        {
            return lobbyState != null
                && !string.IsNullOrEmpty(SessionData.UserId)
                && SessionData.UserId == lobbyState.hostUserId;
        }
    }

    private void DriveAi()
    {
        if (aiUserIds.Count == 0 || NetworkClient.Instance == null) return;

        // Positions, throttled the same way a person's are.
        aiReportTimer -= Time.deltaTime;
        if (aiReportTimer <= 0f)
        {
            aiReportTimer = aiReportInterval;

            foreach (string aiId in aiUserIds)
            {
                if (playersByUserId.TryGetValue(aiId, out Player ai) && ai != null)
                {
                    NetworkClient.Instance.SendPositionAs(aiId, ai.transform.position);
                }
            }
        }

        if (string.IsNullOrEmpty(kanamachiUserId) || !aiUserIds.Contains(kanamachiUserId)) return;
        if (IsRoundStarting) return;

        if (isGuessing)
        {
            DriveAiGuess();
            return;
        }

        if (matchStarted) DriveAiCatch();
    }

    // A blindfolded AI grabs whoever comes within reach, the same rule the
    // server applies to a person pressing Space.
    private void DriveAiCatch()
    {
        if (!playersByUserId.TryGetValue(kanamachiUserId, out Player kanamachi) || kanamachi == null)
        {
            return;
        }

        foreach (var pair in playersByUserId)
        {
            if (pair.Key == kanamachiUserId || pair.Value == null) continue;

            float distance = Vector2.Distance(
                    kanamachi.transform.position, pair.Value.transform.position);

            if (distance <= catchReach * 0.8f)
            {
                NetworkClient.Instance.SendCatchAttemptAs(kanamachiUserId, pair.Key);
                return;
            }
        }
    }

    // It pauses first, so the guess does not appear instantly and read as a
    // machine answering. Then it picks at random - an AI has no better
    // information than a blindfolded person does.
    private void DriveAiGuess()
    {
        if (Time.time < aiGuessAt) return;

        List<string> options = new List<string>();
        foreach (var pair in playersByUserId)
        {
            if (pair.Key != kanamachiUserId && pair.Value != null) options.Add(pair.Key);
        }

        if (options.Count == 0) return;

        string guess = options[Random.Range(0, options.Count)];
        NetworkClient.Instance.SendGuessAs(kanamachiUserId, guess);

        // Stops a second guess going out before the result comes back.
        aiGuessAt = Time.time + 5f;
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
        NetworkClient.Instance.OnPlayerClapped -= HandleClap;
        NetworkClient.Instance.OnLobbyState -= HandleLobbyState;
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

    // Remembered from the lobby so the match can spawn the AI slots and use
    // the characters people actually chose, rather than guessing from ids.
    private NetworkClient.LobbyState lobbyState;

    // A clap is heard by everyone, and panned from where the clapper is -
    // which is exactly the risk they took by doing it.
    private void HandleClap(string userId)
    {
        if (!playersByUserId.TryGetValue(userId, out Player clapper) || clapper == null) return;

        if (GameAudio.Instance != null)
        {
            Player listener = localPlayer != null ? localPlayer : clapper;
            GameAudio.Instance.PlayClap(listener.transform.position, clapper.transform.position);
        }

        // Only the blindfolded player is told a clap happened. Everyone else
        // can see who did it.
        if (LocalPlayerIsKanamachi && UIGameHud.Instance != null)
        {
            UIGameHud.Instance.ShowToast("Somebody clapped.", 1.2f);
        }
    }

    private void HandleLobbyState(NetworkClient.LobbyState state)
    {
        lobbyState = state;

        if (UIManager.Instance != null) UIManager.Instance.ApplyLobbyState(state);
    }

    // Chosen in the lobby if it was; otherwise derived from the id, which
    // keeps older flows working.
    private Character ChosenCharacterFor(string userId)
    {
        if (lobbyState == null || lobbyState.slots == null) return CharacterFor(userId);

        foreach (NetworkClient.LobbySlot slot in lobbyState.slots)
        {
            if (slot.userId != userId || string.IsNullOrEmpty(slot.character)) continue;

            foreach (Character candidate in characterPool)
            {
                if (candidate != null && candidate.characterName == slot.character)
                {
                    return candidate;
                }
            }
        }

        return CharacterFor(userId);
    }

    private void HandleGameStarted(int round)
    {
        currentRound = round;
        matchStarted = true;
        roundsPlayedInLevel = 0;

        Debug.Log($"[NetworkGameManager] Match started, round {round}.");

        if (UIManager.Instance != null) UIManager.Instance.Show(UIManager.Screen.InGame);
        RefreshHud();

        SpawnAiPlayers();
        roundStartTimer = roundStartDelay;

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

        if (GameFeel.Instance != null) GameFeel.Instance.ShakeForCatch();
        if (GameAudio.Instance != null) GameAudio.Instance.PlayCatch();

        Debug.Log($"[NetworkGameManager] {NameOf(kanamachiId)} caught somebody.");

        aiGuessAt = Time.time + aiGuessDelay;

        OpenGuessPanel();
        RefreshHud();
    }

    private void HandleCatchRejected(string reason)
    {
        string text;
        if (reason == "too_far") text = "Too far - get closer.";
        else if (reason == "round_starting") text = "Wait - the round is starting.";
        else text = "You are not the Kanamachi.";

        ShowMessage(text);
    }

    private void HandleGuessResult(bool correct, string newKanamachiId)
    {
        isGuessing = false;

        if (UIGameHud.Instance != null) UIGameHud.Instance.HideGuessPanel();

        string caughtName = NameOf(caughtUserId);
        caughtUserId = null;

        if (GameFeel.Instance != null) GameFeel.Instance.ShakeForGuess(correct);
        if (GameAudio.Instance != null) GameAudio.Instance.PlayGuessResult(correct);

        ShowMessage(correct
            ? $"Correct - it was {caughtName}."
            : $"Wrong guess. It was {caughtName}.");

        // The server may have handed the blindfold to somebody new.
        HandleKanamachiChanged(newKanamachiId);

        BeginNextRound();

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
        aiUserIds.Clear();
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

    // Everyone goes back to their starting place and nobody can be caught
    // for a moment. Each client moves its own player; the others follow
    // through the usual position updates, and the host moves the AI.
    private void BeginNextRound()
    {
        roundStartTimer = roundStartDelay;

        if (localPlayer != null)
        {
            localPlayer.transform.position = SpawnPointFor(SessionData.UserId);
        }

        if (IsHost)
        {
            foreach (string aiId in aiUserIds)
            {
                if (playersByUserId.TryGetValue(aiId, out Player ai) && ai != null)
                {
                    ai.transform.position = SpawnPointFor(aiId);
                }
            }
        }

        if (UIGameHud.Instance != null) UIGameHud.Instance.ShowToast("Get ready...", 1.8f);
    }

    private void AdvanceRound()
    {
        currentRound++;
        roundsPlayedInLevel++;

        if (roundsPlayedInLevel < roundsPerLevel) return;
        if (levelManager == null) return;

        roundsPlayedInLevel = 0;

        // Faded through, so the arena is never seen rebuilding itself.
        if (GameFeel.Instance != null)
        {
            GameFeel.Instance.FadeThrough(() =>
            {
                if (levelManager.AdvanceToNextLevel())
                {
                    ShowMessage($"Moving on to {levelManager.GetCurrentLevelName()}.");
                }
                else
                {
                    ShowMessage("That was the final level.");
                }
            });
            return;
        }

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

    // AI slots become real players on every machine. They are driven
    // locally rather than by the server, but because the personality and the
    // spawn point both come from the shared lobby state, each client ends up
    // with the same opponents in the same places.
    private void SpawnAiPlayers()
    {
        if (lobbyState == null || lobbyState.slots == null) return;

        foreach (NetworkClient.LobbySlot slot in lobbyState.slots)
        {
            if (slot.kind != "AI" || string.IsNullOrEmpty(slot.userId)) continue;
            if (playersByUserId.ContainsKey(slot.userId)) continue;

            SpawnAiPlayer(slot);
        }
    }

    private void SpawnAiPlayer(NetworkClient.LobbySlot slot)
    {
        GameObject prefab = aiPlayerPrefab != null ? aiPlayerPrefab : networkPlayerPrefab;
        if (prefab == null) return;

        Vector2 spawn = SpawnPointFor(slot.userId);
        GameObject obj = Instantiate(prefab, spawn, Quaternion.identity);
        obj.name = $"AI_{slot.personality}";

        // The personality classes are the same ones the local game uses -
        // the behaviour written in Phase 6 is reused rather than rewritten.
        Player player = AddPersonality(obj, slot.personality);
        if (player == null)
        {
            Destroy(obj);
            return;
        }

        usernamesByUserId[slot.userId] = slot.username;
        aiUserIds.Add(slot.userId);
        FinishSetup(player, slot.userId, slot.username);

        Debug.Log($"[NetworkGameManager] Spawned {slot.personality} AI as {player.DisplayName}.");
    }

    private Player AddPersonality(GameObject obj, string personality)
    {
        switch (personality)
        {
            case "AGGRESSIVE": return obj.AddComponent<AggressiveAI>();
            case "SNEAKY": return obj.AddComponent<SneakyAI>();
            case "CAREFUL": return obj.AddComponent<CarefulAI>();
            case "RANDOM": return obj.AddComponent<RandomAI>();
            default: return obj.AddComponent<RandomAI>();
        }
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
        // Breathing, squashing and kicking up dust - added here so it
        // applies to local and remote players alike.
        PlayerAnimator animator = player.gameObject.AddComponent<PlayerAnimator>();
        animator.dustSprite = dustSprite;

        Character character = ChosenCharacterFor(userId);
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

        // Same reasoning as the spawn points: the slot number is already
        // unique, so use it before falling back to a hash.
        int index = SlotIndexOf(userId);
        if (index >= 0) return characterPool[index % characterPool.Length];

        return characterPool[StableHash(userId) % characterPool.Length];
    }

    // Slot index first, hash only as a fallback.
    //
    // Hashing the id gave two players the same corner often enough to be a
    // problem: four ids into four slots collide regularly, and a round that
    // starts with two people standing on each other is decided instantly.
    // The lobby already numbers every player, so that number is used.
    private Vector2 SpawnPointFor(string userId)
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return Vector2.zero;

        int index = SlotIndexOf(userId);
        if (index >= 0) return spawnPoints[index % spawnPoints.Length];

        return spawnPoints[StableHash(userId) % spawnPoints.Length];
    }

    private int SlotIndexOf(string userId)
    {
        if (lobbyState == null || lobbyState.slots == null) return -1;

        foreach (NetworkClient.LobbySlot slot in lobbyState.slots)
        {
            if (slot.userId == userId) return slot.index;
        }

        return -1;
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

    // Turns a character's sound profile into something the Kanamachi can
    // match against what they just heard. This is what makes the guess a
    // deduction rather than a one-in-three chance.
    private string FootstepHint(Character character)
    {
        if (character == null) return "unfamiliar steps";

        string pitch;
        if (character.footstepPitchOffset >= 0.12f) pitch = "light, high steps";
        else if (character.footstepPitchOffset >= 0.03f) pitch = "light steps";
        else if (character.footstepPitchOffset <= -0.12f) pitch = "heavy, low steps";
        else if (character.footstepPitchOffset <= -0.03f) pitch = "heavy steps";
        else pitch = "even steps";

        string pace;
        if (character.moveSpeedMultiplier >= 1.04f) pace = "quick";
        else if (character.moveSpeedMultiplier <= 0.96f) pace = "slow";
        else pace = null;

        return pace == null ? pitch : $"{pitch}, {pace}";
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

        // A blindfolded AI decides for itself; nobody is asked to choose.
        if (aiUserIds.Contains(kanamachiUserId))
        {
            UIGameHud.Instance.ShowWaitingForGuess(NameOf(kanamachiUserId));
            return;
        }

        List<UIGameHud.GuessOption> options = new List<UIGameHud.GuessOption>();

        foreach (var pair in playersByUserId)
        {
            if (pair.Key == SessionData.UserId || pair.Value == null) continue;

            options.Add(new UIGameHud.GuessOption
            {
                userId = pair.Key,
                displayName = pair.Value.DisplayName,
                hint = FootstepHint(pair.Value.character)
            });
        }

        UIGameHud.Instance.ShowGuessPanel(options, guessedUserId =>
        {
            UIGameHud.Instance.HideGuessPanel();
            NetworkClient.Instance.SendGuess(guessedUserId);
        });
    }
}
