using System.Collections.Generic;
using UnityEngine;

namespace Fable
{
    /// <summary>Every menu in the game. Each one is a ListScreen with its own rows.</summary>
    public static class Menus
    {
        static GameState S { get { return Game.State; } }

        static void Push(ListScreen screen) { Game.I.Push(screen); }

        // ------------------------------------------------------------------ title

        public static void OpenTitle()
        {
            var screen = new ListScreen("Fablebound", () =>
            {
                var rows = new List<Row>();
                rows.Add(new Row("Continue", "Pick up your journey where you last saved.", () =>
                {
                    if (!Game.I.Continue()) Game.I.Say("The save file could not be read.");
                }) { enabled = SaveSystem.HasSave });
                rows.Add(new Row("New Game", "Choose a class and set out from Hearthmeadow.", OpenClassSelect));
                rows.Add(new Row("Settings", "Volume, UI size, camera and battle speed.", OpenSettings));
                rows.Add(new Row("Quit", "Close the game.", Application.Quit));
                return rows;
            });
            screen.cancellable = false;
            screen.header = () => "A creature-collecting tale of yokai, myth and fairy story.\n"
                + Database.Species.Length + " creatures. 12 types. 12 classes.";
            Push(screen);
        }

        static string ClassSummary(ClassDef c)
        {
            return c.desc + "\n\n"
                + "Type: " + TypeChart.Name(c.primary) + "   (ascends to " + c.advancedName + ", adding " + TypeChart.Name(c.secondary) + ")\n"
                + "Favours: " + (c.physical ? "techniques (AP)" : "spells (MP)") + "\n"
                + "HP " + c.hp + "  Atk " + c.atk + "  Def " + c.def + "  Mag " + c.mag + "  Res " + c.res + "  Spd " + c.spd + "\n"
                + "MP " + c.mp + "  AP " + c.ap + "\n\n"
                + "Signature skills: " + Database.GetMove(c.unique1).name + ", " + Database.GetMove(c.unique2).name;
        }

        static void OpenClassSelect()
        {
            var screen = new ListScreen("Choose your class", () =>
            {
                var rows = new List<Row>();
                foreach (var c in ClassDatabase.Classes)
                {
                    var cls = c;
                    rows.Add(new Row(cls.name, ClassSummary(cls), () => Game.I.NewGame(cls.id))
                    {
                        right = TypeChart.Name(cls.primary), color = TypeChart.ColorOf(cls.primary)
                    });
                }
                return rows;
            });
            screen.header = () => "Your class sets your type, stats and skills.\nYou can change class later and keep what you learned.";
            Push(screen);
        }

        // ------------------------------------------------------------------ main menu

        public static void OpenMain()
        {
            var screen = new ListScreen("Menu", () =>
            {
                var rows = new List<Row>();
                rows.Add(new Row("Party", "Your team: you and up to three creatures. Check stats, innate abilities and moves.", OpenParty));
                rows.Add(new Row("Inventory", "Use tonics, talismans and relics.", OpenInventory));
                rows.Add(new Row("Skills", "Choose which of your learned skills are equipped for battle.", OpenSkills));
                rows.Add(new Row("Skill Tree", "Spend skill points on your current class.", () => OpenSkillTree(S.player.Class)));
                rows.Add(new Row("Class", "Change class, or attach an imprinted type.", OpenClasses));
                rows.Add(new Row("Type Chart", "What each type is strong and weak against.", OpenTypeChart));
                rows.Add(new Row("Settings", "Volume, UI size, camera and battle speed.", OpenSettings));
                rows.Add(new Row("Save", "Write your progress to disk.", () =>
                    Game.I.Say(SaveSystem.Save(S) ? "Progress saved." : "Saving failed. Check the console for details.")));
                rows.Add(new Row("Quit to Title", "Return to the title screen. Unsaved progress is lost.", OpenQuitConfirm));
                return rows;
            });
            screen.header = () =>
            {
                int minutes = Mathf.FloorToInt(S.playSeconds / 60f);
                return S.player.Describe() + "\nGold " + S.gold + "    Creatures bound " + S.caught.Count + "/" + Database.Species.Length
                    + "    Time " + (minutes / 60) + "h " + (minutes % 60) + "m";
            };
            Push(screen);
        }

        static void OpenQuitConfirm()
        {
            ListScreen screen = null;
            screen = new ListScreen("Quit to title?", () => new List<Row>
            {
                new Row("No, keep playing", "Go back to the menu.", () => screen.Close()),
                new Row("Yes, quit", "Anything since your last save is lost.", () => Game.I.QuitToTitle())
            });
            Push(screen);
        }

        // ------------------------------------------------------------------ party

        static void OpenParty()
        {
            var screen = new ListScreen("Party", () =>
            {
                var rows = new List<Row>();
                var p = S.player;
                string skills = "\nSkills: ";
                for (int i = 0; i < p.equippedSkills.Count; i++)
                    skills += (i > 0 ? ", " : "") + Database.GetMove(p.equippedSkills[i]).name;
                rows.Add(new Row(p.name, p.Describe() + skills, OpenSkills)
                {
                    right = p.ClassName + "  Lv " + p.level, color = TypeChart.ColorOf(p.Type1)
                });
                foreach (var c in S.party)
                {
                    var creature = c;
                    rows.Add(new Row(creature.Name, creature.Describe(), () => OpenCreature(creature))
                    {
                        right = (creature.Fainted ? "fainted  " : "") + "Lv " + creature.level, color = TypeChart.ColorOf(creature.Species.Type1)
                    });
                }
                for (int i = S.party.Count; i < GameState.MaxParty; i++)
                    rows.Add(new Row("(empty slot)", "Capture a wild creature with a talisman to fill this slot.") { enabled = false });
                return rows;
            });
            screen.header = () => "Storage holds " + S.storage.Count + " more. Swap creatures at any shrine.";
            Push(screen);
        }

        static void OpenCreature(CreatureInstance c)
        {
            ListScreen screen = null;
            screen = new ListScreen(c.Name, () =>
            {
                var rows = new List<Row>();
                rows.Add(new Row("Moves", "Choose up to " + CreatureInstance.MaxEquipped + " moves to bring into battle.", () => OpenCreatureMoves(c)));
                rows.Add(new Row("Move to front", "Put this creature first in the team order.", () =>
                {
                    S.party.Remove(c);
                    S.party.Insert(0, c);
                }) { enabled = S.party.IndexOf(c) > 0 });
                rows.Add(new Row("Release", "Set this creature free for good.", () =>
                {
                    ListScreen confirm = null;
                    confirm = new ListScreen("Release " + c.Name + "?", () => new List<Row>
                    {
                        new Row("Keep it", "", () => confirm.Close()),
                        new Row("Release it", "It will return to the wild and cannot be recovered.", () =>
                        {
                            S.party.Remove(c);
                            confirm.Close();
                            screen.Close();
                            Game.I.Say(c.Name + " bows once and slips back into the wild.");
                        })
                    });
                    Push(confirm);
                }));
                return rows;
            });
            screen.header = () => c.Describe();
            Push(screen);
        }

        static void OpenCreatureMoves(CreatureInstance c)
        {
            var screen = new ListScreen(c.Name + ": moves", () =>
            {
                var rows = new List<Row>();
                foreach (var id in c.knownMoves)
                {
                    string moveId = id;
                    var m = Database.GetMove(moveId);
                    bool on = c.equippedMoves.Contains(moveId);
                    rows.Add(new Row((on ? "[x] " : "[ ] ") + m.name, m.Summary, () =>
                    {
                        if (c.equippedMoves.Contains(moveId))
                        {
                            if (c.equippedMoves.Count > 1) c.equippedMoves.Remove(moveId);
                        }
                        else if (c.equippedMoves.Count < CreatureInstance.MaxEquipped) c.equippedMoves.Add(moveId);
                        else Game.I.Say("Only " + CreatureInstance.MaxEquipped + " moves can be equipped. Remove one first.");
                    }) { right = m.CostText, color = TypeChart.ColorOf(m.Element) });
                }
                return rows;
            });
            screen.header = () => "Equipped " + c.equippedMoves.Count + "/" + CreatureInstance.MaxEquipped;
            Push(screen);
        }

        // ------------------------------------------------------------------ inventory

        static void OpenInventory()
        {
            var screen = new ListScreen("Inventory", () =>
            {
                var rows = new List<Row>();
                foreach (var stack in S.items)
                {
                    ItemData item;
                    if (!Database.Items.TryGetValue(stack.id, out item)) continue;
                    var it = item;
                    rows.Add(new Row(it.name, it.desc, () => UseItem(it)) { right = "x" + stack.count });
                }
                return rows;
            });
            screen.header = () => "Gold: " + S.gold;
            Push(screen);
        }

        static void UseItem(ItemData item)
        {
            if (item.kind == ItemKind.Capture)
            {
                Game.I.Say("Talismans are thrown in battle. Choose Capture on your own turn.");
                return;
            }
            ListScreen screen = null;
            screen = new ListScreen("Use " + item.name + " on...", () =>
            {
                var rows = new List<Row>();
                if (S.ItemCount(item.id) <= 0)
                {
                    rows.Add(new Row("(none left)") { enabled = false });
                    return rows;
                }
                var p = S.player;
                if (item.kind == ItemKind.Heal || item.kind == ItemKind.Mana)
                {
                    bool useful = item.kind == ItemKind.Heal ? p.hp < p.MaxHp : p.mp < p.MaxMp;
                    rows.Add(new Row(p.name, p.Describe(), () =>
                    {
                        if (item.kind == ItemKind.Heal) p.hp = Mathf.Min(p.MaxHp, p.hp + Mathf.RoundToInt(item.power));
                        else p.mp = Mathf.Min(p.MaxMp, p.mp + Mathf.RoundToInt(item.power));
                        S.AddItem(item.id, -1);
                    }) { right = "HP " + p.hp + "/" + p.MaxHp + "  MP " + p.mp + "/" + p.MaxMp, enabled = useful });
                }
                foreach (var cr in S.party)
                {
                    var c = cr;
                    bool useful;
                    switch (item.kind)
                    {
                        case ItemKind.Heal: useful = !c.Fainted && c.hp < c.MaxHp; break;
                        case ItemKind.Mana: useful = !c.Fainted && c.mp < c.MaxMp; break;
                        case ItemKind.Revive: useful = c.Fainted; break;
                        case ItemKind.Evolve: useful = c.CanEvolveWithItem(item.id); break;
                        default: useful = false; break;
                    }
                    rows.Add(new Row(c.Name, c.Describe(), () =>
                    {
                        S.AddItem(item.id, -1);
                        switch (item.kind)
                        {
                            case ItemKind.Heal: c.hp = Mathf.Min(c.MaxHp, c.hp + Mathf.RoundToInt(item.power)); break;
                            case ItemKind.Mana: c.mp = Mathf.Min(c.MaxMp, c.mp + Mathf.RoundToInt(item.power)); break;
                            case ItemKind.Revive: c.hp = Mathf.Max(1, Mathf.RoundToInt(c.MaxHp * item.power)); break;
                            case ItemKind.Evolve:
                                var lines = new List<string> { "The " + item.name + " dissolves into light around " + c.Name + "..." };
                                lines.AddRange(c.Evolve());
                                if (!S.caught.Contains(c.speciesId)) S.caught.Add(c.speciesId);
                                screen.Close();
                                Game.I.Say(lines, null);
                                break;
                        }
                    }) { right = "HP " + c.hp + "/" + c.MaxHp + "  MP " + c.mp + "/" + c.MaxMp, enabled = useful });
                }
                return rows;
            });
            if (item.kind == ItemKind.Cleanse)
            {
                Game.I.Say("Ailments fade when a battle ends. Save this for the middle of a fight.");
                return;
            }
            screen.header = () => item.desc + "\nLeft: " + S.ItemCount(item.id);
            Push(screen);
        }

        // ------------------------------------------------------------------ skills and classes

        static void OpenSkills()
        {
            var screen = new ListScreen("Skills", () =>
            {
                var rows = new List<Row>();
                var p = S.player;
                foreach (var id in p.knownSkills)
                {
                    string moveId = id;
                    var m = Database.GetMove(moveId);
                    bool on = p.equippedSkills.Contains(moveId);
                    rows.Add(new Row((on ? "[x] " : "[ ] ") + m.name, m.Summary, () =>
                    {
                        if (p.equippedSkills.Contains(moveId))
                        {
                            if (p.equippedSkills.Count > 1) p.equippedSkills.Remove(moveId);
                        }
                        else if (p.equippedSkills.Count < PlayerCharacter.MaxEquipped) p.equippedSkills.Add(moveId);
                        else Game.I.Say("Only " + PlayerCharacter.MaxEquipped + " skills can be equipped. Remove one first.");
                    }) { right = m.CostText, color = TypeChart.ColorOf(m.Element) });
                }
                return rows;
            });
            screen.header = () => "Equipped " + S.player.equippedSkills.Count + "/" + PlayerCharacter.MaxEquipped
                + ". Skills from every class you have trained appear here.\nYou are " + TypeChart.Names(S.player.Type1, S.player.Type2)
                + ": skills of those types hit 25% harder.";
            Push(screen);
        }

        static void OpenSkillTree(ClassDef cls)
        {
            var screen = new ListScreen(cls.name + " skill tree", () =>
            {
                var rows = new List<Row>();
                var p = S.player;
                foreach (var n in cls.nodes)
                {
                    var node = n;
                    bool owned = p.HasNode(cls.id, node.index);
                    string why = p.WhyLocked(cls, node);
                    string mark = owned ? "[x] " : (why == null ? "[ ] " : "[-] ");
                    string desc = node.desc + "\n\nCost: " + node.cost + " skill points"
                        + (node.minLevel > 0 ? "   Level " + node.minLevel + "+" : "")
                        + "\n" + (owned ? "Unlocked." : (why ?? "Press Select to unlock."));
                    rows.Add(new Row(mark + node.name, desc, () =>
                    {
                        string result = p.Unlock(cls, node);
                        if (result != null) Game.I.Say(result);
                    })
                    {
                        right = owned ? "" : node.cost + " SP",
                        enabled = !owned && why == null,
                        color = owned ? UI.GoldColor : UI.TextColor
                    });
                }
                return rows;
            });
            screen.header = () => "Skill points: " + S.player.skillPoints + "    Level " + S.player.level
                + (cls.id == S.player.classId ? "" : "\nYou are not currently a " + cls.name + ".");
            Push(screen);
        }

        static void OpenClasses()
        {
            var screen = new ListScreen("Class", () =>
            {
                var rows = new List<Row>();
                var p = S.player;

                string imprint = p.activeImprint >= 0 ? TypeChart.Name((Element)p.activeImprint) : "none";
                rows.Add(new Row("Attached imprint: " + imprint,
                    "Types you have imprinted: " + ImprintList(p) + "\n\nSelect to cycle. An attached imprint becomes your second type in whatever class you are, replacing the one from ascension."
                    + "\n\nEarn an imprint from the last node of a class's skill tree.", () =>
                {
                    if (p.imprintedTypes.Count == 0) return;
                    int at = p.imprintedTypes.IndexOf(p.activeImprint);
                    p.activeImprint = at + 1 < p.imprintedTypes.Count ? p.imprintedTypes[at + 1] : -1;
                }) { enabled = p.imprintedTypes.Count > 0 });

                foreach (var c in ClassDatabase.Classes)
                {
                    var cls = c;
                    bool current = cls.id == p.classId;
                    bool asc = p.ascended.Contains(cls.id);
                    int owned = 0;
                    for (int i = 0; i < cls.nodes.Length; i++)
                        if (p.HasNode(cls.id, i)) owned++;
                    rows.Add(new Row((current ? "* " : "") + (asc ? cls.advancedName : cls.name),
                        ClassSummary(cls) + "\n\nTree: " + owned + "/" + cls.nodes.Length + " unlocked", () => OpenClassActions(cls))
                    {
                        right = TypeChart.Name(cls.primary) + (asc ? " / " + TypeChart.Name(cls.secondary) : ""),
                        color = TypeChart.ColorOf(cls.primary)
                    });
                }
                return rows;
            });
            screen.header = () => "Now: " + S.player.ClassName + "  (" + TypeChart.Names(S.player.Type1, S.player.Type2) + ")";
            Push(screen);
        }

        static string ImprintList(PlayerCharacter p)
        {
            if (p.imprintedTypes.Count == 0) return "none yet";
            string s = "";
            for (int i = 0; i < p.imprintedTypes.Count; i++)
                s += (i > 0 ? ", " : "") + TypeChart.Name((Element)p.imprintedTypes[i]);
            return s;
        }

        static void OpenClassActions(ClassDef cls)
        {
            ListScreen screen = null;
            screen = new ListScreen(cls.name, () =>
            {
                var rows = new List<Row>();
                bool current = cls.id == S.player.classId;
                rows.Add(new Row("Switch to this class",
                    "Your level, learned skills, permanent bonuses and imprints all carry over. You learn this class's level-up moves at once.", () =>
                {
                    S.player.SetClass(cls.id);
                    screen.Close();
                    Game.I.Say("You are now a " + S.player.ClassName + ". (" + TypeChart.Names(S.player.Type1, S.player.Type2) + ")");
                }) { enabled = !current });
                rows.Add(new Row("View skill tree", "See what this class can teach.", () => OpenSkillTree(cls)));
                return rows;
            });
            screen.header = () => ClassSummary(cls);
            Push(screen);
        }

        static void OpenTypeChart()
        {
            var screen = new ListScreen("Type Chart", () =>
            {
                var rows = new List<Row>();
                for (int a = 0; a < TypeChart.Count; a++)
                {
                    var atk = (Element)a;
                    string strong = "", weak = "", hurtBy = "", shrugs = "";
                    for (int d = 0; d < TypeChart.Count; d++)
                    {
                        var other = (Element)d;
                        float outgoing = TypeChart.Single(atk, other), incoming = TypeChart.Single(other, atk);
                        if (outgoing > 1f) strong += (strong.Length > 0 ? ", " : "") + other;
                        if (outgoing < 1f) weak += (weak.Length > 0 ? ", " : "") + other;
                        if (incoming > 1f) hurtBy += (hurtBy.Length > 0 ? ", " : "") + other;
                        if (incoming < 1f) shrugs += (shrugs.Length > 0 ? ", " : "") + other;
                    }
                    rows.Add(new Row(atk.ToString(),
                        "Attacking\n  Strong against: " + strong + "\n  Resisted by: " + weak
                        + "\n\nDefending\n  Weak to: " + hurtBy + "\n  Resists: " + shrugs
                        + "\n\nField ability: " + TypeChart.AbilityOf(atk)) { color = TypeChart.ColorOf(atk) });
                }
                return rows;
            });
            screen.header = () => "Strong hits deal double damage, resisted hits half.\nWith two types, both count.";
            Push(screen);
        }

        // ------------------------------------------------------------------ settings

        static float Cycle(float value, float[] steps)
        {
            int at = 0;
            for (int i = 0; i < steps.Length; i++)
                if (Mathf.Abs(steps[i] - value) < Mathf.Abs(steps[at] - value)) at = i;
            return steps[(at + 1) % steps.Length];
        }

        static void OpenSettings()
        {
            var screen = new ListScreen("Settings", () =>
            {
                var rows = new List<Row>();
                rows.Add(new Row("Volume", "Master volume. Select to change.", () =>
                {
                    Settings.Volume = Cycle(Settings.Volume, new[] { 0f, 0.2f, 0.4f, 0.6f, 0.8f, 1f });
                    Settings.Save();
                }) { right = Mathf.RoundToInt(Settings.Volume * 100f) + "%" });
                rows.Add(new Row("UI size", "Scale of all menus and text. Select to change.", () =>
                {
                    Settings.UiScale = Cycle(Settings.UiScale, new[] { 0.8f, 0.9f, 1f, 1.1f, 1.2f });
                    Settings.Save();
                }) { right = Mathf.RoundToInt(Settings.UiScale * 100f) + "%" });
                rows.Add(new Row("Camera distance", "How far the camera sits from you in the world. Select to change.", () =>
                {
                    Settings.CameraZoom = Cycle(Settings.CameraZoom, new[] { 0.75f, 1f, 1.3f, 1.6f });
                    Settings.Save();
                }) { right = Settings.CameraZoom < 0.9f ? "Close" : (Settings.CameraZoom < 1.15f ? "Normal" : (Settings.CameraZoom < 1.45f ? "Far" : "Very far")) });
                rows.Add(new Row("Fullscreen", "Switch between fullscreen and windowed.", () =>
                {
                    Settings.Fullscreen = !Settings.Fullscreen;
                    Settings.Save();
                }) { right = Settings.Fullscreen ? "On" : "Off" });
                rows.Add(new Row("Fast battle text", "Shorten the pause after each battle message.", () =>
                {
                    Settings.FastBattle = !Settings.FastBattle;
                    Settings.Save();
                }) { right = Settings.FastBattle ? "On" : "Off" });
                rows.Add(new Row("Controls",
                    "Keyboard\n  Move: WASD or arrow keys\n  Interact / Confirm: E, Space or Enter\n  Menu: Tab or Esc\n  Back: Esc or Backspace\n  Run: Left Shift\n\n"
                    + "Gamepad\n  Move: left stick or D-pad\n  Interact / Confirm: A (south)\n  Menu: Start or Y (north)\n  Back: B (east)\n  Run: X (west) or right trigger"));
                return rows;
            });
            Push(screen);
        }

        // ------------------------------------------------------------------ shrine

        public static void OpenShrine()
        {
            ListScreen screen = null;
            screen = new ListScreen("Shrine", () => new List<Row>
            {
                new Row("Buy supplies", "The shrine keeper sells tonics and talismans.", OpenShop),
                new Row("Creature storage", "Swap creatures between your team and storage.", OpenStorage),
                new Row("Save", "Write your progress to disk.", () =>
                    Game.I.Say(SaveSystem.Save(S) ? "Progress saved." : "Saving failed. Check the console for details.")),
                new Row("Leave", "Return to the road.", () => screen.Close())
            });
            screen.header = () => "Gold: " + S.gold + "\nTeam " + S.party.Count + "/" + GameState.MaxParty + "   Storage " + S.storage.Count;
            Push(screen);
        }

        static void OpenShop()
        {
            var screen = new ListScreen("Supplies", () =>
            {
                var rows = new List<Row>();
                foreach (var i in Database.ItemList)
                {
                    var item = i;
                    if (item.price <= 0) continue;
                    rows.Add(new Row(item.name, item.desc + "\n\nYou have " + S.ItemCount(item.id) + ".", () =>
                    {
                        S.gold -= item.price;
                        S.AddItem(item.id, 1);
                    }) { right = item.price + " g", enabled = S.gold >= item.price });
                }
                return rows;
            });
            screen.header = () => "Gold: " + S.gold;
            Push(screen);
        }

        static void OpenStorage()
        {
            var screen = new ListScreen("Creature storage", () =>
            {
                var rows = new List<Row>();
                rows.Add(new Row("Team (" + S.party.Count + "/" + GameState.MaxParty + ")") { header = true });
                foreach (var cr in S.party)
                {
                    var c = cr;
                    rows.Add(new Row(c.Name, c.Describe() + "\n\nSelect to send to storage.", () =>
                    {
                        S.party.Remove(c);
                        S.storage.Add(c);
                    }) { right = "Lv " + c.level, color = TypeChart.ColorOf(c.Species.Type1) });
                }
                rows.Add(new Row("Storage (" + S.storage.Count + ")") { header = true });
                foreach (var cr in S.storage)
                {
                    var c = cr;
                    rows.Add(new Row(c.Name, c.Describe() + "\n\nSelect to add to your team.", () =>
                    {
                        if (S.party.Count >= GameState.MaxParty)
                        {
                            Game.I.Say("Your team is full. Send a creature to storage first.");
                            return;
                        }
                        S.storage.Remove(c);
                        S.party.Add(c);
                    }) { right = "Lv " + c.level, color = TypeChart.ColorOf(c.Species.Type1) });
                }
                return rows;
            });
            screen.header = () => "Which creatures you carry decides which passes you can open.";
            Push(screen);
        }
    }
}
