using System.Windows;

namespace ShoppingCartApp;

/// <summary>Read-only view of OrderHistoryStore.Orders - no editing/deleting orders, this is
/// purely a history log.</summary>
public partial class OrderHistoryWindow : Window
{
    public OrderHistoryWindow()
    {
        InitializeComponent();
        OrdersListBox.ItemsSource = OrderHistoryStore.Orders;
        UpdateEmptyState();
        OrderHistoryStore.Orders.CollectionChanged += (_, _) => UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        EmptyText.Visibility = OrderHistoryStore.Orders.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
