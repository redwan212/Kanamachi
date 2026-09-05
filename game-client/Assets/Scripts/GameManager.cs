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

    private GameObject kanamachiPlayer;

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
        if (kanamachiPlayer == null || players.Count < 2) return;

        foreach (var player in players)
        {
            if (player == kanamachiPlayer) continue;

            float dist = Vector2.Distance(kanamachiPlayer.transform.position, player.transform.position);
            if (dist <= catchDistance)
            {
                OnCatch(player);
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

    void OnCatch(GameObject caughtPlayer)
    {
        Debug.Log($"[GameManager] {kanamachiPlayer.name} caught {caughtPlayer.name}!");
        kanamachiPlayer = caughtPlayer;
        Debug.Log($"[GameManager] {kanamachiPlayer.name} is now the new Kanamachi.");

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

    // Temporary dev indicator so you can see who's Kanamachi during testing.
    // Remove/replace with real UI once the core loop works.
    void OnGUI()
    {
        if (kanamachiPlayer == null) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 24;
        style.normal.textColor = Color.yellow;
        GUI.Label(new Rect(10, 10, 400, 40), $"Kanamachi: {kanamachiPlayer.name}", style);
    }
}
