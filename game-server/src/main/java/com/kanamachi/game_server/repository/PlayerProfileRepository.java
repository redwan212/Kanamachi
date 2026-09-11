package com.kanamachi.game_server.repository;

import com.kanamachi.game_server.model.PlayerProfile;
import org.springframework.data.mongodb.repository.MongoRepository;

import java.util.Optional;

public interface PlayerProfileRepository extends MongoRepository<PlayerProfile, String> {

    Optional<PlayerProfile> findByUserId(String userId);
}
