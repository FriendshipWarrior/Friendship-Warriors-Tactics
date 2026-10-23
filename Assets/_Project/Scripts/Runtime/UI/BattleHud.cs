using System;
using System.Collections.Generic;
using FriendshipWarriors.Battle.Combat;
using FriendshipWarriors.Units;
using TMPro;
using UnityEngine;

namespace FriendshipWarriors.UI
{
    /// <summary>
    /// The battle HUD, built in code on a Screen Space Overlay canvas in the pixel-art skin (<see cref="UiSkin"/>):
    /// the objective (top left), a banner (top centre), the turn order (top right), the command menu (left), unit
    /// details with HP/MP/CT bars (bottom right), the timed-press bar (bottom centre) and a controls hint (bottom).
    /// Laid out in logical pixels on a canvas of at least 640x360, scaled by whole numbers (<see cref="PixelUiScale"/>).
    /// Menu navigation comes from the battle flow's own input handling; the mouse can hover and click items directly.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class BattleHud : MonoBehaviour
    {
        private const int TurnOrderRows = 8;
        private const int MenuRowHeight = 16;
        private const int UnitPanelWidth = 324;
        // Name, level and the three bars; wide enough for the gauges row.
        private const int CompactPanelWidth = 260;
        // The job in the compact panel: a soft lavender from the Mana Soul palette, apart from the white name and level.
        private const string JobColor = "#C9A7FF";
        private const int BarWidth = 72;

        private RectTransform _menuPanel;
        private TextMeshProUGUI _menuTitle;
        private MenuList _menuList;
        private MenuModel _menu;

        private readonly List<(UnityEngine.UI.Image team, TextMeshProUGUI label)> _turnRows = new();
        private RectTransform _turnPanel;
        private bool _detailsExpanded;
        private Unit _shownUnit;
        private RectTransform _unitPanel;
        private TextMeshProUGUI _unitName;
        private TextMeshProUGUI _unitJob;
        private TextMeshProUGUI _unitInfo;
        private (UiBar bar, TextMeshProUGUI value, string label) _hp, _mp, _ct;
        private TextMeshProUGUI _hint;
        private RectTransform _hintPanel;
        private TextMeshProUGUI _objective;

        private RectTransform _timingPanel;
        private TextMeshProUGUI _timingTitle;
        private TextMeshProUGUI _timingFeedback;
        private UiBar _timingBar;
        private RectTransform _timingZone;
        private RectTransform _timingMarker;
        private TimedPressRules _timingRules;
        private float _timingStart;
        private Battle.BattleClock _clock;
        private bool _timingRunning;

        private RectTransform _bannerPanel;
        private TextMeshProUGUI _banner;
        private float _bannerHideTime;

        /// <summary>The mouse clicked an enabled menu item.</summary>
        public event Action<int> MenuItemClicked;

        private void Awake()
        {
            PixelCanvasScaler.Ensure(GetComponent<Canvas>());
            int m = UiFactory.Margin;

            // Left, centred: wide enough for the longest item ("Incendiary Barrage  24 MP  (not enough MP)").
            // 28 px in, so the pointer that pokes out of the window stays on screen.
            _menuPanel = UiFactory.Window(transform, "Command Menu", new Vector2(0f, 0.5f), new Vector2(m + 20, 12f), 132f, fitWidth: true);
            _menuTitle = UiFactory.Text(_menuPanel, "Title", title: true);
            _menuList = new MenuList(_menuPanel, MenuRowHeight, framed: false, hover: index => _menu?.Select(index), click: OnMenuClicked);
            _menuPanel.gameObject.SetActive(false);

            var turnPanel = _turnPanel = UiFactory.Window(transform, "Turn Order", new Vector2(1f, 1f), new Vector2(-m, -m), 168f);
            // Tight rows: at 640x360 the list must end above the unit panel's tallest layout.
            turnPanel.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().spacing = 1f;
            UiFactory.Text(turnPanel, "Title", title: true).text = "Turn Order";
            for (int i = 0; i < TurnOrderRows; i++)
                _turnRows.Add(TurnRow(turnPanel, i));

            BuildUnitPanel();

            // Bottom, full width: hints can be long ("Move Vance: choose a highlighted tile ...").
            _hintPanel = UiFactory.Strip(transform, "Hint", new Vector2(0.5f, 0f), new Vector2(0f, m), PixelUiScale.ReferenceWidth - 2 * m);
            _hint = UiFactory.Text(_hintPanel, "Text", alignment: TextAlignmentOptions.Center);

            BuildTimingPrompt();

            // Top left: the battle's objective, always visible; narrow enough to clear the centred banner.
            var objectivePanel = UiFactory.Strip(transform, "Objective", new Vector2(0f, 1f), new Vector2(m, -m), 180f);
            _objective = UiFactory.Text(objectivePanel, "Text");
            SetObjective("Defeat all enemies");

            _bannerPanel = UiFactory.Window(transform, "Banner", new Vector2(0.5f, 1f), new Vector2(0f, -m), 264f);
            // A one-line banner is shorter than the window's 64 px corners: centre it rather than leave a gap below.
            _bannerPanel.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            _banner = UiFactory.Text(_bannerPanel, "Text", title: true, alignment: TextAlignmentOptions.Center);
            _banner.color = UiFactory.TextColor;
            _bannerPanel.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_timingRunning)
            {
                float t = Mathf.Clamp01((TimingNow - _timingStart) / _timingRules.Duration);
                // Whole pixels, so the marker steps crisply across the bar.
                _timingMarker.anchoredPosition = new Vector2(Mathf.Round(t * _timingBar.InteriorWidth), 0f);
            }
            if (_bannerPanel.gameObject.activeSelf && _bannerHideTime > 0f && Time.unscaledTime >= _bannerHideTime)
                _bannerPanel.gameObject.SetActive(false);
            if (_menu != null)
                _menuList.Tick(Time.unscaledTime);
        }

        // ------------------------------------------------------------------ Public API

        public void ShowMenu(MenuModel menu)
        {
            if (_menu != null)
                _menu.Changed -= RefreshMenu;
            _menu = menu;
            _menu.Changed += RefreshMenu;
            _menuPanel.gameObject.SetActive(true);
            RefreshMenu();
        }

        public void HideMenu()
        {
            if (_menu != null)
                _menu.Changed -= RefreshMenu;
            _menu = null;
            _menuPanel.gameObject.SetActive(false);
        }

        public void SetTurnOrder(IReadOnlyList<Unit> upcoming, Unit active)
        {
            for (int i = 0; i < _turnRows.Count; i++)
            {
                var (team, label) = _turnRows[i];
                bool visible = i < upcoming.Count;
                team.transform.parent.gameObject.SetActive(visible);
                if (!visible)
                    continue;
                var unit = upcoming[i];
                team.color = TeamColor(unit.Team);
                label.text = TurnText(unit, i == 0 && unit == active);
            }
        }

        /// <summary>The turn list with charged spells resolving in their place (FFT shows them in the AT list too).</summary>
        public void SetTurnOrder(IReadOnlyList<Battle.TurnOrderEntry> upcoming, Unit active)
        {
            for (int i = 0; i < _turnRows.Count; i++)
            {
                var (team, label) = _turnRows[i];
                bool visible = i < upcoming.Count;
                team.transform.parent.gameObject.SetActive(visible);
                if (!visible)
                    continue;
                var entry = upcoming[i];
                var unit = entry.Unit;
                team.color = TeamColor(unit.Team);
                label.text = entry.Spell != null
                    ? $"<color=#C9A7FF>* {entry.Spell.DisplayName}</color>{LockText(entry.Charge)} <color={UiFactory.Hex(UiFactory.MutedTextColor)}>({unit.Name})</color>"
                    : TurnText(unit, i == 0 && unit == active);
            }
        }

        /// <summary>The full unit details (FFT's Status) are showing instead of the compact name, job, level and bars.</summary>
        public bool DetailsExpanded => _detailsExpanded;

        /// <summary>
        /// Switches the unit panel between compact (name, job, level, HP/MP/CT) and the full details. The turn order steps aside
        /// while the details are open, so the two never crowd each other at 640x360.
        /// </summary>
        public void SetDetailsExpanded(bool expanded)
        {
            _detailsExpanded = expanded;
            _unitInfo.gameObject.SetActive(expanded);
            _unitPanel.sizeDelta = new Vector2(expanded ? UnitPanelWidth : CompactPanelWidth, _unitPanel.sizeDelta.y);
            _turnPanel.gameObject.SetActive(!expanded);
            SetUnitInfo(_shownUnit);
        }

        public void SetUnitInfo(Unit unit)
        {
            _shownUnit = unit;
            _unitPanel.gameObject.SetActive(unit != null);
            if (unit == null)
                return;

            var s = unit.Stats;
            string muted = UiFactory.Hex(UiFactory.MutedTextColor);
            if (!_detailsExpanded)
            {
                // Name (title font, team colour), then job and level on the smaller body font (12 px is that font's native
                // size, the smallest crisp one), the job told apart by colour.
                _unitName.text = $"<color={UiFactory.Hex(TeamColor(unit.Team))}>{unit.Name}</color>" +
                                 (unit.IsKnockedOut ? $"  <color={UiFactory.Hex(UiFactory.EnemyColor)}>KO ({unit.DeathCounter})</color>" : "");
                _unitJob.text = $"<color={JobColor}>{unit.Job.DisplayName}</color>  <color={muted}>Lv</color> {unit.Level}";
                SetBar(_hp, unit.CurrentHp, s.MaxHp);
                SetBar(_mp, unit.CurrentMp, s.MaxMp);
                SetBar(_ct, Mathf.Min(unit.ChargeTime, 100), 100, unit.ChargeTime.ToString());
                return;
            }
            _unitName.text = $"{unit.Name}  <color={UiFactory.Hex(TeamColor(unit.Team))}>{unit.Team}</color>";
            _unitJob.text = $"{unit.Job.DisplayName}  Lv {unit.Level}  <color={muted}>Job Lv</color> {unit.JobLevel(unit.Job)}" +
                            (unit.IsKnockedOut ? $"  <color={UiFactory.Hex(UiFactory.EnemyColor)}>KO ({unit.DeathCounter})</color>" : "");
            SetBar(_hp, unit.CurrentHp, s.MaxHp);
            SetBar(_mp, unit.CurrentMp, s.MaxMp);
            SetBar(_ct, Mathf.Min(unit.ChargeTime, 100), 100, unit.ChargeTime.ToString());

            string secondary = unit.SecondaryJob != null ? unit.SecondaryJob.CommandName : "-";
            _unitInfo.text =
                $"Speed {s.Speed}  P.Atk {s.PhysicalAttack}  M.Atk {s.MagicAttack}  Morale {unit.Morale}  Resonance {unit.Resonance}\n" +
                $"Move {s.Move}  Jump {s.Jump}  C-Ev {s.ClassEvasion}%  Facing {unit.Facing}  " +
                $"EXP {unit.Experience}/{ExperienceCurve.ToNextLevel(unit.Level)}  Prof {unit.AvailableProficiency(unit.Job)}\n" +
                (unit.Statuses.Count > 0 ? $"<color={UiFactory.Hex(UiFactory.AccentColor)}>{StatusText(unit)}</color>\n" : "") +
                $"<color={muted}>{Gear(unit.Weapon, "Bare hands")} / {Gear(unit.Equipped(Units.Equipment.EquipmentSlot.OffHand), "-")}" +
                $" / {Gear(unit.Head, "-")} / {Gear(unit.Body, "-")} / {Gear(unit.Accessory, "-")}</color>\n" +
                $"<color={muted}>Secondary</color> {secondary}  <color={muted}>Reaction</color> {Name(unit.Reaction)}  " +
                $"<color={muted}>Support</color> {Name(unit.Support)}  <color={muted}>Movement</color> {Name(unit.Movement)}";
        }

        public void SetHint(string text)
        {
            _hint.text = text;
            _hintPanel.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        /// <summary>The objective line in the top-left corner.</summary>
        public void SetObjective(string text) => _objective.text = $"<color={UiFactory.Hex(UiFactory.AccentColor)}>Objective</color>  {text}";

        /// <summary>The battle clock the timing bar sweeps on, so the marker freezes while the battle is paused.</summary>
        public void SetClock(Battle.BattleClock clock) => _clock = clock;

        private float TimingNow => _clock != null ? _clock.Now(Time.unscaledTime) : Time.unscaledTime;

        /// <summary>Shows the sweeping timing bar; the perfect moment is the highlighted zone.</summary>
        public void ShowTimingPrompt(string title, TimedPressRules rules)
        {
            _timingRules = rules;
            _timingStart = TimingNow;
            _timingRunning = true;
            _timingTitle.text = title;
            _timingFeedback.text = " ";
            int width = _timingBar.InteriorWidth;
            int from = Mathf.RoundToInt(Mathf.Clamp01((rules.PerfectTime - rules.Tolerance) / rules.Duration) * width);
            int to = Mathf.RoundToInt(Mathf.Clamp01((rules.PerfectTime + rules.Tolerance) / rules.Duration) * width);
            _timingZone.anchoredPosition = new Vector2(from, 0f);
            _timingZone.sizeDelta = new Vector2(Mathf.Max(1, to - from), 0f);
            _timingMarker.anchoredPosition = Vector2.zero;
            _timingPanel.gameObject.SetActive(true);
        }

        /// <summary>Freezes the marker and shows how the press went.</summary>
        public void StopTimingPrompt(float accuracy, bool pressed)
        {
            _timingRunning = false;
            _timingFeedback.text = !pressed ? "Too late" : accuracy >= 0.9f ? "Perfect!" : accuracy >= 0.6f ? "Great" : accuracy > 0f ? "Good" : "Missed";
        }

        public void HideTimingPrompt()
        {
            _timingRunning = false;
            _timingPanel.gameObject.SetActive(false);
        }

        /// <summary>Shows a message at the top of the screen; it hides itself after <paramref name="seconds"/> (0 = stays).</summary>
        public void ShowBanner(string text, float seconds = 2.5f)
        {
            _banner.text = text;
            _bannerHideTime = seconds > 0f ? Time.unscaledTime + seconds : 0f;
            _bannerPanel.gameObject.SetActive(true);
        }

        private static Color TeamColor(Team team) => team == Team.Player ? UiFactory.PlayerColor : UiFactory.EnemyColor;

        private static string TurnText(Unit unit, bool acting) =>
            $"{(acting ? $"<color={UiFactory.Hex(UiFactory.AccentColor)}>></color> " : "")}{unit.Name}  " +
            $"<color={UiFactory.Hex(UiFactory.MutedTextColor)}>{unit.Job.DisplayName}</color>";

        private static string Name(Units.Abilities.AbilityDefinition ability) => ability != null ? ability.DisplayName : "-";

        /// <summary>" > Vance" for a spell following a unit, " > (5,7)" for one aimed at a tile.</summary>
        private static string LockText(Battle.Magic.ChargingAction charge)
        {
            if (charge == null)
                return "";
            string target = charge.LockedUnit != null ? charge.LockedUnit.Name : $"({charge.Target.X},{charge.Target.Y})";
            return $" <color={UiFactory.Hex(UiFactory.TextColor)}>> {target}</color>";
        }

        /// <summary>Statuses with the time they have left, e.g. "Poison (12), Silence (2 turns), Blind".</summary>
        private static string StatusText(Unit unit)
        {
            var parts = new List<string>();
            foreach (var status in unit.Statuses.Active)
            {
                int left = unit.Statuses.Remaining(status);
                string unitLabel = unit.Statuses.IsTurnBased(status) ? (left == 1 ? " turn" : " turns") : "";
                parts.Add(left > 0 ? $"{status} ({left}{unitLabel})" : status.ToString());
            }
            return string.Join(", ", parts);
        }

        private static string Gear(Units.Equipment.EquipmentDefinition item, string empty) => item != null ? item.DisplayName : empty;

        private static void SetBar((UiBar bar, TextMeshProUGUI value, string label) gauge, int current, int max, string text = null)
        {
            gauge.bar.SetFraction(max > 0 ? (float)current / max : 0f);
            gauge.value.text = $"<color={UiFactory.Hex(UiFactory.MutedTextColor)}>{gauge.label}</color> {text ?? $"{current}/{max}"}";
        }

        // ------------------------------------------------------------------ Building

        private void RefreshMenu()
        {
            _menuTitle.text = _menu.Title;
            _menuTitle.gameObject.SetActive(!string.IsNullOrEmpty(_menu.Title));
            _menuList.Refresh(_menu);
        }

        private void OnMenuClicked(int index)
        {
            if (_menu != null && index < _menu.Items.Count && _menu.Items[index].Enabled)
            {
                _menu.Select(index);
                MenuItemClicked?.Invoke(index);
            }
        }

        private (UnityEngine.UI.Image, TextMeshProUGUI) TurnRow(RectTransform parent, int index)
        {
            var row = new GameObject($"Turn {index}", typeof(RectTransform), typeof(UnityEngine.UI.HorizontalLayoutGroup), typeof(UnityEngine.UI.LayoutElement));
            row.transform.SetParent(parent, false);
            var element = row.GetComponent<UnityEngine.UI.LayoutElement>();
            element.preferredHeight = element.minHeight = 12f;
            var layout = row.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var team = new GameObject("Team", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.LayoutElement));
            team.transform.SetParent(row.transform, false);
            var chip = team.GetComponent<UnityEngine.UI.LayoutElement>();
            chip.preferredWidth = chip.minWidth = 3f;
            chip.preferredHeight = chip.minHeight = 8f;
            var teamImage = team.GetComponent<UnityEngine.UI.Image>();
            teamImage.raycastTarget = false;

            var label = UiFactory.Text(row.transform, "Label");
            // One line per entry: long spell rows ("* Incendiary Barrage (Fairweather)") end in an ellipsis
            // instead of wrapping into the row below.
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            var labelElement = label.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            labelElement.flexibleWidth = 1f;
            labelElement.minWidth = 0f;
            return (teamImage, label);
        }

        private void BuildUnitPanel()
        {
            int m = UiFactory.Margin;
            // Bottom right, above the hint strip (two lines tall at most).
            _unitPanel = UiFactory.Window(transform, "Unit Info", new Vector2(1f, 0f), new Vector2(-m, m + 36), UnitPanelWidth);
            _unitName = UiFactory.Text(_unitPanel, "Name", title: true);
            _unitName.color = UiFactory.TextColor;
            _unitJob = UiFactory.Text(_unitPanel, "Job");

            var gauges = new GameObject("Gauges", typeof(RectTransform), typeof(UnityEngine.UI.HorizontalLayoutGroup));
            gauges.transform.SetParent(_unitPanel, false);
            var layout = gauges.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var skin = UiSkin.Current;
            _hp = Gauge(gauges.transform, "HP", skin != null ? skin.HpColor : Color.green);
            _mp = Gauge(gauges.transform, "MP", skin != null ? skin.MpColor : Color.blue);
            _ct = Gauge(gauges.transform, "CT", skin != null ? skin.CtColor : Color.yellow);

            _unitInfo = UiFactory.Text(_unitPanel, "Details");
            _unitPanel.gameObject.SetActive(false);
            // Compact until the player asks for Status.
            _unitInfo.gameObject.SetActive(false);
            _unitPanel.sizeDelta = new Vector2(CompactPanelWidth, _unitPanel.sizeDelta.y);
        }

        private static (UiBar, TextMeshProUGUI, string) Gauge(Transform parent, string label, Color fill)
        {
            // "HP 34/50" above its bar.
            var group = new GameObject(label, typeof(RectTransform), typeof(UnityEngine.UI.VerticalLayoutGroup));
            group.transform.SetParent(parent, false);
            var layout = group.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 0f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            var value = UiFactory.Text(group.transform, "Value");
            value.textWrappingMode = TextWrappingModes.NoWrap;
            var bar = UiFactory.Bar(group.transform, "Bar", fill, BarWidth);
            return (bar, value, label);
        }

        private void BuildTimingPrompt()
        {
            int m = UiFactory.Margin;
            // Low on screen and left of centre, above the hint, clear of the unit panel and the units the camera frames.
            _timingPanel = UiFactory.Window(transform, "Timing", new Vector2(0.5f, 0f), new Vector2(-104f, m + 36), 176f);
            _timingTitle = UiFactory.Text(_timingPanel, "Title", title: true, alignment: TextAlignmentOptions.Center);

            _timingBar = UiFactory.Bar(_timingPanel, "Bar", new Color(0f, 0f, 0f, 0f), 148);
            _timingBar.SetFraction(1f);
            var skin = UiSkin.Current;
            _timingZone = Block(_timingBar.Interior, "Perfect Zone", skin != null ? skin.AccentColor : new Color(0.95f, 0.70f, 0.25f));
            _timingZone.anchorMin = Vector2.zero;
            _timingZone.anchorMax = new Vector2(0f, 1f);
            _timingZone.pivot = new Vector2(0f, 0.5f);
            _timingMarker = Block(_timingBar.Interior, "Marker", Color.white);
            _timingMarker.anchorMin = Vector2.zero;
            _timingMarker.anchorMax = new Vector2(0f, 1f);
            _timingMarker.pivot = new Vector2(0f, 0.5f);
            _timingMarker.sizeDelta = new Vector2(2f, 4f);

            _timingFeedback = UiFactory.Text(_timingPanel, "Feedback", alignment: TextAlignmentOptions.Center);
            _timingPanel.gameObject.SetActive(false);
        }

        private static RectTransform Block(Transform parent, string blockName, Color color)
        {
            var go = new GameObject(blockName, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return (RectTransform)go.transform;
        }
    }
}
