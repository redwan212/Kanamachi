package com.kanamachi.game_server.model;

import org.springframework.data.annotation.Id;
import org.springframework.data.mongodb.core.mapping.Document;

import java.time.Instant;
import java.util.ArrayList;
import java.util.List;

// One completed match, written once when the final round ends.
//
// Each participant's username is stored next to their id. That duplicates
// data held in the users collection, but MongoDB has no joins - keeping the
// name here means match history and leaderboards can be read in a single
// query instead of looking up every player separately.
@Document(collection = "match_history")
public class MatchHistory {

    @Id
    private String id;

    private String roomCode;
    private String winnerUserId;
    private String winnerUsername;
    private int totalRounds;
    private Instant playedAt = Instant.now();

    private List<Participant> participants = new ArrayList<>();

    public MatchHistory() {
    }

    public MatchHistory(String roomCode, int totalRounds) {
        this.roomCode = roomCode;
        this.totalRounds = totalRounds;
    }

    public static class Participant {
        private String userId;
        private String username;
        private int score;
        private boolean winner;

        public Participant() {
        }

        public Participant(String userId, String username, int score, boolean winner) {
            this.userId = userId;
            this.username = username;
            this.score = score;
            this.winner = winner;
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

        public int getScore() {
            return score;
        }

        public void setScore(int score) {
            this.score = score;
        }

        public boolean isWinner() {
            return winner;
        }

        public void setWinner(boolean winner) {
            this.winner = winner;
        }
    }

    public void addParticipant(String userId, String username, int score, boolean winner) {
        participants.add(new Participant(userId, username, score, winner));
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

    public String getWinnerUserId() {
        return winnerUserId;
    }

    public void setWinnerUserId(String winnerUserId) {
        this.winnerUserId = winnerUserId;
    }

    public String getWinnerUsername() {
        return winnerUsername;
    }

    public void setWinnerUsername(String winnerUsername) {
        this.winnerUsername = winnerUsername;
    }

    public int getTotalRounds() {
        return totalRounds;
    }

    public void setTotalRounds(int totalRounds) {
        this.totalRounds = totalRounds;
    }

    public Instant getPlayedAt() {
        return playedAt;
    }

    public void setPlayedAt(Instant playedAt) {
        this.playedAt = playedAt;
    }

    public List<Participant> getParticipants() {
        return participants;
    }

    public void setParticipants(List<Participant> participants) {
        this.participants = participants;
    }
}
