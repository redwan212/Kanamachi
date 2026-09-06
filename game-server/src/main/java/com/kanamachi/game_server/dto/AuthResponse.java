package com.kanamachi.game_server.dto;

public class AuthResponse {

    private final String token;
    private final String username;
    private final String userId;

    public AuthResponse(String token, String username, String userId) {
        this.token = token;
        this.username = username;
        this.userId = userId;
    }

    public String getToken() {
        return token;
    }

    public String getUsername() {
        return username;
    }

    public String getUserId() {
        return userId;
    }
}
