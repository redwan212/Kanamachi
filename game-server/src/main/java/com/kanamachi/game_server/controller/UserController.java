package com.kanamachi.game_server.controller;

import com.kanamachi.game_server.dto.UserProfileResponse;
import com.kanamachi.game_server.service.UserService;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/users")
public class UserController {

    private final UserService userService;

    public UserController(UserService userService) {
        this.userService = userService;
    }

    @GetMapping("/{id}")
    public UserProfileResponse getUser(@PathVariable String id) {
        return userService.getProfile(id);
    }
}
