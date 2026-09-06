using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Players (assign in Inspector)")]
    public List<GameObject> players = new List<GameObject>();

    [Header("Spawn Points (must match players count)")]
    public Vector2[] spawnPoints;

    [Header("Catch Settings")]
    public float catchDistance = 1f;

    [Header("Scoring")]
    public int correctGuessPoints = 10;
    public int wrongGuessPenalty = 5;

    private GameObject kanamachiPlayer;
    private GameObject pendingCaughtPlayer;

    // True while waiting for the Kanamachi to guess who they caught.
    // Player movement is frozen during this phase (see PlayerController).
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
        if (IsGuessingPhase) return; // frozen until a guess is submitted
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
        kanamachiPlayer = players[index];

        Debug.Log($"[GameManager] {kanamachiPlayer.name} is now the Kanamachi (blind bee).");

        ResetPositions();
    }

    // Instead of immediately swapping roles, freeze the game and ask the
    // Kanamachi to identify who they caught (Phase 2: Identity Guessing).
    void StartGuessPhase(GameObject caughtPlayer)
    {
        IsGuessingPhase = true;
        pendingCaughtPlayer = caughtPlayer;
        Debug.Log($"[GameManager] {kanamachiPlayer.name} caught someone! Time to guess who it is.");
    }

    // Called by the OnGUI buttons below when the Kanamachi picks a name.
    public void SubmitGuess(GameObject guessedPlayer)
    {
        bool correct = guessedPlayer == pendingCaughtPlayer;

        if (correct)
        {
            Debug.Log($"[GameManager] Correct! It was {pendingCaughtPlayer.name}.");
            ScoreManager.Instance?.AddScore(kanamachiPlayer, correctGuessPoints);

            kanamachiPlayer = pendingCaughtPlayer;
            Debug.Log($"[GameManager] {kanamachiPlayer.name} is now the new Kanamachi.");
        }
        else
        {
            Debug.Log($"[GameManager] Wrong guess! It was actually {pendingCaughtPlayer.name}, not {guessedPlayer.name}.");
            ScoreManager.Instance?.AddScore(kanamachiPlayer, -wrongGuessPenalty);
            // Wrong guess: Kanamachi stays the same, caught player gets away.
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

    public bool IsKanamachi(GameObject player)
    {
        return player == kanamachiPlayer;
    }

    public GameObject GetKanamachiPlayer()
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
