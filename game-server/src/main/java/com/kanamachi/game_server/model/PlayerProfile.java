package com.kanamachi.game_server.model;

import org.springframework.data.annotation.Id;
import org.springframework.data.mongodb.core.index.Indexed;
import org.springframework.data.mongodb.core.mapping.Document;

import java.time.Instant;

// Game-side information about a player, kept apart from User.
//
// User holds credentials and belongs to authentication; this holds what the
// player has chosen inside the game. Splitting them means a profile can be
// read and displayed without ever touching a document that contains a
// password hash.
@Document(collection = "player_profiles")
public class PlayerProfile {

    @Id
    private String id;

    @Indexed(unique = true)
    private String userId;

    private String username;
    private String favouriteCharacter;
    private String displayTitle;
    private Instant createdAt = Instant.now();

    public PlayerProfile() {
    }

    public PlayerProfile(String userId, String username) {
        this.userId = userId;
        this.username = username;
    }

    public String getId() {
        return id;
    }

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

    public String getFavouriteCharacter() {
        return favouriteCharacter;
    }

    public void setFavouriteCharacter(String favouriteCharacter) {
        this.favouriteCharacter = favouriteCharacter;
    }

    public String getDisplayTitle() {
        return displayTitle;
    }

    public void setDisplayTitle(String displayTitle) {
        this.displayTitle = displayTitle;
    }

    public Instant getCreatedAt() {
        return createdAt;
    }

    public void setCreatedAt(Instant createdAt) {
        this.createdAt = createdAt;
    }
}
