# Phase 1 Setup Notes

## 1. Import scripts
Copy `PlayerController.cs` and `GameManager.cs` into your Unity project's
`Assets/Scripts/` folder (Unity will compile them automatically).

## 2. Build the arena
- Create an empty GameObject named `Arena`.
- Add a background sprite (any flat color square works for now).
- Add 4 `BoxCollider2D` objects around the edges as walls (not triggers).

## 3. Create the Player prefab
- Create a new GameObject, add a `Sprite Renderer` (any placeholder sprite/square).
- Add a `Rigidbody2D` (set Body Type to Dynamic, Gravity Scale 0).
- Add a `CircleCollider2D` or `BoxCollider2D`.
- Attach `PlayerController.cs`.
- Duplicate it to make Player 1 and Player 2.
  - Player 1: set `Controls` = WASD
  - Player 2: set `Controls` = Arrows

## 4. Set up the GameManager
- Create an empty GameObject named `GameManager`.
- Attach `GameManager.cs`.
- In the Inspector:
  - Drag Player 1 and Player 2 into the `Players` list.
  - Set `Spawn Points` to two Vector2 positions (e.g. (-3, 0) and (3, 0)).
  - Set `Catch Distance` to something like 1.

## 5. Add obstacles
- Drop 2-3 simple `BoxCollider2D` objects into the arena as placeholder
  obstacles (tree, bench, etc.) with a sprite so you can see them.

## 6. Test the loop
Press Play. You should see:
- A yellow debug label top-left showing who's currently Kanamachi.
- Moving the Kanamachi player into the other player triggers a catch.
- Roles swap, both players reset to spawn points, and the label updates.

That loop working end-to-end = Phase 1 complete. Do not move on to
blindfold/sound cues (Phase 2) until this is solid.
