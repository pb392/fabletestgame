using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Fable
{
    public enum BattleResult { Victory, Defeat, Fled }

    /// <summary>
    /// One turn-based battle: [Player][Creature][Creature][Creature] against one to three wild creatures.
    /// Each round everyone acts once in Speed order. Techniques spend AP, which refills every turn;
    /// spells spend MP, which does not. Runs as a coroutine started by Game.
    /// </summary>
    public class Battle
    {
        public readonly List<Combatant> allies = new List<Combatant>();
        public readonly List<Combatant> enemies = new List<Combatant>();

        readonly GameState state;
        readonly bool boss;
        readonly Biome biome;
        readonly Camera cam;
        readonly System.Random rng = new System.Random();
        readonly List<string> log = new List<string>();
        GameObject stage;
        static readonly Vector3 StagePos = new Vector3(0f, 500f, 0f);

        // menu being shown, if any
        string menuTitle;
        List<Row> menuRows;
        List<Combatant> menuTargets;
        int menuSel, menuScroll;
        int choice;
        Combatant actor;
        int capturedCount;
        int expPool, goldPool;

        public BattleResult Result { get; private set; }

        public Battle(GameState state, List<CreatureInstance> wild, Biome biome, bool boss, Camera cam)
        {
            this.state = state;
            this.biome = biome;
            this.boss = boss;
            this.cam = cam;

            allies.Add(Combatant.FromPlayer(state.player));
            for (int i = 0; i < state.party.Count && i < GameState.MaxParty; i++)
                if (!state.party[i].Fainted) allies.Add(Combatant.FromCreature(state.party[i], true));
            for (int i = 0; i < wild.Count; i++)
            {
                var e = Combatant.FromCreature(wild[i], false);
                if (boss)
                {
                    e.maxHp = Mathf.RoundToInt(e.maxHp * 1.5f);
                    e.hp = e.maxHp;
                }
                enemies.Add(e);
            }
            BuildStage();
        }

        // ------------------------------------------------------------------ stage

        void BuildStage()
        {
            stage = new GameObject("BattleStage");
            stage.transform.position = StagePos;

            var mesh = new Mesh();
            Color c = GroundTint();
            Color far = c * 0.75f;
            far.a = 1f;
            mesh.SetVertices(new List<Vector3>
            {
                new Vector3(-30, 0, -12), new Vector3(-30, 0, 30), new Vector3(30, 0, 30), new Vector3(30, 0, -12)
            });
            mesh.SetColors(new List<Color> { c, far, far, c });
            mesh.SetTriangles(new List<int> { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            var ground = new GameObject("Ground");
            ground.transform.SetParent(stage.transform, false);
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            var shader = Resources.Load<Shader>("VertexColor");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            ground.AddComponent<MeshRenderer>().sharedMaterial = new Material(shader);

            for (int i = 0; i < allies.Count; i++)
            {
                var a = allies[i];
                Sprite sprite = a.isPlayer ? SpriteFactory.Player(a.t1) : SpriteFactory.Creature(a.creature.Species);
                float scale = a.isPlayer ? 1.15f : 0.95f + a.creature.Species.stage * 0.18f;
                MakeView(a, sprite, new Vector3(-3.0f - i * 0.55f, 0f, 2.1f - i * 1.5f), scale);
            }
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                var s = e.creature.Species;
                float scale = 0.95f + s.stage * 0.18f + (s.IsDivine ? 0.8f : 0f);
                float z = enemies.Count == 1 ? 0.3f : 1.8f - i * 1.7f;
                MakeView(e, SpriteFactory.Creature(s), new Vector3(3.0f + i * 0.55f, 0f, z), scale);
            }

            cam.transform.position = StagePos + new Vector3(0f, 4.2f, -9.5f);
            cam.transform.LookAt(StagePos + new Vector3(0f, 0.9f, 0.5f));
        }

        Color GroundTint()
        {
            switch (biome)
            {
                case Biome.Meadow: return new Color(0.52f, 0.76f, 0.38f);
                case Biome.Forest: return new Color(0.30f, 0.55f, 0.28f);
                case Biome.Wetland: return new Color(0.42f, 0.55f, 0.36f);
                case Biome.Coast: return new Color(0.88f, 0.82f, 0.58f);
                case Biome.Mountain: return new Color(0.58f, 0.54f, 0.48f);
                case Biome.Snowfield: return new Color(0.92f, 0.95f, 0.98f);
                case Biome.Volcanic: return new Color(0.34f, 0.26f, 0.25f);
                case Biome.Ruins: return new Color(0.46f, 0.42f, 0.52f);
                case Biome.Highlands: return new Color(0.70f, 0.72f, 0.42f);
                default: return new Color(0.90f, 0.86f, 0.70f);
            }
        }

        void MakeView(Combatant c, Sprite sprite, Vector3 local, float scale)
        {
            var go = new GameObject(c.name);
            go.transform.SetParent(stage.transform, false);
            go.transform.localPosition = local;
            go.transform.localScale = Vector3.one * scale;
            c.sprite = go.AddComponent<SpriteRenderer>();
            c.sprite.sprite = sprite;
            go.AddComponent<Billboard>();
            c.view = go;
            c.home = local;
        }

        public void Dispose()
        {
            if (stage != null) UnityEngine.Object.Destroy(stage);
        }

        /// <summary>Per-frame animation: hit flashes, attack lunges and fading out the fallen.</summary>
        public void Update(float dt)
        {
            Animate(allies, dt, 1f);
            Animate(enemies, dt, -1f);
        }

        void Animate(List<Combatant> list, float dt, float dir)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (c.view == null) continue;
                c.flash = Mathf.Max(0f, c.flash - dt);
                c.lunge = Mathf.Max(0f, c.lunge - dt);
                float push = Mathf.Sin(Mathf.Clamp01(c.lunge / 0.25f) * Mathf.PI) * 0.9f * dir;
                float shake = c.flash > 0f ? Mathf.Sin(Time.time * 70f) * 0.08f : 0f;
                float bob = c == actor ? Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 0.12f : 0f;
                c.view.transform.localPosition = c.home + new Vector3(push + shake, bob, 0f);
                Color col = c.sprite.color;
                float targetAlpha = c.Alive ? 1f : 0f;
                col.a = Mathf.MoveTowards(col.a, targetAlpha, dt * 2.5f);
                float tint = c.flash > 0f ? 0.4f : 1f;
                col.g = tint;
                col.b = tint;
                c.sprite.color = col;
            }
        }

        // ------------------------------------------------------------------ flow

        static bool AnyAlive(List<Combatant> list)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].Alive) return true;
            return false;
        }

        Combatant PlayerUnit { get { return allies[0]; } }

        public IEnumerator Run()
        {
            string intro = enemies.Count == 1 ? enemies[0].name + " appears!" : enemies[0].name + " and its pack appear!";
            yield return Say(intro);
            yield return StartTraits(allies, enemies);
            yield return StartTraits(enemies, allies);

            bool fled = false;
            while (AnyAlive(allies) && AnyAlive(enemies) && !fled)
            {
                var order = new List<Combatant>();
                order.AddRange(allies);
                order.AddRange(enemies);
                order.Sort((a, b) => b.Stat(StatKind.Spd).CompareTo(a.Stat(StatKind.Spd)));

                for (int i = 0; i < order.Count && !fled; i++)
                {
                    var c = order[i];
                    if (!c.Alive) continue;
                    if (!AnyAlive(allies) || !AnyAlive(enemies)) break;

                    actor = c;
                    c.guarding = false;
                    c.ap = Mathf.Min(c.maxAp, c.ap + 2 + (c.trait == "vigor" ? 1 : 0));
                    if (c.trait == "wellspring") c.mp = Mathf.Min(c.maxMp, c.mp + Mathf.Max(1, c.maxMp / 25));
                    if (c.trait == "regrowth" && c.hp < c.maxHp) c.hp = Mathf.Min(c.maxHp, c.hp + Mathf.Max(1, c.maxHp / 20));

                    if (c.stunned)
                    {
                        c.stunned = false;
                        yield return Say(c.name + " is stunned and cannot act!");
                    }
                    else if (c.isAlly)
                    {
                        bool[] fleeBox = new bool[1];
                        yield return AllyTurn(c, fleeBox);
                        fled = fleeBox[0];
                    }
                    else
                    {
                        yield return EnemyTurn(c);
                    }

                    if (c.Alive && !fled) yield return Ailments(c);
                }
                actor = null;
            }

            if (fled) Result = BattleResult.Fled;
            else if (AnyAlive(allies)) Result = BattleResult.Victory;
            else Result = BattleResult.Defeat;

            if (Result == BattleResult.Victory) yield return Rewards();
            else if (Result == BattleResult.Defeat) yield return Say("Your team was overwhelmed...");

            for (int i = 0; i < allies.Count; i++) allies[i].WriteBack();
            if (state.player.hp <= 0) state.player.hp = 1;
            Game.I.EndBattle(Result);
        }

        IEnumerator StartTraits(List<Combatant> side, List<Combatant> other)
        {
            for (int i = 0; i < side.Count; i++)
            {
                var c = side[i];
                if (!c.Alive) continue;
                if (c.trait == "firststrike") c.ChangeStage(StatKind.Spd, 1);
                if (c.trait == "intimidate")
                {
                    for (int j = 0; j < other.Count; j++) other[j].ChangeStage(StatKind.Atk, -1);
                    yield return Say(c.name + "'s glare unsettles the other side! (Attack down)");
                }
            }
        }

        IEnumerator Ailments(Combatant c)
        {
            if (c.burn > 0)
            {
                c.burn--;
                int dmg = Mathf.Max(1, c.maxHp * 6 / 100);
                c.hp = Mathf.Max(0, c.hp - dmg);
                c.flash = 0.2f;
                yield return Say(c.name + " is scorched by its burn for " + dmg + ".");
            }
            if (c.Alive && c.poison > 0)
            {
                c.poison--;
                int dmg = Mathf.Max(1, c.maxHp * 8 / 100);
                c.hp = Mathf.Max(0, c.hp - dmg);
                c.flash = 0.2f;
                yield return Say(c.name + " suffers " + dmg + " from poison.");
            }
            if (c.hp <= 0) yield return Fall(c);
        }

        IEnumerator Fall(Combatant c)
        {
            c.hp = 0;
            if (!c.isAlly)
            {
                var s = c.creature.Species;
                expPool += Mathf.RoundToInt(s.expYield * c.level / 4f * (boss ? 1.5f : 1f));
                goldPool += c.level * 3 + rng.Next(10);
            }
            yield return Say(c.name + (c.isAlly ? " falls!" : " is defeated!"));
        }

        // ------------------------------------------------------------------ messages and menus

        IEnumerator Say(string text)
        {
            log.Add(text);
            if (log.Count > 4) log.RemoveAt(0);
            float wait = Settings.FastBattle ? 0.4f : 1.0f;
            yield return null;
            while (wait > 0f)
            {
                if (GameInput.ConfirmDown)
                {
                    GameInput.Consume();
                    break;
                }
                wait -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        /// <summary>Shows a menu and waits. The picked row index ends up in "choice", or -1 if cancelled.</summary>
        IEnumerator Choose(string title, List<Row> rows, bool cancellable, List<Combatant> targets = null)
        {
            menuTitle = title;
            menuRows = rows;
            menuTargets = targets;
            menuSel = 0;
            menuScroll = 0;
            choice = -1;
            yield return null;
            while (true)
            {
                if (GameInput.NavDown) menuSel = (menuSel + 1) % rows.Count;
                if (GameInput.NavUp) menuSel = (menuSel - 1 + rows.Count) % rows.Count;
                if (GameInput.ConfirmDown)
                {
                    GameInput.Consume();
                    if (rows[menuSel].enabled)
                    {
                        choice = menuSel;
                        break;
                    }
                }
                else if (GameInput.CancelDown && cancellable)
                {
                    GameInput.Consume();
                    break;
                }
                yield return null;
            }
            menuRows = null;
            menuTargets = null;
        }

        static List<Combatant> Living(List<Combatant> list)
        {
            var result = new List<Combatant>();
            for (int i = 0; i < list.Count; i++)
                if (list[i].Alive) result.Add(list[i]);
            return result;
        }

        static string Effectiveness(float mult)
        {
            if (mult >= 4f) return "Devastating";
            if (mult >= 2f) return "Strong";
            if (mult <= 0.25f) return "Feeble";
            if (mult <= 0.5f) return "Weak";
            return "";
        }

        /// <summary>Lets the player pick one target. The picked fighter ends up in pickedTarget, or null if cancelled.</summary>
        Combatant pickedTarget;

        IEnumerator ChooseTarget(List<Combatant> candidates, MoveData move)
        {
            pickedTarget = null;
            var rows = new List<Row>();
            for (int i = 0; i < candidates.Count; i++)
            {
                var t = candidates[i];
                var row = new Row(t.name + "  Lv " + t.level,
                    TypeChart.Names(t.t1, t.t2) + "\nHP " + t.hp + "/" + t.maxHp);
                if (move != null && move.Category != MoveCategory.Status && !t.isAlly)
                    row.right = Effectiveness(TypeChart.Multiplier(move.Element, t.t1, t.t2));
                rows.Add(row);
            }
            yield return Choose("Choose a target", rows, true, candidates);
            if (choice >= 0) pickedTarget = candidates[choice];
        }

        // ------------------------------------------------------------------ ally turn

        IEnumerator AllyTurn(Combatant c, bool[] fled)
        {
            while (true)
            {
                var rows = new List<Row>();
                rows.Add(new Row("Skills", "Use a technique (AP) or a spell (MP)."));
                rows.Add(new Row("Guard", "Halve damage until your next turn and recover 1 extra AP."));
                rows.Add(new Row("Items", "Use something from the bag."));
                if (c.isPlayer)
                {
                    rows.Add(new Row("Capture", "Throw a talisman to bind a wild creature. Works best on weakened or ailing targets."));
                    rows.Add(new Row("Flee", boss ? "There is no running from this." : "Try to escape the battle.") { enabled = !boss });
                }
                yield return Choose(c.name + "   AP " + c.ap + "/" + c.maxAp + "   MP " + c.mp + "/" + c.maxMp, rows, false);

                if (choice == 0)
                {
                    var ids = new List<string>(c.moves);
                    if (!ids.Contains("strike")) ids.Add("strike");
                    var moveRows = new List<Row>();
                    for (int i = 0; i < ids.Count; i++)
                    {
                        var m = Database.GetMove(ids[i]);
                        moveRows.Add(new Row(m.name, m.Summary) { right = m.CostText, enabled = c.CanAfford(m), color = TypeChart.ColorOf(m.Element) });
                    }
                    yield return Choose("Skills", moveRows, true);
                    if (choice < 0) continue;
                    var move = Database.GetMove(ids[choice]);
                    var targets = new List<Combatant>();
                    if (move.Target == MoveTarget.OneFoe)
                    {
                        var foes = Living(enemies);
                        if (foes.Count == 1) targets.Add(foes[0]);
                        else
                        {
                            yield return ChooseTarget(foes, move);
                            if (pickedTarget == null) continue;
                            targets.Add(pickedTarget);
                        }
                    }
                    else if (move.Target == MoveTarget.OneAlly)
                    {
                        yield return ChooseTarget(Living(allies), move);
                        if (pickedTarget == null) continue;
                        targets.Add(pickedTarget);
                    }
                    else if (move.Target == MoveTarget.AllFoes) targets = Living(enemies);
                    else if (move.Target == MoveTarget.AllAllies) targets = Living(allies);
                    else targets.Add(c);
                    yield return Execute(c, move, targets);
                    yield break;
                }
                if (choice == 1)
                {
                    c.guarding = true;
                    c.ap = Mathf.Min(c.maxAp, c.ap + 1);
                    yield return Say(c.name + " braces for impact.");
                    yield break;
                }
                if (choice == 2)
                {
                    bool[] used = new bool[1];
                    yield return UseItem(c, used);
                    if (used[0]) yield break;
                    continue;
                }
                if (choice == 3)
                {
                    bool[] used = new bool[1];
                    yield return Capture(c, used);
                    if (used[0]) yield break;
                    continue;
                }
                if (choice == 4)
                {
                    float fastest = 1f;
                    foreach (var e in Living(enemies)) fastest = Mathf.Max(fastest, e.Stat(StatKind.Spd));
                    float chance = Mathf.Clamp(0.55f + 0.3f * (c.Stat(StatKind.Spd) / fastest - 1f), 0.25f, 0.95f);
                    if (rng.NextDouble() < chance)
                    {
                        fled[0] = true;
                        yield return Say("You got away safely.");
                    }
                    else yield return Say("You could not get away!");
                    yield break;
                }
            }
        }

        IEnumerator UseItem(Combatant user, bool[] used)
        {
            var items = new List<ItemData>();
            var rows = new List<Row>();
            for (int i = 0; i < state.items.Count; i++)
            {
                ItemData item;
                if (!Database.Items.TryGetValue(state.items[i].id, out item)) continue;
                if (item.kind == ItemKind.Capture || item.kind == ItemKind.Evolve) continue;
                items.Add(item);
                rows.Add(new Row(item.name, item.desc) { right = "x" + state.items[i].count });
            }
            if (rows.Count == 0)
            {
                yield return Say("The bag holds nothing useful right now.");
                yield break;
            }
            yield return Choose("Items", rows, true);
            if (choice < 0) yield break;
            var picked = items[choice];

            var candidates = new List<Combatant>();
            for (int i = 0; i < allies.Count; i++)
            {
                bool fallen = allies[i].hp <= 0;
                if (picked.kind == ItemKind.Revive ? fallen : !fallen) candidates.Add(allies[i]);
            }
            if (candidates.Count == 0)
            {
                yield return Say("Nobody needs that.");
                yield break;
            }
            yield return ChooseTarget(candidates, null);
            if (pickedTarget == null) yield break;
            var t = pickedTarget;

            state.AddItem(picked.id, -1);
            used[0] = true;
            switch (picked.kind)
            {
                case ItemKind.Heal:
                    t.hp = Mathf.Min(t.maxHp, t.hp + Mathf.RoundToInt(picked.power));
                    yield return Say(user.name + " uses " + picked.name + ". " + t.name + " recovers HP.");
                    break;
                case ItemKind.Mana:
                    t.mp = Mathf.Min(t.maxMp, t.mp + Mathf.RoundToInt(picked.power));
                    yield return Say(user.name + " uses " + picked.name + ". " + t.name + " recovers MP.");
                    break;
                case ItemKind.Revive:
                    t.hp = Mathf.Max(1, Mathf.RoundToInt(t.maxHp * picked.power));
                    t.burn = t.poison = 0;
                    t.stunned = false;
                    yield return Say(user.name + " uses " + picked.name + ". " + t.name + " rises again!");
                    break;
                default:
                    t.burn = t.poison = 0;
                    t.stunned = false;
                    yield return Say(user.name + " uses " + picked.name + ". " + t.name + " is cleansed.");
                    break;
            }
        }

        IEnumerator Capture(Combatant user, bool[] used)
        {
            var items = new List<ItemData>();
            var rows = new List<Row>();
            for (int i = 0; i < state.items.Count; i++)
            {
                ItemData item;
                if (!Database.Items.TryGetValue(state.items[i].id, out item) || item.kind != ItemKind.Capture) continue;
                items.Add(item);
                rows.Add(new Row(item.name, item.desc) { right = "x" + state.items[i].count });
            }
            if (rows.Count == 0)
            {
                yield return Say("You have no talismans. Shrines sell them.");
                yield break;
            }
            yield return Choose("Capture with...", rows, true);
            if (choice < 0) yield break;
            var talisman = items[choice];

            var foes = Living(enemies);
            Combatant target;
            if (foes.Count == 1) target = foes[0];
            else
            {
                var targetRows = new List<Row>();
                for (int i = 0; i < foes.Count; i++)
                    targetRows.Add(new Row(foes[i].name + "  Lv " + foes[i].level, "HP " + foes[i].hp + "/" + foes[i].maxHp)
                    {
                        right = Mathf.RoundToInt(CaptureChance(foes[i], talisman, user) * 100f) + "%"
                    });
                yield return Choose("Bind which creature?", targetRows, true, foes);
                if (choice < 0) yield break;
                target = foes[choice];
            }

            state.AddItem(talisman.id, -1);
            used[0] = true;
            float chance = CaptureChance(target, talisman, user);
            yield return Say(user.name + " throws a " + talisman.name + " at " + target.name + "! (" + Mathf.RoundToInt(chance * 100f) + "%)");
            target.flash = 0.4f;
            if (rng.NextDouble() < chance)
            {
                target.captured = true;
                capturedCount++;
                var cr = target.creature;
                cr.hp = Mathf.Max(1, Mathf.Min(target.hp, cr.MaxHp));
                cr.mp = Mathf.Min(target.mp, cr.MaxMp);
                expPool += Mathf.RoundToInt(cr.Species.expYield * target.level / 8f);
                bool joined = state.AddCreature(cr);
                var t = cr.Trait;
                yield return Say("The talisman holds. " + cr.Species.name + " is bound to you!");
                yield return Say("Innate ability: " + (t != null ? t.name + " - " + t.desc : "none"));
                yield return Say(joined ? cr.Species.name + " joins your team." : "Your team is full, so " + cr.Species.name + " was sent to the shrine keepers' storage.");
            }
            else
            {
                yield return Say(target.name + " tears the talisman apart!");
            }
        }

        float CaptureChance(Combatant target, ItemData talisman, Combatant user)
        {
            float hpFrac = (float)target.hp / target.maxHp;
            float chance = target.creature.Species.captureRate * (1.6f - 1.3f * hpFrac) * talisman.power;
            if (target.HasAilment) chance *= 1.3f;
            if (target.level > user.level + 5) chance *= 0.5f;
            return Mathf.Clamp(chance, 0.01f, 0.95f);
        }

        // ------------------------------------------------------------------ enemy turn

        IEnumerator EnemyTurn(Combatant c)
        {
            var foes = Living(allies);
            if (foes.Count == 0) yield break;
            Combatant target = foes[rng.Next(foes.Count)];
            if (rng.NextDouble() < 0.35)
            {
                for (int i = 0; i < foes.Count; i++)
                    if ((float)foes[i].hp / foes[i].maxHp < (float)target.hp / target.maxHp) target = foes[i];
            }

            MoveData best = Database.GetMove("strike");
            float total = 0f;
            var options = new List<MoveData>();
            var scores = new List<float>();
            for (int i = 0; i < c.moves.Count; i++)
            {
                var m = Database.GetMove(c.moves[i]);
                if (!c.CanAfford(m)) continue;
                float score;
                if (m.Category == MoveCategory.Status)
                {
                    bool healing = m.effect == "Heal" || m.effect == "Cleanse";
                    score = healing ? ((float)c.hp / c.maxHp < 0.5f ? 90f : 2f) : 22f;
                }
                else score = m.power * TypeChart.Multiplier(m.Element, target.t1, target.t2);
                options.Add(m);
                scores.Add(score);
                total += score;
            }
            if (options.Count > 0)
            {
                float roll = (float)rng.NextDouble() * total;
                best = options[options.Count - 1];
                for (int i = 0; i < options.Count; i++)
                {
                    roll -= scores[i];
                    if (roll <= 0f) { best = options[i]; break; }
                }
            }

            var targets = new List<Combatant>();
            switch (best.Target)
            {
                case MoveTarget.OneFoe: targets.Add(target); break;
                case MoveTarget.AllFoes: targets = foes; break;
                case MoveTarget.AllAllies: targets = Living(enemies); break;
                case MoveTarget.OneAlly:
                    var friends = Living(enemies);
                    Combatant weakest = c;
                    for (int i = 0; i < friends.Count; i++)
                        if ((float)friends[i].hp / friends[i].maxHp < (float)weakest.hp / weakest.maxHp) weakest = friends[i];
                    targets.Add(weakest);
                    break;
                default: targets.Add(c); break;
            }
            yield return Execute(c, best, targets);
        }

        // ------------------------------------------------------------------ resolving a move

        IEnumerator Execute(Combatant user, MoveData move, List<Combatant> targets)
        {
            user.Pay(move);
            user.lunge = 0.25f;
            yield return Say(user.name + " uses " + move.name + "!");

            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (!t.Alive) continue;

                if (move.Category == MoveCategory.Status)
                {
                    yield return ApplyEffect(user, t, move, true);
                    continue;
                }

                if (rng.Next(100) >= move.accuracy)
                {
                    yield return Say(user.name + "'s attack misses " + t.name + ".");
                    continue;
                }

                bool physical = move.Category == MoveCategory.Technique;
                float atk = user.Stat(physical ? StatKind.Atk : StatKind.Mag);
                float def = t.Stat(physical ? StatKind.Def : StatKind.Res);
                float dmg = move.power * (atk / def) * (0.4f + user.level * 0.02f) * 0.5f;

                float typeMult = TypeChart.Multiplier(move.Element, t.t1, t.t2);
                dmg *= typeMult;
                if (move.Element != Element.None && (move.Element == user.t1 || move.Element == user.t2))
                    dmg *= user.trait == "attuned" ? 1.45f : 1.25f;
                if (physical && user.trait == "fierce") dmg *= 1.1f;
                if (!physical && user.trait == "gifted") dmg *= 1.1f;
                if (user.trait == "lastlight" && user.hp * 3 < user.maxHp) dmg *= 1.3f;
                if (physical && t.trait == "stalwart") dmg *= 0.9f;
                if (!physical && t.trait == "warded") dmg *= 0.9f;
                if (t.guarding) dmg *= 0.5f;

                int critChance = 6 + (user.trait == "keen" ? 10 : 0);
                bool crit = move.effect == "Crit" || rng.Next(100) < critChance;
                if (crit) dmg *= 1.5f;
                dmg *= 0.9f + (float)rng.NextDouble() * 0.1f;

                int amount = Mathf.Max(1, Mathf.RoundToInt(dmg));
                t.hp = Mathf.Max(0, t.hp - amount);
                t.flash = 0.3f;

                string note = "";
                if (typeMult >= 2f) note = " It strikes a weakness!";
                else if (typeMult <= 0.5f) note = " It is resisted.";
                if (crit) note += " A critical hit!";
                yield return Say(t.name + " takes " + amount + " damage." + note);

                if (move.effect == "Drain" && user.Alive)
                {
                    int heal = Mathf.Max(1, amount * move.effectPower / 100);
                    user.hp = Mathf.Min(user.maxHp, user.hp + heal);
                }
                if (physical && t.trait == "thornhide" && user.Alive)
                {
                    int back = Mathf.Max(1, amount / 10);
                    user.hp = Mathf.Max(0, user.hp - back);
                    user.flash = 0.2f;
                    yield return Say(t.name + "'s thorny hide wounds " + user.name + " for " + back + ".");
                }

                if (t.hp <= 0) yield return Fall(t);
                else yield return ApplyEffect(user, t, move, false);

                if (user.hp <= 0)
                {
                    yield return Fall(user);
                    yield break;
                }
            }
        }

        IEnumerator ApplyEffect(Combatant user, Combatant t, MoveData move, bool announceFailure)
        {
            string e = move.effect;
            if (string.IsNullOrEmpty(e) || e == "None" || e == "Drain" || e == "Crit") yield break;
            if (rng.Next(100) >= move.effectChance)
            {
                if (announceFailure) yield return Say("But it fails against " + t.name + ".");
                yield break;
            }

            switch (e)
            {
                case "Burn":
                    if (t.trait == "purity") { if (announceFailure) yield return Say(t.name + " cannot be burned."); break; }
                    if (t.burn == 0) { t.burn = 4; yield return Say(t.name + " is burned!"); }
                    break;
                case "Poison":
                    if (t.trait == "purity") { if (announceFailure) yield return Say(t.name + " cannot be poisoned."); break; }
                    if (t.poison == 0) { t.poison = 4; yield return Say(t.name + " is poisoned!"); }
                    break;
                case "Stun":
                    if (t.trait == "unshaken") { yield return Say(t.name + " shrugs off the stun."); break; }
                    if (!t.stunned) { t.stunned = true; yield return Say(t.name + " is stunned!"); }
                    break;
                case "Heal":
                case "Cleanse":
                    int heal = Mathf.Max(1, t.maxHp * move.effectPower / 100);
                    t.hp = Mathf.Min(t.maxHp, t.hp + heal);
                    if (e == "Cleanse")
                    {
                        t.burn = t.poison = 0;
                        t.stunned = false;
                    }
                    yield return Say(t.name + " recovers " + heal + " HP" + (e == "Cleanse" ? " and is cleansed." : "."));
                    break;
                default:
                    bool up = e.EndsWith("Up");
                    string statName = e.Substring(0, 3);
                    StatKind kind;
                    if (statName == "Atk") kind = StatKind.Atk;
                    else if (statName == "Def") kind = StatKind.Def;
                    else if (statName == "Mag") kind = StatKind.Mag;
                    else if (statName == "Res") kind = StatKind.Res;
                    else kind = StatKind.Spd;
                    int steps = Mathf.Max(1, move.effectPower);
                    if (t.ChangeStage(kind, up ? steps : -steps))
                        yield return Say(t.name + "'s " + StatLabel(kind) + (up ? " rises!" : " falls!"));
                    else if (announceFailure)
                        yield return Say(t.name + "'s " + StatLabel(kind) + " cannot change any further.");
                    break;
            }
        }

        static string StatLabel(StatKind k)
        {
            switch (k)
            {
                case StatKind.Atk: return "Attack";
                case StatKind.Def: return "Defense";
                case StatKind.Mag: return "Magic";
                case StatKind.Res: return "Resistance";
                default: return "Speed";
            }
        }

        // ------------------------------------------------------------------ rewards

        IEnumerator Rewards()
        {
            bool fortune = false;
            for (int i = 0; i < allies.Count; i++)
                if (allies[i].Alive && allies[i].trait == "fortune") fortune = true;
            if (fortune) goldPool = Mathf.RoundToInt(goldPool * 1.25f);
            if (boss) goldPool *= 4;
            state.gold += goldPool;
            yield return Say("Victory! Gained " + expPool + " exp and " + goldPool + " gold.");

            for (int i = 0; i < allies.Count; i++) allies[i].WriteBack();
            for (int i = 0; i < allies.Count; i++)
            {
                var a = allies[i];
                if (a.hp <= 0) continue;
                List<string> lines;
                if (a.isPlayer) lines = a.player.GainExp(expPool);
                else
                {
                    lines = a.creature.GainExp(expPool);
                    while (a.creature.CanEvolveByLevel)
                    {
                        var s = a.creature.Species;
                        lines.Add("What? " + a.creature.Name + " is changing...");
                        lines.AddRange(a.creature.Evolve());
                        if (a.sprite != null) a.sprite.sprite = SpriteFactory.Creature(a.creature.Species);
                        if (!state.caught.Contains(a.creature.speciesId)) state.caught.Add(a.creature.speciesId);
                        if (s.id == a.creature.speciesId) break;
                    }
                }
                // levelling changed max values; refresh the battle copy so WriteBack keeps the gains
                a.maxHp = a.isPlayer ? a.player.MaxHp : a.creature.MaxHp;
                a.maxMp = a.isPlayer ? a.player.MaxMp : a.creature.MaxMp;
                a.hp = a.isPlayer ? a.player.hp : a.creature.hp;
                a.mp = a.isPlayer ? a.player.mp : a.creature.mp;
                a.level = a.isPlayer ? a.player.level : a.creature.level;
                for (int j = 0; j < lines.Count; j++) yield return Say(lines[j]);
            }
        }

        // ------------------------------------------------------------------ drawing

        public void Draw()
        {
            // message log
            var banner = new Rect(UI.W * 0.14f, 16f, UI.W * 0.72f, 104f);
            UI.Box(banner, UI.PanelColor);
            for (int i = 0; i < log.Count; i++)
            {
                bool latest = i == log.Count - 1;
                UI.Label(new Rect(banner.x + 16, banner.y + 6 + i * 23f, banner.width - 32, 24), log[i], latest ? 18 : 16,
                    latest ? UI.TextColor : UI.DimColor);
            }

            // plates over the wild creatures
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (!e.Alive || e.view == null) continue;
                float height = 2.1f * e.view.transform.localScale.y;
                Vector3 p = UI.WorldToCanvas(cam, e.view.transform.position + Vector3.up * height);
                if (p.z < 0f) continue;
                var r = new Rect(p.x - 95f, p.y - 50f, 190f, 50f);
                UI.Box(r, UI.PanelColor);
                UI.Label(new Rect(r.x + 6, r.y + 2, r.width - 12, 20), e.name + "  Lv " + e.level, 14, UI.TextColor);
                UI.Bar(new Rect(r.x + 6, r.y + 22, r.width - 12, 9), (float)e.hp / e.maxHp, UI.HpColor((float)e.hp / e.maxHp));
                UI.Label(new Rect(r.x + 6, r.y + 32, r.width - 12, 18), TypeChart.Names(e.t1, e.t2) + Ailment(e), 12, TypeChart.ColorOf(e.t1));
            }

            // markers: whose turn it is, and which target is highlighted
            if (actor != null && actor.view != null && actor.Alive) Marker(actor, "v", UI.GoldColor);
            if (menuTargets != null && menuRows != null && menuSel < menuTargets.Count) Marker(menuTargets[menuSel], "V", Color.white);

            // team panel
            float rowH = 40f;
            var team = new Rect(20f, UI.H - 20f - rowH * allies.Count - 14f, UI.W * 0.46f, rowH * allies.Count + 14f);
            UI.Box(team, UI.PanelColor);
            for (int i = 0; i < allies.Count; i++)
            {
                var a = allies[i];
                float y = team.y + 8f + i * rowH;
                Color nameColor = a.hp <= 0 ? UI.DimColor : (a == actor ? UI.GoldColor : UI.TextColor);
                UI.Label(new Rect(team.x + 10, y, 210, 22), a.name + "  Lv " + a.level, 16, nameColor);
                UI.Label(new Rect(team.x + 10, y + 19, 210, 18), TypeChart.Names(a.t1, a.t2) + Ailment(a), 12, TypeChart.ColorOf(a.t1));
                float bx = team.x + 225f, bw = team.width - 235f;
                float hpFrac = (float)a.hp / a.maxHp;
                UI.Bar(new Rect(bx, y + 2, bw * 0.5f, 14), hpFrac, UI.HpColor(hpFrac));
                UI.Label(new Rect(bx, y + 1, bw * 0.5f, 16), "HP " + a.hp + "/" + a.maxHp, 12, Color.white, TextAnchor.UpperCenter);
                UI.Bar(new Rect(bx + bw * 0.52f, y + 2, bw * 0.48f, 14), a.maxMp > 0 ? (float)a.mp / a.maxMp : 0f, UI.MpColor);
                UI.Label(new Rect(bx + bw * 0.52f, y + 1, bw * 0.48f, 16), "MP " + a.mp + "/" + a.maxMp, 12, Color.white, TextAnchor.UpperCenter);
                for (int p = 0; p < a.maxAp; p++)
                    UI.Box(new Rect(bx + p * 16f, y + 21, 13, 9), p < a.ap ? UI.ApColor : new Color(0f, 0f, 0f, 0.6f));
                UI.Label(new Rect(bx + a.maxAp * 16f + 6, y + 17, 60, 16), "AP", 12, UI.ApColor);
            }

            // command menu
            if (menuRows != null)
            {
                const float RowH = 28f;
                int visible = 7;
                var panel = new Rect(UI.W * 0.5f + 6f, UI.H - 20f - 330f, UI.W * 0.5f - 26f, 330f);
                UI.Panel(panel);
                UI.Label(new Rect(panel.x + 14, panel.y + 8, panel.width - 28, 26), menuTitle, 18, UI.GoldColor);
                if (menuSel < menuScroll) menuScroll = menuSel;
                if (menuSel >= menuScroll + visible) menuScroll = menuSel - visible + 1;
                for (int i = menuScroll; i < menuRows.Count && i < menuScroll + visible; i++)
                {
                    var row = menuRows[i];
                    var r = new Rect(panel.x + 8, panel.y + 40 + (i - menuScroll) * RowH, panel.width - 16, RowH - 2);
                    if (i == menuSel) UI.Box(r, UI.SelectColor);
                    Color col = row.enabled ? row.color : UI.DimColor;
                    UI.Label(new Rect(r.x + 8, r.y + 3, r.width - 16, r.height), (i == menuSel ? "> " : "  ") + row.label, 17, col);
                    if (!string.IsNullOrEmpty(row.right))
                        UI.Label(new Rect(r.x + 8, r.y + 3, r.width - 16, r.height), row.right, 17, col, TextAnchor.UpperRight);
                }
                if (menuSel < menuRows.Count && !string.IsNullOrEmpty(menuRows[menuSel].desc))
                    UI.Label(new Rect(panel.x + 14, panel.y + 44 + visible * RowH, panel.width - 28, panel.height - 50 - visible * RowH),
                        menuRows[menuSel].desc, 15, UI.TextColor);
            }
        }

        static string Ailment(Combatant c)
        {
            string s = "";
            if (c.burn > 0) s += "  BURN";
            if (c.poison > 0) s += "  POISON";
            if (c.stunned) s += "  STUN";
            if (c.guarding) s += "  GUARD";
            return s;
        }

        void Marker(Combatant c, string glyph, Color color)
        {
            if (c == null || c.view == null) return;
            float height = (c.isAlly ? 2.25f : 2.9f) * c.view.transform.localScale.y;
            Vector3 p = UI.WorldToCanvas(cam, c.view.transform.position + Vector3.up * height);
            if (p.z < 0f) return;
            UI.Label(new Rect(p.x - 20f, p.y - 24f, 40f, 24f), glyph, 20, color, TextAnchor.UpperCenter);
        }
    }
}
