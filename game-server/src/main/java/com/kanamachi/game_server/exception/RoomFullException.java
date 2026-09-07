package com.kanamachi.game_server.exception;

public class RoomFullException extends RuntimeException {
    public RoomFullException(String message) {
        super(message);
    }
}
