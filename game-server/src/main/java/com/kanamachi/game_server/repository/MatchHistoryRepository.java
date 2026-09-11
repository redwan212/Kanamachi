package com.kanamachi.game_server.repository;

import com.kanamachi.game_server.model.MatchHistory;
import org.springframework.data.mongodb.repository.MongoRepository;

import java.util.List;

public interface MatchHistoryRepository extends MongoRepository<MatchHistory, String> {

    // Spring Data derives the query from the method name, including the sort
    // and the limit, so no query body is needed.
    List<MatchHistory> findTop20ByOrderByPlayedAtDesc();

    List<MatchHistory> findByParticipantsUserIdOrderByPlayedAtDesc(String userId);

    List<MatchHistory> findByRoomCodeOrderByPlayedAtDesc(String roomCode);
}
