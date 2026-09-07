package com.kanamachi.game_server.dto;

import java.util.List;

public class RoomResponse {

    private final String roomCode;
    private final String hostUserId;
    private final List<String> players;
    private final String state;
    private final int maxPlayers;

    public RoomResponse(String roomCode, String hostUserId, List<String> players, String state, int maxPlayers) {
        this.roomCode = roomCode;
        this.hostUserId = hostUserId;
        this.players = players;
        this.state = state;
        this.maxPlayers = maxPlayers;
    }

    public String getRoomCode() {
        return roomCode;
    }

    public String getHostUserId() {
        return hostUserId;
    }

    public List<String> getPlayers() {
        return players;
    }

    public String getState() {
        return state;
    }

    public int getMaxPlayers() {
        return maxPlayers;
    }
}
