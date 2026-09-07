package com.kanamachi.game_server.game;

import com.kanamachi.game_server.exception.RoomNotFoundException;
import org.springframework.stereotype.Component;

import java.util.Map;
import java.util.Random;
import java.util.concurrent.ConcurrentHashMap;

// Owns the collection of active rooms. Thread-safe via ConcurrentHashMap
// since multiple clients can create/join/leave rooms simultaneously.
@Component
public class RoomManager {

    private static final String ROOM_CODE_CHARS = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private final Map<String, GameRoom> roomsByCode = new ConcurrentHashMap<>();
    private final Random random = new Random();

    public GameRoom createRoom(String hostUserId, String hostUsername, int maxPlayers) {
        String roomCode = generateUniqueRoomCode();
        GameRoom room = new GameRoom(roomCode, hostUserId, maxPlayers);
        room.addPlayer(hostUserId, hostUsername);
        roomsByCode.put(roomCode, room);
        return room;
    }

    public GameRoom joinRoom(String roomCode, String userId, String username) {
        GameRoom room = getRoomOrThrow(roomCode);
        room.addPlayer(userId, username);
        return room;
    }

    public void leaveRoom(String roomCode, String userId) {
        GameRoom room = roomsByCode.get(roomCode);
        if (room == null) return;

        room.removePlayer(userId);
        if (room.getPlayers().isEmpty()) {
            roomsByCode.remove(roomCode);
        }
    }

    public GameRoom getRoom(String roomCode) {
        return roomsByCode.get(roomCode);
    }

    public GameRoom getRoomOrThrow(String roomCode) {
        GameRoom room = roomsByCode.get(roomCode);
        if (room == null) {
            throw new RoomNotFoundException("Room not found: " + roomCode);
        }
        return room;
    }

    private String generateUniqueRoomCode() {
        String code;
        do {
            code = generateRoomCode();
        } while (roomsByCode.containsKey(code));
        return code;
    }

    private String generateRoomCode() {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < 6; i++) {
            sb.append(ROOM_CODE_CHARS.charAt(random.nextInt(ROOM_CODE_CHARS.length())));
        }
        return sb.toString();
    }
}
