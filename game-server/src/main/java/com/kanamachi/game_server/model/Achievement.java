package com.kanamachi.game_server.model;

import org.springframework.data.annotation.Id;
import org.springframework.data.mongodb.core.mapping.Document;

import java.time.Instant;

// One achievement a player has unlocked. A row only exists once it has been
// earned, so the absence of a document is what "locked" means - there is no
// need to pre-create every achievement for every player.
@Document(collection = "achievements")
public class Achievement {

    public enum Type {
        FIRST_MATCH,
        FIRST_WIN,
        SHARP_EARS,      // three correct guesses in one match
        VETERAN,         // ten matches played
        CHAMPION         // five matches won
    }

    @Id
    private String id;

    private String userId;
    private String username;
    private Type type;
    private String title;
    private String description;
    private Instant unlockedAt = Instant.now();

    public Achievement() {
    }

    public Achievement(String userId, String username, Type type, String title, String description) {
        this.userId = userId;
        this.username = username;
        this.type = type;
        this.title = title;
        this.description = description;
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

    public Type getType() {
        return type;
    }

    public void setType(Type type) {
        this.type = type;
    }

    public String getTitle() {
        return title;
    }

    public void setTitle(String title) {
        this.title = title;
    }

    public String getDescription() {
        return description;
    }

    public void setDescription(String description) {
        this.description = description;
    }

    public Instant getUnlockedAt() {
        return unlockedAt;
    }

    public void setUnlockedAt(Instant unlockedAt) {
        this.unlockedAt = unlockedAt;
    }
}
