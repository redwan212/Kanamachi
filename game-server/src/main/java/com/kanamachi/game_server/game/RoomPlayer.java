package com.kanamachi.game_server.game;

// A player's membership record within a room (lobby-level info,
// not their live in-match position - see PlayerState for that).
public class RoomPlayer {

    private final String userId;
    private final String username;
    private boolean ready;

    public RoomPlayer(String userId, String username) {
        this.userId = userId;
        this.username = username;
    }

    public String getUserId() {
        return userId;
    }

    public String getUsername() {
        return username;
    }

    public boolean isReady() {
        return ready;
    }

    public void setReady(boolean ready) {
        this.ready = ready;
    }
}
