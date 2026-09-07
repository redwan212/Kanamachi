package com.kanamachi.game_server.game;

// A player's live, in-match state (position, score, activity) -
// this changes constantly during a round, so it stays in server memory
// rather than being written to MongoDB on every update.
public class PlayerState {

    private final String userId;
    private double x;
    private double y;
    private boolean active = true;
    private int score = 0;

    public PlayerState(String userId) {
        this.userId = userId;
    }

    public String getUserId() {
        return userId;
    }

    public double getX() {
        return x;
    }

    public void setX(double x) {
        this.x = x;
    }

    public double getY() {
        return y;
    }

    public void setY(double y) {
        this.y = y;
    }

    public boolean isActive() {
        return active;
    }

    public void setActive(boolean active) {
        this.active = active;
    }

    public int getScore() {
        return score;
    }

    public void addScore(int amount) {
        this.score += amount;
    }
}
