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

    // False until the constructor has established the initial sort, so the SelectedIndex = 0
    // assignment below applies its sort synchronously (no delay) rather than leaving the list
    // briefly unsorted while an InteractionDelay runs.
    private bool _ready;

    public OrderHistoryWindow()
    {
        InitializeComponent();
        OrdersListBox.ItemsSource = _ordersView.View;
        HistorySortComboBox.SelectedIndex = 0; // "Newest first" - fires SelectionChanged -> ApplySort
        _ready = true;
        UpdateEmptyState();
        OrderHistoryStore.Orders.CollectionChanged += (_, _) => UpdateEmptyState();
    }

    private async void HistorySortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // A user changing the sort gets the same 0.1-1s beat as every other interaction in the
        // app (combo disabled + wait cursor meanwhile); the constructor's initial selection
        // does not.
        if (_ready)
        {
            await InteractionDelay.Wait(HistorySortComboBox);
        }

        ApplySort();
    }

    /// <summary>Sorts <see cref="_ordersView"/> (this window's view), never
    /// OrderHistoryStore.Orders itself, so the stored/persisted order is untouched. The date
    /// sorts are explicit PlacedAt sorts rather than relying on the store's insert order.</summary>
    private void ApplySort()
    {
        var tag = (HistorySortComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
        var sorts = _ordersView.View.SortDescriptions;
        sorts.Clear();
        switch (tag)
        {
            case "Oldest":
                sorts.Add(new SortDescription(nameof(OrderRecord.PlacedAt), ListSortDirection.Ascending));
                break;
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
