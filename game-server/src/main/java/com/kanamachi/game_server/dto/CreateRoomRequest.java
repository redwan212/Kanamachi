package com.kanamachi.game_server.dto;

import jakarta.validation.constraints.NotBlank;

public class CreateRoomRequest {

    @NotBlank(message = "hostUserId is required")
    private String hostUserId;

    @NotBlank(message = "hostUsername is required")
    private String hostUsername;

    private Integer maxPlayers; // optional, defaults to 4 if not provided

    public String getHostUserId() {
        return hostUserId;
    }

    public void setHostUserId(String hostUserId) {
        this.hostUserId = hostUserId;
    }

    public String getHostUsername() {
        return hostUsername;
    }

    public void setHostUsername(String hostUsername) {
        this.hostUsername = hostUsername;
    }

    public Integer getMaxPlayers() {
        return maxPlayers;
    }

    public void setMaxPlayers(Integer maxPlayers) {
        this.maxPlayers = maxPlayers;
    }
}
