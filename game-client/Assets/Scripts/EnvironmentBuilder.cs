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

    [Header("Level 2 - Mela Props")]
    public Sprite melaStallRedSprite;
    public Sprite melaStallBlueSprite;
    public Sprite melaStageSprite;
    public Sprite melaCartSprite;
    public Sprite melaTableSprite;

    [Header("Level 3 - Storm Props")]
    public Sprite stormHutSprite;
    public Sprite stormCartSprite;
    public Sprite rainPuddleSprite;

    [Header("Level 4 - Final Catch Props")]
    public Sprite villagePondSprite;
    public Sprite treeClusterSprite;

    [Header("Level Ground Tiles")]
    public Sprite groundTileMela;
    public Sprite groundTileStorm;
    public Sprite groundTileVillage;

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

        ConfigureWalls();
    }

    // Called by LevelManager whenever a level loads.
    public void Build(int levelIndex, string levelName)
    {
        Clear();
        ConfigureWalls();

        // Swap ground tile so each level has its own look.
        switch (levelIndex)
        {
            case 1: if (groundTileMela   != null) groundTile = groundTileMela;   break;
            case 2: if (groundTileStorm  != null) groundTile = groundTileStorm;  break;
            case 3: if (groundTileVillage != null) groundTile = groundTileVillage; break;
        }

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

    // Ensures all four perimeter walls use the same matching brick wall sprite,
    // oriented inward with correct tiling and colliders.
    private void ConfigureWalls()
    {
        GameObject top = GameObject.Find("Wall_Top");
        GameObject bottom = GameObject.Find("Wall_Bottom");
        GameObject left = GameObject.Find("Wall_Left");
        GameObject right = GameObject.Find("Wall_Right");

        Sprite brickSprite = null;
        if (bottom != null)
        {
            SpriteRenderer bSr = bottom.GetComponent<SpriteRenderer>();
            if (bSr != null && bSr.sprite != null) brickSprite = bSr.sprite;
        }

        if (brickSprite == null) return;

        // 1. Bottom Wall (clean brick faces UP into arena, leaves face DOWN into void)
        if (bottom != null)
        {
            bottom.transform.localPosition = new Vector3(0f, -5.6f, 0f);
            bottom.transform.localRotation = Quaternion.identity;
            bottom.transform.localScale = Vector3.one;
            SpriteRenderer sr = bottom.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = brickSprite;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = new Vector2(17f, 1.25f);
                sr.sortingOrder = 65;
                sr.flipY = false;
                sr.flipX = false;
            }
            BoxCollider2D col = bottom.GetComponent<BoxCollider2D>();
            if (col != null) col.size = new Vector2(17f, 1.25f);
        }

        // 2. Top Wall (clean brick faces DOWN into arena, leaves face UP into void)
        if (top != null)
        {
            top.transform.localPosition = new Vector3(0f, 5.6f, 0f);
            top.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            top.transform.localScale = Vector3.one;
            SpriteRenderer sr = top.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = brickSprite;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = new Vector2(17f, 1.25f);
                sr.sortingOrder = -55;
                sr.flipY = false;
                sr.flipX = false;
            }
            BoxCollider2D col = top.GetComponent<BoxCollider2D>();
            if (col != null) col.size = new Vector2(17f, 1.25f);
        }

        // 3. Left Wall (clean brick faces RIGHT into arena, leaves face LEFT into void)
        if (left != null)
        {
            left.transform.localPosition = new Vector3(-8.1f, 0f, 0f);
            left.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            left.transform.localScale = Vector3.one;
            SpriteRenderer sr = left.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = brickSprite;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = new Vector2(12f, 1.25f);
                sr.sortingOrder = -50;
                sr.flipY = false;
                sr.flipX = false;
            }
            BoxCollider2D col = left.GetComponent<BoxCollider2D>();
            if (col != null) col.size = new Vector2(12f, 1.25f);
        }

        // 4. Right Wall (clean brick faces LEFT into arena, leaves face RIGHT into void)
        if (right != null)
        {
            right.transform.localPosition = new Vector3(8.1f, 0f, 0f);
            right.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            right.transform.localScale = Vector3.one;
            SpriteRenderer sr = right.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = brickSprite;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = new Vector2(12f, 1.25f);
                sr.sortingOrder = -50;
                sr.flipY = false;
                sr.flipX = false;
            }
            BoxCollider2D col = right.GetComponent<BoxCollider2D>();
            if (col != null) col.size = new Vector2(12f, 1.25f);
        }
    }

    // ---------- Ground ----------

    private void BuildGround()
    {
        if (groundTile == null) return;

        // One single seamless continuous tiled ground covering the entire arena cleanly.
        GameObject groundObj = NewSprite("Ground", groundTile, Vector2.zero, GroundSortingOrder);
        SpriteRenderer renderer = groundObj.GetComponent<SpriteRenderer>();
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.size = new Vector2(arenaHalfSize.x * 2f, arenaHalfSize.y * 2f);
        renderer.tileMode = SpriteTileMode.Continuous;
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

    private void BuildCourtyard(System.Random random)
    {
        // Bengali Hut placed neatly in top-center, flush against the top wall
        Prop(houseSprite, new Vector2(0.0f, 3.85f), 0.55f, 0.55f);

        // Bamboo fences extending to the sides of the house as garden borders
        Prop(fenceSprite, new Vector2(-2.8f, 4.2f), 0.85f, 0.30f);
        Prop(fenceSprite, new Vector2( 2.8f, 4.2f), 0.85f, 0.30f);

        // Coconut Trees standing tall on left and right
        Prop(treeSprite, new Vector2(-4.8f, -1.2f), 1.15f, 0.35f, false, 0.35f); // Left tree moved down
        Prop(treeSprite, new Vector2( 4.8f, -1.0f), 1.15f, 0.35f, false, 0.35f);

        // Stone Well on the right side, near the tree
        Prop(wellSprite, new Vector2(3.5f, -0.6f), 0.70f, 0.50f);

        // Charpai resting in lower-left courtyard
        Prop(charpaiSprite, new Vector2(-2.8f, -2.2f), 0.85f, 0.35f);

        // Clay pots neatly placed by house porch and near the well
        Prop(potSprite, new Vector2(-1.1f, 2.8f), 0.65f, 0.28f);
        Prop(potSprite, new Vector2( 2.5f, -0.2f), 0.60f, 0.28f);
    }

    // The fair: busier, lantern-lit, more to bump into.
    private void BuildMela(System.Random random)
    {
        Sprite redStall  = melaStallRedSprite  != null ? melaStallRedSprite  : houseSprite;
        Sprite blueStall = melaStallBlueSprite != null ? melaStallBlueSprite : houseSprite;
        Sprite stage     = melaStageSprite     != null ? melaStageSprite     : houseSprite;
        Sprite cart      = melaCartSprite      != null ? melaCartSprite      : charpaiSprite;
        Sprite table     = melaTableSprite     != null ? melaTableSprite     : charpaiSprite;

        // Stage centered along top wall, pushed higher
        Prop(stage,     new Vector2( 0.0f,  4.2f), 0.65f, 0.50f);
        // Colorful stalls flanking left and right of stage, pushed higher
        Prop(blueStall, new Vector2(-3.4f,  4.0f), 0.60f, 0.45f);
        Prop(redStall,  new Vector2( 3.4f,  4.0f), 0.60f, 0.45f);

        // 3 mela tables along the right side
        Prop(table, new Vector2( 6.0f,  2.2f), 0.65f, 0.35f);
        Prop(table, new Vector2( 6.0f,  0.2f), 0.65f, 0.35f);
        Prop(table, new Vector2( 6.0f, -1.8f), 0.65f, 0.35f);

        // Well at the lower-right area (replaces bottom stall)
        Prop(wellSprite, new Vector2( 2.0f, -3.6f), 0.70f, 0.50f);

        // Mela Table as a sweet/food stall counter near upper-left
        Prop(table,     new Vector2(-4.2f,  2.0f), 0.65f, 0.35f);
        // Mela Table on the left side (replaces cart)
        Prop(table,     new Vector2(-4.5f, -1.0f), 0.65f, 0.35f);

        // Well at lower-right, shifted so it doesn't crowd the right stalls
        Prop(wellSprite, new Vector2( 4.8f, -3.0f), 0.70f, 0.50f);
        // Charpai resting in lower-left, away from bottom stall
        Prop(charpaiSprite, new Vector2(-3.0f, -3.2f), 0.80f, 0.35f);
        
        // Coconut Tree placed in a better spot (near upper-left stalls)
        Prop(treeSprite, new Vector2(-5.5f,  2.5f), 1.0f, 0.35f, false, 0.35f);

        // Lanterns placed symmetrically in the exact 4 corners touching the walls
        Prop(lanternSprite, new Vector2(-7.2f,  4.8f), 0.85f, 0f);
        Prop(lanternSprite, new Vector2( 7.2f,  4.8f), 0.85f, 0f);
        Prop(lanternSprite, new Vector2(-7.2f, -4.8f), 0.85f, 0f);
        Prop(lanternSprite, new Vector2( 7.2f, -4.8f), 0.85f, 0f);
    }

    // Storm night: things have been blown around, middle has clear space.
    private void BuildStormNight(System.Random random)
    {
        Sprite sHut   = stormHutSprite   != null ? stormHutSprite   : houseSprite;
        Sprite sCart  = stormCartSprite  != null ? stormCartSprite  : charpaiSprite;
        Sprite puddle = rainPuddleSprite != null ? rainPuddleSprite : potSprite;

        // Damaged Storm Huts placed on opposite sides, safely away from spawn corners
        Prop(sHut,  new Vector2(-3.5f,  2.0f), 0.55f, 0.55f);       // Left side
        Prop(sHut,  new Vector2( 3.5f,  2.0f), 0.55f, 0.55f, true); // Right side (flipped to face inward)

        // Stone Well standing sturdy in upper-right
        Prop(wellSprite, new Vector2( 3.0f,  4.0f), 0.70f, 0.50f);
        // Overturned cart in lower-left
        Prop(sCart, new Vector2(-4.5f, -1.8f), 0.75f, 0.40f);
        // Charpai in lower-right
        Prop(charpaiSprite, new Vector2( 3.5f, -2.5f), 0.80f, 0.35f);
        // Wind-blown Coconut Tree on right edge
        Prop(treeSprite, new Vector2( 5.5f, -0.5f), 1.0f, 0.35f, false, 0.35f);
        // Another wind-blown tree on left edge
        Prop(treeSprite, new Vector2(-5.5f, -0.5f), 1.0f, 0.35f, false, 0.35f);

        // Broken fence pieces along boundaries
        Prop(fenceSprite, new Vector2(-2.0f,  2.2f), 0.80f, 0.30f);
        Prop(fenceSprite, new Vector2( 2.0f,  2.2f), 0.80f, 0.30f);
    }

    // The final round: structured village scene with pond and corner lanterns.
    private void BuildFinalCatch(System.Random random)
    {
        Sprite pond    = villagePondSprite  != null ? villagePondSprite  : wellSprite;
        Sprite cluster = treeClusterSprite  != null ? treeClusterSprite  : treeSprite;

        // Structured Bengali Village scene
        // Main house in the top center
        Prop(houseSprite, new Vector2( 0.0f,  4.0f), 0.60f, 0.55f);
        // Second smaller house on the left
        Prop(houseSprite, new Vector2(-4.5f,  3.5f), 0.50f, 0.55f);

        // Lush Tree Clusters framing the village at the top boundaries
        Prop(cluster,     new Vector2( 4.5f,  3.8f), 0.75f, 0.45f, false, 0.4f);
        Prop(cluster,     new Vector2(-6.0f,  3.8f), 0.75f, 0.45f, false, 0.4f);

        // Scenic Village Pond resting in lower-left to balance the scene
        // Decorative (no collider) so it always spawns, plus invisible blocker
        Prop(pond,        new Vector2(-4.0f, -2.2f), 0.75f, 0f);
        // Invisible collider to block players from walking through the pond
        AddInvisibleBlocker(new Vector2(-4.0f, -2.2f), new Vector2(2.0f, 1.5f));

        // Stone well placed elegantly near the center-right
        Prop(wellSprite,  new Vector2( 3.5f,  1.5f), 0.70f, 0.50f);
        
        // Fences wrapping around the main village area
        Prop(fenceSprite, new Vector2(-2.5f,  2.5f), 0.80f, 0.30f);
        Prop(fenceSprite, new Vector2( 2.5f,  2.5f), 0.80f, 0.30f);

        // Charpai resting in lower-right shade near a tree
        Prop(charpaiSprite, new Vector2( 4.5f, -2.2f), 0.80f, 0.35f);
        Prop(treeSprite,    new Vector2( 5.5f, -2.5f), 0.90f, 0.35f, false, 0.35f);

        // Village lanterns in the precise 4 corners of the arena
        Prop(lanternSprite, new Vector2(-7.2f,  4.8f), 0.85f, 0f);
        Prop(lanternSprite, new Vector2( 7.2f,  4.8f), 0.85f, 0f);
        Prop(lanternSprite, new Vector2(-7.2f, -4.8f), 0.85f, 0f);
        Prop(lanternSprite, new Vector2( 7.2f, -4.8f), 0.85f, 0f);

        // Lanterns flanking the main house entrance
        Prop(lanternSprite, new Vector2(-1.5f,  3.0f), 0.75f, 0f);
        Prop(lanternSprite, new Vector2( 1.5f,  3.0f), 0.75f, 0f);
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
    private GameObject Prop(Sprite sprite, Vector2 position, float scale, float colliderHeight, bool flipX = false, float colWidth = 0.8f, float rotationZ = 0f)
    {
        if (sprite == null) return null;

        // A prop sitting on top of a spawn point traps whoever appears
        // there, so solid props near one are simply skipped.
        if (colliderHeight > 0f && IsNearSpawnPoint(position)) return null;

        GameObject obj = NewSprite(sprite.name, sprite, position, 0, flipX);
        obj.transform.localScale = Vector3.one * scale;

        if (rotationZ != 0f)
            obj.transform.localRotation = Quaternion.Euler(0f, 0f, rotationZ);

        // Sorting by height, so a player walking behind a house is hidden by
        // it and one walking in front is drawn over it.
        obj.GetComponent<SpriteRenderer>().sortingOrder =
                PropSortingBase + Mathf.RoundToInt(-position.y * 10f);

        if (colliderHeight > 0f)
        {
            float width = sprite.rect.width / sprite.pixelsPerUnit * scale;
            float height = sprite.rect.height / sprite.pixelsPerUnit * scale;

            BoxCollider2D collider = obj.AddComponent<BoxCollider2D>();

            // The collider blocks movement - make it robust so players don't walk through
            collider.size = new Vector2(width * colWidth, height * colliderHeight);
            collider.offset = new Vector2(0f, -height * 0.5f + collider.size.y * 0.5f);
        }

        return obj;
    }

    // Creates an invisible BoxCollider2D at the given position & size.
    // Used for things like ponds where the sprite must always render
    // (colliderHeight = 0 skips the spawn-point check) but players
    // should still be blocked from walking through.
    private void AddInvisibleBlocker(Vector2 position, Vector2 size)
    {
        GameObject blocker = new GameObject("InvisibleBlocker");
        blocker.transform.SetParent(root, false);
        blocker.transform.position = position;

        BoxCollider2D col = blocker.AddComponent<BoxCollider2D>();
        col.size = size;
        col.offset = Vector2.zero;

        spawned.Add(blocker);
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

    private GameObject NewSprite(string name, Sprite sprite, Vector2 position, int sortingOrder, bool flipX = false)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(root, false);
        obj.transform.position = position;

        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        renderer.flipX = flipX;

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

        StopAllCoroutines(); // Stop previous lightning if any

        switch (levelIndex)
        {
            case 0:  // Courtyard, late afternoon
                globalLight.color = new Color(1f, 0.92f, 0.78f);
                break;
            case 1:  // Mela, lantern-lit evening
                globalLight.color = new Color(1f, 0.78f, 0.52f);
                break;
            case 2:  // Storm night, cold and blue with lightning
                globalLight.color = new Color(0.62f, 0.74f, 0.95f);
                StartCoroutine(LightningEffect(globalLight));
                break;
            default: // Final catch, torchlight
                globalLight.color = new Color(1f, 0.66f, 0.45f);
                break;
        }
    }

    private System.Collections.IEnumerator LightningEffect(UnityEngine.Rendering.Universal.Light2D light)
    {
        Color normalColor = light.color;
        float normalIntensity = light.intensity;
        
        while (true)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(3f, 8f));
            
            // First flash
            light.color = Color.white;
            light.intensity = normalIntensity * 3f;
            yield return new WaitForSeconds(0.1f);
            light.color = normalColor;
            light.intensity = normalIntensity;
            
            // Random chance for a secondary quick flash
            if (UnityEngine.Random.value > 0.5f)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.05f, 0.2f));
                light.color = Color.white;
                light.intensity = normalIntensity * 2f;
                yield return new WaitForSeconds(0.1f);
                light.color = normalColor;
                light.intensity = normalIntensity;
            }
        }
    }
}
