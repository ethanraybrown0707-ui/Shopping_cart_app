using System.Windows;

namespace ShoppingCartApp;

/// <summary>
/// Settings &gt; Interaction Delay... - a small modal editor for <see cref="DelaySettings"/>
/// (on/off + the min/max millisecond range InteractionDelay picks from). OK validates and
/// persists via DelaySettings.Update; Cancel discards.
///
/// This window's own buttons deliberately do NOT go through InteractionDelay - it's the place
/// you go to turn a too-slow delay back down, so it must stay responsive whatever the setting.
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();

        EnabledCheckBox.IsChecked = DelaySettings.Enabled;
        MinTextBox.Text = DelaySettings.MinMilliseconds.ToString();
        MaxTextBox.Text = DelaySettings.MaxMilliseconds.ToString();
        UpdateRangeEnabled();
    }

    private void EnabledCheckBox_Changed(object sender, RoutedEventArgs e) => UpdateRangeEnabled();

    private void UpdateRangeEnabled() => RangeGrid.IsEnabled = EnabledCheckBox.IsChecked == true;

    private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
    {
        EnabledCheckBox.IsChecked = DelaySettings.DefaultEnabled;
        MinTextBox.Text = DelaySettings.DefaultMinMilliseconds.ToString();
        MaxTextBox.Text = DelaySettings.DefaultMaxMilliseconds.ToString();
        HideValidation();
        UpdateRangeEnabled();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var enabled = EnabledCheckBox.IsChecked == true;

        if (!int.TryParse(MinTextBox.Text.Trim(), out var min) || min < 0)
        {
            ShowValidation("Min must be a whole number of milliseconds (0 or more).");
            return;
        }
        if (!int.TryParse(MaxTextBox.Text.Trim(), out var max) || max < 0)
        {
            ShowValidation("Max must be a whole number of milliseconds (0 or more).");
            return;
        }
        if (min > max)
        {
            ShowValidation("Min can't be greater than Max.");
            return;
        }
        if (max > DelaySettings.CeilingMilliseconds)
        {
            ShowValidation($"Max can't exceed {DelaySettings.CeilingMilliseconds} ms.");
            return;
        }

        DelaySettings.Update(enabled, min, max);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ShowValidation(string message)
    {
        ValidationText.Text = message;
        ValidationText.Visibility = Visibility.Visible;
    }

    private void HideValidation() => ValidationText.Visibility = Visibility.Collapsed;
}
