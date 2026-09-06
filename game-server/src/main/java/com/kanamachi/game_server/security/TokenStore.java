package com.kanamachi.game_server.security;

import org.springframework.stereotype.Component;

import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;

// A simple in-memory session token store for Phase 4.
// If a persistent or standards-based approach (e.g. JWT) is needed later,
// this class can be swapped out without touching the rest of the auth flow.
@Component
public class TokenStore {

    private final Map<String, String> tokenToUserId = new ConcurrentHashMap<>();

    public String issueToken(String userId) {
        String token = UUID.randomUUID().toString();
        tokenToUserId.put(token, userId);
        return token;
    }

    public String getUserId(String token) {
        return tokenToUserId.get(token);
    }

    public void revoke(String token) {
        tokenToUserId.remove(token);
    }
}
