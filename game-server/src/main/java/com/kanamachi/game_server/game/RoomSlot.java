package com.kanamachi.game_server.game;

// One place in a room.
//
// A room always has the same number of slots. Each is empty, taken by a
// person, or filled by an AI - which is how spec Section 20's rule works in
// practice: whatever is left over when the humans have joined gets a
// computer opponent rather than leaving the courtyard half empty.
public class RoomSlot {

    public enum Kind {
        EMPTY,
        HUMAN,
        AI
    }

    public enum AiPersonality {
        AGGRESSIVE,
        SNEAKY,
        CAREFUL,
        RANDOM
    }

    private final int index;

    private Kind kind = Kind.EMPTY;
    private String userId;
    private String username;
    private String characterName;
    private AiPersonality personality;
    private boolean ready;

    public RoomSlot(int index) {
        this.index = index;
    }

    public void takeBy(String userId, String username) {
        this.kind = Kind.HUMAN;
        this.userId = userId;
        this.username = username;
        this.personality = null;
    }

    // AI slots still get an id, because the rest of the game addresses every
    // player by one - scores, catches and guesses all key on it.
    public void fillWithAi(AiPersonality personality) {
        this.kind = Kind.AI;
        this.personality = personality;
        this.userId = "ai-" + index;
        this.username = readableName(personality);
        this.ready = true;
    }

    public void clear() {
        this.kind = Kind.EMPTY;
        this.userId = null;
        this.username = null;
        this.characterName = null;
        this.personality = null;
        this.ready = false;
    }

    private String readableName(AiPersonality personality) {
        switch (personality) {
            case AGGRESSIVE: return "Bold one";
            case SNEAKY: return "Quiet one";
            case CAREFUL: return "Careful one";
            default: return "Restless one";
        }
    }

    public boolean isOccupied() {
        return kind != Kind.EMPTY;
    }

    public int getIndex() {
        return index;
    }

    public Kind getKind() {
        return kind;
    }

    public String getUserId() {
        return userId;
    }

    public String getUsername() {
        return username;
    }

    public String getCharacterName() {
        return characterName;
    }

    public void setCharacterName(String characterName) {
        this.characterName = characterName;
    }

    public AiPersonality getPersonality() {
        return personality;
    }

    public boolean isReady() {
        return ready;
    }

    public void setReady(boolean ready) {
        this.ready = ready;
    }
}
