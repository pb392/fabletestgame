using System.Collections.Generic;
using UnityEngine;

namespace Fable
{
    /// <summary>A fighter inside one battle. Wraps either the player or a creature and holds battle-only state.</summary>
    public class Combatant
    {
        public string name;
        public bool isAlly;
        public bool isPlayer;
        public PlayerCharacter player;
        public CreatureInstance creature;
        public int level;
        public Element t1, t2;
        public int maxHp, maxMp, maxAp;
        public int hp, mp, ap;
        public readonly int[] stats = new int[5];
        public readonly int[] stages = new int[5];
        public List<string> moves = new List<string>();
        public string trait = "";
        public bool guarding;
        public bool stunned;
        public int burn, poison;
        public bool captured;

        // presentation
        public GameObject view;
        public SpriteRenderer sprite;
        public Vector3 home;
        public float flash;
        public float lunge;

        public bool Alive { get { return hp > 0 && !captured; } }
        public bool HasAilment { get { return burn > 0 || poison > 0 || stunned; } }

        public static Combatant FromPlayer(PlayerCharacter p)
        {
            var c = new Combatant
            {
                name = p.name, isAlly = true, isPlayer = true, player = p, level = p.level,
                t1 = p.Type1, t2 = p.Type2, maxHp = p.MaxHp, maxMp = p.MaxMp, maxAp = p.MaxAp
            };
            c.hp = Mathf.Clamp(p.hp, 1, c.maxHp);
            c.mp = Mathf.Clamp(p.mp, 0, c.maxMp);
            c.ap = c.maxAp;
            for (int i = 0; i < 5; i++) c.stats[i] = p.Stat((StatKind)i);
            c.moves = new List<string>(p.equippedSkills);
            return c;
        }

        public static Combatant FromCreature(CreatureInstance cr, bool ally)
        {
            var s = cr.Species;
            var c = new Combatant
            {
                name = ally ? cr.Name : "Wild " + cr.Name, isAlly = ally, creature = cr, level = cr.level,
                t1 = s.Type1, t2 = s.Type2, maxHp = cr.MaxHp, maxMp = cr.MaxMp, maxAp = cr.MaxAp, trait = cr.traitId
            };
            c.hp = Mathf.Clamp(cr.hp, 0, c.maxHp);
            c.mp = Mathf.Clamp(cr.mp, 0, c.maxMp);
            c.ap = c.maxAp;
            for (int i = 0; i < 5; i++) c.stats[i] = cr.Stat((StatKind)i);
            c.moves = new List<string>(cr.equippedMoves);
            return c;
        }

        public float Stat(StatKind k)
        {
            int stage = stages[(int)k];
            float mult = stage >= 0 ? 1f + 0.25f * stage : 1f / (1f + 0.25f * -stage);
            return Mathf.Max(1f, stats[(int)k] * mult);
        }

        public bool CanAfford(MoveData m)
        {
            return m.costIsMP ? mp >= m.cost : ap >= m.cost;
        }

        public void Pay(MoveData m)
        {
            if (m.costIsMP) mp -= m.cost;
            else ap -= m.cost;
        }

        /// <summary>Changes a stat stage, clamped to three steps either way. Returns false if it could not move.</summary>
        public bool ChangeStage(StatKind k, int delta)
        {
            int old = stages[(int)k];
            stages[(int)k] = Mathf.Clamp(old + delta, -3, 3);
            return stages[(int)k] != old;
        }

        public void WriteBack()
        {
            if (isPlayer)
            {
                player.hp = Mathf.Clamp(hp, 0, player.MaxHp);
                player.mp = Mathf.Clamp(mp, 0, player.MaxMp);
            }
            else if (creature != null)
            {
                creature.hp = Mathf.Clamp(hp, 0, creature.MaxHp);
                creature.mp = Mathf.Clamp(mp, 0, creature.MaxMp);
            }
        }
    }
}
