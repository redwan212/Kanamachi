package com.kanamachi.game_server.game;

import java.util.LinkedHashMap;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

// The server-authoritative state for one room's active match.
// The Unity clients never decide who is Kanamachi or whether a catch is
// valid - they only report actions, and this class (via the WebSocket
// handler) is what actually validates and updates the game state.
public class GameSession {

    public enum GameState { WAITING, PLAYING, GUESSING, FINISHED }

    private final String roomCode;
    private final Map<String, PlayerState> playerStates = new ConcurrentHashMap<>();

    // userId -> username, so saved records can carry readable names
    private final Map<String, String> playerNames = new ConcurrentHashMap<>();

    private volatile String kanamachiUserId;
    private volatile String pendingCaughtPlayerId;
    private volatile GameState gameState = GameState.WAITING;
    private volatile int currentRound = 0;

    public GameSession(String roomCode) {
        this.roomCode = roomCode;
    }

    public String getRoomCode() {
        return roomCode;
    }

    public Map<String, PlayerState> getPlayerStates() {
        return playerStates;
    }

    public void updatePosition(String userId, double x, double y) {
        PlayerState state = playerStates.computeIfAbsent(userId, PlayerState::new);
        state.setX(x);
        state.setY(y);
    }

    public double distanceBetween(String userIdA, String userIdB) {
        PlayerState a = playerStates.get(userIdA);
        PlayerState b = playerStates.get(userIdB);
        if (a == null || b == null) return Double.MAX_VALUE;

        double dx = a.getX() - b.getX();
        double dy = a.getY() - b.getY();
        return Math.sqrt(dx * dx + dy * dy);
    }

    public void setPlayerName(String userId, String username) {
        if (userId == null || username == null) return;
        playerNames.put(userId, username);
    }

    public Map<String, String> getPlayerNames() {
        return playerNames;
    }

    // ---- scoring ----
    // Points are calculated here, on the server, and never taken from the
    // client. A modified Unity build can claim it moved somewhere or that it
    // guessed - it cannot award itself a single point.

    public void addScore(String userId, int amount) {
        if (userId == null) return;
        playerStates.computeIfAbsent(userId, PlayerState::new).addScore(amount);
    }

    public int getScore(String userId) {
        PlayerState state = playerStates.get(userId);
        return state != null ? state.getScore() : 0;
    }

    public Map<String, Integer> getScoreboard() {
        Map<String, Integer> scores = new LinkedHashMap<>();
        for (Map.Entry<String, PlayerState> entry : playerStates.entrySet()) {
            scores.put(entry.getKey(), entry.getValue().getScore());
        }
        return scores;
    }

    // The player with the most points. Null while nobody has scored.
    public String getLeadingUserId() {
        String leader = null;
        int best = Integer.MIN_VALUE;

        for (PlayerState state : playerStates.values()) {
            if (state.getScore() > best) {
                best = state.getScore();
                leader = state.getUserId();
            }
        }
        return leader;
    }

    public int nextRound() {
        return ++currentRound;
    }

    public String getKanamachiUserId() {
        return kanamachiUserId;
    }

    public void setKanamachiUserId(String kanamachiUserId) {
        this.kanamachiUserId = kanamachiUserId;
    }

    public String getPendingCaughtPlayerId() {
        return pendingCaughtPlayerId;
    }

    public void setPendingCaughtPlayerId(String pendingCaughtPlayerId) {
        this.pendingCaughtPlayerId = pendingCaughtPlayerId;
    }

    public GameState getGameState() {
        return gameState;
    }

    public void setGameState(GameState gameState) {
        this.gameState = gameState;
    }

    public int getCurrentRound() {
        return currentRound;
    }

    public void setCurrentRound(int currentRound) {
        this.currentRound = currentRound;
    }
}
