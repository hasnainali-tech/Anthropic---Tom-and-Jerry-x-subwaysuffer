# The Great Cheese Escape — Credits & Asset Sources

## Characters
- **Mouse hero** (caramel mouse with red scarf and blue sneakers) and **cat** (orange tabby):
  original designs, built and animated procedurally in code (`Assets/Scripts/MouseHero.cs`, `CatChaser.cs`).

## Art
- Floors, wallpapers, wainscoting, trap wood, window sky, effect sprites, reflection cubemap and the app icon:
  generated procedurally by `Assets/Editor/CheeseEscapeArt.cs` (Tools > Cheese Escape > Generate Art).
- The house, the mouse den, food, traps and small props: built procedurally at runtime (`Assets/Scripts/House/HouseBuilder.cs`).
- UI sprites and icons (bottle caps, paper, tape, icons): drawn procedurally at runtime (`Assets/Scripts/UIArt.cs`).
- Furniture: Mnostva_Art furniture pack (`Assets/Mnostva_Art`) — see that pack's own licence.

## Fonts (in `Assets/Resources/Fonts`, licence files included)
- **Luckiest Guy** by Astigmatic — Apache License 2.0 (`LuckiestGuy-LICENSE.txt`)
- **Lilita One** by Juan Montoreano — SIL Open Font License 1.1 (`LilitaOne-OFL.txt`)
- **Patrick Hand** by Patrick Wagesreiter — SIL Open Font License 1.1 (`PatrickHand-OFL.txt`)

## Audio
- Music and jump / slide / chew sounds: the project's files in `Assets/sfx`.
- UI click, whoosh, bonk, countdown beeps, pickup sparkle and jingles: synthesised in code at startup
  (`Assets/Scripts/AudioManager.cs`).
