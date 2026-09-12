# Kanamachi

An online multiplayer game built around the Bangladeshi folk game *kanamachi*,
where one blindfolded player — the blind bee — tries to catch the others by
sound alone, and then name whoever they caught.

Two to four players share a courtyard. The blindfolded one sees almost
nothing: a small circle of lantern light, and footsteps that grow louder as
somebody comes closer. Everyone else can see the whole courtyard, and can
clap to taunt — at the cost of announcing exactly where they are standing.

---

## What it is built from

| | |
|---|---|
| Client | Unity 6, 2D URP |
| Server | Spring Boot, WebSocket + REST |
| Database | MongoDB |

---

## Running it

**Server**

```bash
cd game-server
./mvnw spring-boot:run
```

MongoDB must be running on `localhost:27017`. The server listens on port 8080.

**Client**

Open `game-client` in Unity 6 and press Play, or run a build. The server
address is set on the `NetworkManager` object, on the `ApiClient` component.

To play across two machines, set that address to the host machine's LAN IP
rather than `localhost`.

---

## Playing

1. Create an account, or sign in.
2. Create a room, or join one with its code.
3. Pick a character. Empty slots can be filled with AI opponents.
4. The host starts the match.

**Controls**

| | |
|---|---|
| Move | WASD or arrow keys |
| Clap (when you can see) | C |
| Grab (when blindfolded) | Space |

A match runs twelve rounds across four courtyards. Correct guesses score ten
points, wrong ones lose five, and staying unidentified earns three.

---

## How it is put together

The server owns the rules. Clients report what their player did and are told
what actually happened: whether a catch was in range, whether a guess was
right, who is blindfolded next, and what everybody scored. A modified client
can misreport its own position, but it cannot award itself a point.

AI opponents are simulated on the clients, because that is where the
behaviour already lived. The host's client speaks for them, and the server
accepts those messages only from the host and only for slots that really
hold an AI — so the rules stay in one place.

Levels, story chapters and the difficulty curve are driven by the client from
the round count, which every client receives identically. That keeps them in
step without adding messages the server would have to track.

### Structure

```
game-client/Assets/
  Art/            sprites, built to docs/ART_STYLE_GUIDE.md
  Characters/     character definitions - colour, speed, footstep pitch
  Prefabs/
  Scripts/
    Network/      REST and WebSocket clients, networked players
    UI/           screens, built in code from a single theme
    *.cs          gameplay: players, levels, sound cues, story, AI

game-server/src/main/java/com/kanamachi/game_server/
  controller/     auth, rooms, leaderboard
  game/           rooms, sessions, slots, player state
  model/          MongoDB documents
  repository/
  service/
  websocket/      the live match
```

### Object model

`Player` is abstract. `HumanPlayer` reads a keyboard, `AIPlayer` decides for
itself, and the four personalities below it — aggressive, sneaky, careful and
random — each override the same movement method and behave differently.

`Level` is abstract in the same way. Each of the four levels supplies its own
ambient noise, difficulty and sound-cue behaviour: the Storm Night returns a
cue system that distorts what the blindfolded player hears, and nothing else
in the game has to know that.

`ISoundCueSystem` is the seam between them, which is why a level can change
how hearing works without touching a player.

---

## Data

Six collections: `users`, `player_profiles`, `player_stats`, `match_history`,
`rounds` and `achievements`.

Rounds are written as they resolve, so a match abandoned halfway still leaves
a record. Match totals, statistics and any newly earned achievements are
written when the final round ends.

| Endpoint | |
|---|---|
| `GET /api/leaderboard?sortBy=score\|wins` | top players |
| `GET /api/stats/{userId}` | one player's record |
| `GET /api/matches` | recent matches |
| `GET /api/matches/{userId}` | one player's matches |
| `GET /api/achievements/{userId}` | what they have unlocked |

---

## Status

Phases 1 to 7 are complete. Deployment, automated tests and the written
report remain.

See `docs/` for the art style guide and phase notes.
