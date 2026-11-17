using System.Collections.Generic;
using UnityEngine;

namespace Fable
{
    class Roamer
    {
        public GameObject go;
        public int speciesId;
        public int level;
        public Vector3 pos;
        public Vector3 target;
        public float retarget;
        public bool aggressive;
    }

    /// <summary>The overworld at runtime: the player, wandering wild creatures and everything you can interact with.</summary>
    public class World
    {
        public readonly WorldMap map = new WorldMap();
        public Vector3 playerPos;
        public Interactable nearby;

        readonly GameState state;
        readonly Camera cam;
        readonly List<Roamer> roamers = new List<Roamer>();
        readonly HashSet<int> opened = new HashSet<int>();
        readonly System.Random rng = new System.Random();
        GameObject playerGo;
        SpriteRenderer playerSprite;
        Element playerSpriteType = Element.None;
        float spawnTimer, grace, abilityTimer, walkTime;
        bool canSurf, canFreeze;
        int trackRank;

        const float WalkSpeed = 5f, RunSpeed = 8f, Radius = 0.3f;
        const int MaxRoamers = 9;

        public World(GameState state, Camera cam)
        {
            this.state = state;
            this.cam = cam;
            map.Generate(state.seed);
            map.BuildMesh();
            for (int i = 0; i < state.opened.Count; i++) opened.Add(state.opened[i]);
            foreach (var it in map.interactables) BuildVisual(it);

            playerGo = new GameObject("Player");
            playerGo.transform.SetParent(map.root.transform, false);
            playerSprite = playerGo.AddComponent<SpriteRenderer>();
            playerGo.AddComponent<Billboard>();
            playerPos = new Vector3(state.px, 0f, state.pz);
            if (!Free(playerPos)) playerPos = new Vector3(WorldMap.Center + 0.5f, 0f, WorldMap.Center + 0.5f);
            RefreshAbilities();
            grace = 2f;
        }

        public void Destroy()
        {
            if (map.root != null) Object.Destroy(map.root);
        }

        // ------------------------------------------------------------------ visuals

        static GameObject Prim(PrimitiveType type, Vector3 pos, Vector3 scale, Color color, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (r != null) r.material.color = color;
            return go;
        }

        static Element ElementFor(FieldAbility a)
        {
            switch (a)
            {
                case FieldAbility.Surf: return Element.Tide;
                case FieldAbility.Smash: return Element.Stone;
                case FieldAbility.Cut: return Element.Grove;
                case FieldAbility.Kindle: return Element.Ember;
                case FieldAbility.Freeze: return Element.Frost;
                case FieldAbility.Glide: return Element.Gale;
                case FieldAbility.Lantern: return Element.Radiance;
                case FieldAbility.Phase: return Element.Spirit;
                case FieldAbility.Track: return Element.Feral;
                default: return Element.Arcane;
            }
        }

        void BuildVisual(Interactable it)
        {
            bool done = opened.Contains(it.id);
            Transform parent = map.root.transform;
            switch (it.kind)
            {
                case InteractKind.Chest:
                    if (!done)
                        it.go = Prim(PrimitiveType.Cube, it.pos + Vector3.up * 0.3f, new Vector3(0.7f, 0.6f, 0.5f),
                            it.rank > 1 ? new Color(0.30f, 0.60f, 0.90f) : new Color(0.72f, 0.48f, 0.20f), parent);
                    break;
                case InteractKind.Cache:
                    if (!done)
                    {
                        it.go = Prim(PrimitiveType.Sphere, it.pos + Vector3.up * 0.3f, Vector3.one * 0.55f, new Color(0.80f, 0.50f, 0.95f), parent);
                        it.go.SetActive(false);
                    }
                    break;
                case InteractKind.RelicChest:
                    if (!done)
                        it.go = Prim(PrimitiveType.Cube, it.pos + Vector3.up * 0.45f, new Vector3(0.9f, 0.9f, 0.7f), new Color(1f, 0.85f, 0.30f), parent);
                    break;
                case InteractKind.Shrine:
                    it.go = Prim(PrimitiveType.Cylinder, it.pos + Vector3.up * 0.9f, new Vector3(0.5f, 0.9f, 0.5f), new Color(0.85f, 0.20f, 0.18f), parent);
                    Prim(PrimitiveType.Cube, it.pos + Vector3.up * 1.95f, new Vector3(1.9f, 0.25f, 0.7f), new Color(0.85f, 0.20f, 0.18f), it.go.transform.parent);
                    break;
                case InteractKind.Gate:
                    if (!done)
                    {
                        Vector3 radial = new Vector3(it.pos.x - WorldMap.Center, 0f, it.pos.z - WorldMap.Center).normalized;
                        it.go = Prim(PrimitiveType.Cube, it.pos + Vector3.up * 1.2f, new Vector3(3.8f, 2.4f, 2.6f),
                            TypeChart.ColorOf(ElementFor(it.ability)), parent);
                        it.go.transform.rotation = Quaternion.LookRotation(radial);
                    }
                    break;
                case InteractKind.Altar:
                    it.go = Prim(PrimitiveType.Cylinder, it.pos + Vector3.up * 0.15f, new Vector3(1.8f, 0.15f, 1.8f),
                        done ? new Color(0.5f, 0.5f, 0.5f) : new Color(1f, 0.95f, 0.75f), parent);
                    if (!done)
                    {
                        var s = Database.GetSpecies(it.speciesId);
                        var orb = Prim(PrimitiveType.Sphere, it.pos + Vector3.up * 1.1f, Vector3.one * 0.6f, TypeChart.ColorOf(s.Type1), parent);
                        orb.transform.SetParent(it.go.transform, true);
                    }
                    break;
            }
        }

        void MarkOpened(Interactable it, bool removeVisual)
        {
            opened.Add(it.id);
            if (!state.opened.Contains(it.id)) state.opened.Add(it.id);
            if (removeVisual && it.go != null)
            {
                Object.Destroy(it.go);
                it.go = null;
            }
        }

        // ------------------------------------------------------------------ movement

        bool Walkable(int x, int z)
        {
            switch (map.TileAt(x, z))
            {
                case Tile.Ground: return true;
                case Tile.Water: return canSurf;
                case Tile.Lava: return canFreeze;
                case Tile.Gate: return opened.Contains(map.gateId[x, z]);
                default: return false;
            }
        }

        bool Free(Vector3 p)
        {
            return Walkable(Mathf.FloorToInt(p.x - Radius), Mathf.FloorToInt(p.z - Radius))
                && Walkable(Mathf.FloorToInt(p.x + Radius), Mathf.FloorToInt(p.z - Radius))
                && Walkable(Mathf.FloorToInt(p.x - Radius), Mathf.FloorToInt(p.z + Radius))
                && Walkable(Mathf.FloorToInt(p.x + Radius), Mathf.FloorToInt(p.z + Radius));
        }

        public void RefreshAbilities()
        {
            canSurf = state.FieldRank(FieldAbility.Surf) >= 1;
            canFreeze = state.FieldRank(FieldAbility.Freeze) >= 1;
            trackRank = state.FieldRank(FieldAbility.Track);
            foreach (var it in map.interactables)
                if (it.kind == InteractKind.Cache && it.go != null)
                    it.go.SetActive(trackRank >= it.rank);
        }

        public void Update(float dt)
        {
            abilityTimer -= dt;
            if (abilityTimer <= 0f)
            {
                abilityTimer = 0.5f;
                RefreshAbilities();
            }
            if (grace > 0f) grace -= dt;

            Vector2 mv = GameInput.Move;
            float speed = GameInput.RunHeld ? RunSpeed : WalkSpeed;
            if (mv.sqrMagnitude > 0.01f)
            {
                Vector3 stepX = new Vector3(mv.x * speed * dt, 0f, 0f);
                Vector3 stepZ = new Vector3(0f, 0f, mv.y * speed * dt);
                // if the ground under you stopped being walkable (your swimmer left the team), let you walk out
                bool stuck = !Free(playerPos);
                if (stuck || Free(playerPos + stepX)) playerPos += stepX;
                if (stuck || Free(playerPos + stepZ)) playerPos += stepZ;
                walkTime += dt * (GameInput.RunHeld ? 14f : 10f);
                if (Mathf.Abs(mv.x) > 0.1f) playerSprite.flipX = mv.x < 0f;
            }
            else walkTime = 0f;

            state.px = playerPos.x;
            state.pz = playerPos.z;

            var p = state.player;
            if (playerSpriteType != p.Type1 || playerSprite.sprite == null)
            {
                playerSpriteType = p.Type1;
                playerSprite.sprite = SpriteFactory.Player(p.Type1);
            }
            float bob = Mathf.Abs(Mathf.Sin(walkTime)) * 0.12f;
            playerGo.transform.position = playerPos + Vector3.up * bob;

            UpdateRoamers(dt);
            FindNearby();
            if (GameInput.InteractDown && nearby != null)
            {
                GameInput.Consume();
                Interact(nearby);
            }
        }

        public void UpdateCamera()
        {
            Vector3 target = playerPos + Vector3.up * 0.8f;
            float zoom = Mathf.Clamp(Settings.CameraZoom, 0.6f, 1.8f);
            cam.transform.position = target + new Vector3(0f, 9.5f, -9f) * zoom;
            cam.transform.LookAt(target);
        }

        // ------------------------------------------------------------------ wild creatures

        void UpdateRoamers(float dt)
        {
            spawnTimer -= dt;
            if (spawnTimer <= 0f && roamers.Count < MaxRoamers)
            {
                spawnTimer = 0.35f;
                TrySpawn();
            }

            for (int i = roamers.Count - 1; i >= 0; i--)
            {
                var r = roamers[i];
                float dist = Vector3.Distance(r.pos, playerPos);
                if (dist > 26f)
                {
                    Object.Destroy(r.go);
                    roamers.RemoveAt(i);
                    continue;
                }
                bool chasing = r.aggressive && grace <= 0f && dist < 5.5f;
                r.retarget -= dt;
                if (chasing) r.target = playerPos;
                else if (r.retarget <= 0f)
                {
                    r.retarget = 1.5f + (float)rng.NextDouble() * 3f;
                    r.target = r.pos + new Vector3((float)rng.NextDouble() * 8f - 4f, 0f, (float)rng.NextDouble() * 8f - 4f);
                }
                Vector3 to = r.target - r.pos;
                to.y = 0f;
                if (to.magnitude > 0.2f)
                {
                    Vector3 next = r.pos + to.normalized * (chasing ? 3.1f : 1.3f) * dt;
                    if (map.TileAt(Mathf.FloorToInt(next.x), Mathf.FloorToInt(next.z)) == Tile.Ground
                        && WorldMap.Dist(next.x, next.z) > 7f)
                    {
                        r.pos = next;
                        var sr = r.go.GetComponent<SpriteRenderer>();
                        if (sr != null && Mathf.Abs(to.x) > 0.05f) sr.flipX = to.x < 0f;
                    }
                    else r.retarget = 0f;
                }
                r.go.transform.position = r.pos + Vector3.up * (Mathf.Abs(Mathf.Sin(Time.time * 4f + i)) * 0.08f);

                if (grace <= 0f && dist < 0.85f)
                {
                    roamers.RemoveAt(i);
                    Object.Destroy(r.go);
                    grace = 2.5f;
                    BeginWildBattle(r.speciesId, r.level, false, null);
                    return;
                }
            }
        }

        void TrySpawn()
        {
            double ang = rng.NextDouble() * Mathf.PI * 2f;
            float dist = 9f + (float)rng.NextDouble() * 8f;
            Vector3 p = playerPos + new Vector3(Mathf.Cos((float)ang) * dist, 0f, Mathf.Sin((float)ang) * dist);
            int x = Mathf.FloorToInt(p.x), z = Mathf.FloorToInt(p.z);
            if (map.TileAt(x, z) != Tile.Ground) return;
            if (WorldMap.Dist(p.x, p.z) < 9f) return; // the village green stays peaceful
            Biome b = map.BiomeAt(p.x, p.z);
            int level = Mathf.Clamp(WorldMap.LevelAt(p.x, p.z) + rng.Next(-1, 3), 1, 62);
            int species = WorldMap.PickWild(b, level, WorldMap.BandAt(p.x, p.z), rng);
            var s = Database.GetSpecies(species);
            if (s.IsDivine) level = Mathf.Max(level, 55);

            var go = new GameObject("Wild " + s.name);
            go.transform.SetParent(map.root.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Creature(s);
            go.AddComponent<Billboard>();
            float scale = 0.55f + s.stage * 0.12f + (s.IsDivine ? 0.35f : 0f);
            go.transform.localScale = Vector3.one * scale;
            go.transform.position = p;
            roamers.Add(new Roamer
            {
                go = go, speciesId = species, level = level, pos = p, target = p,
                aggressive = rng.NextDouble() < 0.6
            });
        }

        public void ClearRoamers()
        {
            for (int i = 0; i < roamers.Count; i++) Object.Destroy(roamers[i].go);
            roamers.Clear();
            grace = 2.5f;
        }

        void BeginWildBattle(int speciesId, int level, bool boss, System.Action<BattleResult> onEnd)
        {
            var enemies = new List<CreatureInstance>();
            enemies.Add(CreatureInstance.Create(speciesId, level, rng));
            Biome b = map.BiomeAt(playerPos.x, playerPos.z);
            if (!boss && !Database.GetSpecies(speciesId).IsDivine)
            {
                int extra = rng.Next(0, Mathf.Min(3, 1 + state.party.Count));
                for (int i = 0; i < extra; i++)
                {
                    int lv = Mathf.Max(1, level + rng.Next(-2, 2));
                    int sp = WorldMap.PickWild(b, lv, 0, rng);
                    enemies.Add(CreatureInstance.Create(sp, lv, rng));
                }
            }
            Game.I.StartBattle(enemies, b, boss, onEnd);
        }

        // ------------------------------------------------------------------ interaction

        void FindNearby()
        {
            nearby = null;
            float best = float.MaxValue;
            foreach (var it in map.interactables)
            {
                if (it.kind != InteractKind.Shrine && it.kind != InteractKind.Altar && opened.Contains(it.id)) continue;
                if (it.kind == InteractKind.Cache && trackRank < it.rank) continue;
                float dx = it.pos.x - playerPos.x, dz = it.pos.z - playerPos.z;
                float d = dx * dx + dz * dz;
                if (d < it.reach * it.reach && d < best)
                {
                    best = d;
                    nearby = it;
                }
            }
        }

        public string PromptFor(Interactable it)
        {
            switch (it.kind)
            {
                case InteractKind.Chest: return "Open chest";
                case InteractKind.Cache: return "Dig up hidden cache";
                case InteractKind.RelicChest: return "Open relic chest";
                case InteractKind.Shrine: return "Pray at shrine";
                case InteractKind.Gate: return "Examine the blocked pass (" + it.ability + ")";
                default: return opened.Contains(it.id) ? "Examine the silent altar" : "Approach the altar of " + Database.GetSpecies(it.speciesId).name;
            }
        }

        static string GateText(FieldAbility a)
        {
            switch (a)
            {
                case FieldAbility.Surf: return "A deep, fast channel floods the pass.";
                case FieldAbility.Smash: return "A boulder the size of a house blocks the pass.";
                case FieldAbility.Cut: return "Iron-hard brambles choke the pass.";
                case FieldAbility.Kindle: return "A wall of ancient ice seals the pass.";
                case FieldAbility.Freeze: return "A river of magma crosses the pass.";
                case FieldAbility.Glide: return "The path ends at a sheer chasm.";
                case FieldAbility.Lantern: return "The tunnel ahead is pitch black, and something in it is breathing.";
                case FieldAbility.Phase: return "A spirit barrier hums across the pass.";
                default: return "A rune-locked door bars the pass.";
            }
        }

        static int LevelForRank(int rank) { return rank >= 3 ? 42 : (rank == 2 ? 25 : 8); }

        string Helper(FieldAbility a)
        {
            for (int i = 0; i < state.party.Count; i++)
                if (state.party[i].FieldRank(a) > 0) return state.party[i].Name;
            return "Your creature";
        }

        void Interact(Interactable it)
        {
            switch (it.kind)
            {
                case InteractKind.Chest:
                case InteractKind.Cache:
                    MarkOpened(it, true);
                    Game.I.Say(Loot(it));
                    break;
                case InteractKind.RelicChest:
                    MarkOpened(it, true);
                    state.AddItem(it.itemId, 1);
                    Game.I.Say("Inside the relic chest lies the " + Database.Items[it.itemId].name + "!",
                        Database.Items[it.itemId].desc + " Use it from the Inventory.");
                    break;
                case InteractKind.Shrine:
                    state.respawnX = playerPos.x;
                    state.respawnZ = playerPos.z;
                    state.HealAll();
                    Game.I.OpenShrine();
                    break;
                case InteractKind.Gate:
                    if (state.FieldRank(it.ability) >= it.rank)
                    {
                        string who = Helper(it.ability);
                        MarkOpened(it, true);
                        Game.I.Say(GateText(it.ability), who + " uses " + it.ability + ". The way is open!");
                    }
                    else
                    {
                        Game.I.Say(GateText(it.ability),
                            "You need " + it.ability + " rank " + it.rank + ": a " + TypeChart.AbilityTypes(it.ability)
                            + " type creature of level " + LevelForRank(it.rank) + " or higher on your team.",
                            "Other passes through this wall need other abilities.");
                    }
                    break;
                case InteractKind.Altar:
                    if (opened.Contains(it.id))
                    {
                        Game.I.Say("The altar is silent. Its god has already answered you.");
                        break;
                    }
                    var s = Database.GetSpecies(it.speciesId);
                    Game.I.Say(new List<string>
                    {
                        "The air above the altar splits open.",
                        s.name + ", one of the ten divine, descends to test you!"
                    }, () => BeginWildBattle(it.speciesId, 60, true, result =>
                    {
                        if (result != BattleResult.Victory) return;
                        MarkOpened(it, false);
                        if (it.go != null) Object.Destroy(it.go);
                        it.go = null;
                        BuildVisual(it);
                    }));
                    break;
            }
        }

        string[] Loot(Interactable it)
        {
            var r = new System.Random(it.id * 31 + state.seed);
            int tier = WorldMap.BandAt(it.pos.x, it.pos.z) + (it.rank - 1) + (it.kind == InteractKind.Cache ? 1 : 0);
            int gold = (30 + r.Next(40)) * (1 + tier);
            string item;
            int count;
            int roll = r.Next(4);
            if (tier <= 0) { item = roll < 2 ? "potion" : "talisman"; count = 2; }
            else if (tier == 1) { item = roll == 0 ? "ether" : (roll == 1 ? "salt" : (roll == 2 ? "potion" : "talisman")); count = 3; }
            else if (tier == 2) { item = roll == 0 ? "hi_potion" : (roll == 1 ? "revive" : (roll == 2 ? "silver_talisman" : "ether")); count = 2; }
            else { item = roll == 0 ? "gold_talisman" : (roll == 1 ? "revive" : (roll == 2 ? "hi_potion" : "silver_talisman")); count = roll == 0 ? 1 : 3; }
            state.gold += gold;
            state.AddItem(item, count);
            return new[] { "Found " + gold + " gold and " + count + " x " + Database.Items[item].name + "!" };
        }

        public void Respawn()
        {
            playerPos = new Vector3(state.respawnX, 0f, state.respawnZ);
            if (!Free(playerPos)) playerPos = new Vector3(WorldMap.Center + 0.5f, 0f, WorldMap.Center + 0.5f);
            ClearRoamers();
        }
    }
}
