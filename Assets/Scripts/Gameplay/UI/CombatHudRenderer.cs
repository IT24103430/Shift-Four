using System.Collections.Generic;
using UnityEngine;

namespace ShiftFour
{
    // Uses the project's existing IMGUI module. No new UI package, scene installer or runtime art assets.
    internal sealed class CombatHudRenderer
    {
        private string nameDraft = string.Empty;
        private static readonly Color Ink = new Color(.035f, .055f, .07f, .84f);
        private static readonly Color White = new Color(.92f, .95f, .94f);
        private static readonly Color Muted = new Color(.53f, .64f, .67f);
        private static readonly Color Cyan = new Color(.42f, .84f, .79f);
        private static readonly Color Red = new Color(.96f, .29f, .29f);
        private static readonly Vector2[] CrosshairDirections = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

        public void Draw(GameSession session, PlayerController player, Health health, List<CombatHud.DroneBar> drones,
            float fraction, float trail, bool recovering, float hit, float defeat, float flash, float? bearing, float recoil)
        {
            if (session.Ending != null && session.Ending.IsPlaying) return;
            if (session.Intro != null && session.Intro.IsPlaying) return;
            var previous = GUI.matrix; var previousColor = GUI.color; int previousDepth = GUI.depth;
            float scale = Mathf.Max(.1f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
            float w = Screen.width / scale, h = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale); GUI.depth = -10;
            try
            {
                if (session.Phase != GameSession.LaunchPhase.Playing && !session.Ended)
                {
                    DrawLaunch(session, w, h);
                    return;
                }
                if (!session.Ended)
                {
                    DamageEdges(w, h, flash);
                    foreach (var bar in drones)
                        if (bar.Visible && bar.Health != null && bar.Health.IsAlive) DrawDroneBar(bar, scale, w, h);
                    if (!session.IsPlanting) DrawCrosshair(new Vector2(w / 2, h / 2), hit, defeat, recoil);
                    if (bearing.HasValue) DrawBearing(new Vector2(w / 2, h / 2), bearing.Value, flash);
                    if (!session.IsPlanting) DrawAmmoCue(player.Weapons, w, h);
                }
                DrawMission(session, w);
                DrawVitals(session, health, h, fraction, trail, recovering, flash);
                if (session.ShowGo)
                    Label(new Rect(w / 2 - 130, h / 2 - 75, 260, 100), "GO", 72, Cyan, TextAnchor.MiddleCenter);
                if (session.ShowDecisions) DrawDebug(session, drones, w);
                if (!session.Ended)
                {
                    string prompt = !string.IsNullOrEmpty(session.Notice) ? session.Notice : player.InteractionPrompt;
                    if (!session.IsPlanting && !string.IsNullOrEmpty(prompt))
                    {
                        float width = Mathf.Min(540, w - 740);
                        Panel(new Rect((w - width) / 2, h - 100, width, 54));
                        Label(new Rect((w - width) / 2 + 14, h - 91, width - 28, 40), prompt, 13, White, TextAnchor.MiddleCenter);
                    }
                    if (!session.IsPlanting) DrawWeapon(player.Weapons, w, h);
                }
                else
                {
                    Fill(new Rect(0, 0, w, h), new Color(.015f, .025f, .035f, .55f));
                    var box = new Rect(w / 2 - 270, h / 2 - 104, 540, 208);
                    Panel(box); Fill(new Rect(box.x, box.y, 540, 3), session.Won ? Cyan : Red);
                    Label(new Rect(box.x + 28, box.y + 22, 484, 35), session.Won ? "SHIFT COMPLETE" :
                        health.IsAlive ? "MISSION FAILED" : "YOU DIED", 27, White);
                    string detail = session.Won ? "Bomb planted. All four sectors clear." :
                        session.Outcome.Replace("\nPress R to restart", string.Empty);
                    Label(new Rect(box.x + 28, box.y + 70, 484, 78), detail, 14, Muted);
                    DrawRestart(session, new Rect(box.x + 28, box.y + 154, 200, 34), session.Won ? Cyan : Red);
                }
            }
            finally { GUI.matrix = previous; GUI.color = previousColor; GUI.depth = previousDepth; }
        }
        private void DrawLaunch(GameSession session, float w, float h)
        {
            Fill(new Rect(0, 0, w, h), new Color(.015f, .025f, .035f, .82f));
            if (session.Phase == GameSession.LaunchPhase.Welcome)
            {
                var welcome = new Rect(w / 2 - 300, h / 2 - 170, 600, 340);
                Panel(welcome);
                Fill(new Rect(welcome.x, welcome.y, welcome.width, 3), Cyan);
                Label(new Rect(welcome.x + 30, welcome.y + 38, 540, 54), "SHIFT-FOUR", 40, White,
                    TextAnchor.MiddleCenter);
                Label(new Rect(welcome.x + 30, welcome.y + 99, 540, 26), "GROUP 26  /  TACTICAL LAB ESCAPE", 14,
                    Cyan, TextAnchor.MiddleCenter);
                Label(new Rect(welcome.x + 65, welcome.y + 146, 470, 56),
                    "Plant the bomb in Checkpoint. Clear four sectors. Escape before detonation.", 16, Muted, TextAnchor.MiddleCenter);
                if (GUI.Button(new Rect(welcome.x + 110, welcome.y + 233, 380, 48), "NEW GAME"))
                    session.OpenNameEntry();
                Label(new Rect(welcome.x + 50, welcome.y + 296, 500, 20),
                    "WASD MOVE  /  MOUSE AIM  /  E INTERACT", 11, Muted, TextAnchor.MiddleCenter);
                return;
            }
            var box = new Rect(w / 2 - 260, h / 2 - 130, 520, 260);
            Panel(box);
            Fill(new Rect(box.x, box.y, box.width, 3), Cyan);
            if (session.Phase == GameSession.LaunchPhase.NameEntry)
            {
                Label(new Rect(box.x + 28, box.y + 20, 464, 36), "NEW GAME", 27, White);
                Label(new Rect(box.x + 28, box.y + 64, 464, 24), "ENTER YOUR NAME", 12, Cyan);
                nameDraft = GUI.TextField(new Rect(box.x + 28, box.y + 94, 464, 35), nameDraft, 18);
                if (GUI.Button(new Rect(box.x + 28, box.y + 148, 464, 42), "START MISSION") ||
                    (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return))
                    session.Begin(nameDraft);
                if (GUI.Button(new Rect(box.x + 28, box.y + 203, 464, 34), "PLAY SHOWCASE"))
                    session.BeginShowcase(nameDraft);
            }
            else
            {
                Label(new Rect(box.x + 28, box.y + 26, 464, 25), "GET READY, " + session.PlayerName.ToUpperInvariant(), 13, Cyan,
                    TextAnchor.MiddleCenter);
                Label(new Rect(box.x + 28, box.y + 63, 464, 110), Mathf.CeilToInt(session.CountdownRemaining).ToString(),
                    76, White, TextAnchor.MiddleCenter);
                Label(new Rect(box.x + 28, box.y + 184, 464, 20), "MOVEMENT AND SECURITY ACTIVATE AT GO", 10, Muted,
                    TextAnchor.MiddleCenter);
            }
        }
        public static Rect RestartButtonBounds(int width, int height)
        {
            float scale = Mathf.Max(.1f, Mathf.Min(width / 1280f, height / 720f));
            return new Rect(width / 2f - 242 * scale, height / 2f + 50 * scale, 200 * scale, 34 * scale);
        }
        private static void DrawRestart(GameSession session, Rect rect, Color accent)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Fill(rect, hover ? new Color(.17f, .26f, .28f, 1) : new Color(.10f, .16f, .18f, 1));
            Fill(new Rect(rect.x, rect.y, 3, rect.height), accent);
            Label(rect, "RESTART   [R]", 12, White, TextAnchor.MiddleCenter);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) session.Restart();
        }
        private static void DrawWeapon(WeaponController weapons, float w, float h)
        {
            var state = weapons != null ? weapons.ActiveWeapon : null;
            if (state == null) return;
            Panel(new Rect(w - 350, h - 177, 318, 145));
            bool reload = state.ReloadState == WeaponReloadState.Reloading;
            bool empty = state.RoundsInMagazine == 0;
            Color color = empty || weapons.DryFireStrength > 0 ? Red : White;
            Label(new Rect(w - 334, h - 167, 286, 22), state.Definition.DisplayName.ToUpperInvariant(), 13, Muted, TextAnchor.MiddleRight);
            Label(new Rect(w - 334, h - 143, 230, 40), state.RoundsInMagazine.ToString("00"), 32, color, TextAnchor.MiddleRight);
            Label(new Rect(w - 96, h - 128, 48, 24), "/ " + state.ReserveRounds.ToString("00"), 16, Muted, TextAnchor.MiddleRight);
            string status = reload ? "RELOADING" : empty ? state.ReserveRounds > 0 ? "EMPTY  /  R TO RELOAD" : "OUT OF AMMO" :
                state.RoundsInMagazine <= Mathf.Max(1, state.MagazineCapacity / 4) && state.ReserveRounds > 0 ? "LOW AMMO  /  R TO RELOAD" : "R  RELOAD";
            Label(new Rect(w - 334, h - 104, 286, 20), status, 10, reload ? Cyan : empty ? Red : Muted, TextAnchor.MiddleRight);
            Fill(new Rect(w - 334, h - 80, 286, 3), new Color(.16f, .22f, .24f));
            Fill(new Rect(w - 334, h - 80, 286 * (reload ? state.ReloadProgress : (float)state.RoundsInMagazine / state.MagazineCapacity), 3), reload ? Cyan : color);
            Label(new Rect(w - 334, h - 70, 286, 16), "WASD MOVE   SPACE JUMP   E INTERACT", 9, Muted, TextAnchor.MiddleRight);
            Label(new Rect(w - 334, h - 51, 286, 16), "LMB FIRE   F3 DETAILS   ESC CURSOR", 9, Muted, TextAnchor.MiddleRight);
        }
        private static void DrawAmmoCue(WeaponController weapons, float w, float h)
        {
            var state = weapons != null ? weapons.ActiveWeapon : null;
            if (state == null) return;
            bool reloading = state.ReloadState == WeaponReloadState.Reloading;
            string message = reloading ? "RELOADING" : state.RoundsInMagazine == 0 ?
                state.ReserveRounds == 0 ? "OUT OF AMMO" : "EMPTY" :
                state.RoundsInMagazine <= 2 ? "LOW AMMO" : string.Empty;
            if (message.Length == 0) return;
            var box = new Rect(w / 2 - 92, h / 2 + 46, 184, reloading ? 38 : 29);
            Panel(box);
            Color accent = reloading ? Cyan : Red;
            Fill(new Rect(box.x, box.y, 3, box.height), accent);
            Label(new Rect(box.x + 8, box.y + 2, box.width - 16, 25), message, 12, accent, TextAnchor.MiddleCenter);
            if (reloading)
                Fill(new Rect(box.x + 9, box.y + 31, (box.width - 18) * state.ReloadProgress, 2), Cyan);
        }
        private void DrawMission(GameSession session, float w)
        {
            int room = Mathf.Clamp(session.CurrentRoom, 0, 3);
            Panel(new Rect(32, 28, 362, 109));
            Fill(new Rect(32, 28, 3, 109), Cyan);
            Label(new Rect(49, 37, 200, 20), "S H I F T  F O U R", 11, Cyan);
            Label(new Rect(258, 37, 118, 20), "SECTOR 0" + (room + 1) + " / 04", 10, Muted, TextAnchor.MiddleRight);
            Label(new Rect(49, 60, 325, 30), MissionProgress.Rooms[room].ToUpperInvariant(), 23, White);
            int taskRoom = session.TaskRoom;
            string next = taskRoom == 4 ? "All rooms clear. Reach the exit." :
                taskRoom == 0 && !session.BombPlanted ? "Plant the time bomb in Checkpoint (Room 1)." :
                taskRoom != room ? "Continue to " + MissionProgress.Rooms[taskRoom] + "." :
                !session.Progress.DroneDefeated(taskRoom) ? "Disable the " + MissionProgress.Agents[taskRoom] + " drone." :
                "Activate the room console.";
            Label(new Rect(49, 96, 325, 26), next, 12, Muted);

            if (session.MissionTimerActive)
            {
                float seconds = session.BombRemaining;
                int whole = Mathf.CeilToInt(seconds);
                Panel(new Rect(w / 2 - 110, 28, 220, 68));
                Label(new Rect(w / 2 - 94, 35, 188, 18), "DETONATION / REACH EXIT", 10, seconds <= 30 ? Red : Cyan,
                    TextAnchor.MiddleCenter);
                Label(new Rect(w / 2 - 94, 54, 188, 34), (whole / 60).ToString("00") + ":" + (whole % 60).ToString("00"),
                    28, seconds <= 30 ? Red : White, TextAnchor.MiddleCenter);
            }

            if (!session.Ended) DrawRoomTasks(session, w);
        }

        private static void DrawRoomTasks(GameSession session, float w)
        {
            float x = w - 338;
            int room = session.TaskRoom;
            Panel(new Rect(x, 28, 306, room == 0 ? 152 : 124));
            Fill(new Rect(x, 28, 3, room == 0 ? 152 : 124), Cyan);
            Label(new Rect(x + 17, 38, 272, 19), room < 4 ?
                "ROOM 0" + (room + 1) + " / 04  TASKS" : "FINAL TASK", 10, Cyan);
            Label(new Rect(x + 17, 60, 272, 26), room < 4 ?
                MissionProgress.Rooms[room].ToUpperInvariant() : "EXTRACTION", 19, White);
            if (room == 4)
            {
                DrawTask(x + 18, 96, 266, session.Won, true, "Reach the exit before detonation");
                return;
            }

            float y = 94;
            if (room == 0)
            {
                DrawTask(x + 18, y, 266, session.BombPlanted, true,
                    session.IsPlanting ? "Planting time bomb..." : "Plant the time bomb");
                y += 27;
            }
            DrawTask(x + 18, y, 266, session.Progress.DroneDefeated(room),
                room != 0 || session.BombPlanted, "Disable the " + MissionProgress.Agents[room] + " drone");
            DrawTask(x + 18, y + 27, 266, session.Progress.ConsoleOnline(room),
                session.Progress.DroneDefeated(room), "Activate the room console");
        }

        private static void DrawTask(float x, float y, float width, bool complete, bool active, string text)
        {
            Color color = complete ? Cyan : active ? White : Muted;
            var marker = new Rect(x, y + 5, 10, 10);
            Fill(marker, color);
            if (!complete) Fill(new Rect(marker.x + 1, marker.y + 1, 8, 8), Ink);
            Label(new Rect(x + 21, y, width - 21, 23), (complete ? "Done: " : "") + text, 12, color);
        }

        private void DrawVitals(GameSession session, Health health, float h, float fraction, float trail, bool recovering, float flash)
        {
            if (!health.IsAlive) fraction = trail = 0;
            bool critical = health.Current / Mathf.Max(1, health.Maximum) <= .35f;
            Color color = critical ? Red : Color.Lerp(Cyan, Red, flash);
            Panel(new Rect(32, h - 137, 282, 105));
            Fill(new Rect(32, h - 137, 3, 105), color);
            Label(new Rect(49, h - 128, 120, 18), session.PlayerName.ToUpperInvariant(), 10, Muted);
            string status = !health.IsAlive ? "OFFLINE" : recovering ? "RECOVERING" : critical ? "CRITICAL" : "STABLE";
            Label(new Rect(169, h - 128, 126, 18), status, 9, recovering ? Cyan : critical ? Red : Muted, TextAnchor.MiddleRight);
            Label(new Rect(47, h - 110, 110, 50), Mathf.CeilToInt(health.Current).ToString(), 38, White);
            Label(new Rect(153, h - 93, 143, 22), "/ " + Mathf.CeilToInt(health.Maximum) + " HP", 13, Muted);
            Fill(new Rect(49, h - 54, 248, 7), new Color(.16f, .22f, .24f));
            Fill(new Rect(49, h - 54, 248 * Mathf.Clamp01(trail), 7), new Color(.8f, .55f, .29f));
            Fill(new Rect(49, h - 54, 248 * Mathf.Clamp01(fraction), 7), color);
            for (int i = 1; i < 10; i++) Fill(new Rect(49 + i * 24.8f, h - 54, 2, 7), Ink);
            if (recovering)
                Fill(new Rect(49, h - 57, 248 * Mathf.Clamp01(fraction), 2), new Color(.68f, 1, .87f, .4f + .2f * Mathf.Sin(Time.unscaledTime * 5)));
        }
        private void DrawDroneBar(CombatHud.DroneBar bar, float scale, float w, float h)
        {
            Vector2 p = bar.ScreenPoint / scale;
            float x = Mathf.Clamp(p.x - 70, 20, w - 160), y = Mathf.Clamp(p.y - 20, 4, h - 34);
            Color background = Ink; background.a *= bar.Alpha;
            Fill(new Rect(x - 7, y - 3, 154, 30), background);
            Label(new Rect(x, y, 140, 16), bar.Label + "   " + Mathf.CeilToInt(bar.Health.Current) + " / " + Mathf.CeilToInt(bar.Health.Maximum), 10,
                new Color(White.r, White.g, White.b, bar.Alpha));
            Fill(new Rect(x, y + 19, 140, 4), new Color(.22f, .12f, .14f, bar.Alpha));
            Fill(new Rect(x, y + 19, 140 * Mathf.Clamp01(bar.Trail), 4), new Color(.9f, .56f, .41f, bar.Alpha));
            Fill(new Rect(x, y + 19, 140 * Mathf.Clamp01(bar.Fraction), 4), new Color(Red.r, Red.g, Red.b, bar.Alpha));
        }
        private static void DrawCrosshair(Vector2 center, float hit, float defeat, float recoil)
        {
            float gap = 4 + recoil * 3, length = 5;
            foreach (var direction in CrosshairDirections)
            {
                Line(center + direction * (gap - 1), center + direction * (gap + length + 1), new Color(0, 0, 0, .7f), 3);
                Line(center + direction * gap, center + direction * (gap + length), new Color(.9f, .95f, .94f, .88f), 1.4f);
            }
            Fill(new Rect(center.x - 1, center.y - 1, 2, 2), White);
            float alpha = Mathf.Max(hit, defeat);
            if (alpha <= 0) return;
            Color color = defeat > 0 ? Red : White; color.a = alpha;
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2)
                Line(center + new Vector2(x, y) * 6, center + new Vector2(x, y) * (defeat > 0 ? 14 : 11), color, defeat > 0 ? 2.4f : 1.8f);
            if (defeat > 0) Label(new Rect(center.x - 110, center.y + 26, 220, 20), "DRONE DISABLED", 11, color, TextAnchor.MiddleCenter);
        }
        private static void DamageEdges(float w, float h, float strength)
        {
            if (strength <= 0) return;
            for (int i = 0; i < 14; i++)
            {
                float opacity = .22f * strength * Mathf.Pow(1 - i / 14f, 1.5f);
                var color = new Color(.85f, .08f, .10f, opacity);
                Fill(new Rect(i * 4, 0, 4, h), color); Fill(new Rect(w - (i + 1) * 4, 0, 4, h), color);
                Fill(new Rect(0, i * 3, w, 3), color); Fill(new Rect(0, h - (i + 1) * 3, w, 3), color);
            }
        }
        private static void DrawBearing(Vector2 center, float bearing, float flash)
        {
            var previous = GUI.matrix;
            GUI.matrix = previous * Matrix4x4.Translate(center) *
                Matrix4x4.Rotate(Quaternion.Euler(0, 0, bearing)) * Matrix4x4.Translate(-center);
            var color = Red; color.a = Mathf.Max(.25f, flash) * .85f;
            Line(center + new Vector2(-9, -69), center + new Vector2(0, -77), color, 3);
            Line(center + new Vector2(0, -77), center + new Vector2(9, -69), color, 3);
            GUI.matrix = previous;
        }
        private static void DrawDebug(GameSession session, List<CombatHud.DroneBar> drones, float w)
        {
            Panel(new Rect(w - 394, 155, 362, 214));
            Label(new Rect(w - 378, 167, 330, 20), "AGENT / GATE DETAILS   [F3]", 10, Cyan);
            for (int i = 0; i < drones.Count; i++)
            {
                var drone = drones[i];
                if (drone.Brain == null || drone.Health == null) continue;
                Label(new Rect(w - 378, 194 + i * 38, 330, 38), drone.Label + " / " + (drone.Health.IsAlive ? drone.Brain.CurrentDecision : "Disabled"), 10, White);
            }
            Label(new Rect(w - 378, 345, 330, 18), "CURRENT GATE: " + session.GateStatus(session.CurrentRoom), 10, Muted);
        }
        private static void Panel(Rect rect)
        {
            Fill(rect, Ink);
            Fill(new Rect(rect.x, rect.y, rect.width, 1), new Color(.45f, .59f, .62f, .25f));
        }
        private static void Fill(Rect rect, Color color)
        {
            var previous = GUI.color; GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = previous;
        }
        private static void Line(Vector2 a, Vector2 b, Color color, float width)
        {
            var previous = GUI.matrix;
            float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            // Compose in design coordinates so outer HUD scaling also scales the rotation pivot.
            GUI.matrix = previous * Matrix4x4.TRS(a, Quaternion.Euler(0, 0, angle), Vector3.one);
            Fill(new Rect(0, -width * .5f, Vector2.Distance(a, b), width), color);
            GUI.matrix = previous;
        }
        private static readonly Dictionary<int, GUIStyle> LabelStyles = new Dictionary<int, GUIStyle>();
        private static void Label(Rect rect, string value, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            int key = size * 16 + (int)anchor;
            if (!LabelStyles.TryGetValue(key, out var style))
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = anchor,
                    fontStyle = size >= 20 ? FontStyle.Bold : FontStyle.Normal, wordWrap = true, padding = new RectOffset() };
                LabelStyles.Add(key, style);
            }
            var previous = GUI.color; GUI.color = color;
            style.normal.textColor = Color.white; GUI.Label(rect, value, style); GUI.color = previous;
        }
    }
}
