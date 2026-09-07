package com.kanamachi.game_server.controller;

import com.kanamachi.game_server.dto.CreateRoomRequest;
import com.kanamachi.game_server.dto.JoinRoomRequest;
import com.kanamachi.game_server.dto.RoomResponse;
import com.kanamachi.game_server.game.GameRoom;
import com.kanamachi.game_server.game.RoomManager;
import jakarta.validation.Valid;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

import java.util.List;
import java.util.stream.Collectors;

@RestController
@RequestMapping("/api/rooms")
public class RoomController {

    private final RoomManager roomManager;

    public RoomController(RoomManager roomManager) {
        this.roomManager = roomManager;
    }

    @PostMapping
    public RoomResponse createRoom(@Valid @RequestBody CreateRoomRequest request) {
        int maxPlayers = request.getMaxPlayers() != null ? request.getMaxPlayers() : 4;
        GameRoom room = roomManager.createRoom(request.getHostUserId(), request.getHostUsername(), maxPlayers);
        return toResponse(room);
    }

    @PostMapping("/{roomCode}/join")
    public RoomResponse joinRoom(@PathVariable String roomCode, @Valid @RequestBody JoinRoomRequest request) {
        GameRoom room = roomManager.joinRoom(roomCode, request.getUserId(), request.getUsername());
        return toResponse(room);
    }

    @DeleteMapping("/{roomCode}/leave")
    public ResponseEntity<Void> leaveRoom(@PathVariable String roomCode, @RequestParam String userId) {
        roomManager.leaveRoom(roomCode, userId);
        return ResponseEntity.noContent().build();
    }

    @GetMapping("/{roomCode}")
    public RoomResponse getRoom(@PathVariable String roomCode) {
        GameRoom room = roomManager.getRoomOrThrow(roomCode);
        return toResponse(room);
    }

    private RoomResponse toResponse(GameRoom room) {
        List<String> usernames = room.getPlayers().values().stream()
                .map(p -> p.getUsername())
                .collect(Collectors.toList());

        return new RoomResponse(room.getRoomCode(), room.getHostUserId(), usernames,
                room.getState().name(), room.getMaxPlayers());
    }
}
