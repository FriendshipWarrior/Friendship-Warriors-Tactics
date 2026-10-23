using System;
using System.Collections.Generic;
using FriendshipWarriors.Battle.Combat;
using FriendshipWarriors.Units;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FriendshipWarriors.UI
{
    /// <summary>
    /// Placeholder battle HUD, built in code on a Screen Space Overlay canvas: the command menu (left), the turn
    /// order (top right), unit details (bottom left), a controls hint (bottom), the timed-press bar (center) and a
    /// result banner (top). Menu navigation comes from the battle flow's own input handling; the mouse can hover and
    /// click items directly.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class BattleHud : MonoBehaviour
    {
        private static readonly Color PanelColor = new(0.07f, 0.08f, 0.11f, 0.88f);
        private static readonly Color ItemColor = new(1f, 1f, 1f, 0.04f);
        private static readonly Color SelectedColor = new(0.95f, 0.70f, 0.25f, 0.90f);
        private static readonly Color TextColor = new(0.93f, 0.92f, 0.88f);
        private static readonly Color SelectedTextColor = new(0.10f, 0.08f, 0.05f);
        private static readonly Color DisabledTextColor = new(0.55f, 0.55f, 0.58f);
        private static readonly Color PlayerColor = new(0.35f, 0.65f, 1f);
        private static readonly Color EnemyColor = new(1f, 0.38f, 0.38f);

        private const int TurnOrderRows = 8;

        private RectTransform _menuPanel;
        private TextMeshProUGUI _menuTitle;
        private readonly List<(UnityEngine.UI.Image background, TextMeshProUGUI label)> _menuRows = new();
        private MenuModel _menu;

        private readonly List<(UnityEngine.UI.Image team, TextMeshProUGUI label)> _turnRows = new();
        private TextMeshProUGUI _unitInfo;
        private RectTransform _unitPanel;
        private TextMeshProUGUI _hint;
        private TextMeshProUGUI _objective;

        private RectTransform _timingPanel;
        private TextMeshProUGUI _timingTitle;
        private TextMeshProUGUI _timingFeedback;
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
            _menuPanel = Panel("Command Menu", new Vector2(0f, 0.5f), new Vector2(40f, 60f), 340f);
            _menuTitle = Label(_menuPanel, "Title", 30, FontStyles.Bold);
            _menuPanel.gameObject.SetActive(false);

            var turnPanel = Panel("Turn Order", new Vector2(1f, 1f), new Vector2(-40f, -40f), 380f);
            Label(turnPanel, "Title", 28, FontStyles.Bold).text = "Turn Order";
            for (int i = 0; i < TurnOrderRows; i++)
                _turnRows.Add(TurnRow(turnPanel, i));

            _unitPanel = Panel("Unit Info", new Vector2(0f, 0f), new Vector2(40f, 90f), 680f);
            _unitInfo = Label(_unitPanel, "Details", 26, FontStyles.Normal);
            _unitPanel.gameObject.SetActive(false);

            var hintPanel = Panel("Hint", new Vector2(0.5f, 0f), new Vector2(0f, 24f), 1200f);
            _hint = Label(hintPanel, "Text", 24, FontStyles.Normal);
            _hint.alignment = TextAlignmentOptions.Center;

            BuildTimingPrompt();

            // Top left: the battle's objective, always visible.
            // 440 wide keeps it clear of the centred banner (which starts 450 px left of centre at 1920 wide).
            var objectivePanel = Panel("Objective", new Vector2(0f, 1f), new Vector2(40f, -40f), 440f);
            _objective = Label(objectivePanel, "Text", 24, FontStyles.Normal);
            SetObjective("Defeat all enemies");

            _bannerPanel = Panel("Banner", new Vector2(0.5f, 1f), new Vector2(0f, -40f), 900f);
            _banner = Label(_bannerPanel, "Text", 32, FontStyles.Bold);
            _banner.alignment = TextAlignmentOptions.Center;
            _bannerPanel.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_timingRunning)
            {
                float t = Mathf.Clamp01((TimingNow - _timingStart) / _timingRules.Duration);
                _timingMarker.anchorMin = _timingMarker.anchorMax = new Vector2(t, 0.5f);
            }
            if (_bannerPanel.gameObject.activeSelf && _bannerHideTime > 0f && Time.unscaledTime >= _bannerHideTime)
                _bannerPanel.gameObject.SetActive(false);
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
                team.color = unit.Team == Team.Player ? PlayerColor : EnemyColor;
                string marker = i == 0 && unit == active ? "> " : "   ";
                label.text = $"{marker}{unit.Name}  <color=#9A9AA0>{unit.Job.DisplayName}</color>";
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
                team.color = unit.Team == Team.Player ? PlayerColor : EnemyColor;
                if (entry.Spell != null)
                {
                    label.text = $"   <color=#C9A7FF>* {entry.Spell.DisplayName}</color>  <color=#9A9AA0>({unit.Name})</color>";
                    continue;
                }
                string marker = i == 0 && unit == active ? "> " : "   ";
                label.text = $"{marker}{unit.Name}  <color=#9A9AA0>{unit.Job.DisplayName}</color>";
            }
        }

        public void SetUnitInfo(Unit unit)
        {
            _unitPanel.gameObject.SetActive(unit != null);
            if (unit == null)
                return;

            var s = unit.Stats;
            string secondary = unit.SecondaryJob != null ? unit.SecondaryJob.CommandName : "-";
            _unitInfo.text =
                $"<b>{unit.Name}</b>  <color=#{(unit.Team == Team.Player ? "6FA8FF" : "FF7070")}>{unit.Team}</color>\n" +
                $"{unit.Job.DisplayName}  Lv {unit.Level}   Job Lv {unit.JobLevel(unit.Job)}   CT {unit.ChargeTime}\n" +
                $"HP {unit.CurrentHp}/{s.MaxHp}   MP {unit.CurrentMp}/{s.MaxMp}\n" +
                $"Speed {s.Speed}   P.Atk {s.PhysicalAttack}   M.Atk {s.MagicAttack}\n" +
                $"Morale {unit.Morale}   Resonance {unit.Resonance}\n" +
                $"Move {s.Move}   Jump {s.Jump}   C-Ev {s.ClassEvasion}%   Facing {unit.Facing}\n" +
                $"EXP {unit.Experience}/{ExperienceCurve.ToNextLevel(unit.Level)}   Prof {unit.AvailableProficiency(unit.Job)}" +
                (unit.IsKnockedOut ? $"   <color=#FF7070>KO ({unit.DeathCounter})</color>" : "") + "\n" +
                (unit.Statuses.Count > 0 ? $"<color=#E8C25A>{StatusText(unit)}</color>\n" : "") +
                $"<size=80%>{Gear(unit.Weapon, "Bare hands")} | {Gear(unit.Equipped(Units.Equipment.EquipmentSlot.OffHand), "-")}" +
                $" | {Gear(unit.Head, "-")} | {Gear(unit.Body, "-")} | {Gear(unit.Accessory, "-")}</size>\n" +
                $"<size=80%>Secondary {secondary} | Reaction {Name(unit.Reaction)} | Support {Name(unit.Support)} | Movement {Name(unit.Movement)}</size>";
        }

        public void SetHint(string text) => _hint.text = text;

        /// <summary>The objective line in the top-left corner.</summary>
        public void SetObjective(string text) => _objective.text = $"<color=#F2B340>Objective</color>  {text}";

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
            float center = rules.PerfectTime / rules.Duration;
            float half = rules.Tolerance / rules.Duration;
            _timingZone.anchorMin = new Vector2(Mathf.Clamp01(center - half), 0f);
            _timingZone.anchorMax = new Vector2(Mathf.Clamp01(center + half), 1f);
            _timingMarker.anchorMin = _timingMarker.anchorMax = new Vector2(0f, 0.5f);
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

        private static string Name(Units.Abilities.AbilityDefinition ability) => ability != null ? ability.DisplayName : "-";

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

        // ------------------------------------------------------------------ Menu rendering

        private void RefreshMenu()
        {
            _menuTitle.text = _menu.Title;
            while (_menuRows.Count < _menu.Items.Count)
                _menuRows.Add(MenuRow(_menuPanel, _menuRows.Count));

            for (int i = 0; i < _menuRows.Count; i++)
            {
                var (background, label) = _menuRows[i];
                bool visible = i < _menu.Items.Count;
                background.gameObject.SetActive(visible);
                if (!visible)
                    continue;

                var item = _menu.Items[i];
                bool selected = i == _menu.SelectedIndex;
                label.text = item.Label;
                background.color = selected ? SelectedColor : ItemColor;
                label.color = !item.Enabled ? DisabledTextColor : selected ? SelectedTextColor : TextColor;
            }
        }

        private (UnityEngine.UI.Image, TextMeshProUGUI) MenuRow(RectTransform parent, int index)
        {
            var row = new GameObject($"Item {index}", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 56f;
            var background = row.GetComponent<UnityEngine.UI.Image>();
            background.raycastTarget = true;

            var label = Text(row.transform, "Label", 32, FontStyles.Normal);
            var rect = (RectTransform)label.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(20f, 0f);
            rect.offsetMax = new Vector2(-12f, 0f);
            label.alignment = TextAlignmentOptions.MidlineLeft;

            // Hover selects, click confirms: the same path a controller takes.
            var trigger = row.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, () => _menu?.Select(index));
            AddTrigger(trigger, EventTriggerType.PointerClick, () =>
            {
                if (_menu != null && index < _menu.Items.Count && _menu.Items[index].Enabled)
                {
                    _menu.Select(index);
                    MenuItemClicked?.Invoke(index);
                }
            });
            return (background, label);
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        private (UnityEngine.UI.Image, TextMeshProUGUI) TurnRow(RectTransform parent, int index)
        {
            var row = new GameObject($"Turn {index}", typeof(RectTransform), typeof(UnityEngine.UI.HorizontalLayoutGroup), typeof(UnityEngine.UI.LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 36f;
            var layout = row.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var team = new GameObject("Team", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.LayoutElement));
            team.transform.SetParent(row.transform, false);
            team.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 8f;
            var teamImage = team.GetComponent<UnityEngine.UI.Image>();
            teamImage.raycastTarget = false;

            var label = Text(row.transform, "Label", 24, FontStyles.Normal);
            label.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1f;
            return (teamImage, label);
        }

        private void BuildTimingPrompt()
        {
            // Low on screen, above the hint, so it never covers the units the camera is centered on.
            _timingPanel = Panel("Timing", new Vector2(0.5f, 0f), new Vector2(0f, 110f), 660f);
            _timingTitle = Label(_timingPanel, "Title", 34, FontStyles.Bold);
            _timingTitle.alignment = TextAlignmentOptions.Center;

            var bar = new GameObject("Bar", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.LayoutElement));
            bar.transform.SetParent(_timingPanel, false);
            bar.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 30f;
            var track = bar.GetComponent<UnityEngine.UI.Image>();
            track.color = new Color(1f, 1f, 1f, 0.12f);
            track.raycastTarget = false;

            _timingZone = Block(bar.transform, "Perfect Zone", new Color(0.95f, 0.70f, 0.25f, 0.85f));
            _timingZone.offsetMin = _timingZone.offsetMax = Vector2.zero;
            _timingMarker = Block(bar.transform, "Marker", Color.white);
            _timingMarker.sizeDelta = new Vector2(8f, 44f);

            _timingFeedback = Label(_timingPanel, "Feedback", 26, FontStyles.Normal);
            _timingFeedback.alignment = TextAlignmentOptions.Center;
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

        // ------------------------------------------------------------------ Building blocks

        /// <summary>A dark panel anchored at <paramref name="anchor"/> that grows to fit its vertically stacked content.</summary>
        private RectTransform Panel(string panelName, Vector2 anchor, Vector2 offset, float width)
        {
            var go = new GameObject(panelName, typeof(RectTransform), typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.VerticalLayoutGroup), typeof(UnityEngine.UI.ContentSizeFitter));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(width, 0f);

            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.color = PanelColor;
            image.raycastTarget = false;

            var layout = go.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 14, 14);
            layout.spacing = 6f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            go.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            return rect;
        }

        private static TextMeshProUGUI Label(RectTransform parent, string labelName, float size, FontStyles style)
        {
            var text = Text(parent, labelName, size, style);
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private static TextMeshProUGUI Text(Transform parent, string textName, float size, FontStyles style)
        {
            var go = new GameObject(textName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.color = TextColor;
            text.raycastTarget = false;
            text.richText = true;
            return text;
        }
    }
}
