package com.kanamachi.game_server.repository;

import com.kanamachi.game_server.model.Achievement;
import org.springframework.data.mongodb.repository.MongoRepository;

import java.util.List;

public interface AchievementRepository extends MongoRepository<Achievement, String> {

    List<Achievement> findByUserIdOrderByUnlockedAtDesc(String userId);

    // Used before awarding, so the same achievement is never stored twice.
    boolean existsByUserIdAndType(String userId, Achievement.Type type);
}
