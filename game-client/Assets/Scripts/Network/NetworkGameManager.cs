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

    public bool LocalPlayerIsKanamachi
    {
        get { return !string.IsNullOrEmpty(kanamachiUserId) && kanamachiUserId == SessionData.UserId; }
    }

    // Nothing freezes movement yet - catching and guessing come next.
    public bool IsInputFrozen { get { return false; } }

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
        Debug.Log($"[NetworkGameManager] Match started, round {round}.");
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
        if (!showHud) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 18;
        style.normal.textColor = Color.yellow;

        float x = Screen.width - 330f;

        string you = localPlayer != null ? localPlayer.DisplayName : "(not spawned)";
        GUI.Label(new Rect(x, 10, 320, 26), $"You: {you}", style);

        GUIStyle small = new GUIStyle();
        small.fontSize = 15;
        small.normal.textColor = Color.white;

        GUI.Label(new Rect(x, 38, 320, 24),
            matchStarted ? $"Round {currentRound}" : "Waiting for another player...", small);

        GUI.Label(new Rect(x, 60, 320, 24),
            LocalPlayerIsKanamachi ? "You are the KANAMACHI - you cannot see."
                                   : $"Kanamachi: {NameOf(kanamachiUserId)}", small);

        GUI.Label(new Rect(x, 82, 320, 24), $"Players here: {playersByUserId.Count}", small);
    }
}
