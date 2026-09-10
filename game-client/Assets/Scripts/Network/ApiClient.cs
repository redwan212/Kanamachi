using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Talks to the Spring Boot REST API. Everything before the match starts goes
// through here: registering, logging in, and creating or joining a room.
// Live gameplay uses the WebSocket instead (see NetworkClient).
//
// Every call takes an onSuccess and an onError callback rather than returning
// a value, because UnityWebRequest is asynchronous. Errors arrive as a plain
// message string that a UI screen can show the player directly.
public class ApiClient : MonoBehaviour
{
    public static ApiClient Instance;

    [Header("Server")]
    [Tooltip("No trailing slash. Use the machine's LAN IP when testing across two computers.")]
    public string baseUrl = "http://localhost:8080";

    [Tooltip("Seconds before a request is treated as failed.")]
    public int timeoutSeconds = 10;

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

    // ---------- Auth ----------

    public void Register(string username, string email, string password,
                         Action<AuthResponse> onSuccess, Action<string> onError)
    {
        string body = JsonUtility.ToJson(new RegisterRequest
        {
            username = username,
            email = email,
            password = password
        });

        StartCoroutine(Post("/api/auth/register", body, json =>
        {
            AuthResponse auth = JsonUtility.FromJson<AuthResponse>(json);
            SessionData.SetUser(auth.token, auth.userId, auth.username);
            onSuccess?.Invoke(auth);
        }, onError));
    }

    public void Login(string username, string password,
                      Action<AuthResponse> onSuccess, Action<string> onError)
    {
        string body = JsonUtility.ToJson(new LoginRequest
        {
            username = username,
            password = password
        });

        StartCoroutine(Post("/api/auth/login", body, json =>
        {
            AuthResponse auth = JsonUtility.FromJson<AuthResponse>(json);
            SessionData.SetUser(auth.token, auth.userId, auth.username);
            onSuccess?.Invoke(auth);
        }, onError));
    }

    // ---------- Rooms ----------

    public void CreateRoom(int maxPlayers, Action<RoomResponse> onSuccess, Action<string> onError)
    {
        if (!SessionData.IsLoggedIn)
        {
            onError?.Invoke("You must log in before creating a room.");
            return;
        }

        string body = JsonUtility.ToJson(new CreateRoomRequest
        {
            hostUserId = SessionData.UserId,
            hostUsername = SessionData.Username,
            maxPlayers = maxPlayers
        });

        StartCoroutine(Post("/api/rooms", body, json =>
        {
            RoomResponse room = JsonUtility.FromJson<RoomResponse>(json);
            SessionData.SetRoom(room.roomCode, room.hostUserId);
            onSuccess?.Invoke(room);
        }, onError));
    }

    public void JoinRoom(string roomCode, Action<RoomResponse> onSuccess, Action<string> onError)
    {
        if (!SessionData.IsLoggedIn)
        {
            onError?.Invoke("You must log in before joining a room.");
            return;
        }

        if (string.IsNullOrWhiteSpace(roomCode))
        {
            onError?.Invoke("Enter a room code.");
            return;
        }

        string body = JsonUtility.ToJson(new JoinRoomRequest
        {
            userId = SessionData.UserId,
            username = SessionData.Username
        });

        StartCoroutine(Post($"/api/rooms/{roomCode.Trim().ToUpper()}/join", body, json =>
        {
            RoomResponse room = JsonUtility.FromJson<RoomResponse>(json);
            SessionData.SetRoom(room.roomCode, room.hostUserId);
            onSuccess?.Invoke(room);
        }, onError));
    }

    // Used by the lobby to refresh who is currently in the room.
    public void GetRoom(string roomCode, Action<RoomResponse> onSuccess, Action<string> onError)
    {
        StartCoroutine(Get($"/api/rooms/{roomCode}", json =>
        {
            onSuccess?.Invoke(JsonUtility.FromJson<RoomResponse>(json));
        }, onError));
    }

    // ---------- Plumbing ----------

    private IEnumerator Post(string path, string jsonBody,
                             Action<string> onSuccess, Action<string> onError)
    {
        string url = baseUrl + path;

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] payload = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(payload);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = timeoutSeconds;

            if (SessionData.IsLoggedIn)
            {
                request.SetRequestHeader("Authorization", "Bearer " + SessionData.Token);
            }

            yield return request.SendWebRequest();

            HandleResponse(request, url, onSuccess, onError);
        }
    }

    private IEnumerator Get(string path, Action<string> onSuccess, Action<string> onError)
    {
        string url = baseUrl + path;

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = timeoutSeconds;

            if (SessionData.IsLoggedIn)
            {
                request.SetRequestHeader("Authorization", "Bearer " + SessionData.Token);
            }

            yield return request.SendWebRequest();

            HandleResponse(request, url, onSuccess, onError);
        }
    }

    private void HandleResponse(UnityWebRequest request, string url,
                                Action<string> onSuccess, Action<string> onError)
    {
        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log($"[ApiClient] {url} -> {request.responseCode}");
            onSuccess?.Invoke(request.downloadHandler.text);
            return;
        }

        string message = ExtractErrorMessage(request);
        Debug.LogWarning($"[ApiClient] {url} failed ({request.responseCode}): {message}");
        onError?.Invoke(message);
    }

    // The server's GlobalExceptionHandler returns a JSON body with a message
    // field. When the request never reached the server there is no body, so
    // fall back to something a player can act on.
    private string ExtractErrorMessage(UnityWebRequest request)
    {
        string raw = request.downloadHandler != null ? request.downloadHandler.text : null;

        if (!string.IsNullOrEmpty(raw))
        {
            try
            {
                ErrorResponse parsed = JsonUtility.FromJson<ErrorResponse>(raw);
                if (parsed != null && !string.IsNullOrEmpty(parsed.message))
                {
                    return parsed.message;
                }
            }
            catch (Exception)
            {
                // Body was not the shape we expected - fall through.
            }
        }

        if (request.result == UnityWebRequest.Result.ConnectionError)
        {
            return "Cannot reach the server. Is it running, and is the address correct?";
        }

        return $"Request failed ({request.responseCode}).";
    }

    // ---------- Wire formats ----------
    // JsonUtility needs plain serializable classes with public fields whose
    // names match the server's JSON exactly.

    [Serializable] private class RegisterRequest
    {
        public string username;
        public string email;
        public string password;
    }

    [Serializable] private class LoginRequest
    {
        public string username;
        public string password;
    }

    [Serializable] private class CreateRoomRequest
    {
        public string hostUserId;
        public string hostUsername;
        public int maxPlayers;
    }

    [Serializable] private class JoinRoomRequest
    {
        public string userId;
        public string username;
    }

    [Serializable] private class ErrorResponse
    {
        public string message;
    }

    [Serializable] public class AuthResponse
    {
        public string token;
        public string username;
        public string userId;
    }

    [Serializable] public class RoomResponse
    {
        public string roomCode;
        public string hostUserId;
        public string[] players;
        public string state;
        public int maxPlayers;
    }
}
