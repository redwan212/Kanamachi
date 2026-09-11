package com.kanamachi.game_server.model;

import org.springframework.data.annotation.Id;
import org.springframework.data.mongodb.core.mapping.Document;

import java.time.Instant;

// One round within a match: who was blindfolded, who they caught, and
// whether the identity guess was right. Written as each round resolves, so
// a match can be reviewed afterwards rather than only summarised.
@Document(collection = "rounds")
public class RoundRecord {

    @Id
    private String id;

    private String roomCode;
    private int roundNumber;

    private String kanamachiUserId;
    private String kanamachiUsername;

    private String caughtUserId;
    private String caughtUsername;

    private String guessedUserId;
    private boolean guessCorrect;

    private int pointsAwarded;
    private Instant occurredAt = Instant.now();

    public RoundRecord() {
    }

    public RoundRecord(String roomCode, int roundNumber) {
        this.roomCode = roomCode;
        this.roundNumber = roundNumber;
    }

    public String getId() {
        return id;
    }

    public String getRoomCode() {
        return roomCode;
    }

    public void setRoomCode(String roomCode) {
        this.roomCode = roomCode;
    }

    public int getRoundNumber() {
        return roundNumber;
    }

    public void setRoundNumber(int roundNumber) {
        this.roundNumber = roundNumber;
    }

    public String getKanamachiUserId() {
        return kanamachiUserId;
    }

    public void setKanamachiUserId(String kanamachiUserId) {
        this.kanamachiUserId = kanamachiUserId;
    }

    public String getKanamachiUsername() {
        return kanamachiUsername;
    }

    public void setKanamachiUsername(String kanamachiUsername) {
        this.kanamachiUsername = kanamachiUsername;
    }

    public String getCaughtUserId() {
        return caughtUserId;
    }

    public void setCaughtUserId(String caughtUserId) {
        this.caughtUserId = caughtUserId;
    }

    public String getCaughtUsername() {
        return caughtUsername;
    }

    public void setCaughtUsername(String caughtUsername) {
        this.caughtUsername = caughtUsername;
    }

    public String getGuessedUserId() {
        return guessedUserId;
    }

    public void setGuessedUserId(String guessedUserId) {
        this.guessedUserId = guessedUserId;
    }

    public boolean isGuessCorrect() {
        return guessCorrect;
    }

    public void setGuessCorrect(boolean guessCorrect) {
        this.guessCorrect = guessCorrect;
    }

    public int getPointsAwarded() {
        return pointsAwarded;
    }

    public void setPointsAwarded(int pointsAwarded) {
        this.pointsAwarded = pointsAwarded;
    }

    public Instant getOccurredAt() {
        return occurredAt;
    }

    public void setOccurredAt(Instant occurredAt) {
        this.occurredAt = occurredAt;
    }
}
