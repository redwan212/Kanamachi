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

    private Player kanamachiPlayer;
    private Player pendingCaughtPlayer;

    // True while waiting for the Kanamachi to guess who they caught.
    public bool IsGuessingPhase { get; private set; }

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
        if (IsGuessingPhase) return;
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

        Debug.Log($"[GameManager] {kanamachiPlayer.name} is now the Kanamachi (blind bee).");
    }

    void StartGuessPhase(Player caughtPlayer)
    {
        IsGuessingPhase = true;
        pendingCaughtPlayer = caughtPlayer;
        Debug.Log($"[GameManager] {kanamachiPlayer.name} caught someone! Time to guess who it is.");
    }

    public void SubmitGuess(Player guessedPlayer)
    {
        bool correct = guessedPlayer == pendingCaughtPlayer;

        if (correct)
        {
            Debug.Log($"[GameManager] Correct! It was {pendingCaughtPlayer.name}.");
            ScoreManager.Instance?.AddScore(kanamachiPlayer.gameObject, correctGuessPoints);
            SetKanamachi(pendingCaughtPlayer);
        }
        else
        {
            Debug.Log($"[GameManager] Wrong guess! It was actually {pendingCaughtPlayer.name}, not {guessedPlayer.name}.");
            ScoreManager.Instance?.AddScore(kanamachiPlayer.gameObject, -wrongGuessPenalty);
        }

        IsGuessingPhase = false;
        pendingCaughtPlayer = null;
        ResetPositions();
    }

    void ResetPositions()
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (spawnPoints != null && i < spawnPoints.Length)
                players[i].transform.position = spawnPoints[i];
        }
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
        if (kanamachiPlayer == null) return;

        GUIStyle labelStyle = new GUIStyle();
        labelStyle.fontSize = 24;
        labelStyle.normal.textColor = Color.yellow;
        GUI.Label(new Rect(10, 10, 400, 40), $"Kanamachi: {kanamachiPlayer.name}", labelStyle);

        if (IsGuessingPhase)
        {
            GUIStyle promptStyle = new GUIStyle();
            promptStyle.fontSize = 22;
            promptStyle.normal.textColor = Color.red;
            GUI.Label(new Rect(Screen.width / 2f - 150, Screen.height / 2f - 90, 400, 40),
                "Who did you catch?", promptStyle);

            float buttonY = Screen.height / 2f - 30;
            foreach (var player in players)
            {
                if (player == kanamachiPlayer) continue;

                if (GUI.Button(new Rect(Screen.width / 2f - 75, buttonY, 150, 40), player.name))
                {
                    SubmitGuess(player);
                }
                buttonY += 50;
            }
        }
    }
}
