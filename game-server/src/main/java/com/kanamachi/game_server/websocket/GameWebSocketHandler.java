package com.kanamachi.game_server.websocket;

import tools.jackson.databind.ObjectMapper;
import com.kanamachi.game_server.game.GameSession;
import com.kanamachi.game_server.game.GameSessionManager;
import com.kanamachi.game_server.game.RoomSlot;
import com.kanamachi.game_server.service.MatchResultService;
import org.springframework.stereotype.Component;
import org.springframework.web.socket.CloseStatus;
import org.springframework.web.socket.TextMessage;
import org.springframework.web.socket.WebSocketSession;
import org.springframework.web.socket.handler.TextWebSocketHandler;

import java.io.IOException;
import java.util.ArrayList;
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

    private static final int CORRECT_GUESS_POINTS = 10;
    private static final int WRONG_GUESS_PENALTY = 5;
    private static final int ESCAPE_POINTS = 3;

    // Four levels of three rounds each, matching the client's level pacing.
    private static final int ROUNDS_PER_MATCH = 12;

    // Grace period at the start of every round, in milliseconds.
    private static final long ROUND_GRACE_MS = 2500;

    private final GameSessionManager gameSessionManager;
    private final MatchResultService matchResultService;
    private final ObjectMapper objectMapper = new ObjectMapper();
    private final Random random = new Random();

    // roomCode -> currently connected sessions in that room
    private final Map<String, List<WebSocketSession>> roomSessions = new ConcurrentHashMap<>();

    public GameWebSocketHandler(GameSessionManager gameSessionManager,
                                MatchResultService matchResultService) {
        this.gameSessionManager = gameSessionManager;
        this.matchResultService = matchResultService;
    }

    @Override
    public void afterConnectionEstablished(WebSocketSession session) throws IOException {
        String roomCode = roomCodeOf(session);
        String userId = userIdOf(session);
        String username = usernameOf(session);

        roomSessions.computeIfAbsent(roomCode, code -> new CopyOnWriteArrayList<>()).add(session);

        GameSession gameSession = gameSessionManager.getOrCreate(roomCode);
        gameSession.setPlayerName(userId, username);

        // Every arrival takes a slot. A full room is refused rather than
        // silently letting a fifth person stand in the courtyard.
        RoomSlot slot = gameSession.seat(userId, username);
        if (slot == null) {
            session.sendMessage(new TextMessage(objectMapper.writeValueAsString(
                    msg("type", "ERROR", "message", "This room is full."))));
            session.close();
            return;
        }

        // The lobby replaces the old PLAYER_JOINED broadcast: it carries the
        // whole room in one message, so a client that arrives late sees the
        // same thing as one that was already here.
        broadcastLobby(roomCode, gameSession);
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
            case "PLAYER_MOVED" -> handlePlayerMoved(roomCode,
                    actingUserId(gameSession, userId, data), data, gameSession);
            case "CATCH_ATTEMPT" -> handleCatchAttempt(session, roomCode,
                    actingUserId(gameSession, userId, data), data, gameSession);
            case "GUESS" -> handleGuess(roomCode,
                    actingUserId(gameSession, userId, data), data, gameSession);
            case "PLAYER_CLAPPED" -> broadcast(roomCode, msg("type", "PLAYER_CLAPPED", "userId", userId), userId);
            case "SET_CHARACTER" -> handleSetCharacter(roomCode, userId, data, gameSession);
            case "SET_AI_SLOT" -> handleSetAiSlot(roomCode, userId, data, gameSession);
            case "START_MATCH" -> handleStartMatch(roomCode, userId, gameSession);
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

        // Rounds begin with everyone back at their starting places, and for
        // a moment nobody can be caught. Without this the Kanamachi simply
        // grabs whoever they were already touching when the last round
        // ended, and the match resolves itself.
        long sinceRoundStart = System.currentTimeMillis() - gameSession.getRoundStartedAt();
        boolean roundStarting = sinceRoundStart < ROUND_GRACE_MS;

        if (isKanamachi && closeEnough && !roundStarting) {
            gameSession.setGameState(GameSession.GameState.GUESSING);
            gameSession.setPendingCaughtPlayerId(targetUserId);

            broadcast(roomCode, msg(
                    "type", "CATCH_SUCCESS",
                    "kanamachiId", userId,
                    "caughtPlayerId", targetUserId
            ), null);
        } else {
            String reason;
            if (!isKanamachi) reason = "not_kanamachi";
            else if (roundStarting) reason = "round_starting";
            else reason = "too_far";

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

        // Scoring happens here and nowhere else. The client is told the
        // result, never asked for it.
        if (correct) {
            gameSession.addScore(userId, CORRECT_GUESS_POINTS);

            gameSession.setKanamachiUserId(actualCaughtPlayerId);
        } else {
            gameSession.addScore(userId, -WRONG_GUESS_PENALTY);

            // Staying hidden well enough to be misidentified is worth something.
            if (actualCaughtPlayerId != null) {
                gameSession.addScore(actualCaughtPlayerId, ESCAPE_POINTS);
            }
        }

        int round = gameSession.nextRound();
        gameSession.markRoundStart();
        System.out.println(">>> round now " + round + " of " + ROUNDS_PER_MATCH + " in room " + roomCode);

        broadcast(roomCode, msg(
                "type", "GUESS_RESULT",
                "correct", correct,
                "newKanamachiId", gameSession.getKanamachiUserId(),
                "round", round
        ), null);

        if (correct) {
            broadcast(roomCode, msg(
                    "type", "KANAMACHI_CHANGED",
                    "kanamachiId", gameSession.getKanamachiUserId()
            ), null);
        }

        broadcastScores(roomCode, gameSession);

        int pointsAwarded = correct ? CORRECT_GUESS_POINTS : -WRONG_GUESS_PENALTY;

        // Persistence is deliberately best-effort: a database problem should
        // not knock the players out of a match in progress.
        try {
            matchResultService.saveRound(
                    roomCode, round - 1, userId, actualCaughtPlayerId,
                    guessedPlayerId, correct, pointsAwarded,
                    gameSession.getPlayerNames());
        } catch (Exception e) {
            System.err.println("[GameWebSocketHandler] Could not save round: " + e.getMessage());
        }

        gameSession.setPendingCaughtPlayerId(null);

        if (round > ROUNDS_PER_MATCH) {
            finishMatch(roomCode, gameSession);
        } else {
            gameSession.setGameState(GameSession.GameState.PLAYING);
        }
    }

    // The match is over once every level has been played out. The winner is
    // decided here, from the server's own scores - a client cannot claim it.
    private void finishMatch(String roomCode, GameSession gameSession) throws IOException {
        System.out.println(">>> finishMatch called for room " + roomCode
                + ", rounds=" + gameSession.getCurrentRound()
                + ", players=" + gameSession.getScoreboard().size());

        gameSession.setGameState(GameSession.GameState.FINISHED);

        String winnerId = gameSession.getLeadingUserId();

        broadcast(roomCode, msg(
                "type", "MATCH_OVER",
                "winnerUserId", winnerId,
                "scores", gameSession.getScoreboard(),
                "rounds", gameSession.getCurrentRound() - 1
        ), null);

        try {
            matchResultService.saveFinishedMatch(
                    roomCode,
                    gameSession.getCurrentRound() - 1,
                    winnerId,
                    gameSession.getScoreboard(),
                    gameSession.getPlayerNames());

            System.out.println(">>> MATCH SAVED to MongoDB for room " + roomCode);
        } catch (Exception e) {
            System.out.println(">>> MATCH SAVE FAILED: " + e.getClass().getSimpleName()
                    + " - " + e.getMessage());
            e.printStackTrace();
        }
    }

    // Sent after every scoring event so all clients show the same numbers.
    private void broadcastScores(String roomCode, GameSession gameSession) throws IOException {
        broadcast(roomCode, msg(
                "type", "SCORE_UPDATE",
                "scores", gameSession.getScoreboard(),
                "leadingUserId", gameSession.getLeadingUserId()
        ), null);
    }

    // Once at least 2 players are connected and no Kanamachi has been chosen
    // yet, the server randomly assigns one and starts the match. Clients
    // never choose this themselves - it's a server-authoritative decision.
    // The complete state of the room: who is in which slot, what they chose,
    // and who may press start.
    private void broadcastLobby(String roomCode, GameSession gameSession) throws IOException {
        List<Map<String, Object>> slots = new ArrayList<>();

        for (RoomSlot slot : gameSession.getSlots()) {
            Map<String, Object> entry = new LinkedHashMap<>();
            entry.put("index", slot.getIndex());
            entry.put("kind", slot.getKind().name());
            entry.put("userId", slot.getUserId());
            entry.put("username", slot.getUsername());
            entry.put("character", slot.getCharacterName());
            entry.put("personality", slot.getPersonality() != null
                    ? slot.getPersonality().name() : null);
            slots.add(entry);
        }

        broadcast(roomCode, msg(
                "type", "LOBBY_STATE",
                "hostUserId", gameSession.getHostUserId(),
                "humans", gameSession.countHumans(),
                "occupied", gameSession.countOccupied(),
                "slots", slots
        ), null);
    }

    private void handleSetCharacter(String roomCode, String userId,
                                    Map<String, Object> data, GameSession gameSession)
            throws IOException {

        String character = (String) data.get("character");
        if (character == null) return;

        // One character per room, so two players are never indistinguishable
        // to whoever is blindfolded.
        for (RoomSlot other : gameSession.getSlots()) {
            if (character.equals(other.getCharacterName()) && !userId.equals(other.getUserId())) {
                return;
            }
        }

        RoomSlot slot = gameSession.slotOf(userId);
        if (slot == null) return;

        slot.setCharacterName(character);
        broadcastLobby(roomCode, gameSession);
    }

    // Only the host arranges the AI slots.
    private void handleSetAiSlot(String roomCode, String userId,
                                 Map<String, Object> data, GameSession gameSession)
            throws IOException {

        if (!userId.equals(gameSession.getHostUserId())) return;

        Integer index = asInt(data.get("index"));
        if (index == null) return;

        RoomSlot slot = gameSession.slotAt(index);
        if (slot == null || slot.getKind() == RoomSlot.Kind.HUMAN) return;

        String personality = (String) data.get("personality");

        if (personality == null || personality.isEmpty()) {
            slot.clear();
        } else if ("NEXT".equals(personality)) {
            // Filling an empty slot: pick one the room does not have yet, so
            // three AI opponents are three different opponents.
            slot.fillWithAi(unusedPersonality(gameSession));
        } else {
            try {
                slot.fillWithAi(RoomSlot.AiPersonality.valueOf(personality));
            } catch (IllegalArgumentException e) {
                return;
            }
        }

        broadcastLobby(roomCode, gameSession);
    }

    private void handleStartMatch(String roomCode, String userId, GameSession gameSession)
            throws IOException {

        if (!userId.equals(gameSession.getHostUserId())) return;
        if (gameSession.getKanamachiUserId() != null) return;

        // Two participants minimum - a blind bee with nobody to catch is not
        // a game. AI slots count, so one person plus an AI is enough.
        if (gameSession.countOccupied() < 2) {
            broadcast(roomCode, msg(
                    "type", "ERROR",
                    "message", "Add another player or an AI before starting."
            ), null);
            return;
        }

        startMatch(roomCode, gameSession);
    }

    private void startMatch(String roomCode, GameSession gameSession) throws IOException {
        gameSession.registerAiNames();

        // Anybody in a slot can be blindfolded, AI included. The host's
        // client plays the AI turns, so a blind AI still grabs and guesses.
        List<String> candidates = new ArrayList<>();
        for (RoomSlot slot : gameSession.getSlots()) {
            if (slot.isOccupied()) candidates.add(slot.getUserId());
        }

        if (candidates.isEmpty()) return;
        String kanamachiId = candidates.get(random.nextInt(candidates.size()));

        gameSession.setKanamachiUserId(kanamachiId);
        gameSession.setGameState(GameSession.GameState.PLAYING);
        gameSession.setCurrentRound(1);
        gameSession.markRoundStart();

        broadcast(roomCode, msg("type", "GAME_STARTED", "round", 1), null);
        broadcast(roomCode, msg("type", "KANAMACHI_CHANGED", "kanamachiId", kanamachiId), null);
        broadcastScores(roomCode, gameSession);
    }

    // AI opponents are simulated on the clients, so the server never hears
    // from them directly. The host speaks for them instead: a message may
    // carry "asPlayerId", and it is honoured only when the sender is the
    // host and that slot really is an AI. Everything after this point treats
    // the AI exactly like any other player, so none of the game rules need
    // to know the difference.
    private String actingUserId(GameSession gameSession, String senderId, Map<String, Object> data) {
        Object asPlayer = data.get("asPlayerId");
        if (asPlayer == null) return senderId;

        String target = asPlayer.toString();
        if (target.isEmpty()) return senderId;

        if (!senderId.equals(gameSession.getHostUserId())) return senderId;

        RoomSlot slot = gameSession.slotOf(target);
        if (slot == null || slot.getKind() != RoomSlot.Kind.AI) return senderId;

        return target;
    }

    private RoomSlot.AiPersonality unusedPersonality(GameSession gameSession) {
        for (RoomSlot.AiPersonality candidate : RoomSlot.AiPersonality.values()) {
            boolean taken = false;

            for (RoomSlot slot : gameSession.getSlots()) {
                if (slot.getPersonality() == candidate) {
                    taken = true;
                    break;
                }
            }

            if (!taken) return candidate;
        }

        return RoomSlot.AiPersonality.RANDOM;
    }

    // A person other than the one given, chosen at random.
    private String randomHuman(GameSession gameSession, String excludeUserId) {
        List<String> humans = new ArrayList<>();

        for (RoomSlot slot : gameSession.getSlots()) {
            if (slot.getKind() != RoomSlot.Kind.HUMAN) continue;
            if (slot.getUserId() == null) continue;
            if (slot.getUserId().equals(excludeUserId)) continue;

            humans.add(slot.getUserId());
        }

        if (humans.isEmpty()) return null;
        return humans.get(random.nextInt(humans.size()));
    }

    private Integer asInt(Object value) {
        if (value instanceof Number) return ((Number) value).intValue();
        if (value instanceof String) {
            try { return Integer.parseInt((String) value); } catch (NumberFormatException e) { return null; }
        }
        return null;
    }

    // Matches used to begin the moment a second person connected. The host
    // starts them now, which is what the room's WAITING state was for and
    // what makes choosing a character or an AI opponent possible at all.
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
            // Free the slot so somebody else can take it, and hand on the
            // host role if the person who left was holding it.
            gameSession.release(userId);
            broadcastLobby(roomCode, gameSession);
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
