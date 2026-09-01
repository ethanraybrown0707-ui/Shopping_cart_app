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
/// Each call picks a fresh random delay in [100ms, 1000ms], shows the wait cursor for the
/// duration (ref-counted, so two overlapping interactions don't clear it early), and - when
/// given a control - disables it so it reads as "working" and can't be double-fired. The wait
/// is <c>await</c>ed, not slept, so the UI thread keeps redrawing and other controls keep
/// working while it runs.
///
/// Set the environment variable <c>SHOPPING_CART_DISABLE_INTERACTION_DELAY=1</c> to make every
/// call a no-op - used by the verifier and UI-test harnesses so their runs stay fast and
/// deterministic (they assert on behaviour, not on this cosmetic latency; one dedicated
/// verifier check launches without the bypass to confirm the delay is actually there).
/// </summary>
public static class InteractionDelay
{
    private static readonly bool Disabled = IsTruthy(
        Environment.GetEnvironmentVariable("SHOPPING_CART_DISABLE_INTERACTION_DELAY"));

    private static readonly Random Rng = new();

    // UI-thread only (every caller is an async void handler that resumes on the UI thread), so
    // a plain int is fine - no locking needed.
    private static int _pending;

    /// <summary>Lower bound of the random delay, milliseconds. Also the value a caller can
    /// compare against when checking "did this actually pause?".</summary>
    public const int MinMilliseconds = 100;

    /// <summary>Upper bound of the random delay, milliseconds.</summary>
    public const int MaxMilliseconds = 1000;

    /// <summary>Whether the delay is switched off for this process (see the class remarks).</summary>
    public static bool IsDisabled => Disabled;

    /// <param name="controlToDisable">The control that was interacted with - disabled for the
    /// duration and re-enabled afterwards (only if it was enabled to begin with). Pass null to
    /// just show the wait cursor, e.g. for a text box that must stay editable.</param>
    public static Task Wait(FrameworkElement? controlToDisable = null)
    {
        if (Disabled)
        {
            return Task.CompletedTask;
        }

        // MaxMilliseconds + 1 because Random.Next's upper bound is exclusive.
        return RunAsync(Rng.Next(MinMilliseconds, MaxMilliseconds + 1), controlToDisable);
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
