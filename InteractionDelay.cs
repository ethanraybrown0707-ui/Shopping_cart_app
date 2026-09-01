using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ShoppingCartApp;

/// <summary>
/// A deliberate short pause between interacting with a control and that interaction's effect
/// taking hold, applied app-wide - see the handlers in <see cref="BasketControl"/>,
/// <see cref="MainWindow"/> and <see cref="OrderHistoryWindow"/>, which all
/// <c>await InteractionDelay.Wait(...)</c> before doing their work.
///
/// Each call picks a fresh random delay in the range configured under Settings &gt;
/// Interaction Delay... (<see cref="DelaySettings"/>, default 100-1000ms), shows the wait
/// cursor for the duration (ref-counted, so two overlapping interactions don't clear it
/// early), and - when given a control - disables it so it reads as "working" and can't be
/// double-fired. The wait is <c>await</c>ed, not slept, so the UI thread keeps redrawing and
/// other controls keep working while it runs.
///
/// Set the environment variable <c>SHOPPING_CART_DISABLE_INTERACTION_DELAY=1</c> to force
/// every call to a no-op regardless of the setting - used by the verifier and UI-test
/// harnesses so their runs stay fast and deterministic (they assert on behaviour, not on this
/// cosmetic latency; one dedicated verifier check runs without the bypass to confirm the
/// delay is there, and another drives the Settings dialog).
/// </summary>
public static class InteractionDelay
{
    private static readonly bool ForcedOff = IsTruthy(
        Environment.GetEnvironmentVariable("SHOPPING_CART_DISABLE_INTERACTION_DELAY"));

    private static readonly Random Rng = new();

    // UI-thread only (every caller is an async void handler that resumes on the UI thread), so
    // a plain int is fine - no locking needed.
    private static int _pending;

    /// <summary>Whether the delay is hard-off for this process via the env var (which wins
    /// over the Settings dialog).</summary>
    public static bool IsForcedOff => ForcedOff;

    /// <summary>Whether a call to <see cref="Wait"/> will currently pause at all - false when
    /// forced off or switched off in Settings.</summary>
    public static bool IsActive => !ForcedOff && DelaySettings.Enabled;

    /// <param name="controlToDisable">The control that was interacted with - disabled for the
    /// duration and re-enabled afterwards (only if it was enabled to begin with). Pass null to
    /// just show the wait cursor, e.g. for a text box that must stay editable.</param>
    public static Task Wait(FrameworkElement? controlToDisable = null)
    {
        if (!IsActive)
        {
            return Task.CompletedTask;
        }

        var min = DelaySettings.MinMilliseconds;
        var max = DelaySettings.MaxMilliseconds;

        // Random.Next needs upper > lower; when the range is a single value just use it.
        var milliseconds = min >= max ? min : Rng.Next(min, max + 1);
        return RunAsync(milliseconds, controlToDisable);
    }

    private static async Task RunAsync(int milliseconds, FrameworkElement? controlToDisable)
    {
        var reEnableAfter = controlToDisable is { IsEnabled: true };
        if (reEnableAfter)
        {
            controlToDisable!.IsEnabled = false;
        }

        _pending++;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            await Task.Delay(milliseconds);
        }
        finally
        {
            if (--_pending == 0)
            {
                Mouse.OverrideCursor = null;
            }

            if (reEnableAfter)
            {
                controlToDisable!.IsEnabled = true;
            }
        }
    }

    private static bool IsTruthy(string? value) =>
        string.Equals(value, "1", StringComparison.Ordinal) ||
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
