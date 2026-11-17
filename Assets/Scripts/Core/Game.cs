using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fable
{
    /// <summary>
    /// The root of the game. It creates itself when you press Play in any scene, builds the world
    /// in code, and routes each frame to whichever of these is active: a battle, the top menu
    /// screen, or the overworld.
    /// </summary>
    public class Game : MonoBehaviour
    {
        public static Game I;
        public static Camera Cam;
        public static GameState State { get { return I != null ? I.state : null; } }

        GameState state;
        World world;
        Battle battle;
        Action<BattleResult> battleCallback;
        readonly List<GameScreen> stack = new List<GameScreen>();

        // Unity 6.6 enters Play Mode without reloading scripts by default, so statics from the
        // previous run survive. Clear them before anything else starts.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            I = null;
            Cam = null;
            GameInput.Consume();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("Game");
            DontDestroyOnLoad(go);
            go.AddComponent<Game>();
        }

        void Awake()
        {
            if (I != null && I != this)
            {
                Destroy(gameObject);
                return;
            }
            I = this;
            Application.targetFrameRate = 60;
            Database.Load();
            ClassDatabase.Build();
            Settings.Load();
            SetupScene();
            Menus.OpenTitle();
        }

        void SetupScene()
        {
            Cam = Camera.main;
            if (Cam == null && Camera.allCamerasCount > 0) Cam = Camera.allCameras[0];
            if (Cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                Cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            DontDestroyOnLoad(Cam.gameObject);
            Cam.orthographic = false;
            Cam.fieldOfView = 40f;
            Cam.nearClipPlane = 0.3f;
            Cam.farClipPlane = 120f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color(0.55f, 0.75f, 0.92f);

            // flat ambient light plus a sun, so the simple props read clearly whatever the scene contains
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.72f, 0.72f, 0.76f);
            var sun = new GameObject("Fable Sun");
            DontDestroyOnLoad(sun);
            var sunLight = sun.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.intensity = 0.6f;
            sunLight.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // ------------------------------------------------------------------ starting and loading

        public void NewGame(string classId)
        {
            state = new GameState();
            state.seed = UnityEngine.Random.Range(1, 1000000);
            state.px = state.respawnX = WorldMap.Center + 0.5f;
            state.pz = state.respawnZ = WorldMap.Center + 0.5f;
            state.player.classId = classId;
            state.player.SetClass(classId);
            state.player.FullHeal();
            state.AddItem("potion", 5);
            state.AddItem("talisman", 8);
            state.AddItem("ether", 2);
            EnterWorld();
            var c = state.player.Class;
            Say("You wake beneath the red gate of Hearthmeadow, a " + c.name + " of the " + TypeChart.Name(c.primary) + " type.",
                "Beyond the meadow, creatures out of every story wander the land. Weaken one, then bind it with a talisman.",
                "Up to three creatures can fight beside you. Their types open sealed passes through the three great walls.",
                "Move: WASD or left stick.  Interact: E or (A).  Menu: Tab or Start.  Run: Shift or (X).");
        }

        public bool Continue()
        {
            var loaded = SaveSystem.Load();
            if (loaded == null) return false;
            state = loaded;
            if (state.player.equippedSkills.Count == 0) state.player.SetClass(state.player.classId);
            EnterWorld();
            return true;
        }

        void EnterWorld()
        {
            stack.Clear();
            if (world != null) world.Destroy();
            world = new World(state, Cam);
            world.UpdateCamera();
        }

        public void QuitToTitle()
        {
            stack.Clear();
            if (world != null) world.Destroy();
            world = null;
            state = null;
            Menus.OpenTitle();
        }

        // ------------------------------------------------------------------ screens

        public void Push(GameScreen screen) { stack.Add(screen); }
        public void Pop(GameScreen screen) { stack.Remove(screen); }

        public void Say(params string[] lines)
        {
            Push(new MessageScreen(new List<string>(lines), null));
        }

        public void Say(List<string> lines, Action onDone)
        {
            if (lines == null || lines.Count == 0)
            {
                if (onDone != null) onDone();
                return;
            }
            Push(new MessageScreen(lines, onDone));
        }

        public void OpenShrine()
        {
            Menus.OpenShrine();
            Say("You pray at the shrine. Your whole team is restored, and you will return here if you fall.");
        }

        // ------------------------------------------------------------------ battles

        public void StartBattle(List<CreatureInstance> wild, Biome biome, bool boss, Action<BattleResult> onEnd)
        {
            if (battle != null) return;
            battleCallback = onEnd;
            battle = new Battle(state, wild, biome, boss, Cam);
            StartCoroutine(battle.Run());
        }

        public void EndBattle(BattleResult result)
        {
            if (battle == null) return;
            battle.Dispose();
            battle = null;
            var callback = battleCallback;
            battleCallback = null;

            if (result == BattleResult.Defeat)
            {
                int lost = state.gold / 10;
                state.gold -= lost;
                state.HealAll();
                world.Respawn();
                Say("Everything goes dark...", "You wake at the last shrine you prayed at, " + lost + " gold lighter.");
            }
            world.ClearRoamers();
            world.RefreshAbilities();
            world.UpdateCamera();
            GameInput.Consume();
            if (callback != null) callback(result);
        }

        // ------------------------------------------------------------------ frame loop

        void Update()
        {
            GameInput.Poll();
            float dt = Time.deltaTime;

            if (battle != null)
            {
                battle.Update(dt);
                return;
            }
            if (state != null) state.playSeconds += dt;

            if (stack.Count > 0)
            {
                stack[stack.Count - 1].Update();
                return;
            }
            if (world == null) return;

            world.Update(dt);
            if (battle != null) return;
            world.UpdateCamera();
            if (stack.Count == 0 && GameInput.MenuDown)
            {
                GameInput.Consume();
                Menus.OpenMain();
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            UI.Begin();

            if (battle != null)
            {
                battle.Draw();
                return;
            }
            if (world != null && state != null) DrawHud(stack.Count == 0);

            int first = stack.Count - 1;
            while (first > 0 && stack[first].DrawBelow) first--;
            for (int i = Mathf.Max(0, first); i < stack.Count; i++) stack[i].Draw();
        }

        void DrawHud(bool full)
        {
            var p = state.player;
            Vector3 pos = world.playerPos;
            float rowH = 34f;
            int rows = 1 + state.party.Count;
            var panel = new Rect(16f, 16f, 300f, 34f + rows * rowH);
            UI.Box(panel, UI.PanelColor);
            UI.Label(new Rect(panel.x + 10, panel.y + 6, panel.width - 20, 22),
                WorldMap.BiomeName(world.map.BiomeAt(pos.x, pos.z)) + "   wild Lv ~" + WorldMap.LevelAt(pos.x, pos.z), 15, UI.GoldColor);

            float y = panel.y + 30f;
            HudRow(panel.x + 10, y, p.name + "  Lv " + p.level, p.hp, p.MaxHp, p.mp, p.MaxMp, p.Type1);
            for (int i = 0; i < state.party.Count; i++)
            {
                var c = state.party[i];
                y += rowH;
                HudRow(panel.x + 10, y, c.Name + "  Lv " + c.level, c.hp, c.MaxHp, c.mp, c.MaxMp, c.Species.Type1);
            }

            if (!full) return;
            UI.Label(new Rect(UI.W - 316f, 16f, 300f, 22f), state.gold + " gold", 17, UI.GoldColor, TextAnchor.UpperRight);
            UI.Label(new Rect(16f, UI.H - 30f, UI.W - 32f, 22f),
                "Move: WASD / stick    Interact: E / (A)    Menu: Tab / Start    Run: Shift / (X)", 14, UI.DimColor);

            if (world.nearby != null)
            {
                var r = new Rect(UI.W * 0.5f - 230f, UI.H - 96f, 460f, 40f);
                UI.Panel(r);
                UI.Label(r, "[E / (A)]  " + world.PromptFor(world.nearby), 18, UI.TextColor, TextAnchor.MiddleCenter);
            }
        }

        static void HudRow(float x, float y, string label, int hp, int maxHp, int mp, int maxMp, Element type)
        {
            UI.Label(new Rect(x, y, 280f, 20f), label, 14, hp > 0 ? UI.TextColor : UI.DimColor);
            UI.Box(new Rect(x - 6f, y + 3f, 3f, 26f), TypeChart.ColorOf(type));
            float frac = maxHp > 0 ? (float)hp / maxHp : 0f;
            UI.Bar(new Rect(x, y + 20f, 170f, 8f), frac, UI.HpColor(frac));
            UI.Bar(new Rect(x + 176f, y + 20f, 104f, 8f), maxMp > 0 ? (float)mp / maxMp : 0f, UI.MpColor);
        }
    }
}
