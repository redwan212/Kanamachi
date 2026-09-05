using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

// This is an EDITOR-ONLY tool. It must live inside an "Editor" folder.
// After Unity compiles it, a new menu appears at the top: Kanamachi > Setup Players and GameManager
public class KanamachiSetupTool
{
    [MenuItem("Kanamachi/Setup Players and GameManager")]
    public static void SetupPlayersAndGameManager()
    {
        // Use Unity's built-in circle sprite (same one used by GameObject > 2D Object > Sprites > Circle)
        Sprite circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("Sprites/Circle.psd");

        GameObject player1 = CreatePlayer("Player1", new Vector3(-3, 0, 0), circleSprite, ControlScheme.WASD, Color.cyan);
        GameObject player2 = CreatePlayer("Player2", new Vector3(3, 0, 0), circleSprite, ControlScheme.Arrows, Color.magenta);

        GameObject gmObject = GameObject.Find("GameManager");
        if (gmObject == null)
        {
            gmObject = new GameObject("GameManager");
        }

        GameManager gm = gmObject.GetComponent<GameManager>();
        if (gm == null)
        {
            gm = gmObject.AddComponent<GameManager>();
        }

        gm.players = new List<GameObject> { player1, player2 };
        gm.spawnPoints = new Vector2[] { new Vector2(-3, 0), new Vector2(3, 0) };
        gm.catchDistance = 1f;

        EditorUtility.SetDirty(gm);
        EditorSceneManager_MarkDirty();

        Debug.Log("[KanamachiSetupTool] Player1, Player2, and GameManager created and wired up successfully.");
    }

    private static GameObject CreatePlayer(string name, Vector3 position, Sprite sprite, ControlScheme scheme, Color color)
    {
        // If it already exists (re-running the tool), remove and recreate cleanly.
        GameObject existing = GameObject.Find(name);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject player = new GameObject(name);
        player.transform.position = position;

        SpriteRenderer sr = player.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;

        Rigidbody2D rb = player.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;

        CircleCollider2D col = player.AddComponent<CircleCollider2D>();
        col.radius = 0.5f;

        PlayerController controller = player.AddComponent<PlayerController>();
        controller.controls = scheme;
        controller.moveSpeed = 4f;

        return player;
    }

    // Marks the current scene as needing a save, so Ctrl+S actually saves these new objects.
    private static void EditorSceneManager_MarkDirty()
    {
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }
}
