using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Coroutine;
using GameHelper;
using GameHelper.CoroutineEvents;
using GameHelper.Plugin;
using GameHelper.RemoteEnums;
using GameHelper.RemoteEnums.Entity;
using GameHelper.RemoteObjects.Components;
using GameHelper.RemoteObjects.States.InGameStateObjects;
using GameHelper.Utils;
using ImGuiNET;
using Newtonsoft.Json;

namespace DPSMeter
{
    public sealed class DPSMeter : PCore<DPSSettings>
    {
        private readonly Dictionary<uint, int> lastHpEs = new();
        private readonly HashSet<uint> seenThisFrame = new();
        private readonly Queue<(float time, int dmg)> window = new();
        private ActiveCoroutine areaResetCo;

        private float sessionDamage;
        private float areaDamage;
        private float maxRollingDps;

        private float _timeNow = 0f;
        private float _pluginStart = 0f;
        private float _areaStart = 0f;

        private string SettingsPath => Path.Join(this.DllDirectory, "config", "DPSMeter.settings.json");

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public override void OnEnable(bool isGameOpened)
        {
            LoadSettingsSafe();
            areaResetCo = CoroutineHandler.Start(OnAreaChanged(), "", 0);

            _timeNow = 0f;
            _pluginStart = 0f;
            _areaStart = 0f;

            sessionDamage = 0f;
            areaDamage = 0f;
            maxRollingDps = 0f;

            lastHpEs.Clear();
            window.Clear();
            seenThisFrame.Clear();
        }

        public override void OnDisable()
        {
            if (areaResetCo != null)
            {
                areaResetCo.Cancel();
                areaResetCo = null;
            }
            lastHpEs.Clear();
            window.Clear();
            seenThisFrame.Clear();
        }

        public override void SaveSettings()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(this.Settings, Formatting.Indented));
        }

        private void LoadSettingsSafe()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var txt = File.ReadAllText(SettingsPath);
                    this.Settings = JsonConvert.DeserializeObject<DPSSettings>(txt) ?? new DPSSettings();
                }
                else this.Settings = new DPSSettings();
            }
            catch
            {
                this.Settings = new DPSSettings();
            }
        }

        public override void DrawSettings()
        {
            if (ImGui.CollapsingHeader("General"))
            {
                if (ImGui.BeginTable("dps_general", 2))
                {
                    ImGui.TableNextColumn(); ImGui.Checkbox("Draw when game is backgrounded", ref Settings.DrawWhenGameInBackground);
                    ImGui.TableNextColumn(); ImGui.Checkbox("Only include Rare/Unique", ref Settings.OnlyRareUnique);

                    ImGui.TableNextColumn(); ImGui.SetNextItemWidth(220);
                    ImGui.DragFloat("Screen Range (px)", ref Settings.ScreenRangePx, 5f, 100f, 4000f);

                    ImGui.TableNextColumn(); ImGui.SetNextItemWidth(220);
                    ImGui.DragFloat("Rolling Window (s)", ref Settings.RollingWindowSeconds, 0.25f, 1f, 60f);

                    ImGui.TableNextColumn(); ImGui.SetNextItemWidth(220);
                    ImGui.DragFloat("Idle Reset (s) (0=never)", ref Settings.IdleResetSeconds, 0.25f, 0f, 60f);

                    ImGui.TableNextColumn(); ImGui.SetNextItemWidth(220);
                    ImGui.DragFloat("Damage Floor (ignore tiny ticks)", ref Settings.MinDamageSample, 1f, 0f, 5000f);

                    ImGui.EndTable();
                }
            }

            if (ImGui.CollapsingHeader("Display"))
            {
                if (ImGui.BeginTable("dps_display", 2))
                {
                    ImGui.TableNextColumn(); ImGui.Checkbox("Show Rolling DPS", ref Settings.ShowRolling);
                    ImGui.TableNextColumn(); ImGui.Checkbox("Show Max DPS", ref Settings.ShowMax);

                    ImGui.TableNextColumn(); ImGui.Checkbox("Show Session DPS", ref Settings.ShowSession);
                    ImGui.TableNextColumn(); ImGui.Checkbox("Show Area DPS", ref Settings.ShowArea);

                    ImGui.TableNextColumn(); ImGui.Checkbox("Humanize (K/M)", ref Settings.HumanizeNumbers);
                    ImGui.TableNextColumn(); ImGui.Checkbox("Show Sparkline", ref Settings.ShowSparkline);

                    ImGui.TableNextColumn(); ImGui.SetNextItemWidth(220);
                    ImGui.SliderFloat("Big Number Scale", ref Settings.BigNumberScale, 0.8f, 2.0f);

                    ImGui.TableNextColumn(); ImGui.SetNextItemWidth(220);
                    ImGui.DragFloat2("Anchor (x,y)", ref Settings.Anchor, 1f, -4000, 4000);

                    ImGui.TableNextColumn(); ImGui.SetNextItemWidth(220);
                    ImGui.DragFloat("Min Panel Width (px)", ref Settings.PanelWidth, 1f, 160f, 1600f);

                    ImGui.TableNextColumn(); ImGui.SetNextItemWidth(220);
                    ImGui.DragFloat2("Panel Padding (x,y)", ref Settings.PanelPadding, 0.5f, 0f, 40f);

                    ImGui.TableNextColumn(); ImGui.SetNextItemWidth(220);
                    ImGui.DragFloat("Corner Radius", ref Settings.CornerRadius, 0.5f, 0f, 24f);

                    ImGui.TableNextColumn(); ImGui.ColorEdit4("Panel BG", ref Settings.PanelBg);
                    ImGui.TableNextColumn(); ImGui.ColorEdit4("Panel Border", ref Settings.PanelBorder);

                    ImGui.TableNextColumn(); ImGui.ColorEdit4("Header Color", ref Settings.HeaderColor);
                    ImGui.TableNextColumn(); ImGui.ColorEdit4("Value Color", ref Settings.ValueColor);

                    ImGui.TableNextColumn(); ImGui.ColorEdit4("Accent", ref Settings.AccentColor);
                    ImGui.TableNextColumn(); ImGui.SliderFloat("Accent Glow", ref Settings.AccentGlow, 0f, 1f);

                    ImGui.TableNextColumn(); ImGui.SliderFloat("Border Thickness", ref Settings.BorderThickness, 0.5f, 4f);
                    ImGui.TableNextColumn(); ImGui.SliderFloat("Shadow Alpha", ref Settings.ShadowAlpha, 0f, 1f);

                    ImGui.TableNextColumn(); ImGui.SliderFloat("Progress Height", ref Settings.ProgressHeight, 6f, 24f);
                    ImGui.TableNextColumn(); ImGui.ColorEdit4("Progress Fill", ref Settings.ProgressFill);

                    ImGui.TableNextColumn(); ImGui.ColorEdit4("Progress BG", ref Settings.ProgressBg);
                    ImGui.TableNextColumn(); ImGui.SliderFloat("Spark Fill Alpha", ref Settings.SparkFillAlpha, 0f, 0.6f);

                    ImGui.EndTable();
                }
            }

            if (ImGui.Button("Reset Session"))
            {
                sessionDamage = 0;
                maxRollingDps = 0;
                window.Clear();
                _pluginStart = _timeNow;
            }
        }

        public override void DrawUI()
        {
            if (Core.States.GameCurrentState != GameStateTypes.InGameState && Core.States.GameCurrentState != GameStateTypes.EscapeState) return;

            var inGame = Core.States.InGameStateObject;
            var world = inGame.CurrentWorldInstance;
            var area = inGame.CurrentAreaInstance;

            if (!Settings.DrawWhenGameInBackground && !Core.Process.Foreground) return;
            if (world.AreaDetails.IsTown || world.AreaDetails.IsHideout) return;
            if (inGame.GameUi.SkillTreeNodesUiElements.Count > 0) return;

            float dt = MathF.Max(0.0001f, ImGui.GetIO().DeltaTime);
            _timeNow += dt;
            if (_pluginStart <= 0f) _pluginStart = _timeNow;
            if (_areaStart <= 0f) _areaStart = _timeNow;

            int frameDamage = 0;
            seenThisFrame.Clear();

            var center = new Vector2(Core.Overlay.Size.Width / 2f, Core.Overlay.Size.Height / 2f);

            foreach (var kv in area.AwakeEntities)
            {
                var e = kv.Value;
                if (!e.IsValid || e.EntityState == EntityStates.PinnacleBossHidden || e.EntityState == EntityStates.Useless)
                    continue;
                if (e.EntityType != EntityTypes.Monster) continue;

                if (Settings.OnlyRareUnique)
                {
                    ObjectMagicProperties omp;
                    if (e.TryGetComponent<ObjectMagicProperties>(out omp, true))
                    {
                        if (omp.Rarity != Rarity.Rare && omp.Rarity != Rarity.Unique)
                            continue;
                    }
                }

                Render r;
                if (!e.TryGetComponent<Render>(out r, true)) continue;

                var pos = r.WorldPosition;
                pos.Z -= r.ModelBounds.Z;
                var scr = world.WorldToScreen(pos, pos.Z);

                if (Vector2.Distance(scr, center) > Settings.ScreenRangePx)
                    continue;

                Life life;
                if (!e.TryGetComponent<Life>(out life, true)) continue;

                int cur = Math.Max(0, life.Health.Current + life.EnergyShield.Current);
                seenThisFrame.Add(e.Id);

                if (lastHpEs.TryGetValue(e.Id, out int prev))
                {
                    int delta = prev - cur;
                    if (delta > Settings.MinDamageSample)
                        frameDamage += delta;

                    lastHpEs[e.Id] = cur;
                }
                else
                {
                    lastHpEs[e.Id] = cur;
                }
            }

            if (lastHpEs.Count > 0)
            {
                tmpIds.Clear();
                foreach (var id in lastHpEs.Keys)
                    if (!seenThisFrame.Contains(id)) tmpIds.Add(id);
                foreach (var id in tmpIds) lastHpEs.Remove(id);
            }

            if (frameDamage > 0)
            {
                sessionDamage += frameDamage;
                areaDamage += frameDamage;
                window.Enqueue((_timeNow, frameDamage));
            }

            while (window.Count > 0 && (_timeNow - window.Peek().time) > Settings.RollingWindowSeconds)
                window.Dequeue();

            float rollingDps = 0f;
            if (window.Count > 0)
            {
                int sum = 0; foreach (var s in window) sum += s.dmg;
                float denom = MathF.Max(0.001f, Settings.RollingWindowSeconds);
                rollingDps = sum / denom;
            }

            if (rollingDps > maxRollingDps) maxRollingDps = rollingDps;

            if (Settings.IdleResetSeconds > 0 && window.Count > 0)
            {
                float lastHit = window.Last().time;
                if ((_timeNow - lastHit) > Settings.IdleResetSeconds)
                    window.Clear();
            }

            DrawPanel(rollingDps);
        }

        private void DrawPanel(float rollingDps)
        {
            var dl = ImGui.GetBackgroundDrawList();

            // ----------- build data -----------
            rows.Clear();
            if (Settings.ShowRolling) rows.Add(("Rolling DPS", rollingDps));
            if (Settings.ShowMax) rows.Add(("Max DPS", maxRollingDps));
            if (Settings.ShowSession) rows.Add(("Session DPS", sessionDamage / MathF.Max(_timeNow - _pluginStart, 0.0001f)));
            if (Settings.ShowArea) rows.Add(("Area DPS", areaDamage / MathF.Max(_timeNow - _areaStart, 0.0001f)));

            float headerStrip = 6f;
            float lineH = ImGui.GetFontSize() * 1.1f;
            string bigText = Settings.HumanizeNumbers ? Humanize(rollingDps) : $"{rollingDps:0}";
            Vector2 bigSz = MeasureTextScaled(bigText, Settings.BigNumberScale);
            float rowsH = rows.Count > 0 ? rows.Count * (lineH + Settings.RowSpacing) - Settings.RowSpacing : 0f;
            bool showProg = (Settings.ShowRolling && Settings.ShowMax && maxRollingDps > 0f);
            float progH = showProg ? (Settings.ProgressHeight + 6f) : 0f;

            // ----------- auto width measurement -----------
            float labelWmax = 0f;
            float valueWmax = bigSz.X; // start with big number width
            for (int i = 0; i < rows.Count; i++)
            {
                var (label, value) = rows[i];
                float lw = ImGui.CalcTextSize(label).X;
                labelWmax = MathF.Max(labelWmax, lw);

                string val = Settings.HumanizeNumbers ? Humanize(value) : $"{value:0}";
                float vw = ImGui.CalcTextSize(val).X;
                valueWmax = MathF.Max(valueWmax, vw);
            }
            // Note: we no longer reserve width for the progress-bar max text (it's not drawn anymore)

            float contentGap = 12f; // gap between left labels and right values
            float minContentW = MathF.Max(labelWmax + contentGap + valueWmax, bigSz.X);
            float requiredW = Settings.PanelPadding.X + minContentW + Settings.PanelPadding.X;
            float panelW = MathF.Max(Settings.PanelWidth, requiredW);

            // ----------- total height -----------
            float totalH = headerStrip + Settings.PanelPadding.Y + bigSz.Y + 6f + progH + rowsH + Settings.PanelPadding.Y;

            // ----------- panel background -----------
            float x = Settings.Anchor.X;
            float y = Settings.Anchor.Y;
            var p0 = new Vector2(x, y);
            var p1 = new Vector2(x + panelW, y + totalH);
            dl.AddRectFilled(p0, p1, ImGuiHelper.Color(Settings.PanelBg), Settings.CornerRadius);
            dl.AddRect(p0, p1, ImGuiHelper.Color(Settings.PanelBorder), Settings.CornerRadius, 0, Settings.BorderThickness);

            // accent strip + glow
            uint accent = ImGuiHelper.Color(Settings.AccentColor);
            dl.AddRectFilled(new Vector2(x, y), new Vector2(x + panelW, y + headerStrip), accent, Settings.CornerRadius);
            if (Settings.AccentGlow > 0f)
            {
                dl.AddRectFilled(new Vector2(x, y + headerStrip), new Vector2(x + panelW, y + headerStrip + 6f),
                    WithAlpha(Settings.AccentColor, Settings.AccentGlow), 0f);
            }

            // content origin and edges
            float contentX = x + Settings.PanelPadding.X;
            float contentRight = x + panelW - Settings.PanelPadding.X;
            float contentY = y + headerStrip + 6f;

            // Big number (centered)
            var bigPos = new Vector2(x + (panelW - bigSz.X) * 0.5f, contentY);
            AddTextShadow(dl, bigPos, Settings.ValueColor, bigText, Settings.ShadowAlpha);

            float cursorY = bigPos.Y + bigSz.Y + 6f;

            // progress bar (Rolling / Max) — no text
            if (showProg)
            {
                float h = Settings.ProgressHeight;
                var bar0 = new Vector2(contentX, cursorY);
                var bar1 = new Vector2(contentRight, cursorY + h);

                dl.AddRectFilled(bar0, bar1, ImGuiHelper.Color(Settings.ProgressBg), h * 0.5f);

                float pct = Clamp(rollingDps / MathF.Max(1e-3f, maxRollingDps), 0f, 1f);
                var fill1 = new Vector2(bar0.X + (bar1.X - bar0.X) * pct, bar1.Y);
                dl.AddRectFilled(bar0, fill1, ImGuiHelper.Color(Settings.ProgressFill), h * 0.5f);

                cursorY += h + 6f;
            }

            // rows (labels left, values right)
            float cy = cursorY;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                dl.AddText(new Vector2(contentX, cy), ImGuiHelper.Color(Settings.HeaderColor), row.label);

                string val = Settings.HumanizeNumbers ? Humanize(row.value) : $"{row.value:0}";
                AddTextRight(dl, new Vector2(contentRight, cy), val, Settings.ValueColor);

                cy += lineH + Settings.RowSpacing;

                if (i < rows.Count - 1)
                    AddSeparator(dl, contentX, cy - (Settings.RowSpacing * 0.5f), panelW - Settings.PanelPadding.X * 2f, new Vector4(1, 1, 1, 0.06f), 1f);
            }

            // sparkline (below the panel)
            if (Settings.ShowSparkline)
            {
                float sh = Settings.SparkHeight;
                float sx = contentX;
                float sw = panelW - Settings.PanelPadding.X * 2f;
                float sy = y + totalH + 6f;

                float span = MathF.Max(0.001f, Settings.RollingWindowSeconds);
                float t0 = _timeNow - span;
                float max = 1f;
                foreach (var s in window) if (s.dmg > max) max = s.dmg;

                var fillPts = new List<Vector2>();
                fillPts.Add(new Vector2(sx, sy + sh));
                Vector2? prev = null;
                foreach (var s in window)
                {
                    float tx = Clamp((s.time - t0) / span, 0f, 1f);
                    float ty = max <= 0 ? 0 : (s.dmg / max);
                    var pt = new Vector2(sx + tx * sw, sy + sh - ty * sh);
                    if (prev != null) dl.AddLine(prev.Value, pt, ImGuiHelper.Color(Settings.ValueColor), 1.5f);
                    fillPts.Add(pt);
                    prev = pt;
                }
                fillPts.Add(new Vector2(sx + sw, sy + sh));

                if (fillPts.Count >= 3)
                {
                    for (int i = 1; i < fillPts.Count - 1; i++)
                    {
                        var a = fillPts[i];
                        var b = new Vector2(fillPts[i].X, sy + sh);
                        var c = new Vector2(fillPts[i + 1].X, sy + sh);
                        var d = fillPts[i + 1];
                        dl.AddQuadFilled(a, d, c, b, WithAlpha(Settings.ValueColor, Settings.SparkFillAlpha));
                    }
                }

                if (window.Count > 0)
                {
                    var last = window.Last();
                    float tx = Clamp((last.time - t0) / span, 0f, 1f);
                    float ty = max <= 0 ? 0 : (last.dmg / max);
                    var dot = new Vector2(sx + tx * sw, sy + sh - ty * sh);
                    dl.AddCircleFilled(dot, 3.5f, ImGuiHelper.Color(Settings.ValueColor));
                    dl.AddCircle(dot, 3.5f, ImGuiHelper.Color(new Vector4(1, 1, 1, 0.9f)));
                }

                dl.AddRect(new Vector2(sx, sy), new Vector2(sx + sw, sy + sh), ImGuiHelper.Color(Settings.PanelBorder));
            }
        }

        private static void AddTextShadow(ImDrawListPtr dl, Vector2 pos, Vector4 color, string text, float shadowAlpha = 0.9f)
        {
            uint sh = ImGuiHelper.Color(new Vector4(0, 0, 0, shadowAlpha));
            dl.AddText(pos + new Vector2(1, 0), sh, text);
            dl.AddText(pos + new Vector2(-1, 0), sh, text);
            dl.AddText(pos + new Vector2(0, 1), sh, text);
            dl.AddText(pos + new Vector2(0, -1), sh, text);
            dl.AddText(pos, ImGuiHelper.Color(color), text);
        }

        private static Vector2 MeasureTextScaled(string text, float scale)
        {
            ImGui.SetWindowFontScale(scale);
            var sz = ImGui.CalcTextSize(text);
            ImGui.SetWindowFontScale(1f);
            return sz;
        }

        private static float Snap(float v) => (float)Math.Floor(v) + 0.5f;

        private static void AddSeparator(ImDrawListPtr dl, float x, float y, float w, Vector4 color, float thickness = 1f)
        {
            y = Snap(y);
            dl.AddLine(new Vector2(x, y), new Vector2(x + w, y), ImGuiHelper.Color(color), thickness);
        }

        private static void AddTextRight(ImDrawListPtr dl, Vector2 rightEdge, string text, Vector4 color)
        {
            var sz = ImGui.CalcTextSize(text);
            var pos = new Vector2(rightEdge.X - sz.X, rightEdge.Y);
            dl.AddText(pos, ImGuiHelper.Color(color), text);
        }

        private static uint WithAlpha(Vector4 c, float aMul)
        {
            return ImGuiHelper.Color(new Vector4(c.X, c.Y, c.Z, MathF.Max(0f, MathF.Min(1f, c.W * aMul))));
        }

        private static string Humanize(float v)
        {
            if (v >= 1_000_000f) return $"{v / 1_000_000f:0.##}M";
            if (v >= 1_000f) return $"{v / 1_000f:0.#}K";
            return $"{v:0}";
        }

        private IEnumerator<Wait> OnAreaChanged()
        {
            for (; ; )
            {
                yield return new Wait(RemoteEvents.AreaChanged);
                areaDamage = 0;
                window.Clear();
                lastHpEs.Clear();
                _areaStart = _timeNow;
            }
        }

        private static readonly List<(string label, float value)> rows = new();
        private static readonly List<uint> tmpIds = new();
    }
}
