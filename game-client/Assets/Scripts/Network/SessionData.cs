using UnityEngine;

// Holds who is logged in and which room they are in, for the lifetime of the
// application. Scenes come and go; this does not. Everything network-related
// reads the token and userId from here instead of passing them around.
//
// Nothing is written to disk - closing the game means logging in again.
// That is deliberate for now: the server's TokenStore is in-memory too, so a
// saved token would stop working the moment the server restarts.
public static class SessionData
{
    public static string Token { get; private set; }
    public static string UserId { get; private set; }
    public static string Username { get; private set; }

    public static string RoomCode { get; private set; }
    public static string HostUserId { get; private set; }

    public static bool IsLoggedIn
    {
        get { return !string.IsNullOrEmpty(Token) && !string.IsNullOrEmpty(UserId); }
    }

    public static bool IsInRoom
    {
        get { return !string.IsNullOrEmpty(RoomCode); }
    }

    // True when this player created the room, so only they can start the match
    // or choose AI personalities for the empty slots.
    public static bool IsHost
    {
        get { return IsInRoom && !string.IsNullOrEmpty(UserId) && UserId == HostUserId; }
    }

    public static void SetUser(string token, string userId, string username)
    {
        Token = token;
        UserId = userId;
        Username = username;

        Debug.Log($"[SessionData] Logged in as {username} (id {userId}).");
    }

    public static void SetRoom(string roomCode, string hostUserId)
    {
        RoomCode = roomCode;
        HostUserId = hostUserId;

        Debug.Log($"[SessionData] Joined room {roomCode} (host {hostUserId}).");
    }

    public static void ClearRoom()
    {
        RoomCode = null;
        HostUserId = null;
    }

    public static void ClearAll()
    {
        Token = null;
        UserId = null;
        Username = null;
        ClearRoom();

        Debug.Log("[SessionData] Session cleared.");
    }
}
