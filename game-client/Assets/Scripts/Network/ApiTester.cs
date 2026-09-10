using System.Collections.Generic;
using UnityEngine;

// A throwaway screen for exercising the server before any real UI exists.
// Attach it to the same object as ApiClient and NetworkClient.
//
// Everything is editable on screen rather than only in the Inspector, so a
// standalone build can register a second account and join a room without
// being rebuilt. Delete this once the real login and lobby screens exist.
public class ApiTester : MonoBehaviour
{
    [Header("Starting values (editable on screen too)")]
    public string username = "redwan";
    public string password = "test1234";
    public string roomCodeToJoin = "";

    private string status = "Not connected.";
    private readonly List<string> log = new List<string>();

    void Start()
    {
        if (NetworkClient.Instance == null) return;

        NetworkClient.Instance.OnConnected += () => AddLog("connected");
        NetworkClient.Instance.OnDisconnected += () => AddLog("disconnected");
        NetworkClient.Instance.OnPlayerJoined += (id, name) => AddLog($"joined: {name}");
        NetworkClient.Instance.OnPlayerLeft += id => AddLog("a player left");
        NetworkClient.Instance.OnGameStarted += round => AddLog($"GAME STARTED round {round}");
        NetworkClient.Instance.OnKanamachiChanged += id =>
            AddLog(id == SessionData.UserId ? "YOU are the Kanamachi" : "someone else is Kanamachi");
        NetworkClient.Instance.OnPlayerMoved += (id, x, y) => AddLog($"moved {x:F1},{y:F1}");
        NetworkClient.Instance.OnCatchSuccess += (k, c) => AddLog("CATCH_SUCCESS");
        NetworkClient.Instance.OnCatchRejected += reason => AddLog($"catch rejected: {reason}");
        NetworkClient.Instance.OnGuessResult += (ok, id) => AddLog($"guess {(ok ? "correct" : "wrong")}");
        NetworkClient.Instance.OnServerError += m => AddLog($"error: {m}");
    }

    private void AddLog(string line)
    {
        log.Add(line);
        if (log.Count > 12) log.RemoveAt(0);
    }

    void OnGUI()
    {
        // A dark panel so the text stays readable over the game view.
        GUI.Box(new Rect(4, 4, 250, 500), GUIContent.none);

        GUIStyle labelStyle = new GUIStyle();
        labelStyle.fontSize = 13;
        labelStyle.normal.textColor = Color.white;

        GUIStyle statusStyle = new GUIStyle();
        statusStyle.fontSize = 14;
        statusStyle.wordWrap = true;
        statusStyle.normal.textColor = Color.white;

        float y = 12f;

        GUI.Label(new Rect(12, y, 90, 20), "Username", labelStyle);
        username = GUI.TextField(new Rect(100, y - 3, 145, 22), username, 20);
        y += 28f;

        GUI.Label(new Rect(12, y, 90, 20), "Password", labelStyle);
        password = GUI.TextField(new Rect(100, y - 3, 145, 22), password, 30);
        y += 32f;

        if (GUI.Button(new Rect(12, y, 110, 28), "Register"))
        {
            status = "Registering...";
            ApiClient.Instance.Register(username, username + "@test.com", password,
                auth => status = $"Registered: {auth.username}",
                error => status = "Register failed:\n" + error);
        }

        if (GUI.Button(new Rect(133, y, 110, 28), "Login"))
        {
            status = "Logging in...";
            ApiClient.Instance.Login(username, password,
                auth => status = $"Logged in: {auth.username}",
                error => status = "Login failed:\n" + error);
        }
        y += 38f;

        if (GUI.Button(new Rect(12, y, 231, 28), "Create room (host)"))
        {
            status = "Creating room...";
            ApiClient.Instance.CreateRoom(4,
                room => status = $"ROOM CODE: {room.roomCode}\nplayers {room.players.Length}/{room.maxPlayers}\nhost: {SessionData.IsHost}",
                error => status = "Create failed:\n" + error);
        }
        y += 36f;

        GUI.Label(new Rect(12, y, 60, 20), "Code", labelStyle);
        roomCodeToJoin = GUI.TextField(new Rect(70, y - 3, 100, 22), roomCodeToJoin, 8);

        if (GUI.Button(new Rect(175, y - 4, 68, 26), "Join"))
        {
            status = "Joining...";
            ApiClient.Instance.JoinRoom(roomCodeToJoin,
                room => status = $"Joined {room.roomCode}\n{string.Join(", ", room.players)}",
                error => status = "Join failed:\n" + error);
        }
        y += 34f;

        if (GUI.Button(new Rect(12, y, 110, 28), "Refresh"))
        {
            if (!SessionData.IsInRoom)
            {
                status = "Not in a room yet.";
            }
            else
            {
                ApiClient.Instance.GetRoom(SessionData.RoomCode,
                    room => status = $"{room.roomCode}\n{string.Join(", ", room.players)}",
                    error => status = "Refresh failed:\n" + error);
            }
        }

        bool connected = NetworkClient.Instance != null && NetworkClient.Instance.IsConnected;

        if (GUI.Button(new Rect(133, y, 110, 28), connected ? "Disconnect" : "Connect"))
        {
            if (NetworkClient.Instance == null)
            {
                status = "No NetworkClient in the scene.";
            }
            else if (connected)
            {
                NetworkClient.Instance.Disconnect();
            }
            else
            {
                NetworkClient.Instance.Connect();
            }
        }
        y += 38f;

        GUI.Label(new Rect(12, y, 231, 80), status, statusStyle);
        y += 84f;

        GUIStyle logStyle = new GUIStyle();
        logStyle.fontSize = 12;
        logStyle.normal.textColor = connected ? Color.green : Color.gray;
        GUI.Label(new Rect(12, y, 231, 200), string.Join("\n", log), logStyle);
    }
}
