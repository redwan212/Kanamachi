package com.kanamachi.game_server.service;

import com.kanamachi.game_server.dto.AuthResponse;
import com.kanamachi.game_server.dto.LoginRequest;
import com.kanamachi.game_server.dto.RegisterRequest;
import com.kanamachi.game_server.dto.UserProfileResponse;
import com.kanamachi.game_server.exception.DuplicateUserException;
import com.kanamachi.game_server.exception.InvalidCredentialsException;
import com.kanamachi.game_server.exception.UserNotFoundException;
import com.kanamachi.game_server.model.User;
import com.kanamachi.game_server.repository.UserRepository;
import com.kanamachi.game_server.security.TokenStore;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.stereotype.Service;

@Service
public class UserService {

    private final UserRepository userRepository;
    private final PasswordEncoder passwordEncoder;
    private final TokenStore tokenStore;

    public UserService(UserRepository userRepository, PasswordEncoder passwordEncoder, TokenStore tokenStore) {
        this.userRepository = userRepository;
        this.passwordEncoder = passwordEncoder;
        this.tokenStore = tokenStore;
    }

    public AuthResponse register(RegisterRequest request) {
        if (userRepository.existsByUsername(request.getUsername())) {
            throw new DuplicateUserException("Username is already taken.");
        }
        if (userRepository.existsByEmail(request.getEmail())) {
            throw new DuplicateUserException("Email is already registered.");
        }

        String hashedPassword = passwordEncoder.encode(request.getPassword());
        User user = new User(request.getUsername(), request.getEmail(), hashedPassword);
        User saved = userRepository.save(user);

        String token = tokenStore.issueToken(saved.getId());
        return new AuthResponse(token, saved.getUsername(), saved.getId());
    }

    public AuthResponse login(LoginRequest request) {
        User user = userRepository.findByUsername(request.getUsername())
                .orElseThrow(() -> new InvalidCredentialsException("Invalid username or password."));

        if (!passwordEncoder.matches(request.getPassword(), user.getPassword())) {
            throw new InvalidCredentialsException("Invalid username or password.");
        }

        String token = tokenStore.issueToken(user.getId());
        return new AuthResponse(token, user.getUsername(), user.getId());
    }

    public UserProfileResponse getProfile(String id) {
        User user = userRepository.findById(id)
                .orElseThrow(() -> new UserNotFoundException("User not found."));

        return new UserProfileResponse(user.getId(), user.getUsername(), user.getCreatedAt());
    }
}
