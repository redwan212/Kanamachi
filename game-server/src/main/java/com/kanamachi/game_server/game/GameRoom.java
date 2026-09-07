package com.kanamachi.game_server.game;

import com.kanamachi.game_server.exception.RoomFullException;

import java.time.Instant;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

// Represents a lobby room: who is in it, how many can join, and its state.
// This is deliberately kept in server memory (not MongoDB) since room
// membership is temporary, real-time information.
public class GameRoom {

    public enum RoomState { WAITING, STARTING, IN_GAME, FINISHED, CLOSED }

    private final String roomCode;
    private final String hostUserId;
    private final int maxPlayers;
    private final Instant createdAt = Instant.now();

    private final Map<String, RoomPlayer> players = new ConcurrentHashMap<>();
    private volatile RoomState state = RoomState.WAITING;

    public GameRoom(String roomCode, String hostUserId, int maxPlayers) {
        this.roomCode = roomCode;
        this.hostUserId = hostUserId;
        this.maxPlayers = maxPlayers;
    }

    public synchronized void addPlayer(String userId, String username) {
        if (players.size() >= maxPlayers) {
            throw new RoomFullException("Room " + roomCode + " is full.");
        }
        players.put(userId, new RoomPlayer(userId, username));
    }

    public void removePlayer(String userId) {
        players.remove(userId);
    }

    public String getRoomCode() {
        return roomCode;
    }

    public String getHostUserId() {
        return hostUserId;
    }

    public int getMaxPlayers() {
        return maxPlayers;
    }

    public Instant getCreatedAt() {
        return createdAt;
    }

    public Map<String, RoomPlayer> getPlayers() {
        return players;
    }

    public RoomState getState() {
        return state;
    }

    public void setState(RoomState state) {
        this.state = state;
    }
}
