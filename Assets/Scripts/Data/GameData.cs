using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fable
{
    public enum MoveCategory { Technique = 0, Spell = 1, Status = 2 }
    public enum MoveTarget { OneFoe = 0, AllFoes = 1, Self = 2, OneAlly = 3, AllAllies = 4 }

    [Serializable]
    public class MoveData
    {
        public string id;
        public string name;
        public int type;
        public int category;
        public int power;
        public int accuracy;
        public int cost;
        public bool costIsMP;
        public int target;
        public string effect;
        public int effectChance;
        public int effectPower;
        public string desc;

        public Element Element { get { return (Element)type; } }
        public MoveCategory Category { get { return (MoveCategory)category; } }
        public MoveTarget Target { get { return (MoveTarget)target; } }
        public string CostText { get { return cost <= 0 ? "free" : cost + (costIsMP ? " MP" : " AP"); } }
        public bool TargetsAllies { get { return target >= 2; } }

        public string Summary
        {
            get { return TypeChart.Name(Element) + " - " + CostText + "\n" + desc; }
        }
    }

    [Serializable]
    public class SpeciesData
    {
        public int id;
        public string name;
        public int t1;
        public int t2;
        public int stage;
        public int family;
        public string arch;
        public int hp;
        public int atk;
        public int def;
        public int mag;
        public int res;
        public int spd;
        public int mp;
        public int ap;
        public int evolvesTo;
        public int evolveLevel;
        public string evolveItem;
        public int[] learnLevels;
        public string[] learnMoves;
        public string[] traits;
        public int rarity;
        public int[] biomes;
        public float captureRate;
        public string origin;
        public int expYield;
        public bool hiddenForm;

        public Element Type1 { get { return (Element)t1; } }
        public Element Type2 { get { return (Element)t2; } }
        public bool IsDivine { get { return origin == "Divine"; } }

        public bool HasType(Element e)
        {
            return e != Element.None && (Type1 == e || Type2 == e);
        }

        public bool LivesIn(int biome)
        {
            for (int i = 0; i < biomes.Length; i++)
                if (biomes[i] == biome) return true;
            return false;
        }
    }

    [Serializable] class SpeciesFile { public SpeciesData[] species; }
    [Serializable] class MoveFile { public MoveData[] moves; }

    public class TraitData
    {
        public string id, name, desc;
        public TraitData(string id, string name, string desc) { this.id = id; this.name = name; this.desc = desc; }
    }

    public enum ItemKind { Heal, Mana, Revive, Cleanse, Capture, Evolve }

    public class ItemData
    {
        public string id, name, desc;
        public ItemKind kind;
        public float power;
        public int price;
        public ItemData(string id, string name, ItemKind kind, float power, int price, string desc)
        {
            this.id = id; this.name = name; this.kind = kind; this.power = power; this.price = price; this.desc = desc;
        }
    }

    /// <summary>Static game data. Creatures and moves come from JSON; traits and items are small enough to live here.</summary>
    public static class Database
    {
        public static SpeciesData[] Species;
        public static readonly Dictionary<string, MoveData> Moves = new Dictionary<string, MoveData>();
        public static readonly Dictionary<string, TraitData> Traits = new Dictionary<string, TraitData>();
        public static readonly Dictionary<string, ItemData> Items = new Dictionary<string, ItemData>();
        public static readonly List<ItemData> ItemList = new List<ItemData>();
        static bool loaded;

        public static void Load()
        {
            if (loaded) return;
            loaded = true;

            var speciesText = Resources.Load<TextAsset>("Data/species");
            var moveText = Resources.Load<TextAsset>("Data/moves");
            if (speciesText == null || moveText == null)
            {
                Debug.LogError("Missing Assets/Resources/Data/species.json or moves.json. Run Tools/generate_data.py.");
                Species = new SpeciesData[0];
                return;
            }
            Species = JsonUtility.FromJson<SpeciesFile>(speciesText.text).species;
            foreach (var m in JsonUtility.FromJson<MoveFile>(moveText.text).moves) Moves[m.id] = m;

            AddTrait("hardy", "Hardy", "Max HP +10%.");
            AddTrait("stalwart", "Stalwart", "Takes 10% less damage from techniques.");
            AddTrait("warded", "Warded", "Takes 10% less damage from spells.");
            AddTrait("thornhide", "Thornhide", "Returns 10% of technique damage to the attacker.");
            AddTrait("unshaken", "Unshaken", "Cannot be stunned.");
            AddTrait("fierce", "Fierce", "Techniques deal 10% more damage.");
            AddTrait("vigor", "Vigor", "Recovers 1 extra AP each turn.");
            AddTrait("intimidate", "Intimidate", "Foes begin battle with lowered Attack.");
            AddTrait("swift", "Swift", "Speed +10%.");
            AddTrait("keen", "Keen", "Critical hits are 10% more likely.");
            AddTrait("firststrike", "First Strike", "Begins battle with raised Speed.");
            AddTrait("gifted", "Gifted", "Spells deal 10% more damage.");
            AddTrait("attuned", "Attuned", "Moves matching its own type hit harder still.");
            AddTrait("scholar", "Scholar", "Earns 20% more experience.");
            AddTrait("regrowth", "Regrowth", "Recovers 5% HP each turn.");
            AddTrait("purity", "Purity", "Cannot be burned or poisoned.");
            AddTrait("fortune", "Fortune", "Battles it wins pay 25% more gold.");
            AddTrait("mythic", "Mythic", "All stats +6%. A rare blessing.");
            AddTrait("lastlight", "Last Light", "Deals 30% more damage below one third HP. Rare.");
            AddTrait("wellspring", "Wellspring", "Recovers 4% MP each turn. Rare.");

            AddItem("potion", "Dewdrop Tonic", ItemKind.Heal, 60, 30, "Restores 60 HP to one ally.");
            AddItem("hi_potion", "Peach of Vigor", ItemKind.Heal, 180, 120, "Restores 180 HP to one ally.");
            AddItem("ether", "Moonwell Water", ItemKind.Mana, 40, 60, "Restores 40 MP to one ally.");
            AddItem("revive", "Ember Feather", ItemKind.Revive, 0.5f, 200, "Revives a fallen ally with half HP.");
            AddItem("salt", "Purifying Salt", ItemKind.Cleanse, 0, 40, "Cures burn, poison and stun.");
            AddItem("talisman", "Paper Talisman", ItemKind.Capture, 1f, 40, "Binds a weakened wild creature to your team.");
            AddItem("silver_talisman", "Silver Talisman", ItemKind.Capture, 1.6f, 150, "A stronger binding. Better capture odds.");
            AddItem("gold_talisman", "Golden Talisman", ItemKind.Capture, 2.6f, 500, "A sacred binding. The best capture odds.");
            AddItem("celestial_pearl", "Celestial Pearl", ItemKind.Evolve, 0, 0, "A level 50 Kyubi holding this may ascend.");
            AddItem("kings_feather_fan", "King's Feather Fan", ItemKind.Evolve, 0, 0, "A level 50 Daitengu may claim the mountain throne.");
            AddItem("gnawed_root_rune", "Gnawed Root Rune", ItemKind.Evolve, 0, 0, "A level 50 Fafnir may descend to the world's roots.");
            AddItem("gleipnir_thread", "Gleipnir Thread", ItemKind.Evolve, 0, 0, "A level 50 Wargrim may break its last binding.");
            AddItem("simurgh_plume", "Simurgh Plume", ItemKind.Evolve, 0, 0, "A level 50 Phoenix may rise beyond rebirth.");
            AddItem("eightfold_sake", "Eightfold Sake", ItemKind.Evolve, 0, 0, "A level 50 Orochi may grow its eight heads.");
        }

        static void AddTrait(string id, string name, string desc) { Traits[id] = new TraitData(id, name, desc); }

        static void AddItem(string id, string name, ItemKind kind, float power, int price, string desc)
        {
            var item = new ItemData(id, name, kind, power, price, desc);
            Items[id] = item;
            ItemList.Add(item);
        }

        public static SpeciesData GetSpecies(int id)
        {
            if (id < 0 || id >= Species.Length) return Species[0];
            return Species[id];
        }

        public static MoveData GetMove(string id)
        {
            MoveData m;
            if (id != null && Moves.TryGetValue(id, out m)) return m;
            return Moves["strike"];
        }

        public static TraitData GetTrait(string id)
        {
            TraitData t;
            if (id != null && Traits.TryGetValue(id, out t)) return t;
            return null;
        }

        public static string MoveId(Element e, string slot)
        {
            return e.ToString().ToLower() + "_" + slot;
        }
    }
}
