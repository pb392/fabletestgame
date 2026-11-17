using UnityEngine;

namespace Fable
{
    /// <summary>The twelve types. Integer values match the "t1"/"t2"/"type" fields in the JSON data.</summary>
    public enum Element
    {
        None = -1,
        Ember = 0, Tide = 1, Grove = 2, Gale = 3, Stone = 4, Frost = 5,
        Storm = 6, Spirit = 7, Shade = 8, Radiance = 9, Feral = 10, Arcane = 11
    }

    /// <summary>Things a creature can do outside battle. Each type grants one.</summary>
    public enum FieldAbility { Surf, Smash, Cut, Kindle, Freeze, Glide, Lantern, Phase, Track, Unseal }

    public static class TypeChart
    {
        public const int Count = 12;
        static readonly float[,] chart = Build();

        static float[,] Build()
        {
            var c = new float[Count, Count];
            for (int a = 0; a < Count; a++)
                for (int d = 0; d < Count; d++)
                    c[a, d] = 1f;

            // attacker, then the three types it is strong against, then the three that resist it
            Set(c, Element.Ember, new[] { Element.Grove, Element.Frost, Element.Feral }, new[] { Element.Tide, Element.Stone, Element.Ember });
            Set(c, Element.Tide, new[] { Element.Ember, Element.Stone }, new[] { Element.Grove, Element.Tide, Element.Frost });
            Set(c, Element.Grove, new[] { Element.Tide, Element.Stone, Element.Storm }, new[] { Element.Ember, Element.Grove, Element.Gale });
            Set(c, Element.Gale, new[] { Element.Grove, Element.Feral }, new[] { Element.Stone, Element.Storm, Element.Frost });
            Set(c, Element.Stone, new[] { Element.Ember, Element.Storm, Element.Gale }, new[] { Element.Tide, Element.Grove, Element.Stone });
            Set(c, Element.Frost, new[] { Element.Grove, Element.Gale, Element.Tide }, new[] { Element.Ember, Element.Frost, Element.Stone });
            Set(c, Element.Storm, new[] { Element.Tide, Element.Gale }, new[] { Element.Stone, Element.Grove, Element.Storm });
            Set(c, Element.Spirit, new[] { Element.Feral, Element.Arcane }, new[] { Element.Shade, Element.Radiance });
            Set(c, Element.Shade, new[] { Element.Spirit, Element.Radiance, Element.Arcane }, new[] { Element.Shade, Element.Feral, Element.Ember });
            Set(c, Element.Radiance, new[] { Element.Shade, Element.Spirit, Element.Frost }, new[] { Element.Radiance, Element.Arcane, Element.Grove });
            Set(c, Element.Feral, new[] { Element.Arcane, Element.Shade }, new[] { Element.Stone, Element.Spirit, Element.Gale });
            Set(c, Element.Arcane, new[] { Element.Radiance, Element.Stone, Element.Ember }, new[] { Element.Arcane, Element.Feral, Element.Shade });
            return c;
        }

        static void Set(float[,] c, Element atk, Element[] strong, Element[] weak)
        {
            foreach (var s in strong) c[(int)atk, (int)s] = 2f;
            foreach (var w in weak) c[(int)atk, (int)w] = 0.5f;
        }

        public static float Single(Element atk, Element def)
        {
            if (atk == Element.None || def == Element.None) return 1f;
            return chart[(int)atk, (int)def];
        }

        public static float Multiplier(Element atk, Element d1, Element d2)
        {
            float m = Single(atk, d1);
            if (d2 != d1) m *= Single(atk, d2);
            return m;
        }

        static readonly Color[] colors =
        {
            new Color(0.93f, 0.36f, 0.20f), // Ember
            new Color(0.22f, 0.52f, 0.90f), // Tide
            new Color(0.33f, 0.70f, 0.30f), // Grove
            new Color(0.62f, 0.86f, 0.82f), // Gale
            new Color(0.62f, 0.52f, 0.40f), // Stone
            new Color(0.66f, 0.84f, 0.98f), // Frost
            new Color(0.96f, 0.82f, 0.22f), // Storm
            new Color(0.62f, 0.56f, 0.90f), // Spirit
            new Color(0.34f, 0.26f, 0.44f), // Shade
            new Color(0.99f, 0.93f, 0.62f), // Radiance
            new Color(0.76f, 0.48f, 0.26f), // Feral
            new Color(0.86f, 0.38f, 0.82f), // Arcane
        };

        public static Color ColorOf(Element e)
        {
            return e == Element.None ? new Color(0.75f, 0.75f, 0.75f) : colors[(int)e];
        }

        public static string Name(Element e)
        {
            return e == Element.None ? "Typeless" : e.ToString();
        }

        public static string Names(Element a, Element b)
        {
            if (b == Element.None || b == a) return Name(a);
            return Name(a) + " / " + Name(b);
        }

        public static FieldAbility AbilityOf(Element e)
        {
            switch (e)
            {
                case Element.Tide: return FieldAbility.Surf;
                case Element.Stone: return FieldAbility.Smash;
                case Element.Grove: return FieldAbility.Cut;
                case Element.Ember: return FieldAbility.Kindle;
                case Element.Frost: return FieldAbility.Freeze;
                case Element.Gale: return FieldAbility.Glide;
                case Element.Radiance: return FieldAbility.Lantern;
                case Element.Storm: return FieldAbility.Lantern;
                case Element.Spirit: return FieldAbility.Phase;
                case Element.Shade: return FieldAbility.Phase;
                case Element.Feral: return FieldAbility.Track;
                default: return FieldAbility.Unseal;
            }
        }

        public static string AbilityTypes(FieldAbility a)
        {
            switch (a)
            {
                case FieldAbility.Surf: return "Tide";
                case FieldAbility.Smash: return "Stone";
                case FieldAbility.Cut: return "Grove";
                case FieldAbility.Kindle: return "Ember";
                case FieldAbility.Freeze: return "Frost";
                case FieldAbility.Glide: return "Gale";
                case FieldAbility.Lantern: return "Radiance or Storm";
                case FieldAbility.Phase: return "Spirit or Shade";
                case FieldAbility.Track: return "Feral";
                default: return "Arcane";
            }
        }
    }
}
