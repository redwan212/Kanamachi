package com.kanamachi.game_server.dto;

import jakarta.validation.constraints.NotBlank;

public class JoinRoomRequest {

    @NotBlank(message = "userId is required")
    private String userId;

    @NotBlank(message = "username is required")
    private String username;

    public String getUserId() {
        return userId;
    }

    public void setUserId(String userId) {
        this.userId = userId;
    }

    public String getUsername() {
        return username;
    }

    public void setUsername(String username) {
        this.username = username;
    }
}
