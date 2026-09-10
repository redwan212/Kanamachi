using System;
using NativeWebSocket;
using UnityEngine;
using UnityEngine.Networking;

// The live connection to the Spring Boot server during a match.
//
// Everything time-sensitive goes through here rather than the REST API:
// movement, catch attempts, guesses and claps. The server is authoritative -
// this class reports what the local player did and then waits to be told
// what actually happened. It never decides a catch or a guess itself.
//
// Other scripts subscribe to the events below instead of parsing JSON.
public class NetworkClient : MonoBehaviour
{
    public static NetworkClient Instance;

    [Header("Movement")]
    [Tooltip("How many position updates to send per second. Higher is smoother but noisier.")]
    public float sendsPerSecond = 10f;

    [Tooltip("Don't send an update unless the player moved at least this far.")]
    public float minMoveDistance = 0.05f;

    [Header("Debug")]
    public bool logMessages = true;

    // ---------- Events other scripts listen to ----------

    public event Action OnConnected;
    public event Action OnDisconnected;

    public event Action<string, string> OnPlayerJoined;     // userId, username
    public event Action<string> OnPlayerLeft;               // userId
    public event Action<int> OnGameStarted;                 // round
    public event Action<string> OnKanamachiChanged;         // kanamachiId
    public event Action<string, float, float> OnPlayerMoved; // playerId, x, y
    public event Action<string, string> OnCatchSuccess;     // kanamachiId, caughtPlayerId
    public event Action<string> OnCatchRejected;            // reason
    public event Action<bool, string> OnGuessResult;        // correct, newKanamachiId
    public event Action<string> OnPlayerClapped;            // userId
    public event Action<string> OnServerError;              // message

    public bool IsConnected
    {
        get { return socket != null && socket.State == WebSocketState.Open; }
    }

    private WebSocket socket;
    private float sendTimer;
    private Vector2 lastSentPosition;
    private bool hasSentPosition;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        // NativeWebSocket queues incoming messages and only raises them when
        // this is called, so callbacks land on Unity's main thread. Without
        // it, nothing is ever received.
#if !UNITY_WEBGL || UNITY_EDITOR
        if (socket != null)
        {
            socket.DispatchMessageQueue();
        }
#endif

        if (sendTimer > 0f) sendTimer -= Time.deltaTime;
    }

    // ---------- Connection ----------

    public async void Connect()
    {
        if (!SessionData.IsLoggedIn)
        {
            Debug.LogWarning("[NetworkClient] Log in before connecting.");
            return;
        }

        if (!SessionData.IsInRoom)
        {
            Debug.LogWarning("[NetworkClient] Join a room before connecting.");
            return;
        }

        if (IsConnected)
        {
            Debug.LogWarning("[NetworkClient] Already connected.");
            return;
        }

        string url = BuildUrl();
        Debug.Log($"[NetworkClient] Connecting to {url}");

        socket = new WebSocket(url);

        socket.OnOpen += () =>
        {
            Debug.Log("[NetworkClient] Connected.");
            hasSentPosition = false;
            OnConnected?.Invoke();
        };

        socket.OnError += error =>
        {
            Debug.LogWarning($"[NetworkClient] Socket error: {error}");
            OnServerError?.Invoke(error);
        };

        socket.OnClose += closeCode =>
        {
            Debug.Log($"[NetworkClient] Disconnected ({closeCode}).");
            OnDisconnected?.Invoke();
        };

        socket.OnMessage += bytes =>
        {
            string json = System.Text.Encoding.UTF8.GetString(bytes);
            HandleMessage(json);
        };

        // Connect() does not return until the socket closes, so it is
        // deliberately not awaited any further than this.
        await socket.Connect();
    }

    public async void Disconnect()
    {
        if (socket == null) return;

        await socket.Close();
        socket = null;
    }

    async void OnApplicationQuit()
    {
        if (socket != null)
        {
            await socket.Close();
        }
    }

    // http://host:8080  ->  ws://host:8080/ws/game/CODE?userId=...&username=...
    private string BuildUrl()
    {
        string http = ApiClient.Instance != null ? ApiClient.Instance.baseUrl : "http://localhost:8080";
        string ws = http.Replace("https://", "wss://").Replace("http://", "ws://").TrimEnd('/');

        string userId = UnityWebRequest.EscapeURL(SessionData.UserId);
        string username = UnityWebRequest.EscapeURL(SessionData.Username);

        return $"{ws}/ws/game/{SessionData.RoomCode}?userId={userId}&username={username}";
    }

    // ---------- Sending ----------

    // Called every frame by the local player; throttled internally so the
    // server isn't flooded with 60 messages a second per player.
    public void SendPosition(Vector2 position)
    {
        if (!IsConnected) return;
        if (sendTimer > 0f) return;

        if (hasSentPosition && Vector2.Distance(position, lastSentPosition) < minMoveDistance)
        {
            return;
        }

        Send($"{{\"type\":\"PLAYER_MOVED\",\"x\":{position.x.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"y\":{position.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}");

        lastSentPosition = position;
        hasSentPosition = true;
        sendTimer = 1f / Mathf.Max(1f, sendsPerSecond);
    }

    // The server checks whether this player really is the Kanamachi and
    // whether the target is within range, so a rejection is normal.
    public void SendCatchAttempt(string targetPlayerId)
    {
        if (!IsConnected) return;
        Send($"{{\"type\":\"CATCH_ATTEMPT\",\"targetPlayerId\":\"{targetPlayerId}\"}}");
    }

    public void SendGuess(string guessedPlayerId)
    {
        if (!IsConnected) return;
        Send($"{{\"type\":\"GUESS\",\"guessedPlayerId\":\"{guessedPlayerId}\"}}");
    }

    public void SendClap()
    {
        if (!IsConnected) return;
        Send("{\"type\":\"PLAYER_CLAPPED\"}");
    }

    private void Send(string json)
    {
        if (logMessages) Debug.Log($"[NetworkClient] -> {json}");
        socket.SendText(json);
    }

    // ---------- Receiving ----------

    private void HandleMessage(string json)
    {
        if (logMessages) Debug.Log($"[NetworkClient] <- {json}");

        ServerMessage message;
        try
        {
            message = JsonUtility.FromJson<ServerMessage>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NetworkClient] Could not parse message: {e.Message}");
            return;
        }

        if (message == null || string.IsNullOrEmpty(message.type)) return;

        switch (message.type)
        {
            case "PLAYER_JOINED":
                OnPlayerJoined?.Invoke(message.userId, message.username);
                break;

            case "PLAYER_LEFT":
                OnPlayerLeft?.Invoke(message.userId);
                break;

            case "GAME_STARTED":
                OnGameStarted?.Invoke(message.round);
                break;

            case "KANAMACHI_CHANGED":
                OnKanamachiChanged?.Invoke(message.kanamachiId);
                break;

            case "PLAYER_MOVED":
                OnPlayerMoved?.Invoke(message.playerId, message.x, message.y);
                break;

            case "CATCH_SUCCESS":
                OnCatchSuccess?.Invoke(message.kanamachiId, message.caughtPlayerId);
                break;

            case "CATCH_ATTEMPT":
                // Only ever sent back on rejection.
                OnCatchRejected?.Invoke(message.reason);
                break;

            case "GUESS_RESULT":
                OnGuessResult?.Invoke(message.correct, message.newKanamachiId);
                break;

            case "PLAYER_CLAPPED":
                OnPlayerClapped?.Invoke(message.userId);
                break;

            case "ERROR":
                Debug.LogWarning($"[NetworkClient] Server error: {message.message}");
                OnServerError?.Invoke(message.message);
                break;

            default:
                Debug.Log($"[NetworkClient] Unhandled message type: {message.type}");
                break;
        }
    }

    // One flat class covering every field the server can send. JsonUtility
    // leaves anything absent at its default value, so a single type is
    // simpler here than nine near-identical ones.
    [Serializable]
    private class ServerMessage
    {
        public string type;

        public string userId;
        public string username;
        public string playerId;
        public string kanamachiId;
        public string caughtPlayerId;
        public string newKanamachiId;
        public string reason;
        public string result;
        public string message;

        public float x;
        public float y;
        public int round;
        public bool correct;
    }
}
