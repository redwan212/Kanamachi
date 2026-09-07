package com.kanamachi.game_server.websocket;

import org.springframework.http.server.ServerHttpRequest;
import org.springframework.http.server.ServerHttpResponse;
import org.springframework.http.server.ServletServerHttpRequest;
import org.springframework.web.socket.WebSocketHandler;
import org.springframework.web.socket.server.HandshakeInterceptor;
import org.springframework.web.util.UriTemplate;

import java.util.Map;

// Runs once, before a WebSocket connection is upgraded. Pulls the roomCode
// out of the URL path (/ws/game/{roomCode}) and userId/username out of the
// query string, storing them on the session so the handler can use them
// without re-parsing the URL on every message.
public class RoomHandshakeInterceptor implements HandshakeInterceptor {

    private static final UriTemplate ROOM_URI_TEMPLATE = new UriTemplate("/ws/game/{roomCode}");

    @Override
    public boolean beforeHandshake(ServerHttpRequest request, ServerHttpResponse response,
                                    WebSocketHandler wsHandler, Map<String, Object> attributes) {
        String path = request.getURI().getPath();

        if (ROOM_URI_TEMPLATE.matches(path)) {
            Map<String, String> variables = ROOM_URI_TEMPLATE.match(path);
            attributes.put("roomCode", variables.get("roomCode"));
        }

        if (request instanceof ServletServerHttpRequest servletRequest) {
            String userId = servletRequest.getServletRequest().getParameter("userId");
            String username = servletRequest.getServletRequest().getParameter("username");
            attributes.put("userId", userId);
            attributes.put("username", username);
        }

        return true;
    }

    @Override
    public void afterHandshake(ServerHttpRequest request, ServerHttpResponse response,
                                WebSocketHandler wsHandler, Exception exception) {
        // Nothing needed here.
    }
}
