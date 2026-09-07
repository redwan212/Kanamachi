using System.Collections.Generic;
using UnityEngine;

// Lightweight score tracker. This is a starting point - the full Scoring
// system (catches, correct guesses, etc.) will expand on this later.
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    private Dictionary<GameObject, int> scores = new Dictionary<GameObject, int>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    public void AddScore(GameObject player, int amount)
    {
        if (!scores.ContainsKey(player)) scores[player] = 0;
        scores[player] += amount;
        Debug.Log($"[ScoreManager] {NameOf(player)} score: {scores[player]}");
    }

    public int GetScore(GameObject player)
    {
        return scores.ContainsKey(player) ? scores[player] : 0;
    }

    // Prefer the assigned Character's name over the raw GameObject name,
    // so the scoreboard reads "Rafi: 15 pts" instead of "Player1: 15 pts".
    private string NameOf(GameObject go)
    {
        Player p = go.GetComponent<Player>();
        return p != null ? p.DisplayName : go.name;
    }

    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 16;
        style.normal.textColor = Color.white;

        int y = 70;
        foreach (var kvp in scores)
        {
            GUI.Label(new Rect(10, y, 300, 25), $"{NameOf(kvp.Key)}: {kvp.Value} pts", style);
            y += 20;
        }
    }
}
