package com.kanamachi.game_server.service;

import com.kanamachi.game_server.model.Achievement;
import com.kanamachi.game_server.model.MatchHistory;
import com.kanamachi.game_server.model.PlayerProfile;
import com.kanamachi.game_server.model.PlayerStats;
import com.kanamachi.game_server.model.RoundRecord;
import com.kanamachi.game_server.repository.AchievementRepository;
import com.kanamachi.game_server.repository.MatchHistoryRepository;
import com.kanamachi.game_server.repository.PlayerProfileRepository;
import com.kanamachi.game_server.repository.PlayerStatsRepository;
import com.kanamachi.game_server.repository.RoundRecordRepository;
import org.springframework.stereotype.Service;

import java.util.Map;

// Turns a finished match into stored records.
//
// The WebSocket handler owns live state and knows nothing about MongoDB;
// this class owns persistence and knows nothing about sockets. Keeping the
// two apart means the game rules can be read without wading through
// database code, and the saving can be tested on its own.
@Service
public class MatchResultService {

    private final MatchHistoryRepository matchHistoryRepository;
    private final PlayerStatsRepository playerStatsRepository;
    private final RoundRecordRepository roundRecordRepository;
    private final AchievementRepository achievementRepository;
    private final PlayerProfileRepository playerProfileRepository;

    public MatchResultService(MatchHistoryRepository matchHistoryRepository,
                              PlayerStatsRepository playerStatsRepository,
                              RoundRecordRepository roundRecordRepository,
                              AchievementRepository achievementRepository,
                              PlayerProfileRepository playerProfileRepository) {
        this.matchHistoryRepository = matchHistoryRepository;
        this.playerStatsRepository = playerStatsRepository;
        this.roundRecordRepository = roundRecordRepository;
        this.achievementRepository = achievementRepository;
        this.playerProfileRepository = playerProfileRepository;
    }

    // Called once, when the final round of a match resolves.
    public MatchHistory saveFinishedMatch(String roomCode,
                                          int totalRounds,
                                          String winnerUserId,
                                          Map<String, Integer> scores,
                                          Map<String, String> usernames) {

        MatchHistory history = new MatchHistory(roomCode, totalRounds);
        history.setWinnerUserId(winnerUserId);
        history.setWinnerUsername(nameOf(usernames, winnerUserId));

        for (Map.Entry<String, Integer> entry : scores.entrySet()) {
            String userId = entry.getKey();
            int score = entry.getValue();
            boolean won = userId.equals(winnerUserId);

            history.addParticipant(userId, nameOf(usernames, userId), score, won);

            updateStats(userId, nameOf(usernames, userId), score, won);
            ensureProfile(userId, nameOf(usernames, userId));
        }

        return matchHistoryRepository.save(history);
    }

    // Saved as each round resolves, not at the end, so a match abandoned
    // halfway still leaves a record of what happened.
    public void saveRound(String roomCode,
                          int roundNumber,
                          String kanamachiUserId,
                          String caughtUserId,
                          String guessedUserId,
                          boolean correct,
                          int pointsAwarded,
                          Map<String, String> usernames) {

        RoundRecord record = new RoundRecord(roomCode, roundNumber);
        record.setKanamachiUserId(kanamachiUserId);
        record.setKanamachiUsername(nameOf(usernames, kanamachiUserId));
        record.setCaughtUserId(caughtUserId);
        record.setCaughtUsername(nameOf(usernames, caughtUserId));
        record.setGuessedUserId(guessedUserId);
        record.setGuessCorrect(correct);
        record.setPointsAwarded(pointsAwarded);

        roundRecordRepository.save(record);

        // Guess accuracy belongs to the guesser; being caught belongs to
        // whoever was grabbed.
        PlayerStats guesserStats = statsFor(kanamachiUserId, nameOf(usernames, kanamachiUserId));
        if (correct) {
            guesserStats.addCorrectGuess();
        } else {
            guesserStats.addWrongGuess();
        }
        playerStatsRepository.save(guesserStats);

        if (caughtUserId != null) {
            PlayerStats caughtStats = statsFor(caughtUserId, nameOf(usernames, caughtUserId));
            caughtStats.addTimeCaught();
            playerStatsRepository.save(caughtStats);
        }
    }

    private void updateStats(String userId, String username, int score, boolean won) {
        PlayerStats stats = statsFor(userId, username);
        stats.recordMatch(score, won);
        playerStatsRepository.save(stats);

        awardAchievements(userId, username, stats);
    }

    private PlayerStats statsFor(String userId, String username) {
        return playerStatsRepository.findByUserId(userId)
                .orElseGet(() -> new PlayerStats(userId, username));
    }

    private void ensureProfile(String userId, String username) {
        if (playerProfileRepository.findByUserId(userId).isEmpty()) {
            playerProfileRepository.save(new PlayerProfile(userId, username));
        }
    }

    // Each achievement is checked against the freshly updated stats and only
    // stored if it has not been earned before.
    private void awardAchievements(String userId, String username, PlayerStats stats) {
        if (stats.getMatchesPlayed() >= 1) {
            award(userId, username, Achievement.Type.FIRST_MATCH,
                    "First game", "Played your first match of Kanamachi.");
        }

        if (stats.getMatchesWon() >= 1) {
            award(userId, username, Achievement.Type.FIRST_WIN,
                    "Kanamachi Master", "Won your first match.");
        }

        if (stats.getCorrectGuesses() >= 3) {
            award(userId, username, Achievement.Type.SHARP_EARS,
                    "Sharp ears", "Identified three players correctly.");
        }

        if (stats.getMatchesPlayed() >= 10) {
            award(userId, username, Achievement.Type.VETERAN,
                    "Veteran", "Played ten matches.");
        }

        if (stats.getMatchesWon() >= 5) {
            award(userId, username, Achievement.Type.CHAMPION,
                    "Champion of the para", "Won five matches.");
        }
    }

    private void award(String userId, String username, Achievement.Type type,
                       String title, String description) {
        if (achievementRepository.existsByUserIdAndType(userId, type)) return;

        achievementRepository.save(new Achievement(userId, username, type, title, description));
    }

    private String nameOf(Map<String, String> usernames, String userId) {
        if (userId == null) return "unknown";
        String name = usernames.get(userId);
        return name != null ? name : userId;
    }
}
