package com.kanamachi.game_server.game;

import org.springframework.stereotype.Component;

import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

// Keeps one GameSession per active room. Thread-safe for concurrent rooms.
@Component
public class GameSessionManager {

    private final Map<String, GameSession> sessions = new ConcurrentHashMap<>();

    public GameSession getOrCreate(String roomCode) {
        return sessions.computeIfAbsent(roomCode, GameSession::new);
    }

    public GameSession get(String roomCode) {
        return sessions.get(roomCode);
    }

    public void remove(String roomCode) {
        sessions.remove(roomCode);
    }
}
