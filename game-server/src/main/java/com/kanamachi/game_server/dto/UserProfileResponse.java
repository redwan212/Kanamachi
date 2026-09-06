package com.kanamachi.game_server.dto;

import java.time.Instant;

public class UserProfileResponse {

    private final String id;
    private final String username;
    private final Instant createdAt;

    public UserProfileResponse(String id, String username, Instant createdAt) {
        this.id = id;
        this.username = username;
        this.createdAt = createdAt;
    }

    public String getId() {
        return id;
    }

    public String getUsername() {
        return username;
    }

    public Instant getCreatedAt() {
        return createdAt;
    }
}
