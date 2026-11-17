using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fable
{
    public enum Tile : byte { Void, Ground, Water, Tree, Rock, Wall, Lava, Pillar, Gate }
    public enum Biome { Meadow = 0, Forest = 1, Wetland = 2, Coast = 3, Mountain = 4, Snowfield = 5, Volcanic = 6, Ruins = 7, Highlands = 8, Sanctum = 9 }
    public enum InteractKind { Chest, Cache, RelicChest, Shrine, Gate, Altar }

    public class Interactable
    {
        public int id;
        public InteractKind kind;
        public Vector3 pos;
        public GameObject go;
        public FieldAbility ability;
        public int rank;
        public string itemId;
        public int speciesId;
        public float reach = 1.7f;
    }

    /// <summary>
    /// Builds the open world from a seed. The land is a great disc:
    /// a safe meadow in the middle, eight biomes fanning out around it, and three ring walls
    /// that divide it into bands of rising level. Each wall has eight gates, each needing a
    /// different field ability, so which creatures you carry decides which way you can go.
    /// The outermost band is the Sanctum, where the divine creatures wait.
    /// </summary>
    public class WorldMap
    {
        public const int N = 192;
        public const float Center = 96f;
        public const float EdgeRadius = 90f;
        public static readonly float[] RingRadius = { 30f, 52f, 72f };
        static readonly Biome[] SectorBiome =
        {
            Biome.Forest, Biome.Wetland, Biome.Coast, Biome.Highlands,
            Biome.Mountain, Biome.Snowfield, Biome.Volcanic, Biome.Ruins
        };
        static readonly FieldAbility[] GateAbilities =
        {
            FieldAbility.Surf, FieldAbility.Smash, FieldAbility.Cut, FieldAbility.Kindle, FieldAbility.Freeze,
            FieldAbility.Glide, FieldAbility.Lantern, FieldAbility.Phase, FieldAbility.Unseal
        };

        public readonly Tile[,] tiles = new Tile[N, N];
        public readonly byte[,] biome = new byte[N, N];
        public readonly int[,] gateId = new int[N, N];
        public readonly List<Interactable> interactables = new List<Interactable>();
        public GameObject root;

        System.Random rng;
        float ox, oz;
        readonly HashSet<int> used = new HashSet<int>();
        bool[,] reachGround, reachAll;

        public static string BiomeName(Biome b)
        {
            switch (b)
            {
                case Biome.Meadow: return "Hearthmeadow";
                case Biome.Forest: return "Whispering Wood";
                case Biome.Wetland: return "Kappa Fens";
                case Biome.Coast: return "Mermaid Coast";
                case Biome.Mountain: return "Tengu Crags";
                case Biome.Snowfield: return "Yuki Fields";
                case Biome.Volcanic: return "Ember Barrens";
                case Biome.Ruins: return "Hundred-Ghost Ruins";
                case Biome.Highlands: return "Thunder Highlands";
                default: return "Sanctum of the Gods";
            }
        }

        public static float Dist(float x, float z)
        {
            float dx = x - Center, dz = z - Center;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>0 inside the first wall, up to 3 in the Sanctum.</summary>
        public static int BandAt(float x, float z)
        {
            float d = Dist(x, z);
            int band = 0;
            for (int i = 0; i < RingRadius.Length; i++)
                if (d > RingRadius[i]) band = i + 1;
            return band;
        }

        public static int LevelAt(float x, float z)
        {
            return Mathf.Clamp(Mathf.RoundToInt(1f + Mathf.Max(0f, Dist(x, z) - 8f) * 0.68f), 1, 60);
        }

        public Biome BiomeAt(float x, float z)
        {
            int ix = Mathf.Clamp(Mathf.FloorToInt(x), 0, N - 1), iz = Mathf.Clamp(Mathf.FloorToInt(z), 0, N - 1);
            return (Biome)biome[ix, iz];
        }

        public Tile TileAt(int x, int z)
        {
            if (x < 0 || z < 0 || x >= N || z >= N) return Tile.Void;
            return tiles[x, z];
        }

        // ------------------------------------------------------------------ generation

        public void Generate(int seed)
        {
            rng = new System.Random(seed);
            ox = (float)rng.NextDouble() * 500f;
            oz = (float)rng.NextDouble() * 500f;

            for (int x = 0; x < N; x++)
            {
                for (int z = 0; z < N; z++)
                {
                    gateId[x, z] = -1;
                    float fx = x + 0.5f, fz = z + 0.5f;
                    float d = Dist(fx, fz);
                    if (d > EdgeRadius + 2f) { tiles[x, z] = Tile.Void; continue; }
                    float ang = Mathf.Atan2(fz - Center, fx - Center);
                    float a01 = (ang + Mathf.PI) / (2f * Mathf.PI);

                    Biome b;
                    if (d < 14f) b = Biome.Meadow;
                    else if (d > RingRadius[2]) b = Biome.Sanctum;
                    else
                    {
                        float wobble = (Mathf.PerlinNoise(fx * 0.03f + ox, fz * 0.03f + oz) - 0.5f) * 0.05f;
                        float a = Mathf.Repeat(a01 + wobble, 1f);
                        b = SectorBiome[Mathf.Clamp(Mathf.FloorToInt(a * 8f), 0, 7)];
                    }
                    biome[x, z] = (byte)b;

                    if (d > EdgeRadius) { tiles[x, z] = Tile.Wall; continue; }

                    bool onRing = false;
                    for (int i = 0; i < RingRadius.Length; i++)
                        if (Mathf.Abs(d - RingRadius[i]) < 1.1f) onRing = true;
                    if (onRing) { tiles[x, z] = Tile.Wall; continue; }

                    tiles[x, z] = Feature(b, fx, fz, d);
                }
            }

            PlaceGates();
            Clear(Center, Center, 5);
            PlaceFixed();
            Flood();
            PlaceLoot();
        }

        Tile Feature(Biome b, float fx, float fz, float d)
        {
            float n = Mathf.PerlinNoise(fx * 0.11f + oz, fz * 0.11f + ox);
            double r = rng.NextDouble();
            switch (b)
            {
                case Biome.Meadow: return (d > 5f && r < 0.02) ? Tile.Tree : Tile.Ground;
                case Biome.Forest: return (n > 0.62f || r < 0.06) ? Tile.Tree : Tile.Ground;
                case Biome.Wetland: return n > 0.58f ? Tile.Water : (r < 0.04 ? Tile.Tree : Tile.Ground);
                case Biome.Coast: return n > 0.60f ? Tile.Water : (r < 0.02 ? Tile.Rock : Tile.Ground);
                case Biome.Mountain: return (n > 0.63f || r < 0.05) ? Tile.Rock : Tile.Ground;
                case Biome.Snowfield: return n > 0.70f ? Tile.Rock : (r < 0.04 ? Tile.Tree : Tile.Ground);
                case Biome.Volcanic: return n > 0.62f ? Tile.Lava : (r < 0.04 ? Tile.Rock : Tile.Ground);
                case Biome.Ruins: return n > 0.72f ? Tile.Rock : (r < 0.07 ? Tile.Pillar : Tile.Ground);
                case Biome.Highlands: return n > 0.70f ? Tile.Rock : (r < 0.02 ? Tile.Tree : Tile.Ground);
                default: return r < 0.03 ? Tile.Pillar : Tile.Ground;
            }
        }

        void Clear(float cx, float cz, int radius)
        {
            int ix = Mathf.FloorToInt(cx), iz = Mathf.FloorToInt(cz);
            for (int x = ix - radius; x <= ix + radius; x++)
            {
                for (int z = iz - radius; z <= iz + radius; z++)
                {
                    if (x < 0 || z < 0 || x >= N || z >= N) continue;
                    Tile t = tiles[x, z];
                    if (t == Tile.Void || t == Tile.Wall || t == Tile.Gate) continue;
                    if ((x - ix) * (x - ix) + (z - iz) * (z - iz) > radius * radius) continue;
                    tiles[x, z] = Tile.Ground;
                }
            }
        }

        void PlaceGates()
        {
            for (int ring = 0; ring < RingRadius.Length; ring++)
            {
                float R = RingRadius[ring];
                for (int k = 0; k < 8; k++)
                {
                    float ang = -Mathf.PI + (k + 0.5f) * Mathf.PI / 4f;
                    int id = 1000 + ring * 8 + k;
                    float gx = Center + Mathf.Cos(ang) * R, gz = Center + Mathf.Sin(ang) * R;
                    Clear(gx, gz, 5);
                    for (int x = Mathf.FloorToInt(gx) - 4; x <= Mathf.FloorToInt(gx) + 4; x++)
                    {
                        for (int z = Mathf.FloorToInt(gz) - 4; z <= Mathf.FloorToInt(gz) + 4; z++)
                        {
                            if (x < 0 || z < 0 || x >= N || z >= N || tiles[x, z] != Tile.Wall) continue;
                            float fx = x + 0.5f, fz = z + 0.5f;
                            float a = Mathf.Atan2(fz - Center, fx - Center);
                            if (Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, ang * Mathf.Rad2Deg)) * Mathf.Deg2Rad * R < 1.6f)
                            {
                                tiles[x, z] = Tile.Gate;
                                gateId[x, z] = id;
                            }
                        }
                    }
                    interactables.Add(new Interactable
                    {
                        id = id, kind = InteractKind.Gate, pos = new Vector3(gx, 0f, gz),
                        ability = GateAbilities[(k + ring * 3) % GateAbilities.Length], rank = ring + 1, reach = 3.4f
                    });
                }
            }
        }

        void PlaceFixed()
        {
            // the village shrine, then one shrine per biome in each band, then one in the Sanctum
            AddAt(new Interactable { id = 2000, kind = InteractKind.Shrine }, Center + 2.5f, Center + 2.5f, true);
            float[] radii = { 22f, 41f, 62f };
            int n = 1;
            for (int band = 0; band < radii.Length; band++)
            {
                for (int k = 0; k < 8; k++)
                {
                    float ang = -Mathf.PI + (k + 0.22f) * Mathf.PI / 4f;
                    AddAt(new Interactable { id = 2000 + n, kind = InteractKind.Shrine },
                        Center + Mathf.Cos(ang) * radii[band], Center + Mathf.Sin(ang) * radii[band], true);
                    n++;
                }
            }
            AddAt(new Interactable { id = 2000 + n, kind = InteractKind.Shrine }, Center + 79f, Center + 4f, true);

            // ten altars, one for each divine creature
            int altar = 0;
            for (int i = 0; i < Database.Species.Length; i++)
            {
                if (!Database.Species[i].IsDivine) continue;
                float ang = altar * Mathf.PI * 2f / 10f + 0.3f;
                AddAt(new Interactable { id = 5000 + altar, kind = InteractKind.Altar, speciesId = i, reach = 2.2f },
                    Center + Mathf.Cos(ang) * 84f, Center + Mathf.Sin(ang) * 84f, true);
                altar++;
            }

            // six relic chests hold the items for hidden evolutions
            int relic = 0;
            for (int i = 0; i < Database.ItemList.Count; i++)
            {
                if (Database.ItemList[i].kind != ItemKind.Evolve) continue;
                float ang = relic * Mathf.PI * 2f / 6f + 0.85f;
                AddAt(new Interactable { id = 6000 + relic, kind = InteractKind.RelicChest, itemId = Database.ItemList[i].id },
                    Center + Mathf.Cos(ang) * 79f, Center + Mathf.Sin(ang) * 79f, true);
                relic++;
            }
        }

        void AddAt(Interactable it, float x, float z, bool clear)
        {
            if (clear) Clear(x, z, 2);
            int ix = Mathf.FloorToInt(x), iz = Mathf.FloorToInt(z);
            it.pos = new Vector3(ix + 0.5f, 0f, iz + 0.5f);
            used.Add(ix * N + iz);
            interactables.Add(it);
        }

        void Flood()
        {
            reachGround = FloodFrom(false);
            reachAll = FloodFrom(true);
        }

        bool[,] FloodFrom(bool liquids)
        {
            var seen = new bool[N, N];
            var queue = new Queue<int>();
            int c = (int)Center;
            seen[c, c] = true;
            queue.Enqueue(c * N + c);
            int[] dx = { 1, -1, 0, 0 }, dz = { 0, 0, 1, -1 };
            while (queue.Count > 0)
            {
                int v = queue.Dequeue();
                int x = v / N, z = v % N;
                for (int i = 0; i < 4; i++)
                {
                    int nx = x + dx[i], nz = z + dz[i];
                    if (nx < 0 || nz < 0 || nx >= N || nz >= N || seen[nx, nz]) continue;
                    Tile t = tiles[nx, nz];
                    bool ok = t == Tile.Ground || t == Tile.Gate || (liquids && (t == Tile.Water || t == Tile.Lava));
                    if (!ok) continue;
                    seen[nx, nz] = true;
                    queue.Enqueue(nx * N + nz);
                }
            }
            return seen;
        }

        void PlaceLoot()
        {
            int chests = 0, islands = 0, caches = 0, guard = 0;
            while ((chests < 80 || islands < 16 || caches < 30) && guard++ < 200000)
            {
                int x = rng.Next(N), z = rng.Next(N);
                if (tiles[x, z] != Tile.Ground || used.Contains(x * N + z)) continue;
                float d = Dist(x + 0.5f, z + 0.5f);
                if (d < 8f || d > EdgeRadius - 2f) continue;
                bool island = reachAll[x, z] && !reachGround[x, z];
                if (!reachAll[x, z]) continue;
                if (island)
                {
                    if (islands >= 16) continue;
                    // chests only a swimmer or a lava-walker can reach hold better loot
                    AddAt(new Interactable { id = 3500 + islands, kind = InteractKind.Chest, rank = 2 }, x + 0.5f, z + 0.5f, false);
                    islands++;
                }
                else if (chests < 80)
                {
                    AddAt(new Interactable { id = 3000 + chests, kind = InteractKind.Chest, rank = 1 }, x + 0.5f, z + 0.5f, false);
                    chests++;
                }
                else if (caches < 30)
                {
                    AddAt(new Interactable
                    {
                        id = 4000 + caches, kind = InteractKind.Cache, ability = FieldAbility.Track,
                        rank = Mathf.Clamp(BandAt(x + 0.5f, z + 0.5f) + 1, 1, 3)
                    }, x + 0.5f, z + 0.5f, false);
                    caches++;
                }
            }
        }

        // ------------------------------------------------------------------ wild creatures

        /// <summary>Chooses a wild species for a biome and level, evolving it to match stronger regions.</summary>
        public static int PickWild(Biome b, int level, int band, System.Random r)
        {
            var all = Database.Species;
            // divine creatures: a faint chance in the outer bands, a real one in the Sanctum
            double divineChance = b == Biome.Sanctum ? 0.02 : (band >= 2 ? 0.002 : 0.0);
            if (r.NextDouble() < divineChance)
            {
                var gods = new List<int>();
                for (int i = 0; i < all.Length; i++)
                    if (all[i].IsDivine) gods.Add(i);
                if (gods.Count > 0) return gods[r.Next(gods.Count)];
            }

            var pool = new List<int>();
            var weights = new List<int>();
            int total = 0;
            for (int i = 0; i < all.Length; i++)
            {
                var s = all[i];
                if (s.stage != 0 || s.IsDivine) continue;
                // the Sanctum draws from every biome
                if (b != Biome.Sanctum && !s.LivesIn((int)b)) continue;
                int w = s.rarity == 0 ? 10 : 4;
                pool.Add(i);
                weights.Add(w);
                total += w;
            }
            if (pool.Count == 0) return 0;
            int roll = r.Next(total), pick = pool[0];
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= weights[i];
                if (roll < 0) { pick = pool[i]; break; }
            }
            // older regions hold evolved forms; hidden evolutions never appear in the wild
            var sp = all[pick];
            while (sp.evolvesTo >= 0 && string.IsNullOrEmpty(sp.evolveItem) && level >= sp.evolveLevel + 3 && r.NextDouble() < 0.7)
            {
                pick = sp.evolvesTo;
                sp = all[pick];
            }
            return pick;
        }

        // ------------------------------------------------------------------ mesh

        static float HeightOf(Tile t)
        {
            switch (t)
            {
                case Tile.Water: return -0.18f;
                case Tile.Lava: return -0.08f;
                case Tile.Tree: return 1.5f;
                case Tile.Rock: return 0.9f;
                case Tile.Wall: return 2.2f;
                case Tile.Pillar: return 1.9f;
                default: return 0f;
            }
        }

        static Color GroundColor(Biome b)
        {
            switch (b)
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

        static Color TileColor(Tile t, Biome b, int x, int z)
        {
            int hash = ((x * 73856093) ^ (z * 19349663)) & 15;
            float v = hash / 16f * 0.06f - 0.03f;
            Color c;
            switch (t)
            {
                case Tile.Water: c = new Color(0.20f, 0.46f, 0.80f); break;
                case Tile.Lava: c = new Color(0.95f, 0.42f, 0.12f); break;
                case Tile.Tree: c = b == Biome.Snowfield ? new Color(0.70f, 0.84f, 0.80f) : new Color(0.14f, 0.40f, 0.18f); break;
                case Tile.Rock: c = new Color(0.44f, 0.42f, 0.42f); break;
                case Tile.Wall: c = new Color(0.24f, 0.22f, 0.28f); break;
                case Tile.Pillar: c = new Color(0.80f, 0.78f, 0.72f); break;
                case Tile.Gate: c = GroundColor(b) * 0.7f; break;
                default: c = GroundColor(b); break;
            }
            return new Color(Mathf.Clamp01(c.r + v), Mathf.Clamp01(c.g + v), Mathf.Clamp01(c.b + v), 1f);
        }

        public void BuildMesh()
        {
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            for (int x = 0; x < N; x++)
            {
                for (int z = 0; z < N; z++)
                {
                    Tile t = tiles[x, z];
                    if (t == Tile.Void) continue;
                    float h = HeightOf(t);
                    Color c = TileColor(t, (Biome)biome[x, z], x, z);
                    Quad(verts, cols, tris, new Vector3(x, h, z), new Vector3(x, h, z + 1), new Vector3(x + 1, h, z + 1), new Vector3(x + 1, h, z), c);
                    if (h <= 0f) continue;
                    Color south = c * 0.72f, side = c * 0.55f;
                    south.a = 1f;
                    side.a = 1f;
                    // only the faces the camera can see: south, west and east
                    if (HeightOf(TileAt(x, z - 1)) < h)
                        Quad(verts, cols, tris, new Vector3(x, 0, z), new Vector3(x, h, z), new Vector3(x + 1, h, z), new Vector3(x + 1, 0, z), south);
                    if (HeightOf(TileAt(x - 1, z)) < h)
                        Quad(verts, cols, tris, new Vector3(x, 0, z + 1), new Vector3(x, h, z + 1), new Vector3(x, h, z), new Vector3(x, 0, z), side);
                    if (HeightOf(TileAt(x + 1, z)) < h)
                        Quad(verts, cols, tris, new Vector3(x + 1, 0, z), new Vector3(x + 1, h, z), new Vector3(x + 1, h, z + 1), new Vector3(x + 1, 0, z + 1), side);
                }
            }

            var mesh = new Mesh();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            root = new GameObject("World");
            var ground = new GameObject("Terrain");
            ground.transform.SetParent(root.transform, false);
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            var shader = Resources.Load<Shader>("VertexColor");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            ground.AddComponent<MeshRenderer>().sharedMaterial = new Material(shader);
        }

        static void Quad(List<Vector3> v, List<Color> c, List<int> t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color color)
        {
            int i = v.Count;
            v.Add(p0); v.Add(p1); v.Add(p2); v.Add(p3);
            c.Add(color); c.Add(color); c.Add(color); c.Add(color);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }
    }
}
