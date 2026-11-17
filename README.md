# Fablebound (fabletestgame)

A 2.5D creature-collecting, turn-based strategy RPG for Unity, drawing on fairy tales, world mythology and Japanese yokai.

You are a class-based fighter who always stands in the first team slot:

```
[ You ] [ Creature 1 ] [ Creature 2 ] [ Creature 3 ]
```

You can fight alone, or bind up to three wild creatures to fight beside you.

> **Status:** first playable foundation. All code was written without access to a Unity editor, so it has **not yet been compiled or play-tested**. Expect to fix a few compile errors on first open. All art is procedural placeholder art.

## Opening the project

1. Open the folder in Unity Hub. The project targets **Unity 6 (6000.0 LTS)**; 2022.3 LTS should also work.
2. Let Unity import. If it asks to enable the new Input System backend, either answer works: the game reads input through the Input System when it is active and falls back to the old Input Manager when it is not.
3. Press **Play** in any scene, even the empty default one. The game builds itself in code (`Game.cs` boots automatically).
4. For a standalone build, run **Fable > Create Boot Scene** once from the menu bar first.

## Controls

| Action | Keyboard | Gamepad |
| --- | --- | --- |
| Move | WASD or arrow keys | Left stick or D-pad |
| Interact / Confirm | E, Space (Enter also confirms) | A (south) |
| Menu (Party, Inventory, Settings...) | Tab or Esc | Start or Y (north) |
| Back | Esc or Backspace | B (east) |
| Run | Left Shift | X (west) or right trigger |

## What is in the game

**Types.** Twelve: Ember, Tide, Grove, Gale, Stone, Frost, Storm, Spirit, Shade, Radiance, Feral, Arcane. Each is strong against two or three others and resisted by two or three. The in-game menu has the full chart.

**Creatures.** 407 species in 201 families:

- 201 base creatures, with one or two types
- 133 of them evolve once; 67 of those evolve a second time; every evolution has two types
- 6 hidden evolutions (level 50 plus a relic found in the Sanctum)
- 10 divine creatures, found only at Sanctum altars or as very rare wanderers in the outer lands

Each individual rolls its own **innate ability** from its species' pool (two common, one rare) and its own stat potential, so two of the same species differ. Creatures level up, learn moves at set levels, and evolve.

**Field abilities.** Every type grants one: Surf, Smash, Cut, Kindle, Freeze, Glide, Lantern, Phase, Track or Unseal. They rank up at levels 8, 25 and 42. Many species share each ability, so you choose by design, stats or type.

**The world.** One seeded open world: a safe meadow at the centre, eight biomes around it, and three ring walls dividing it into bands of rising level. Each wall has eight passes, each sealed by a different obstacle that needs a field ability of the right rank. The outermost band is the Sanctum of the Gods. Wild creatures roam visibly; touching one starts a battle. Shrines heal, save, sell supplies and hold stored creatures.

**Battles.** Turn-based, in Speed order.

- **HP** is health.
- **AP** pays for techniques and refills by 2 every turn.
- **MP** pays for spells and only comes back from items or shrines.
- Guard, use items, throw talismans to capture (the player only), or flee.

**Classes.** Twelve, one per type: Pyromancer, Tidecaller, Druid, Windrunner, Guardian, Frostblade, Thunder Monk, Onmyoji, Shinobi, Paladin, Berserker, Spellweaver.

- Your class gives you its type, stats and level-up moves.
- Each class has a 12-node **skill tree** bought with skill points (2 per level): skills, permanent stat bonuses, signature moves.
- **Ascension** evolves the class and adds a second type.
- **Type Imprint**, the last node, lets you keep that class's type forever and attach it to any other class.
- Skills learned in any class can be equipped in any other (6 slots).

## Project layout

```
Assets/
  Resources/Data/species.json, moves.json   generated game data
  Resources/VertexColor.shader              terrain shader
  Scripts/
    Data/     types and type chart, data loading, traits, items
    Core/     Game (boot and frame loop), input, player, classes, creatures, save
    Battle/   battle flow, damage, capture, rewards
    World/    world generation, overworld movement, wild creatures, interactables
    UI/       immediate-mode UI kit and every menu
    Editor/   Fable > Create Boot Scene
Tools/generate_data.py                      the creature roster and move tables
```

## Editing the roster

`Tools/generate_data.py` is the source of truth for creatures and moves. Each family is one line:

```
Kitling:E > Kitsune:EP > Kyubi:EM >> Tenko:ER | caster | Y
```

That reads: base form and type, evolutions with two types each, an optional hidden evolution after `>>`, then a stat archetype and an origin. Edit, then run `python3 Tools/generate_data.py` from the repository root to rebuild the JSON. Stats, learnsets and trait pools are derived from the line deterministically.

## Replacing placeholder art

Creature sprites are drawn in code from each species' id and types. Put a sprite at `Assets/Resources/Creatures/<Species Name>` and it is used instead.

## Not built yet

Story, quests and NPCs; sound and music; real art and animation; towns; trainer-style battles; balance tuning.
