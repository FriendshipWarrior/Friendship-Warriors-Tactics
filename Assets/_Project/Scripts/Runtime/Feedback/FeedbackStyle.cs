using FriendshipWarriors.Audio;
using FriendshipWarriors.Battle;
using FriendshipWarriors.Units.Status;
using UnityEngine;

namespace FriendshipWarriors.Feedback
{
    /// <summary>
    /// Chooses the text, colour and sound for battle feedback (FFT-style: white damage numbers, green healing, blue
    /// MP, status names), and which sound a battle cue plays. Pure, so it's tested without Play mode.
    /// </summary>
    public static class FeedbackStyle
    {
        public static readonly Color32 DamageColor = new(255, 255, 255, 255);
        public static readonly Color32 CriticalColor = new(255, 214, 74, 255);
        public static readonly Color32 HealColor = new(110, 255, 140, 255);
        public static readonly Color32 MpColor = new(120, 190, 255, 255);
        public static readonly Color32 MissColor = new(200, 200, 210, 255);
        public static readonly Color32 GuardColor = new(150, 205, 255, 255);
        public static readonly Color32 HarmfulStatusColor = new(215, 150, 255, 255);
        public static readonly Color32 HelpfulStatusColor = new(140, 235, 255, 255);
        public static readonly Color32 LiftedStatusColor = new(225, 225, 225, 255);
        public static readonly Color32 PickupColor = new(255, 236, 160, 255);

        /// <summary>The popup for a change: <paramref name="amount"/> for numbers, <paramref name="status"/> for statuses.</summary>
        public static PopupStyle Popup(PopupKind kind, int amount = 0, StatusEffect status = default, string text = null) => kind switch
        {
            PopupKind.Damage => new PopupStyle(amount.ToString(), DamageColor),
            // The number stays the star; the label sits small above it.
            PopupKind.CriticalDamage => new PopupStyle($"<size=55%>Critical!</size>\n{amount}", CriticalColor, 1.2f),
            PopupKind.Heal => new PopupStyle(amount.ToString(), HealColor),
            PopupKind.MpLoss => new PopupStyle($"{amount}<size=60%> MP</size>", MpColor),
            PopupKind.MpRestore => new PopupStyle($"{amount}<size=60%> MP</size>", MpColor),
            PopupKind.Miss => new PopupStyle("Miss", MissColor, 0.8f),
            PopupKind.Guarded => new PopupStyle("Guarded", GuardColor, 0.8f),
            PopupKind.Revived => new PopupStyle(amount.ToString(), HealColor),
            PopupKind.StatusGained => new PopupStyle(StatusName(status),
                StatusRules.IsHarmful(status) ? HarmfulStatusColor : HelpfulStatusColor, 0.7f),
            PopupKind.StatusLifted => new PopupStyle(LiftedText(status), LiftedStatusColor, 0.6f),
            PopupKind.Pickup => new PopupStyle(text ?? "", PickupColor, 0.6f),
            _ => new PopupStyle("", DamageColor),
        };

        /// <summary>How a status is named on screen.</summary>
        public static string StatusName(StatusEffect status) => status switch
        {
            StatusEffect.Immobilize => "Immobilize",
            _ => status.ToString(),
        };

        /// <summary>
        /// Text for a status that went away. Read off the unit, so a cure and running out look the same: harmful
        /// statuses are "lifted", buffs "wore off", and a sleeper wakes.
        /// </summary>
        public static string LiftedText(StatusEffect status) => status switch
        {
            StatusEffect.Sleep => "Awake",
            StatusEffect.Panic => "Steadied",
            _ when StatusRules.IsHarmful(status) => $"{StatusName(status)} lifted",
            _ => $"{StatusName(status)} wore off",
        };

        /// <summary>Whether a status change gets a popup at all (Charging shows as the cast pose instead).</summary>
        public static bool ShowsPopup(StatusEffect status) => status != StatusEffect.Charging;

        /// <summary>The sound for a popup; status lifts and MP stay quiet.</summary>
        public static SoundCue Sound(PopupKind kind) => kind switch
        {
            PopupKind.Damage => SoundCue.Hit,
            PopupKind.CriticalDamage => SoundCue.CriticalHit,
            PopupKind.Heal => SoundCue.Heal,
            PopupKind.MpRestore => SoundCue.Heal,
            PopupKind.Miss => SoundCue.Miss,
            PopupKind.Guarded => SoundCue.Block,
            PopupKind.StatusGained => SoundCue.StatusGained,
            PopupKind.Revived => SoundCue.Revive,
            _ => SoundCue.None,
        };

        /// <summary>The sound when a drop is taken: a chime for a rod, a creak for a chest.</summary>
        public static SoundCue PickupSound(Battle.Drops.DropKind kind) =>
            kind == Battle.Drops.DropKind.SteelRod ? SoundCue.RodTaken : SoundCue.ChestOpened;

        /// <summary>The sound for a battle cue (Miss, Guarded and Critical sound through their popups instead).</summary>
        public static SoundCue Sound(BattleCue cue) => cue switch
        {
            BattleCue.CastStarted => SoundCue.CastStart,
            BattleCue.SpellResolved => SoundCue.SpellResolve,
            BattleCue.TimedPressSuccess => SoundCue.TimedPressSuccess,
            BattleCue.TimedPressFail => SoundCue.TimedPressFail,
            BattleCue.MenuMove => SoundCue.MenuMove,
            BattleCue.MenuConfirm => SoundCue.MenuConfirm,
            BattleCue.MenuCancel => SoundCue.MenuCancel,
            _ => SoundCue.None,
        };
    }
}
