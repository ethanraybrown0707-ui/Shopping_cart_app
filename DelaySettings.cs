using System;
using System.IO;
using System.Text.Json;

namespace ShoppingCartApp;

/// <summary>
/// The user-configurable side of <see cref="InteractionDelay"/> - whether the interaction
/// delay is on, and the millisecond range it picks from. Edited via Settings &gt; Interaction
/// Delay... (see <see cref="SettingsWindow"/>), persisted to interaction-delay.json next to the
/// exe (same AppContext.BaseDirectory / whole-file-rewrite / swallow-failures convention as
/// OrderHistoryStore and FavouritesStore), and read fresh by InteractionDelay on every
/// interaction so a change takes effect immediately.
///
/// The environment variable SHOPPING_CART_DISABLE_INTERACTION_DELAY=1 overrides all of this
/// (forces the delay off) - see InteractionDelay.
/// </summary>
public static class DelaySettings
{
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "interaction-delay.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Upper bound the dialog will accept for either value, so a fat-fingered entry
    /// can't wedge the app behind a minute-long "delay".</summary>
    public const int CeilingMilliseconds = 5000;

    public const bool DefaultEnabled = true;
    public const int DefaultMinMilliseconds = 100;
    public const int DefaultMaxMilliseconds = 1000;

    private static State _state = Load();

    public static bool Enabled => _state.Enabled;
    public static int MinMilliseconds => _state.MinMilliseconds;
    public static int MaxMilliseconds => _state.MaxMilliseconds;

    /// <summary>Applies and persists a new configuration. Values are clamped to
    /// [0, <see cref="CeilingMilliseconds"/>] and max is pulled up to at least min.</summary>
    public static void Update(bool enabled, int minMilliseconds, int maxMilliseconds)
    {
        var min = Math.Clamp(minMilliseconds, 0, CeilingMilliseconds);
        var max = Math.Clamp(maxMilliseconds, min, CeilingMilliseconds);

        _state = new State { Enabled = enabled, MinMilliseconds = min, MaxMilliseconds = max };
        Save();
    }

    /// <summary>The persisted shape. Kept private - callers use the static members above.</summary>
    private sealed class State
    {
        public bool Enabled { get; set; } = DefaultEnabled;
        public int MinMilliseconds { get; set; } = DefaultMinMilliseconds;
        public int MaxMilliseconds { get; set; } = DefaultMaxMilliseconds;
    }

    private static State Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new State();

            var state = JsonSerializer.Deserialize<State>(File.ReadAllText(FilePath)) ?? new State();

            // Defend against a hand-edited file: keep the same invariants Update enforces.
            state.MinMilliseconds = Math.Clamp(state.MinMilliseconds, 0, CeilingMilliseconds);
            state.MaxMilliseconds = Math.Clamp(state.MaxMilliseconds, state.MinMilliseconds, CeilingMilliseconds);
            return state;
        }
        catch
        {
            // Missing/corrupt/unreadable - fall back to the defaults rather than crash over a
            // convenience setting.
            return new State();
        }
    }

    private static void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_state, JsonOptions));
        }
        catch
        {
            // Best-effort persistence - a failed write shouldn't block anything.
        }
    }
}
