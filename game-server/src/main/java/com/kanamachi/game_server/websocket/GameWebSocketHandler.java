package com.kanamachi.game_server.websocket;

import tools.jackson.databind.ObjectMapper;
import com.kanamachi.game_server.game.GameSession;
import com.kanamachi.game_server.game.GameSessionManager;
import org.springframework.stereotype.Component;
import org.springframework.web.socket.CloseStatus;
import org.springframework.web.socket.TextMessage;
import org.springframework.web.socket.WebSocketSession;
import org.springframework.web.socket.handler.TextWebSocketHandler;

import java.io.IOException;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Random;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.CopyOnWriteArrayList;

// This is the real-time heart of the game: every connected Unity client
// talks to this handler over WebSocket. It is server-authoritative -
// clients report actions (moved, caught, guessed) and this class decides
// whether those actions are valid before broadcasting the result.
@Component
public class GameWebSocketHandler extends TextWebSocketHandler {

    private static final double CATCH_RADIUS = 2.0;

    private final GameSessionManager gameSessionManager;
    private final ObjectMapper objectMapper = new ObjectMapper();
    private final Random random = new Random();

    // roomCode -> currently connected sessions in that room
    private final Map<String, List<WebSocketSession>> roomSessions = new ConcurrentHashMap<>();

    public GameWebSocketHandler(GameSessionManager gameSessionManager) {
        this.gameSessionManager = gameSessionManager;
    }

    @Override
    public void afterConnectionEstablished(WebSocketSession session) throws IOException {
        String roomCode = roomCodeOf(session);
        String userId = userIdOf(session);
        String username = usernameOf(session);

        roomSessions.computeIfAbsent(roomCode, code -> new CopyOnWriteArrayList<>()).add(session);

        broadcast(roomCode, msg(
                "type", "PLAYER_JOINED",
                "userId", userId,
                "username", username
        ), null);

        maybeStartMatch(roomCode);
    }

    @Override
    protected void handleTextMessage(WebSocketSession session, TextMessage message) throws IOException {
        String roomCode = roomCodeOf(session);
        String userId = userIdOf(session);

        Map<String, Object> data;
        try {
            @SuppressWarnings("unchecked")
            Map<String, Object> parsed = objectMapper.readValue(message.getPayload(), Map.class);
            data = parsed;
        } catch (Exception e) {
            // Malformed or combined JSON shouldn't kill the connection -
            // just tell the sender their message was invalid.
            sendTo(session, msg("type", "ERROR", "message", "Invalid message format: " + e.getMessage()));
            return;
        }

        String type = (String) data.get("type");
        if (type == null) return;

        GameSession gameSession = gameSessionManager.getOrCreate(roomCode);

        switch (type) {
            case "PLAYER_MOVED" -> handlePlayerMoved(roomCode, userId, data, gameSession);
            case "CATCH_ATTEMPT" -> handleCatchAttempt(session, roomCode, userId, data, gameSession);
            case "GUESS" -> handleGuess(roomCode, userId, data, gameSession);
            case "PLAYER_CLAPPED" -> broadcast(roomCode, msg("type", "PLAYER_CLAPPED", "userId", userId), userId);
            default -> { /* unknown message type - ignore */ }
        }
    }

    private void handlePlayerMoved(String roomCode, String userId, Map<String, Object> data, GameSession gameSession) throws IOException {
        double x = ((Number) data.get("x")).doubleValue();
        double y = ((Number) data.get("y")).doubleValue();
        gameSession.updatePosition(userId, x, y);

        broadcast(roomCode, msg(
                "type", "PLAYER_MOVED",
                "playerId", userId,
                "x", x,
                "y", y
        ), userId); // no need to echo the move back to whoever sent it
    }

    private void handleCatchAttempt(WebSocketSession session, String roomCode, String userId,
                                     Map<String, Object> data, GameSession gameSession) throws IOException {
        String targetUserId = (String) data.get("targetPlayerId");

        boolean isKanamachi = userId.equals(gameSession.getKanamachiUserId());
        double distance = gameSession.distanceBetween(userId, targetUserId);
        boolean closeEnough = distance <= CATCH_RADIUS;

        if (isKanamachi && closeEnough) {
            gameSession.setGameState(GameSession.GameState.GUESSING);
            gameSession.setPendingCaughtPlayerId(targetUserId);

            broadcast(roomCode, msg(
                    "type", "CATCH_SUCCESS",
                    "kanamachiId", userId,
                    "caughtPlayerId", targetUserId
            ), null);
        } else {
            String reason = !isKanamachi ? "not_kanamachi" : "too_far";
            sendTo(session, msg(
                    "type", "CATCH_ATTEMPT",
                    "result", "rejected",
                    "reason", reason
            ));
        }
    }

    private void handleGuess(String roomCode, String userId, Map<String, Object> data, GameSession gameSession) throws IOException {
        String guessedPlayerId = (String) data.get("guessedPlayerId");
        String actualCaughtPlayerId = gameSession.getPendingCaughtPlayerId();

        boolean correct = actualCaughtPlayerId != null && actualCaughtPlayerId.equals(guessedPlayerId);

        if (correct) {
            gameSession.setKanamachiUserId(actualCaughtPlayerId);
        }

        broadcast(roomCode, msg(
                "type", "GUESS_RESULT",
                "correct", correct,
                "newKanamachiId", gameSession.getKanamachiUserId()
        ), null);

        if (correct) {
            broadcast(roomCode, msg(
                    "type", "KANAMACHI_CHANGED",
                    "kanamachiId", gameSession.getKanamachiUserId()
            ), null);
        }

        gameSession.setPendingCaughtPlayerId(null);
        gameSession.setGameState(GameSession.GameState.PLAYING);
    }

    // Once at least 2 players are connected and no Kanamachi has been chosen
    // yet, the server randomly assigns one and starts the match. Clients
    // never choose this themselves - it's a server-authoritative decision.
    private void maybeStartMatch(String roomCode) throws IOException {
        GameSession gameSession = gameSessionManager.getOrCreate(roomCode);
        List<WebSocketSession> sessions = roomSessions.get(roomCode);

        if (sessions == null || sessions.size() < 2) return;
        if (gameSession.getKanamachiUserId() != null) return;

        WebSocketSession chosen = sessions.get(random.nextInt(sessions.size()));
        String kanamachiId = userIdOf(chosen);
        gameSession.setKanamachiUserId(kanamachiId);
        gameSession.setGameState(GameSession.GameState.PLAYING);
        gameSession.setCurrentRound(1);

        broadcast(roomCode, msg("type", "GAME_STARTED", "round", 1), null);
        broadcast(roomCode, msg("type", "KANAMACHI_CHANGED", "kanamachiId", kanamachiId), null);
    }

    @Override
    public void afterConnectionClosed(WebSocketSession session, CloseStatus status) throws IOException {
        String roomCode = roomCodeOf(session);
        String userId = userIdOf(session);

        List<WebSocketSession> sessions = roomSessions.get(roomCode);
        if (sessions != null) {
            sessions.remove(session);
            if (sessions.isEmpty()) {
                roomSessions.remove(roomCode);
                gameSessionManager.remove(roomCode);
                return;
            }
        }

        GameSession gameSession = gameSessionManager.get(roomCode);
        if (gameSession != null && userId != null && userId.equals(gameSession.getKanamachiUserId())) {
            // The Kanamachi disconnected - clear the role so a new one
            // can be assigned instead of leaving the game stuck.
            gameSession.setKanamachiUserId(null);
        }

        broadcast(roomCode, msg("type", "PLAYER_LEFT", "userId", userId), null);

        if (gameSession != null) {
            maybeStartMatch(roomCode);
        }
    }

    // ---- helpers ----

    private void broadcast(String roomCode, Map<String, Object> message, String excludeUserId) throws IOException {
        List<WebSocketSession> sessions = roomSessions.get(roomCode);
        if (sessions == null) return;

        String json = objectMapper.writeValueAsString(message);
        for (WebSocketSession s : sessions) {
            String sessionUserId = userIdOf(s);
            if (excludeUserId != null && excludeUserId.equals(sessionUserId)) continue;
            if (s.isOpen()) {
                s.sendMessage(new TextMessage(json));
            }
        }
    }

    private void sendTo(WebSocketSession session, Map<String, Object> message) throws IOException {
        if (session != null && session.isOpen()) {
            session.sendMessage(new TextMessage(objectMapper.writeValueAsString(message)));
        }
    }

    private Map<String, Object> msg(Object... keyValuePairs) {
        Map<String, Object> map = new LinkedHashMap<>();
        for (int i = 0; i < keyValuePairs.length; i += 2) {
            map.put((String) keyValuePairs[i], keyValuePairs[i + 1]);
        }
        return map;
    }

    private String roomCodeOf(WebSocketSession session) {
        return (String) session.getAttributes().get("roomCode");
    }

    private String userIdOf(WebSocketSession session) {
        return (String) session.getAttributes().get("userId");
    }

    private String usernameOf(WebSocketSession session) {
        return (String) session.getAttributes().get("username");
    }
}
