using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Fable
{
    [Serializable]
    public class ItemStack
    {
        public string id;
        public int count;
    }

    /// <summary>Everything that is saved: the player, the team, the bag and world progress.</summary>
    [Serializable]
    public class GameState
    {
        public const int MaxParty = 3;

        public PlayerCharacter player = new PlayerCharacter();
        public List<CreatureInstance> party = new List<CreatureInstance>();
        public List<CreatureInstance> storage = new List<CreatureInstance>();
        public List<ItemStack> items = new List<ItemStack>();
        public int gold = 200;
        public float px, pz;
        public float respawnX, respawnZ;
        public List<int> opened = new List<int>();  // chests looted, gates cleared, altars answered
        public List<int> caught = new List<int>();  // species ids ever captured
        public int seed = 12345;
        public float playSeconds;

        public int ItemCount(string id)
        {
            for (int i = 0; i < items.Count; i++)
                if (items[i].id == id) return items[i].count;
            return 0;
        }

        public void AddItem(string id, int count)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].id != id) continue;
                items[i].count += count;
                if (items[i].count <= 0) items.RemoveAt(i);
                return;
            }
            if (count > 0) items.Add(new ItemStack { id = id, count = count });
        }

        /// <summary>Adds a captured creature to the team, or to storage when the team is full. Returns true if it joined the team.</summary>
        public bool AddCreature(CreatureInstance c)
        {
            if (!caught.Contains(c.speciesId)) caught.Add(c.speciesId);
            if (party.Count < MaxParty)
            {
                party.Add(c);
                return true;
            }
            storage.Add(c);
            return false;
        }

        /// <summary>Best rank of a field ability among the creatures currently on the team.</summary>
        public int FieldRank(FieldAbility a)
        {
            int best = 0;
            for (int i = 0; i < party.Count; i++)
                best = Mathf.Max(best, party[i].FieldRank(a));
            return best;
        }

        public void HealAll()
        {
            player.FullHeal();
            for (int i = 0; i < party.Count; i++) party[i].FullHeal();
            for (int i = 0; i < storage.Count; i++) storage[i].FullHeal();
        }
    }

    public static class SaveSystem
    {
        static string SavePath { get { return Path.Combine(Application.persistentDataPath, "save.json"); } }

        public static bool HasSave { get { return File.Exists(SavePath); } }

        public static bool Save(GameState state)
        {
            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(state));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Save failed: " + e.Message);
                return false;
            }
        }

        public static GameState Load()
        {
            try
            {
                if (!File.Exists(SavePath)) return null;
                var state = JsonUtility.FromJson<GameState>(File.ReadAllText(SavePath));
                if (state == null || state.player == null) return null;
                return state;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Load failed: " + e.Message);
                return null;
            }
        }
    }

    /// <summary>Player preferences. Kept apart from the save file so they survive a new game.</summary>
    public static class Settings
    {
        public static float Volume = 0.8f;
        public static float UiScale = 1f;
        public static float CameraZoom = 1f;
        public static bool Fullscreen = true;
        public static bool FastBattle;

        public static void Load()
        {
            Volume = PlayerPrefs.GetFloat("volume", 0.8f);
            UiScale = PlayerPrefs.GetFloat("uiScale", 1f);
            CameraZoom = PlayerPrefs.GetFloat("cameraZoom", 1f);
            Fullscreen = PlayerPrefs.GetInt("fullscreen", Screen.fullScreen ? 1 : 0) == 1;
            FastBattle = PlayerPrefs.GetInt("fastBattle", 0) == 1;
            Apply();
        }

        public static void Apply()
        {
            AudioListener.volume = Volume;
            if (Screen.fullScreen != Fullscreen) Screen.fullScreen = Fullscreen;
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat("volume", Volume);
            PlayerPrefs.SetFloat("uiScale", UiScale);
            PlayerPrefs.SetFloat("cameraZoom", CameraZoom);
            PlayerPrefs.SetInt("fullscreen", Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt("fastBattle", FastBattle ? 1 : 0);
            PlayerPrefs.Save();
            Apply();
        }
    }
}
