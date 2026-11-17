using System.Collections.Generic;

namespace Fable
{
    public enum NodeKind { Skill, Stat, Ascend, Imprint }
    public enum BonusStat { Hp, Mp, Atk, Mag, Spd }

    public class SkillNode
    {
        public int index;
        public string name;
        public string desc;
        public int cost;
        public int[] requires = new int[0];
        public int minLevel;
        public NodeKind kind;
        public string moveId;
        public BonusStat bonusStat;
        public int bonusPercent;
    }

    public class ClassDef
    {
        public string id;
        public string name;
        public string advancedName;
        public Element primary;
        public Element secondary;
        public bool physical;
        public int hp, atk, def, mag, res, spd, mp, ap;
        public string desc;
        public string unique1, unique2;
        public SkillNode[] nodes;
        public int[] learnLevels;
        public string[] learnMoves;
    }

    /// <summary>The twelve classes, one per type. Each can ascend to an advanced class with a second type.</summary>
    public static class ClassDatabase
    {
        public static readonly List<ClassDef> Classes = new List<ClassDef>();
        static bool built;

        public static void Build()
        {
            if (built) return;
            built = true;
            //   id            name            advanced            primary          secondary        phys   hp  atk def mag res spd  mp ap
            Add("pyromancer", "Pyromancer", "Phoenix Knight", Element.Ember, Element.Radiance, false, 55, 40, 45, 80, 60, 60, 70, 6,
                "class_immolate", "class_solar_crown", "Wields fox-fire and sun-flame. Fragile, but burns everything down.");
            Add("tidecaller", "Tidecaller", "Abyss Sage", Element.Tide, Element.Shade, false, 65, 40, 55, 70, 70, 50, 75, 6,
                "class_soothing_current", "class_drowning_deep", "A healer who commands rain and river. Keeps the whole team standing.");
            Add("druid", "Druid", "Wildwarden", Element.Grove, Element.Feral, false, 70, 50, 60, 65, 65, 45, 65, 7,
                "class_entangle", "class_world_root", "Speaks for the old forest. Binds foes and mends allies.");
            Add("windrunner", "Windrunner", "Stormrider", Element.Gale, Element.Storm, true, 55, 70, 45, 50, 45, 90, 45, 9,
                "class_twin_gale", "class_eye_of_storm", "Strikes first and strikes often. The fastest class.");
            Add("guardian", "Guardian", "Runeguard", Element.Stone, Element.Arcane, true, 85, 65, 85, 30, 60, 35, 40, 7,
                "class_shield_wall", "class_titans_hammer", "A living rampart. Shields the team and hits like a landslide.");
            Add("frostblade", "Frostblade", "Winter Herald", Element.Frost, Element.Spirit, true, 60, 75, 55, 55, 50, 65, 50, 8,
                "class_hoarfrost_edge", "class_ghost_winter", "A swordsman of the snow country. Slows foes to a crawl.");
            Add("thunder_monk", "Thunder Monk", "Sky Sovereign", Element.Storm, Element.Gale, true, 65, 80, 50, 45, 45, 75, 45, 9,
                "class_thousand_palms", "class_raijin_drumroll", "Fists like drumbeats. Stuns and overwhelms.");
            Add("onmyoji", "Onmyoji", "Celestial Medium", Element.Spirit, Element.Radiance, false, 55, 35, 45, 80, 80, 55, 80, 6,
                "class_shikigami_bind", "class_spirit_parade", "Commands paper spirits. Wards allies and seals foes.");
            Add("shinobi", "Shinobi", "Nightstalker", Element.Shade, Element.Gale, true, 50, 80, 40, 55, 40, 95, 45, 9,
                "class_assassinate", "class_clone_flurry", "Unseen until the blade lands. Devastating, but brittle.");
            Add("paladin", "Paladin", "Seraph", Element.Radiance, Element.Ember, true, 80, 65, 70, 55, 65, 40, 60, 7,
                "class_lay_on_hands", "class_divine_verdict", "A knight of the dawn. Sturdy, with healing light to spare.");
            Add("berserker", "Berserker", "Fenris Heir", Element.Feral, Element.Frost, true, 80, 95, 45, 25, 35, 70, 30, 10,
                "class_bloodrush", "class_ragnarok_fang", "Pure ferocity. The highest Attack and the most AP.");
            Add("spellweaver", "Spellweaver", "Archmage", Element.Arcane, Element.Spirit, false, 50, 30, 40, 95, 70, 60, 90, 6,
                "class_polymorph_curse", "class_tales_end", "A student of every story ever told. The strongest spells.");
        }

        static void Add(string id, string name, string adv, Element p, Element s, bool physical,
            int hp, int atk, int def, int mag, int res, int spd, int mp, int ap, string u1, string u2, string desc)
        {
            var c = new ClassDef
            {
                id = id, name = name, advancedName = adv, primary = p, secondary = s, physical = physical,
                hp = hp, atk = atk, def = def, mag = mag, res = res, spd = spd, mp = mp, ap = ap,
                unique1 = u1, unique2 = u2, desc = desc
            };

            string a1 = physical ? "p1" : "m1", a2 = physical ? "p2" : "m2", a3 = physical ? "p3" : "m4";
            string b1 = physical ? "m1" : "p1", b2 = physical ? "m2" : "p2", b3 = physical ? "m4" : "p3";

            // moves learned simply by levelling up as this class
            c.learnLevels = new[] { 1, 1, 4, 12, 26, 40 };
            c.learnMoves = new[]
            {
                "strike", Database.MoveId(p, a1), Database.MoveId(p, b1),
                Database.MoveId(p, b2), Database.MoveId(p, a3), Database.MoveId(p, b3)
            };

            var n = new List<SkillNode>();
            n.Add(Stat(0, name + " Training", physical ? BonusStat.Atk : BonusStat.Mag, 8, 1));
            n.Add(Skill(1, Database.MoveId(p, "s"), 1, 0, 0));
            n.Add(Skill(2, Database.MoveId(p, a2), 1, 0, 0));
            n.Add(Stat(3, "Vitality", BonusStat.Hp, 10, 2, 1));
            n.Add(Skill(4, u1, 2, 0, 2));
            n.Add(Skill(5, Database.MoveId(p, "m3"), 2, 0, 4));
            n.Add(physical ? Stat(6, "Fleet Foot", BonusStat.Spd, 10, 2, 3) : Stat(6, "Deep Well", BonusStat.Mp, 15, 2, 3));
            n.Add(new SkillNode
            {
                index = 7, kind = NodeKind.Ascend, cost = 3, requires = new[] { 4 }, minLevel = 15,
                name = "Ascension: " + adv,
                desc = "Evolve this class into " + adv + ". Gain " + TypeChart.Name(s) + " as a second type and +15% to all stats while in this class."
            });
            n.Add(Skill(8, Database.MoveId(s, a2), 2, 0, 7));
            n.Add(Skill(9, Database.MoveId(s, "s"), 2, 0, 7));
            n.Add(Skill(10, u2, 4, 25, 5, 7));
            n.Add(new SkillNode
            {
                index = 11, kind = NodeKind.Imprint, cost = 4, requires = new[] { 10 }, minLevel = 30,
                name = "Type Imprint: " + TypeChart.Name(p),
                desc = "Permanently keep the " + TypeChart.Name(p) + " type. You can attach it to any other class as its second type from the Class menu."
            });
            c.nodes = n.ToArray();
            Classes.Add(c);
        }

        static SkillNode Stat(int index, string name, BonusStat stat, int percent, int cost, params int[] requires)
        {
            return new SkillNode
            {
                index = index, kind = NodeKind.Stat, name = name, bonusStat = stat, bonusPercent = percent, cost = cost,
                requires = requires,
                desc = "Permanent +" + percent + "% " + stat + ". Stays with you in every class."
            };
        }

        static SkillNode Skill(int index, string moveId, int cost, int minLevel, params int[] requires)
        {
            var m = Database.GetMove(moveId);
            return new SkillNode
            {
                index = index, kind = NodeKind.Skill, name = "Skill: " + m.name, moveId = moveId, cost = cost,
                minLevel = minLevel, requires = requires,
                desc = m.Summary + "\nOnce learned it can be equipped in any class."
            };
        }

        public static ClassDef Get(string id)
        {
            for (int i = 0; i < Classes.Count; i++)
                if (Classes[i].id == id) return Classes[i];
            return Classes[0];
        }
    }
}
