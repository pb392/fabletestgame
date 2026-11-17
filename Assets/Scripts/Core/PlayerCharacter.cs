using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fable
{
    /// <summary>The player's own fighter. Always the first member of the team.</summary>
    [Serializable]
    public class PlayerCharacter
    {
        public const int MaxEquipped = 6;
        public const int SkillPointsPerLevel = 2;

        public string name = "Wanderer";
        public string classId = "pyromancer";
        public int level = 1;
        public int exp;
        public int skillPoints = 2;
        public List<string> unlockedNodes = new List<string>(); // "classId:nodeIndex"
        public List<string> ascended = new List<string>();      // class ids that have ascended
        public List<int> imprintedTypes = new List<int>();      // types kept permanently
        public int activeImprint = -1;                          // imprinted type attached to the current class
        public List<string> knownSkills = new List<string>();
        public List<string> equippedSkills = new List<string>();
        public int hp;
        public int mp;

        public ClassDef Class { get { return ClassDatabase.Get(classId); } }
        public bool IsAscended { get { return ascended.Contains(classId); } }
        public string ClassName { get { return IsAscended ? Class.advancedName : Class.name; } }
        public Element Type1 { get { return Class.primary; } }

        /// <summary>An attached imprint wins over the ascended class's own second type.</summary>
        public Element Type2
        {
            get
            {
                if (activeImprint >= 0 && imprintedTypes.Contains(activeImprint) && (Element)activeImprint != Class.primary)
                    return (Element)activeImprint;
                return IsAscended ? Class.secondary : Element.None;
            }
        }

        public bool HasNode(string cls, int index) { return unlockedNodes.Contains(cls + ":" + index); }

        int BonusPercent(BonusStat stat)
        {
            int total = 0;
            var classes = ClassDatabase.Classes;
            for (int c = 0; c < classes.Count; c++)
            {
                var nodes = classes[c].nodes;
                for (int i = 0; i < nodes.Length; i++)
                    if (nodes[i].kind == NodeKind.Stat && nodes[i].bonusStat == stat && HasNode(classes[c].id, i))
                        total += nodes[i].bonusPercent;
            }
            return total;
        }

        float AscendMult { get { return IsAscended ? 1.15f : 1f; } }

        public int MaxHp { get { return Mathf.RoundToInt(Formula.Hp(Class.hp, level) * AscendMult * (1f + BonusPercent(BonusStat.Hp) / 100f)); } }
        public int MaxMp { get { return Mathf.RoundToInt(Formula.Mp(Class.mp, level) * AscendMult * (1f + BonusPercent(BonusStat.Mp) / 100f)); } }
        public int MaxAp { get { return Class.ap + (IsAscended ? 1 : 0); } }

        public int Stat(StatKind k)
        {
            var c = Class;
            int b;
            float bonus = 0f;
            switch (k)
            {
                case StatKind.Atk: b = c.atk; bonus = BonusPercent(BonusStat.Atk); break;
                case StatKind.Def: b = c.def; break;
                case StatKind.Mag: b = c.mag; bonus = BonusPercent(BonusStat.Mag); break;
                case StatKind.Res: b = c.res; break;
                default: b = c.spd; bonus = BonusPercent(BonusStat.Spd); break;
            }
            return Mathf.Max(1, Mathf.RoundToInt(Formula.Stat(b, level) * AscendMult * (1f + bonus / 100f)));
        }

        public void FullHeal()
        {
            hp = MaxHp;
            mp = MaxMp;
        }

        public bool LearnSkill(string moveId)
        {
            if (knownSkills.Contains(moveId)) return false;
            knownSkills.Add(moveId);
            if (equippedSkills.Count < MaxEquipped) equippedSkills.Add(moveId);
            return true;
        }

        /// <summary>Teaches every level-up move of the current class the player is already high enough for.</summary>
        public List<string> LearnClassMoves()
        {
            var log = new List<string>();
            var c = Class;
            for (int i = 0; i < c.learnLevels.Length; i++)
                if (c.learnLevels[i] <= level && LearnSkill(c.learnMoves[i]))
                    log.Add(name + " learned " + Database.GetMove(c.learnMoves[i]).name + "!");
            return log;
        }

        public void SetClass(string id)
        {
            float hpFrac = MaxHp > 0 ? (float)hp / MaxHp : 1f;
            float mpFrac = MaxMp > 0 ? (float)mp / MaxMp : 1f;
            classId = id;
            LearnClassMoves();
            if (equippedSkills.Count == 0) equippedSkills.Add("strike");
            hp = Mathf.Max(1, Mathf.RoundToInt(MaxHp * hpFrac));
            mp = Mathf.RoundToInt(MaxMp * mpFrac);
        }

        public List<string> GainExp(int amount)
        {
            var log = new List<string>();
            if (level >= Formula.MaxLevel) return log;
            exp += amount;
            while (level < Formula.MaxLevel && exp >= Formula.ExpToNext(level))
            {
                exp -= Formula.ExpToNext(level);
                int oldMax = MaxHp;
                level++;
                skillPoints += SkillPointsPerLevel;
                hp += MaxHp - oldMax;
                log.Add(name + " grew to level " + level + "! (+" + SkillPointsPerLevel + " skill points)");
                log.AddRange(LearnClassMoves());
            }
            return log;
        }

        /// <summary>Returns null if the node can be bought right now, otherwise the reason it cannot.</summary>
        public string WhyLocked(ClassDef cls, SkillNode node)
        {
            if (HasNode(cls.id, node.index)) return "Already unlocked.";
            if (cls.id != classId) return "Switch to this class to train its tree.";
            for (int i = 0; i < node.requires.Length; i++)
                if (!HasNode(cls.id, node.requires[i]))
                    return "Requires: " + cls.nodes[node.requires[i]].name;
            if (level < node.minLevel) return "Requires level " + node.minLevel + ".";
            if (skillPoints < node.cost) return "Needs " + node.cost + " skill points.";
            return null;
        }

        public string Unlock(ClassDef cls, SkillNode node)
        {
            if (WhyLocked(cls, node) != null) return null;
            skillPoints -= node.cost;
            unlockedNodes.Add(cls.id + ":" + node.index);
            switch (node.kind)
            {
                case NodeKind.Skill:
                    LearnSkill(node.moveId);
                    return "Learned " + Database.GetMove(node.moveId).name + "!";
                case NodeKind.Ascend:
                    ascended.Add(cls.id);
                    FullHeal();
                    return cls.name + " ascended into " + cls.advancedName + "! You are now " + TypeChart.Names(Type1, Type2) + ".";
                case NodeKind.Imprint:
                    if (!imprintedTypes.Contains((int)cls.primary)) imprintedTypes.Add((int)cls.primary);
                    return "The " + TypeChart.Name(cls.primary) + " type is yours forever. Attach it to another class from the Class menu.";
                default:
                    return node.name + " unlocked.";
            }
        }

        public string Describe()
        {
            return name + "  Lv " + level + "   " + ClassName + "   " + TypeChart.Names(Type1, Type2) + "\n"
                + "HP " + hp + "/" + MaxHp + "   MP " + mp + "/" + MaxMp + "   AP " + MaxAp + "\n"
                + "Atk " + Stat(StatKind.Atk) + "  Def " + Stat(StatKind.Def) + "  Mag " + Stat(StatKind.Mag)
                + "  Res " + Stat(StatKind.Res) + "  Spd " + Stat(StatKind.Spd) + "\n"
                + "Exp " + exp + "/" + Formula.ExpToNext(level) + "   Skill points " + skillPoints;
        }
    }
}
