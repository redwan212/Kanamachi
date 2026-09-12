package com.kanamachi.game_server.game;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
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

    // A room is four slots. Humans take them as they connect; whatever is
    // left can be filled with AI before the host starts the match.
    public static final int SLOT_COUNT = 4;

    private final List<RoomSlot> slots = new ArrayList<>();
    private volatile String hostUserId;

    private volatile String kanamachiUserId;
    private volatile String pendingCaughtPlayerId;
    private volatile GameState gameState = GameState.WAITING;
    private volatile int currentRound = 0;

    // When the current round began. Catching is refused for a moment after
    // this, so nobody is grabbed before they have had a chance to move away
    // from where they were standing when the last round ended.
    private volatile long roundStartedAt = System.currentTimeMillis();

    public GameSession(String roomCode) {
        this.roomCode = roomCode;
    }

    private void ensureSlots() {
        if (!slots.isEmpty()) return;

        synchronized (slots) {
            if (slots.isEmpty()) {
                for (int i = 0; i < SLOT_COUNT; i++) {
                    slots.add(new RoomSlot(i));
                }
            }
        }
    }

    public List<RoomSlot> getSlots() {
        ensureSlots();
        return slots;
    }

    // Called when somebody connects. Returns the slot they were given, or
    // null when the room is already full.
    public RoomSlot seat(String userId, String username) {
        ensureSlots();

        RoomSlot existing = slotOf(userId);
        if (existing != null) {
            existing.takeBy(userId, username);
            return existing;
        }

        for (RoomSlot slot : slots) {
            if (!slot.isOccupied()) {
                slot.takeBy(userId, username);
                if (hostUserId == null) hostUserId = userId;
                return slot;
            }
        }

        return null;
    }

    public void release(String userId) {
        ensureSlots();

        RoomSlot slot = slotOf(userId);
        if (slot != null) slot.clear();

        // The room needs a host, so it passes to whoever is still here.
        if (userId != null && userId.equals(hostUserId)) {
            hostUserId = null;
            for (RoomSlot candidate : slots) {
                if (candidate.getKind() == RoomSlot.Kind.HUMAN) {
                    hostUserId = candidate.getUserId();
                    break;
                }
            }
        }
    }

    public RoomSlot slotOf(String userId) {
        ensureSlots();
        if (userId == null) return null;

        for (RoomSlot slot : slots) {
            if (userId.equals(slot.getUserId())) return slot;
        }
        return null;
    }

    public RoomSlot slotAt(int index) {
        ensureSlots();
        if (index < 0 || index >= slots.size()) return null;
        return slots.get(index);
    }

    public int countHumans() {
        ensureSlots();

        int count = 0;
        for (RoomSlot slot : slots) {
            if (slot.getKind() == RoomSlot.Kind.HUMAN) count++;
        }
        return count;
    }

    public int countOccupied() {
        ensureSlots();

        int count = 0;
        for (RoomSlot slot : slots) {
            if (slot.isOccupied()) count++;
        }
        return count;
    }

    // Every AI slot becomes a player for the rest of the match, so their
    // names are registered the same way a person's would be.
    public void registerAiNames() {
        for (RoomSlot slot : getSlots()) {
            if (slot.getKind() == RoomSlot.Kind.AI) {
                setPlayerName(slot.getUserId(), slot.getUsername());
            }
        }
    }

    public String getHostUserId() {
        return hostUserId;
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

    public long getRoundStartedAt() {
        return roundStartedAt;
    }

    public void markRoundStart() {
        this.roundStartedAt = System.currentTimeMillis();
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
