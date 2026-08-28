using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ShoppingCartApp;

/// <summary>Read-only view of OrderHistoryStore.Orders - no editing/deleting orders, this is
/// purely a history log. The Sort dropdown reorders how this window lists the orders without
/// touching the stored (and persisted) collection.</summary>
public partial class OrderHistoryWindow : Window
{
    // A window-local view over the shared Orders collection: each OrderHistoryWindow instance
    // gets its own sort, and closing one doesn't leave the store reordered. (GetDefaultView
    // would be shared across every instance and outlive them.)
    private readonly CollectionViewSource _ordersView = new() { Source = OrderHistoryStore.Orders };

    public OrderHistoryWindow()
    {
        InitializeComponent();
        OrdersListBox.ItemsSource = _ordersView.View;
        HistorySortComboBox.SelectedIndex = 0; // "Newest first" - fires SelectionChanged -> ApplySort
        UpdateEmptyState();
        OrderHistoryStore.Orders.CollectionChanged += (_, _) => UpdateEmptyState();
    }

    private void HistorySortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplySort();

    /// <summary>Sorts <see cref="_ordersView"/> (this window's view), never
    /// OrderHistoryStore.Orders itself, so the stored/persisted order is untouched. "Newest
    /// first" is an explicit PlacedAt-descending sort rather than relying on the store's
    /// insert order.</summary>
    private void ApplySort()
    {
        var tag = (HistorySortComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
        var sorts = _ordersView.View.SortDescriptions;
        sorts.Clear();
        switch (tag)
        {
            case "TotalAscending":
                sorts.Add(new SortDescription(nameof(OrderRecord.Total), ListSortDirection.Ascending));
                break;
            case "TotalDescending":
                sorts.Add(new SortDescription(nameof(OrderRecord.Total), ListSortDirection.Descending));
                break;
            default: // "Newest first"
                sorts.Add(new SortDescription(nameof(OrderRecord.PlacedAt), ListSortDirection.Descending));
                break;
        }
    }

    private void UpdateEmptyState()
    {
        EmptyText.Visibility = OrderHistoryStore.Orders.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
