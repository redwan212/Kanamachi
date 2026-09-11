package com.kanamachi.game_server.model;

import org.springframework.data.annotation.Id;
import org.springframework.data.mongodb.core.index.Indexed;
import org.springframework.data.mongodb.core.mapping.Document;

import java.time.Instant;

// A player's running totals across every match they have played.
// The leaderboard reads this directly, so it is kept current as matches
// finish rather than recalculated from match history on each request.
@Document(collection = "player_stats")
public class PlayerStats {

    @Id
    private String id;

    @Indexed(unique = true)
    private String userId;

    private String username;

    private int matchesPlayed;
    private int matchesWon;
    private int totalScore;
    private int highestScore;
    private int correctGuesses;
    private int wrongGuesses;
    private int timesCaught;

    private Instant lastPlayedAt;

    public PlayerStats() {
    }

    public PlayerStats(String userId, String username) {
        this.userId = userId;
        this.username = username;
    }

    // Called once per finished match.
    public void recordMatch(int score, boolean won) {
        matchesPlayed++;
        if (won) matchesWon++;

        totalScore += score;
        if (score > highestScore) highestScore = score;

        lastPlayedAt = Instant.now();
    }

    public double getWinRate() {
        if (matchesPlayed == 0) return 0d;
        return (double) matchesWon / matchesPlayed;
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

    public int getMatchesPlayed() {
        return matchesPlayed;
    }

    public void setMatchesPlayed(int matchesPlayed) {
        this.matchesPlayed = matchesPlayed;
    }

    public int getMatchesWon() {
        return matchesWon;
    }

    public void setMatchesWon(int matchesWon) {
        this.matchesWon = matchesWon;
    }

    public int getTotalScore() {
        return totalScore;
    }

    public void setTotalScore(int totalScore) {
        this.totalScore = totalScore;
    }

    public int getHighestScore() {
        return highestScore;
    }

    public void setHighestScore(int highestScore) {
        this.highestScore = highestScore;
    }

    public int getCorrectGuesses() {
        return correctGuesses;
    }

    public void addCorrectGuess() {
        this.correctGuesses++;
    }

    public void setCorrectGuesses(int correctGuesses) {
        this.correctGuesses = correctGuesses;
    }

    public int getWrongGuesses() {
        return wrongGuesses;
    }

    public void addWrongGuess() {
        this.wrongGuesses++;
    }

    public void setWrongGuesses(int wrongGuesses) {
        this.wrongGuesses = wrongGuesses;
    }

    public int getTimesCaught() {
        return timesCaught;
    }

    public void addTimeCaught() {
        this.timesCaught++;
    }

    public void setTimesCaught(int timesCaught) {
        this.timesCaught = timesCaught;
    }

    public Instant getLastPlayedAt() {
        return lastPlayedAt;
    }

    public void setLastPlayedAt(Instant lastPlayedAt) {
        this.lastPlayedAt = lastPlayedAt;
    }
}
