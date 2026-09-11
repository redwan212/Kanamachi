package com.kanamachi.game_server.repository;

import com.kanamachi.game_server.model.PlayerStats;
import org.springframework.data.mongodb.repository.MongoRepository;

import java.util.List;
import java.util.Optional;

public interface PlayerStatsRepository extends MongoRepository<PlayerStats, String> {

    Optional<PlayerStats> findByUserId(String userId);

    // The leaderboard: highest total score first.
    List<PlayerStats> findTop20ByOrderByTotalScoreDesc();

    List<PlayerStats> findTop20ByOrderByMatchesWonDesc();
}
