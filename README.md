# The Great Cheese Escape

A free-roam, third-person **stealth & survival** game for Android, made in Unity.
You play a small mouse living inside the walls of a family home. The family has gone on
holiday and left the cat in charge. Sneak through the house, gather food for your pantry,
avoid the cat, and find the way out to the garden.

The mouse hero and the cat are original characters, built and animated procedurally in code.

## Gameplay

- **Free movement** – walk, run, sprint (stamina), sneak (silent, fits under beds and couches),
  jump and climb onto counters and the bed using stacks of books.
- **A connected house** – living room, kitchen, hallway, bedroom, bathroom, a locked storage
  room, the backyard, and the mouse's own **den** behind the living-room wall.
- **Mouse tunnels** – small holes the cat can't fit through, including a secret way into the
  storage room.
- **Food & pantry** – carry up to two snacks at a time (cheese, cookies, bread, grapes, peanuts),
  bring them home and STORE them, then EAT from the pantry later. Hunger slowly drops.
- **Interactions** – open doors, search cupboards / the fridge / the toolbox, read story notes,
  hide (under the bed or couch, in the sink cupboard or laundry basket), throw marbles to make
  noise elsewhere, turn the TV on to lure the cat, push cups off counters, and carefully steal
  cheese from mousetraps.
- **The cat (AI)** – an independent resident with a daily routine (sleeping, watching TV,
  eating, patrolling, grooming, knocking things off counters). It **hears** footsteps, doors and
  crashes and **sees** only with a clear line of sight. Suspicion builds before a chase; if it
  loses you it searches, checks hiding spots, and waits outside mouse holes.
- **Objectives in any order** – get into the storage room (key or secret tunnel), find the
  padlock key, unlock the back door, escape through the gap in the garden fence. Escape with a
  full pantry for 3 stars.
- **Story** – in-engine intro and ending cutscenes, plus five notes to find around the house.
- **Day & night** – the light changes over time; lamps switch on at night.

## Controls

| Action | Phone | Keyboard (editor) |
|---|---|---|
| Move | Left thumb (floating joystick) | WASD / arrows |
| Look around | Drag on the right side | Hold right mouse button |
| Run | RUN (hold) | Shift |
| Sneak | SNEAK | C |
| Jump | JUMP | Space |
| Use / pick up / hide | Contextual action button | E |
| Eat carried food | Tap a hand slot | F |
| Throw marble | THROW | Q |
| Pause | Pause button / Android back | Esc |

## Opening the project

1. Install **Unity 6000.3.28f1** (Unity 6.3) with **Android Build Support** (OpenJDK, SDK & NDK).
2. Open this folder in Unity Hub. The first import rebuilds the `Library` folder (a few minutes).
3. Open **`Assets/Scenes/House.unity`** and press Play.

The house, furniture layout, items, cat AI and UI are generated at runtime by the scripts in
`Assets/Scripts/House/`. If the scene ever needs recreating: **Tools > Cheese Escape > Create House Scene**.
Environment textures/materials can be regenerated with **Tools > Cheese Escape > Generate Art**.

## Building the Android APK

- In the editor: **File > Build Profiles > Android > Build**, or **Tools > Cheese Escape > Build Android APK**.
- Command line (editor closed): `build-android.bat` (see the comments at the top of the file).

The app is landscape, IL2CPP / ARM64, URP (mobile quality level), package `com.cheeseescape.game`.

## Project layout

| Path | What it is |
|---|---|
| `Assets/Scenes/House.unity` | The game (the only scene in the build) |
| `Assets/Scripts/House/` | Free-roam game: `HouseGame` (state, survival, objectives), `HouseBuilder` (house generation), `MouseController`, `ThirdPersonCamera`, `CatAI`, `Interactables`, `HouseUI`, `HouseCutscenes`, `HouseAmbient`, `BlobShadow` |
| `Assets/Scripts/` | Shared code: `MouseHero` / `CatChaser` (original characters), `AudioManager` (incl. synthesised SFX), `GameFX` (particles), `UIArt` (procedural UI sprites), `GameArt`, `GameSettings` |
| `Assets/Editor/` | Art generator, scene setup, Android build script |
| `Assets/Art/Generated/` | Generated textures, normal maps, materials |
| `Assets/Resources/` | `GameArt` registry + fonts (always included in builds) |
| `Assets/Mnostva_Art/` | Furniture models (third-party pack) |
| `Assets/sfx/` | Music and sound effects |
| `Assets/Scenes/SampleScene.unity` | Earlier endless-runner prototype (kept for reference, not in the build) |

## Credits

See [CREDITS.md](CREDITS.md).
