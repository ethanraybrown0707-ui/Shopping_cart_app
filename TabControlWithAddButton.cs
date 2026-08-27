using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace ShoppingCartApp;

/// <summary>
/// A TabControl whose ControlTemplate adds a "+" button into the tab strip itself (see
/// MainWindow.xaml's TabsWithAddButtonTemplate), Chrome/Google-tabs style, instead of a
/// separate labeled button elsewhere in the window.
///
/// Plain TabControl's automation peer (TabControlAutomationPeer, via SelectorAutomationPeer)
/// only reports TabItem children - any other element placed in the ControlTemplate (like that
/// "+" button) renders fine on screen but is completely absent from the UI Automation tree, so
/// it can't be found by AutomationId. This subclass exists solely to fix that: its automation
/// peer walks the realized template for a button carrying a specific Tag marker and folds its
/// peer into the normal children list, alongside the usual TabItem peers.
/// </summary>
public class TabControlWithAddButton : TabControl
{
    /// <summary>Set this as the "+" button's Tag in XAML so the automation peer can find it
    /// without depending on AutomationId (which is also just a property on the same element,
    /// but a dedicated marker keeps this decoupled from whatever AutomationId ends up being).</summary>
    public const string AddButtonMarker = "AddButton";

    protected override AutomationPeer OnCreateAutomationPeer() => new TabControlWithAddButtonAutomationPeer(this);
}

public class TabControlWithAddButtonAutomationPeer : TabControlAutomationPeer
{
    public TabControlWithAddButtonAutomationPeer(TabControlWithAddButton owner) : base(owner)
    {
    }

    protected override List<AutomationPeer> GetChildrenCore()
    {
        var children = base.GetChildrenCore() ?? new List<AutomationPeer>();

        if (Owner is TabControlWithAddButton owner)
        {
            owner.ApplyTemplate(); // ensure the template's visual tree exists before searching it
            var addButton = FindAddButton(owner);
            if (addButton is not null)
            {
                var peer = UIElementAutomationPeer.CreatePeerForElement(addButton);
                if (peer is not null && !children.Contains(peer))
                {
                    children.Add(peer);
                }
            }
        }

        return children;
    }

    private static Button? FindAddButton(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Button { Tag: string tag } button && tag == TabControlWithAddButton.AddButtonMarker)
            {
                return button;
            }

            var found = FindAddButton(child);
            if (found is not null) return found;
        }

        return null;
    }
}
