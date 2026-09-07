using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Players (assign in Inspector)")]
    public List<Player> players = new List<Player>();

    [Header("Spawn Points (must match players count)")]
    public Vector2[] spawnPoints;

    [Header("Catch Settings")]
    public float catchDistance = 1f;

    [Header("Scoring")]
    public int correctGuessPoints = 10;
    public int wrongGuessPenalty = 5;

    [Header("Round Start")]
    [Tooltip("Seconds of safe time after each round starts, so nobody is caught the instant they spawn.")]
    public float roundStartDelay = 2f;

    [Header("Level Progression")]
    [Tooltip("How many rounds are played before moving to the next level.")]
    public int roundsPerLevel = 3;

    private Player kanamachiPlayer;
    private Player pendingCaughtPlayer;
    private int roundsPlayedInLevel;
    private float roundStartTimer;

    // True while waiting for the Kanamachi to guess who they caught.
    public bool IsGuessingPhase { get; private set; }

    // True once the final level has been played out.
    public bool IsMatchOver { get; private set; }

    // True during the short safe period at the start of each round.
    public bool IsRoundStarting { get { return roundStartTimer > 0f; } }

    // True while a story chapter is on screen.
    public bool IsStoryShowing
    {
        get { return StoryManager.Instance != null && StoryManager.Instance.IsShowing; }
    }

    // Players cannot move while guessing, while the story is being read,
    // during the round's safe period, or after the match has ended.
    public bool IsInputFrozen
    {
        get { return IsGuessingPhase || IsMatchOver || IsRoundStarting || IsStoryShowing; }
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        StartNewRound();
    }

    void Update()
    {
        if (IsGuessingPhase || IsMatchOver) return;

        // Let the player read the chapter text before the round begins.
        if (IsStoryShowing) return;

        // Safe period: no catching allowed for the first moments of a round.
        if (roundStartTimer > 0f)
        {
            roundStartTimer -= Time.deltaTime;
            return;
        }
        if (kanamachiPlayer == null || players.Count < 2) return;

        foreach (var player in players)
        {
            if (player == kanamachiPlayer) continue;

            float dist = Vector2.Distance(kanamachiPlayer.transform.position, player.transform.position);
            if (dist <= catchDistance)
            {
                StartGuessPhase(player);
                break;
            }
        }
    }

    void StartNewRound()
    {
        int index = Random.Range(0, players.Count);
        SetKanamachi(players[index]);
        ResetPositions();
    }

    // Demonstrates encapsulation: blindfolded state is set through a method,
    // never directly from outside.
    void SetKanamachi(Player player)
    {
        if (kanamachiPlayer != null) kanamachiPlayer.SetBlindfolded(false);

        kanamachiPlayer = player;
        kanamachiPlayer.SetBlindfolded(true);

        Debug.Log($"[GameManager] {kanamachiPlayer.DisplayName} is now the Kanamachi (blind bee).");
    }

    void StartGuessPhase(Player caughtPlayer)
    {
        IsGuessingPhase = true;
        pendingCaughtPlayer = caughtPlayer;
        Debug.Log($"[GameManager] {kanamachiPlayer.DisplayName} caught someone! Time to guess who it is.");
    }

    public void SubmitGuess(Player guessedPlayer)
    {
        bool correct = guessedPlayer == pendingCaughtPlayer;

        if (correct)
        {
            Debug.Log($"[GameManager] Correct! It was {pendingCaughtPlayer.DisplayName}.");
            ScoreManager.Instance?.AddScore(kanamachiPlayer.gameObject, correctGuessPoints);
            SetKanamachi(pendingCaughtPlayer);
        }
        else
        {
            Debug.Log($"[GameManager] Wrong guess! It was actually {pendingCaughtPlayer.DisplayName}, not {guessedPlayer.DisplayName}.");
            ScoreManager.Instance?.AddScore(kanamachiPlayer.gameObject, -wrongGuessPenalty);
        }

        IsGuessingPhase = false;
        pendingCaughtPlayer = null;
        EndRound();
    }

    // A round finishes after every guess. Once enough rounds have been played
    // on the current level, the LevelManager advances to the next one; when
    // there is no next level, the match is over.
    void EndRound()
    {
        roundsPlayedInLevel++;

        if (roundsPlayedInLevel >= roundsPerLevel)
        {
            roundsPlayedInLevel = 0;

            if (LevelManager.Instance != null && LevelManager.Instance.AdvanceToNextLevel())
            {
                Debug.Log($"[GameManager] Moving on to {LevelManager.Instance.GetCurrentLevelName()}.");
            }
            else
            {
                EndMatch();
                return;
            }
        }

        ResetPositions();
    }

    void EndMatch()
    {
        IsMatchOver = true;
        Debug.Log("[GameManager] Match over.");

        Player winner = GetWinner();
        if (winner != null)
        {
            Debug.Log($"[GameManager] KANAMACHI MASTER: {winner.DisplayName}");
        }
    }

    Player GetWinner()
    {
        Player best = null;
        int bestScore = int.MinValue;

        foreach (var player in players)
        {
            if (player == null) continue;

            int score = ScoreManager.Instance != null
                ? ScoreManager.Instance.GetScore(player.gameObject)
                : 0;

            if (score > bestScore)
            {
                bestScore = score;
                best = player;
            }
        }

        return best;
    }

    void ResetPositions()
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (spawnPoints != null && i < spawnPoints.Length)
                players[i].transform.position = spawnPoints[i];
        }

        // Give everyone a moment to spread out before catching resumes.
        roundStartTimer = roundStartDelay;
    }

    public bool IsKanamachi(Player player)
    {
        return player == kanamachiPlayer;
    }

    public Player GetKanamachiPlayer()
    {
        return kanamachiPlayer;
    }

    void OnGUI()
    {
        if (IsMatchOver)
        {
            DrawMatchOverPanel();
            return;
        }

        // The story panel draws over the whole screen - don't stack the HUD on it.
        if (IsStoryShowing) return;

        if (kanamachiPlayer == null) return;

        GUIStyle labelStyle = new GUIStyle();
        labelStyle.fontSize = 24;
        labelStyle.normal.textColor = Color.yellow;
        GUI.Label(new Rect(10, 10, 500, 40), $"Kanamachi: {kanamachiPlayer.DisplayName}", labelStyle);

        if (LevelManager.Instance != null && LevelManager.Instance.CurrentLevel != null)
        {
            GUIStyle levelStyle = new GUIStyle();
            levelStyle.fontSize = 16;
            levelStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(10, 95, 600, 25),
                $"Level {LevelManager.Instance.CurrentLevelIndex + 1}: {LevelManager.Instance.GetCurrentLevelName()}  " +
                $"|  Round {roundsPlayedInLevel + 1}/{roundsPerLevel}", levelStyle);
        }

        if (IsRoundStarting)
        {
            GUIStyle readyStyle = new GUIStyle();
            readyStyle.fontSize = 26;
            readyStyle.alignment = TextAnchor.MiddleCenter;
            readyStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(Screen.width / 2f - 200, Screen.height / 2f - 20, 400, 40),
                $"Get ready... {Mathf.Ceil(roundStartTimer)}", readyStyle);
        }

        if (IsGuessingPhase)
        {
            DrawGuessButtons();
        }
    }

    // Laid out as a grid so the list still fits on screen with six players.
    void DrawGuessButtons()
    {
        GUIStyle promptStyle = new GUIStyle();
        promptStyle.fontSize = 22;
        promptStyle.alignment = TextAnchor.MiddleCenter;
        promptStyle.normal.textColor = Color.red;

        const float buttonWidth = 150f;
        const float buttonHeight = 40f;
        const float gap = 12f;
        const int columns = 2;

        int count = 0;
        foreach (var player in players)
        {
            if (player != kanamachiPlayer && player != null) count++;
        }

        int rows = Mathf.CeilToInt(count / (float)columns);
        float gridWidth = columns * buttonWidth + (columns - 1) * gap;
        float gridHeight = rows * buttonHeight + (rows - 1) * gap;

        float startX = Screen.width / 2f - gridWidth / 2f;
        float startY = Screen.height / 2f - gridHeight / 2f;

        GUI.Label(new Rect(Screen.width / 2f - 200, startY - 50, 400, 40),
            "Who did you catch?", promptStyle);

        int index = 0;
        foreach (var player in players)
        {
            if (player == kanamachiPlayer || player == null) continue;

            int row = index / columns;
            int column = index % columns;

            float x = startX + column * (buttonWidth + gap);
            float y = startY + row * (buttonHeight + gap);

            if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), player.DisplayName))
            {
                SubmitGuess(player);
            }

            index++;
        }
    }

    void DrawMatchOverPanel()
    {
        GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);

        GUIStyle titleStyle = new GUIStyle();
        titleStyle.fontSize = 30;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = Color.yellow;

        GUIStyle textStyle = new GUIStyle();
        textStyle.fontSize = 18;
        textStyle.alignment = TextAnchor.MiddleCenter;
        textStyle.normal.textColor = Color.white;

        float centerX = Screen.width / 2f - 250f;
        float y = Screen.height / 2f - 160f;

        GUI.Label(new Rect(centerX, y, 500, 40), "GAME OVER", titleStyle);
        y += 50f;

        Player winner = GetWinner();
        if (winner != null)
        {
            GUI.Label(new Rect(centerX, y, 500, 30), "KANAMACHI MASTER", textStyle);
            y += 32f;
            GUI.Label(new Rect(centerX, y, 500, 40), winner.DisplayName, titleStyle);
            y += 55f;
        }

        foreach (var player in players)
        {
            if (player == null) continue;

            int score = ScoreManager.Instance != null
                ? ScoreManager.Instance.GetScore(player.gameObject)
                : 0;

            GUI.Label(new Rect(centerX, y, 500, 25), $"{player.DisplayName}   {score} pts", textStyle);
            y += 26f;
        }
    }
}
