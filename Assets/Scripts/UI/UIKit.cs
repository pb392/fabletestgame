using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fable
{
    /// <summary>
    /// Small immediate-mode drawing helpers. Everything is laid out on a virtual canvas
    /// 720 units tall (before the UI scale setting), so it looks the same at any resolution.
    /// Menus are driven by GameInput rather than the mouse, so they work on a gamepad.
    /// </summary>
    public static class UI
    {
        public static float W = 1280f;
        public static float H = 720f;
        public static float Scale = 1f; // screen pixels per canvas unit

        static GUIStyle style;
        public static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.14f, 0.93f);
        public static readonly Color BorderColor = new Color(0.85f, 0.72f, 0.42f, 1f);
        public static readonly Color SelectColor = new Color(0.85f, 0.72f, 0.42f, 0.30f);
        public static readonly Color TextColor = new Color(0.96f, 0.94f, 0.88f, 1f);
        public static readonly Color DimColor = new Color(0.60f, 0.60f, 0.62f, 1f);
        public static readonly Color GoldColor = new Color(1f, 0.86f, 0.45f, 1f);

        public static void Begin()
        {
            float scale = Screen.height / 720f * Mathf.Clamp(Settings.UiScale, 0.7f, 1.4f);
            if (scale <= 0.01f) scale = 1f;
            Scale = scale;
            W = Screen.width / scale;
            H = Screen.height / scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label);
                style.wordWrap = true;
                style.richText = false;
                style.padding = new RectOffset(0, 0, 0, 0);
            }
        }

        public static void Box(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        public static void Panel(Rect r)
        {
            Box(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), BorderColor);
            Box(r, new Color(PanelColor.r, PanelColor.g, PanelColor.b, 1f));
        }

        public static void Label(Rect r, string text, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            if (string.IsNullOrEmpty(text)) return;
            style.fontSize = size;
            style.alignment = anchor;
            style.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.6f);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
        }

        public static void Label(Rect r, string text, int size = 18)
        {
            Label(r, text, size, TextColor);
        }

        public static void Bar(Rect r, float frac, Color fill)
        {
            Box(r, new Color(0f, 0f, 0f, 0.75f));
            frac = Mathf.Clamp01(frac);
            if (frac > 0f) Box(new Rect(r.x + 1, r.y + 1, (r.width - 2) * frac, r.height - 2), fill);
        }

        public static Color HpColor(float frac)
        {
            if (frac > 0.5f) return new Color(0.35f, 0.82f, 0.38f);
            if (frac > 0.2f) return new Color(0.93f, 0.78f, 0.25f);
            return new Color(0.90f, 0.30f, 0.26f);
        }

        /// <summary>Converts a world position to canvas coordinates. z is negative when behind the camera.</summary>
        public static Vector3 WorldToCanvas(Camera cam, Vector3 world)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            return new Vector3(sp.x / Scale, (Screen.height - sp.y) / Scale, sp.z);
        }

        public static readonly Color MpColor = new Color(0.35f, 0.58f, 0.95f);
        public static readonly Color ApColor = new Color(0.95f, 0.62f, 0.25f);
    }

    /// <summary>A layer of UI. Game keeps a stack of these; only the top one receives input.</summary>
    public abstract class GameScreen
    {
        public virtual bool DrawBelow { get { return false; } }
        public virtual void Update() { }
        public abstract void Draw();
        public void Close() { Game.I.Pop(this); }
    }

    public class Row
    {
        public string label;
        public string right;
        public string desc;
        public Action action;
        public bool enabled = true;
        public bool header;
        public Color color = UI.TextColor;

        public Row(string label, string desc = null, Action action = null)
        {
            this.label = label;
            this.desc = desc;
            this.action = action;
        }
    }

    /// <summary>
    /// A scrolling list on the left with details for the highlighted row on the right.
    /// Nearly every menu in the game is one of these with a different row builder.
    /// </summary>
    public class ListScreen : GameScreen
    {
        public string title;
        public Func<List<Row>> build;
        public Func<string> header;
        public Action onClose;
        public bool cancellable = true;

        List<Row> rows = new List<Row>();
        int sel;
        int scroll;
        const float RowH = 30f;

        public ListScreen(string title, Func<List<Row>> build)
        {
            this.title = title;
            this.build = build;
        }

        void Refresh()
        {
            rows = build() ?? new List<Row>();
            if (rows.Count == 0) rows.Add(new Row("(nothing here)") { enabled = false });
            sel = Mathf.Clamp(sel, 0, rows.Count - 1);
            if (rows[sel].header) Step(1);
        }

        void Step(int dir)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                sel = (sel + dir + rows.Count) % rows.Count;
                if (!rows[sel].header) return;
            }
        }

        public override void Update()
        {
            Refresh();
            if (GameInput.NavDown) Step(1);
            if (GameInput.NavUp) Step(-1);
            if (GameInput.ConfirmDown)
            {
                var row = rows[sel];
                GameInput.Consume();
                if (row.enabled && row.action != null) row.action();
                return;
            }
            if (GameInput.CancelDown && cancellable)
            {
                GameInput.Consume();
                Close();
                if (onClose != null) onClose();
            }
        }

        public override void Draw()
        {
            if (rows.Count == 0) Refresh();
            UI.Box(new Rect(0, 0, UI.W, UI.H), new Color(0f, 0f, 0f, 0.55f));

            float top = 40f, bottom = UI.H - 50f;
            float leftW = UI.W * 0.44f;
            var left = new Rect(40f, top, leftW, bottom - top);
            var right = new Rect(left.xMax + 20f, top, UI.W - left.xMax - 60f, bottom - top);
            UI.Panel(left);
            UI.Panel(right);
            UI.Label(new Rect(left.x + 16, left.y + 10, left.width - 32, 30), title, 24, UI.GoldColor);

            float listTop = left.y + 52f;
            int visible = Mathf.Max(3, Mathf.FloorToInt((left.yMax - listTop - 10f) / RowH));
            if (sel < scroll) scroll = sel;
            if (sel >= scroll + visible) scroll = sel - visible + 1;
            scroll = Mathf.Clamp(scroll, 0, Mathf.Max(0, rows.Count - visible));

            for (int i = scroll; i < rows.Count && i < scroll + visible; i++)
            {
                var row = rows[i];
                var r = new Rect(left.x + 10, listTop + (i - scroll) * RowH, left.width - 20, RowH - 2);
                if (i == sel) UI.Box(r, UI.SelectColor);
                Color c = row.header ? UI.GoldColor : (row.enabled ? row.color : UI.DimColor);
                UI.Label(new Rect(r.x + 10, r.y + 3, r.width - 20, r.height), (i == sel ? "> " : "  ") + row.label, 18, c);
                if (!string.IsNullOrEmpty(row.right))
                    UI.Label(new Rect(r.x + 10, r.y + 3, r.width - 20, r.height), row.right, 18, c, TextAnchor.UpperRight);
            }
            if (scroll > 0) UI.Label(new Rect(left.xMax - 40, listTop - 22, 30, 20), "^", 16, UI.DimColor);
            if (scroll + visible < rows.Count) UI.Label(new Rect(left.xMax - 40, left.yMax - 22, 30, 20), "v", 16, UI.DimColor);

            float y = right.y + 14f;
            if (header != null)
            {
                string h = header();
                UI.Label(new Rect(right.x + 16, y, right.width - 32, 150), h, 17, UI.GoldColor);
                y += 24f + 21f * CountLines(h);
            }
            if (sel < rows.Count && !string.IsNullOrEmpty(rows[sel].desc))
                UI.Label(new Rect(right.x + 16, y, right.width - 32, right.yMax - y - 10), rows[sel].desc, 18, UI.TextColor);

            UI.Label(new Rect(40, UI.H - 40, UI.W - 80, 30),
                "Move: W/S or stick     Select: Enter / Space / (A)" + (cancellable ? "     Back: Esc / (B)" : ""), 15, UI.DimColor);
        }

        static int CountLines(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int n = 1;
            for (int i = 0; i < s.Length; i++)
                if (s[i] == '\n') n++;
            return n;
        }
    }

    /// <summary>A text box at the bottom of the screen that shows one line at a time.</summary>
    public class MessageScreen : GameScreen
    {
        readonly List<string> lines;
        readonly Action onDone;
        int index;

        public override bool DrawBelow { get { return true; } }

        public MessageScreen(List<string> lines, Action onDone)
        {
            this.lines = lines;
            this.onDone = onDone;
        }

        public override void Update()
        {
            if (GameInput.ConfirmDown || GameInput.CancelDown)
            {
                GameInput.Consume();
                index++;
                if (index >= lines.Count)
                {
                    Close();
                    if (onDone != null) onDone();
                }
            }
        }

        public override void Draw()
        {
            if (index >= lines.Count) return;
            var r = new Rect(UI.W * 0.12f, UI.H - 170f, UI.W * 0.76f, 130f);
            UI.Panel(r);
            UI.Label(new Rect(r.x + 20, r.y + 16, r.width - 40, r.height - 40), lines[index], 20, UI.TextColor);
            UI.Label(new Rect(r.x + 20, r.yMax - 28, r.width - 40, 22),
                (index + 1) + "/" + lines.Count + "   Enter / (A)", 14, UI.DimColor, TextAnchor.UpperRight);
        }
    }
}
