using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Dresses the arena for whichever level is being played: tiles the ground,
// places the village props, and sets the colour of the light.
//
// Two rules drive the design.
//
// First, the layout is generated from a seed derived from the level index,
// so every client builds an identical arena without the server having to
// send a single byte about it.
//
// Second, props are not decoration. Each one gets a collider sized to its
// base, so the blindfolded player bumps into the well and the charpai. This
// is what Level.GenerateObstacles() was always meant to produce.
public class EnvironmentBuilder : MonoBehaviour
{
    public static EnvironmentBuilder Instance;

    [Header("Ground")]
    public Sprite groundTile;
    [Tooltip("Half-width and half-height of the arena in world units.")]
    public Vector2 arenaHalfSize = new Vector2(7.5f, 5f);

    [Header("Props")]
    public Sprite houseSprite;
    public Sprite treeSprite;
    public Sprite wellSprite;
    public Sprite charpaiSprite;
    public Sprite potSprite;
    public Sprite fenceSprite;
    public Sprite lanternSprite;

    [Header("Spawn safety")]
    [Tooltip("No prop is placed within this distance of a spawn point, so nobody spawns inside a wall.")]
    public float spawnClearance = 1.6f;

    [Header("Lighting")]
    public Light2D globalLight;

    [Tooltip("Sprite-Lit-Default. Sprites created at runtime do not reliably get a lit material in a build, so it is assigned explicitly.")]
    public Material litSpriteMaterial;

    // Ground sits below everything. Props are sorted by height above this
    // baseline, so a prop high up the screen is still drawn over the floor.
    private const int GroundSortingOrder = -1000;
    private const int PropSortingBase = 0;

    private readonly List<GameObject> spawned = new List<GameObject>();
    private Transform root;

    void Awake()
    {
        Instance = this;

        root = new GameObject("Environment").transform;
        root.SetParent(transform, false);
    }

    // Called by LevelManager whenever a level loads.
    public void Build(int levelIndex, string levelName)
    {
        Clear();
        BuildGround();
        BuildArenaMask();

        // Same seed on every machine, so every player sees the same courtyard.
        System.Random random = new System.Random(1000 + levelIndex);

        ApplyLighting(levelIndex);
        PlaceProps(levelIndex, random);

        Debug.Log($"[EnvironmentBuilder] Dressed {levelName} with {spawned.Count} objects.");
    }

    public void Clear()
    {
        foreach (GameObject obj in spawned)
        {
            if (obj != null) Destroy(obj);
        }
        spawned.Clear();
    }

    // ---------- Ground ----------

    private void BuildGround()
    {
        if (groundTile == null) return;

        // The tile is 64px at 48 pixels-per-unit, so one tile covers 4/3 units.
        float tileSize = groundTile.rect.width / groundTile.pixelsPerUnit;

        int columns = Mathf.CeilToInt(arenaHalfSize.x * 2f / tileSize);
        int rows = Mathf.CeilToInt(arenaHalfSize.y * 2f / tileSize);

        // The grid is centred on the origin rather than started from the
        // corner. Starting from the corner leaves the last row and column
        // hanging past the walls, which makes the floor look misaligned.
        float startX = -(columns - 1) * tileSize * 0.5f;
        float startY = -(rows - 1) * tileSize * 0.5f;

        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                Vector2 position = new Vector2(
                        startX + x * tileSize,
                        startY + y * tileSize);

                GameObject tile = NewSprite("GroundTile", groundTile, position, GroundSortingOrder);

                // Alternating flips break up the repetition without needing
                // a second tile texture.
                SpriteRenderer renderer = tile.GetComponent<SpriteRenderer>();
                renderer.flipX = (x + y) % 2 == 0;
                renderer.flipY = (x * y) % 3 == 0;
            }
        }
    }

    // The alpona ring follows the blindfolded player, so near a wall it
    // would spill outside the courtyard. A mask the size of the arena keeps
    // anything drawn on the ground inside the ground.
    private void BuildArenaMask()
    {
        if (groundTile == null) return;

        GameObject maskObj = new GameObject("ArenaMask");
        maskObj.transform.SetParent(root, false);
        maskObj.transform.position = Vector3.zero;

        SpriteMask mask = maskObj.AddComponent<SpriteMask>();
        mask.sprite = groundTile;

        float tileWorldSize = groundTile.rect.width / groundTile.pixelsPerUnit;
        maskObj.transform.localScale = new Vector3(
                arenaHalfSize.x * 2f / tileWorldSize,
                arenaHalfSize.y * 2f / tileWorldSize,
                1f);

        spawned.Add(maskObj);
    }

    // ---------- Props ----------

    private void PlaceProps(int levelIndex, System.Random random)
    {
        switch (levelIndex)
        {
            case 0: BuildCourtyard(random); break;
            case 1: BuildMela(random); break;
            case 2: BuildStormNight(random); break;
            default: BuildFinalCatch(random); break;
        }
    }

    // A quiet family courtyard: one house, a well, a charpai to trip over.
    private void BuildCourtyard(System.Random random)
    {
        // Spawn points are in the four corners, so the village sits along
        // the middle bands instead - close enough to bump into, far enough
        // that nobody appears inside a wall.
        Prop(houseSprite, new Vector2(-1.6f, 3.6f), 0.95f, 0.55f);
        Prop(wellSprite, new Vector2(2.8f, 2.4f), 1.0f, 0.5f);
        Prop(charpaiSprite, new Vector2(-3.2f, -1.0f), 1.1f, 0.35f);
        Prop(treeSprite, new Vector2(3.4f, -1.6f), 0.7f, 0.45f);
        Prop(potSprite, new Vector2(0.4f, 1.2f), 0.5f, 0.3f);
        Prop(potSprite, new Vector2(1.1f, 0.9f), 0.45f, 0.3f);
        Prop(fenceSprite, new Vector2(-4.6f, 0.8f), 1.0f, 0.3f);
        Prop(fenceSprite, new Vector2(4.8f, 0.2f), 1.0f, 0.3f);
    }

    // The fair: busier, lantern-lit, more to bump into.
    private void BuildMela(System.Random random)
    {
        Prop(houseSprite, new Vector2(2.6f, 3.5f), 0.9f, 0.55f);
        Prop(charpaiSprite, new Vector2(-3.0f, 1.2f), 1.1f, 0.35f);
        Prop(charpaiSprite, new Vector2(-0.4f, -1.4f), 1.1f, 0.35f);
        Prop(wellSprite, new Vector2(-3.2f, -2.0f), 1.0f, 0.5f);
        Prop(treeSprite, new Vector2(3.6f, -1.4f), 0.7f, 0.45f);

        // Lanterns hang overhead, so they have no collider and can sit
        // anywhere, including over the spawn corners.
        for (int i = 0; i < 5; i++)
        {
            Prop(lanternSprite, new Vector2(-5.5f + i * 2.8f, 4.3f), 0.8f, 0f);
        }

        ScatterPots(random, 4);
        Prop(fenceSprite, new Vector2(0.8f, 2.2f), 1.0f, 0.3f);
        Prop(fenceSprite, new Vector2(-1.8f, -3.2f), 1.0f, 0.3f);
    }

    // Storm night: things have been blown around and there is more cover.
    private void BuildStormNight(System.Random random)
    {
        Prop(houseSprite, new Vector2(0f, 3.6f), 0.95f, 0.55f);
        Prop(treeSprite, new Vector2(-3.6f, 1.2f), 0.8f, 0.45f);
        Prop(treeSprite, new Vector2(3.6f, 0.6f), 0.75f, 0.45f);
        Prop(wellSprite, new Vector2(-1.4f, -2.4f), 1.0f, 0.5f);
        Prop(charpaiSprite, new Vector2(2.0f, -2.6f), 1.1f, 0.35f);

        ScatterPots(random, 5);
        ScatterFence(random, 3);
    }

    // The final round: open in the middle so the last catch has room.
    private void BuildFinalCatch(System.Random random)
    {
        Prop(houseSprite, new Vector2(-2.8f, 3.5f), 0.85f, 0.55f);
        Prop(houseSprite, new Vector2(2.8f, 3.5f), 0.85f, 0.55f);
        Prop(treeSprite, new Vector2(-3.8f, -1.4f), 0.8f, 0.45f);
        Prop(treeSprite, new Vector2(3.8f, -1.4f), 0.8f, 0.45f);
        Prop(wellSprite, new Vector2(0f, -2.6f), 1.0f, 0.5f);

        // A ring of lanterns for the last round - the whole village watching.
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.PI * 2f / 6f;
            Prop(lanternSprite, new Vector2(Mathf.Cos(angle) * 6.4f,
                    Mathf.Sin(angle) * 4.2f), 0.7f, 0f);
        }
    }

    private void ScatterPots(System.Random random, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Prop(potSprite, RandomSpot(random), 0.45f, 0.28f);
        }
    }

    private void ScatterFence(System.Random random, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Prop(fenceSprite, RandomSpot(random), 1.0f, 0.3f);
        }
    }

    // Kept away from the centre so the opening moments are not blocked.
    private Vector2 RandomSpot(System.Random random)
    {
        float x = (float)(random.NextDouble() * 2 - 1) * (arenaHalfSize.x - 1.2f);
        float y = (float)(random.NextDouble() * 2 - 1) * (arenaHalfSize.y - 1.2f);

        if (Mathf.Abs(x) < 1.5f && Mathf.Abs(y) < 1.2f) x += 2.5f;

        // Pull anything that landed in a spawn corner back toward the middle.
        if (Mathf.Abs(x) > 4.5f && Mathf.Abs(y) > 2f) x *= 0.6f;

        return new Vector2(x, y);
    }

    // colliderHeight 0 means decorative only - lanterns hang overhead.
    private GameObject Prop(Sprite sprite, Vector2 position, float scale, float colliderHeight)
    {
        if (sprite == null) return null;

        // A prop sitting on top of a spawn point traps whoever appears
        // there, so solid props near one are simply skipped.
        if (colliderHeight > 0f && IsNearSpawnPoint(position)) return null;

        GameObject obj = NewSprite(sprite.name, sprite, position, 0);
        obj.transform.localScale = Vector3.one * scale;

        // Sorting by height, so a player walking behind a house is hidden by
        // it and one walking in front is drawn over it.
        obj.GetComponent<SpriteRenderer>().sortingOrder =
                PropSortingBase + Mathf.RoundToInt(-position.y * 10f);

        if (colliderHeight > 0f)
        {
            float width = sprite.rect.width / sprite.pixelsPerUnit * scale;
            float height = sprite.rect.height / sprite.pixelsPerUnit * scale;

            BoxCollider2D collider = obj.AddComponent<BoxCollider2D>();

            // Only the base blocks movement: the player walks past the top of
            // a tree, not through its trunk.
            collider.size = new Vector2(width * 0.7f, height * colliderHeight * 0.5f);
            collider.offset = new Vector2(0f, -height * 0.5f + collider.size.y * 0.5f);
        }

        return obj;
    }

    private bool IsNearSpawnPoint(Vector2 position)
    {
        if (NetworkGameManager.Instance == null) return false;

        Vector2[] spawns = NetworkGameManager.Instance.spawnPoints;
        if (spawns == null) return false;

        foreach (Vector2 spawn in spawns)
        {
            if (Vector2.Distance(spawn, position) < spawnClearance) return true;
        }

        return false;
    }

    private GameObject NewSprite(string name, Sprite sprite, Vector2 position, int sortingOrder)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(root, false);
        obj.transform.position = position;

        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;

        // Without this the props render unlit in a standalone build and the
        // whole arena stays bright no matter what the lights are doing.
        if (litSpriteMaterial != null)
        {
            renderer.sharedMaterial = litSpriteMaterial;
        }

        spawned.Add(obj);
        return obj;
    }

    // ---------- Lighting ----------

    // Each level has its own time of day, which is the cheapest way to make
    // four arenas built from the same props feel like different places.
    private void ApplyLighting(int levelIndex)
    {
        if (globalLight == null) return;

        switch (levelIndex)
        {
            case 0:  // Courtyard, late afternoon
                globalLight.color = new Color(1f, 0.92f, 0.78f);
                break;
            case 1:  // Mela, lantern-lit evening
                globalLight.color = new Color(1f, 0.78f, 0.52f);
                break;
            case 2:  // Storm night, cold and blue
                globalLight.color = new Color(0.62f, 0.74f, 0.95f);
                break;
            default: // Final catch, torchlight
                globalLight.color = new Color(1f, 0.66f, 0.45f);
                break;
        }
    }
}
