using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fable
{
    public enum StatKind { Atk = 0, Def = 1, Mag = 2, Res = 3, Spd = 4 }

    public static class Formula
    {
        public const int MaxLevel = 70;

        public static int Hp(int baseHp, int level) { return Mathf.RoundToInt(20 + baseHp * (0.5f + level * 0.06f)); }
        public static int Stat(int baseStat, int level) { return Mathf.RoundToInt(5 + baseStat * (0.3f + level * 0.03f)); }
        public static int Mp(int baseMp, int level) { return Mathf.RoundToInt(baseMp * (0.5f + level * 0.03f)); }
        public static int ExpToNext(int level) { return 20 + level * level * 4; }

        /// <summary>Field ability rank a creature of this level provides: 0 none, then 1 to 3.</summary>
        public static int FieldRank(int level)
        {
            if (level >= 42) return 3;
            if (level >= 25) return 2;
            if (level >= 8) return 1;
            return 0;
        }
    }

    /// <summary>One individual creature. Two of the same species differ in trait and potential.</summary>
    [Serializable]
    public class CreatureInstance
    {
        public const int MaxEquipped = 4;

        public int speciesId;
        public string nickname = "";
        public int level = 1;
        public int exp;
        public string traitId = "";
        public int[] potential = new int[6]; // hp, atk, def, mag, res, spd: 0 to 10, each point is +1.5%
        public List<string> knownMoves = new List<string>();
        public List<string> equippedMoves = new List<string>();
        public int hp;
        public int mp;

        public SpeciesData Species { get { return Database.GetSpecies(speciesId); } }
        public string Name { get { return string.IsNullOrEmpty(nickname) ? Species.name : nickname; } }
        public TraitData Trait { get { return Database.GetTrait(traitId); } }
        public bool Fainted { get { return hp <= 0; } }

        public static CreatureInstance Create(int speciesId, int level, System.Random rng)
        {
            var c = new CreatureInstance();
            var s = Database.GetSpecies(speciesId);
            c.speciesId = s.id;
            c.level = Mathf.Clamp(level, 1, Formula.MaxLevel);
            for (int i = 0; i < 6; i++) c.potential[i] = rng.Next(0, 11);
            int roll = rng.Next(100);
            c.traitId = s.traits[roll < 45 ? 0 : (roll < 90 ? 1 : 2)];
            for (int i = 0; i < s.learnLevels.Length; i++)
                if (s.learnLevels[i] <= c.level) c.Learn(s.learnMoves[i]);
            c.FullHeal();
            return c;
        }

        float Mythic { get { return traitId == "mythic" ? 1.06f : 1f; } }
        float Pot(int i) { return 1f + potential[i] * 0.015f; }

        public int MaxHp
        {
            get
            {
                float v = Formula.Hp(Species.hp, level) * Pot(0) * Mythic;
                if (traitId == "hardy") v *= 1.1f;
                return Mathf.Max(1, Mathf.RoundToInt(v));
            }
        }

        public int MaxMp { get { return Formula.Mp(Species.mp, level); } }
        public int MaxAp { get { return Species.ap; } }

        public int Stat(StatKind k)
        {
            var s = Species;
            int b;
            switch (k)
            {
                case StatKind.Atk: b = s.atk; break;
                case StatKind.Def: b = s.def; break;
                case StatKind.Mag: b = s.mag; break;
                case StatKind.Res: b = s.res; break;
                default: b = s.spd; break;
            }
            float v = Formula.Stat(b, level) * Pot((int)k + 1) * Mythic;
            if (k == StatKind.Spd && traitId == "swift") v *= 1.1f;
            return Mathf.Max(1, Mathf.RoundToInt(v));
        }

        public void FullHeal()
        {
            hp = MaxHp;
            mp = MaxMp;
        }

        public bool Learn(string moveId)
        {
            if (knownMoves.Contains(moveId)) return false;
            knownMoves.Add(moveId);
            if (equippedMoves.Count < MaxEquipped) equippedMoves.Add(moveId);
            else
            {
                // newest move replaces the oldest equipped one; the player can rearrange in the party menu
                equippedMoves.RemoveAt(0);
                equippedMoves.Add(moveId);
            }
            return true;
        }

        /// <summary>Adds experience and returns a line of text for every level, move and evolution gained.</summary>
        public List<string> GainExp(int amount)
        {
            var log = new List<string>();
            if (level >= Formula.MaxLevel) return log;
            if (traitId == "scholar") amount = Mathf.RoundToInt(amount * 1.2f);
            exp += amount;
            while (level < Formula.MaxLevel && exp >= Formula.ExpToNext(level))
            {
                exp -= Formula.ExpToNext(level);
                int oldMax = MaxHp;
                level++;
                hp += MaxHp - oldMax;
                log.Add(Name + " grew to level " + level + "!");
                var s = Species;
                for (int i = 0; i < s.learnLevels.Length; i++)
                    if (s.learnLevels[i] == level && Learn(s.learnMoves[i]))
                        log.Add(Name + " learned " + Database.GetMove(s.learnMoves[i]).name + "!");
                if (level == 8 || level == 25 || level == 42)
                    log.Add(Name + "'s field ability grew stronger. (" + FieldAbilityText() + ")");
            }
            return log;
        }

        public bool CanEvolveByLevel
        {
            get
            {
                var s = Species;
                return s.evolvesTo >= 0 && string.IsNullOrEmpty(s.evolveItem) && level >= s.evolveLevel;
            }
        }

        public bool CanEvolveWithItem(string itemId)
        {
            var s = Species;
            return s.evolvesTo >= 0 && s.evolveItem == itemId && level >= s.evolveLevel;
        }

        public List<string> Evolve()
        {
            var log = new List<string>();
            var old = Species;
            if (old.evolvesTo < 0) return log;
            string oldName = Name;
            float hpFrac = MaxHp > 0 ? (float)hp / MaxHp : 1f;
            speciesId = old.evolvesTo;
            var s = Species;
            // the individual keeps its trait slot, so siblings stay distinct after evolving
            int slot = Array.IndexOf(old.traits, traitId);
            if (slot >= 0 && slot < s.traits.Length) traitId = s.traits[slot];
            hp = Mathf.Max(1, Mathf.RoundToInt(MaxHp * hpFrac));
            mp = Mathf.Min(mp, MaxMp);
            log.Add(oldName + " evolved into " + s.name + "! (" + TypeChart.Names(s.Type1, s.Type2) + ")");
            for (int i = 0; i < s.learnLevels.Length; i++)
                if (s.learnLevels[i] <= level && Learn(s.learnMoves[i]))
                    log.Add(Name + " learned " + Database.GetMove(s.learnMoves[i]).name + "!");
            return log;
        }

        public int FieldRank(FieldAbility a)
        {
            var s = Species;
            bool has = (s.Type1 != Element.None && TypeChart.AbilityOf(s.Type1) == a)
                    || (s.Type2 != Element.None && TypeChart.AbilityOf(s.Type2) == a);
            return has ? Formula.FieldRank(level) : 0;
        }

        public string FieldAbilityText()
        {
            var s = Species;
            int rank = Formula.FieldRank(level);
            string a = TypeChart.AbilityOf(s.Type1).ToString();
            if (s.Type2 != Element.None && TypeChart.AbilityOf(s.Type2) != TypeChart.AbilityOf(s.Type1))
                a += ", " + TypeChart.AbilityOf(s.Type2);
            return rank == 0 ? a + " (awakens at level 8)" : a + " rank " + rank;
        }

        public string Describe()
        {
            var s = Species;
            var t = Trait;
            string text = s.name + "  Lv " + level + "   " + TypeChart.Names(s.Type1, s.Type2) + "\n"
                + s.origin + "\n"
                + "HP " + hp + "/" + MaxHp + "   MP " + mp + "/" + MaxMp + "   AP " + MaxAp + "\n"
                + "Atk " + Stat(StatKind.Atk) + "  Def " + Stat(StatKind.Def) + "  Mag " + Stat(StatKind.Mag)
                + "  Res " + Stat(StatKind.Res) + "  Spd " + Stat(StatKind.Spd) + "\n"
                + "Exp " + exp + "/" + Formula.ExpToNext(level) + "\n"
                + "Innate: " + (t != null ? t.name + " - " + t.desc : "none") + "\n"
                + "Field: " + FieldAbilityText() + "\n";
            if (s.evolvesTo >= 0)
            {
                text += string.IsNullOrEmpty(s.evolveItem)
                    ? "Evolves at level " + s.evolveLevel + ".\n"
                    : "Something stirs within it... (level " + s.evolveLevel + " and a rare relic)\n";
            }
            text += "Moves: ";
            for (int i = 0; i < equippedMoves.Count; i++)
                text += (i > 0 ? ", " : "") + Database.GetMove(equippedMoves[i]).name;
            return text;
        }
    }
}
