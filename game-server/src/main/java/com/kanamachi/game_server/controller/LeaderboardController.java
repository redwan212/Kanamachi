package com.kanamachi.game_server.controller;

import com.kanamachi.game_server.model.Achievement;
import com.kanamachi.game_server.model.MatchHistory;
import com.kanamachi.game_server.model.PlayerStats;
import com.kanamachi.game_server.repository.AchievementRepository;
import com.kanamachi.game_server.repository.MatchHistoryRepository;
import com.kanamachi.game_server.repository.PlayerStatsRepository;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import java.util.List;

// Read-only endpoints over the stored match data. Nothing here changes game
// state, so these are safe to call from a menu screen at any time.
@RestController
@RequestMapping("/api")
public class LeaderboardController {

    private final PlayerStatsRepository playerStatsRepository;
    private final MatchHistoryRepository matchHistoryRepository;
    private final AchievementRepository achievementRepository;

    public LeaderboardController(PlayerStatsRepository playerStatsRepository,
                                 MatchHistoryRepository matchHistoryRepository,
                                 AchievementRepository achievementRepository) {
        this.playerStatsRepository = playerStatsRepository;
        this.matchHistoryRepository = matchHistoryRepository;
        this.achievementRepository = achievementRepository;
    }

    // sortBy=score (default) ranks on total points; sortBy=wins ranks on
    // matches won, which rewards consistency rather than one big game.
    @GetMapping("/leaderboard")
    public ResponseEntity<List<PlayerStats>> leaderboard(
            @RequestParam(defaultValue = "score") String sortBy) {

        List<PlayerStats> results = "wins".equalsIgnoreCase(sortBy)
                ? playerStatsRepository.findTop20ByOrderByMatchesWonDesc()
                : playerStatsRepository.findTop20ByOrderByTotalScoreDesc();

        return ResponseEntity.ok(results);
    }

    @GetMapping("/stats/{userId}")
    public ResponseEntity<PlayerStats> stats(@PathVariable String userId) {
        return playerStatsRepository.findByUserId(userId)
                .map(ResponseEntity::ok)
                .orElseGet(() -> ResponseEntity.notFound().build());
    }

    @GetMapping("/matches")
    public ResponseEntity<List<MatchHistory>> recentMatches() {
        return ResponseEntity.ok(matchHistoryRepository.findTop20ByOrderByPlayedAtDesc());
    }

    @GetMapping("/matches/{userId}")
    public ResponseEntity<List<MatchHistory>> matchesFor(@PathVariable String userId) {
        return ResponseEntity.ok(
                matchHistoryRepository.findByParticipantsUserIdOrderByPlayedAtDesc(userId));
    }

    @GetMapping("/achievements/{userId}")
    public ResponseEntity<List<Achievement>> achievements(@PathVariable String userId) {
        return ResponseEntity.ok(achievementRepository.findByUserIdOrderByUnlockedAtDesc(userId));
    }
}
