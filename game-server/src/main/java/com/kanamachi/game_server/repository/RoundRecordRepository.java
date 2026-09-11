package com.kanamachi.game_server.repository;

import com.kanamachi.game_server.model.RoundRecord;
import org.springframework.data.mongodb.repository.MongoRepository;

import java.util.List;

public interface RoundRecordRepository extends MongoRepository<RoundRecord, String> {

    List<RoundRecord> findByRoomCodeOrderByRoundNumberAsc(String roomCode);

    List<RoundRecord> findByKanamachiUserIdOrderByOccurredAtDesc(String userId);
}
