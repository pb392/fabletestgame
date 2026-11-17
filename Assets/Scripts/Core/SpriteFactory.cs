using System.Collections.Generic;
using UnityEngine;

namespace Fable
{
    /// <summary>
    /// Draws placeholder pixel art at runtime so the game is playable with no art assets.
    /// Every species gets a stable, unique silhouette tinted by its types. To use real art,
    /// drop a sprite at Resources/Creatures/{species name} and it is picked up instead.
    /// </summary>
    public static class SpriteFactory
    {
        const int Size = 32;
        static readonly Dictionary<int, Sprite> creatures = new Dictionary<int, Sprite>();
        static readonly Dictionary<int, Sprite> players = new Dictionary<int, Sprite>();
        static readonly Dictionary<Color, Texture2D> solids = new Dictionary<Color, Texture2D>();

        public static Texture2D Solid(Color c)
        {
            Texture2D t;
            if (solids.TryGetValue(c, out t) && t != null) return t;
            t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, c);
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            solids[c] = t;
            return t;
        }

        public static Sprite Creature(SpeciesData s)
        {
            Sprite sprite;
            if (creatures.TryGetValue(s.id, out sprite) && sprite != null) return sprite;
            sprite = Resources.Load<Sprite>("Creatures/" + s.name);
            if (sprite == null) sprite = DrawCreature(s);
            creatures[s.id] = sprite;
            return sprite;
        }

        static Sprite DrawCreature(SpeciesData s)
        {
            var rng = new System.Random(s.id * 7919 + 13);
            var kind = new int[Size, Size]; // 0 empty, 1 body, 2 accent
            int cx = Size / 2;

            int bodyY = 9 + rng.Next(4);
            int bodyRx = Mathf.Min(13, 6 + rng.Next(4) + s.stage);
            int bodyRy = 6 + rng.Next(3) + s.stage;
            Ellipse(kind, cx, bodyY, bodyRx, bodyRy, 1);

            int headRx = 4 + rng.Next(3);
            int headRy = 4 + rng.Next(3);
            int headY = Mathf.Min(Size - headRy - 2, bodyY + bodyRy + rng.Next(3) - 1);
            Ellipse(kind, cx, headY, headRx, headRy, 1);

            // mirrored extras: ears, wings, tails, horns
            int extras = 2 + rng.Next(3) + s.stage;
            for (int i = 0; i < extras; i++)
            {
                int ox = 3 + rng.Next(10);
                int oy = 3 + rng.Next(Size - 8);
                int rx = 1 + rng.Next(4);
                int ry = 2 + rng.Next(5);
                int k = rng.Next(3) == 0 ? 1 : 2;
                Ellipse(kind, cx + ox, oy, rx, ry, k);
                Ellipse(kind, cx - ox, oy, rx, ry, k);
            }
            // belly patch
            Ellipse(kind, cx, bodyY - 1, Mathf.Max(2, bodyRx - 3), Mathf.Max(2, bodyRy - 3), 2, true);

            Color main = TypeChart.ColorOf(s.Type1);
            Color accent = s.Type2 != Element.None ? TypeChart.ColorOf(s.Type2) : Color.Lerp(main, Color.white, 0.45f);
            main = Color.Lerp(main, new Color((float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble()), 0.15f);
            Color outline = Color.Lerp(main, Color.black, 0.75f);
            outline.a = 1f;

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Color c = new Color(0, 0, 0, 0);
                    if (kind[x, y] != 0)
                    {
                        c = kind[x, y] == 1 ? main : accent;
                        float shade = 0.78f + 0.30f * y / Size;
                        c = new Color(c.r * shade, c.g * shade, c.b * shade, 1f);
                    }
                    else if (Near(kind, x, y))
                    {
                        c = outline;
                    }
                    px[y * Size + x] = c;
                }
            }
            // eyes
            int eyeDx = Mathf.Max(1, headRx / 2);
            Color pupil = new Color(0.08f, 0.06f, 0.1f, 1f);
            SetPx(px, cx - eyeDx, headY, Color.white);
            SetPx(px, cx + eyeDx - 1, headY, Color.white);
            SetPx(px, cx - eyeDx, headY + 1, Color.white);
            SetPx(px, cx + eyeDx - 1, headY + 1, Color.white);
            SetPx(px, cx - eyeDx, headY, pupil);
            SetPx(px, cx + eyeDx - 1, headY, pupil);
            if (s.IsDivine || s.hiddenForm)
            {
                // a small halo marks the rarest creatures
                Color gold = new Color(1f, 0.9f, 0.4f, 1f);
                for (int x = cx - 3; x <= cx + 2; x++) SetPx(px, x, Size - 1, gold);
            }
            tex.SetPixels(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.05f), 16f);
            sprite.name = s.name;
            return sprite;
        }

        public static Sprite Player(Element type)
        {
            Sprite sprite;
            if (players.TryGetValue((int)type, out sprite) && sprite != null) return sprite;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[Size * Size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 0);
            Color robe = TypeChart.ColorOf(type);
            Color dark = Color.Lerp(robe, Color.black, 0.6f);
            dark.a = 1f;
            Color skin = new Color(0.96f, 0.80f, 0.66f, 1f);
            Color hair = new Color(0.22f, 0.16f, 0.14f, 1f);
            Fill(px, 11, 1, 14, 8, dark);    // left leg
            Fill(px, 17, 1, 20, 8, dark);    // right leg
            Fill(px, 9, 8, 22, 21, robe);    // robe
            Fill(px, 9, 8, 22, 9, dark);     // hem
            Fill(px, 6, 11, 8, 20, robe);    // left arm
            Fill(px, 23, 11, 25, 20, robe);  // right arm
            Fill(px, 6, 10, 8, 11, skin);
            Fill(px, 23, 10, 25, 11, skin);
            Fill(px, 14, 13, 17, 16, Color.Lerp(robe, Color.white, 0.6f)); // crest
            Fill(px, 11, 21, 20, 29, skin);  // head
            Fill(px, 10, 27, 21, 30, hair);  // hair
            Fill(px, 10, 23, 11, 27, hair);
            Fill(px, 20, 23, 21, 27, hair);
            SetPx(px, 13, 24, dark);
            SetPx(px, 18, 24, dark);
            tex.SetPixels(px);
            tex.Apply();
            sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.03f), 16f);
            players[(int)type] = sprite;
            return sprite;
        }

        static void Ellipse(int[,] grid, int cx, int cy, int rx, int ry, int value, bool onlyOverBody = false)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
            {
                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    if (x < 1 || y < 1 || x >= Size - 1 || y >= Size - 1) continue;
                    float dx = (x - cx + 0.5f) / (rx + 0.5f);
                    float dy = (y - cy) / (ry + 0.5f);
                    if (dx * dx + dy * dy > 1f) continue;
                    if (onlyOverBody && grid[x, y] == 0) continue;
                    grid[x, y] = value;
                }
            }
        }

        static bool Near(int[,] grid, int x, int y)
        {
            if (x > 0 && grid[x - 1, y] != 0) return true;
            if (x < Size - 1 && grid[x + 1, y] != 0) return true;
            if (y > 0 && grid[x, y - 1] != 0) return true;
            if (y < Size - 1 && grid[x, y + 1] != 0) return true;
            return false;
        }

        static void SetPx(Color[] px, int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size) return;
            px[y * Size + x] = c;
        }

        static void Fill(Color[] px, int x0, int y0, int x1, int y1, Color c)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    SetPx(px, x, y, c);
        }
    }
}
