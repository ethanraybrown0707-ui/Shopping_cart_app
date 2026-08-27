using System.Collections.ObjectModel;
using System.Windows;

namespace ShoppingCartApp;

/// <summary>
/// Hosts one or more independent baskets, each its own tab (own catalog + own cart). "New
/// Basket" adds a tab; each tab's header has a close button. There must always be at least one
/// basket open, so closing the last remaining tab is a no-op. All the actual catalog/cart/
/// checkout logic lives per-tab in BasketControl - this window only manages which baskets exist.
///
/// Headers are positional, not permanent identities: after every add/close, RenumberBaskets
/// relabels every Basket to match its left-to-right position, so the leftmost tab always reads
/// "Basket 1" (rather than counting up forever and leaving gaps once a basket closes).
/// </summary>
public partial class MainWindow : Window
{
    public ObservableCollection<Basket> Baskets { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        BasketsTabControl.ItemsSource = Baskets;

        var firstBasket = new Basket();
        Baskets.Add(firstBasket);
        RenumberBaskets();
        BasketsTabControl.SelectedItem = firstBasket;
    }

    private void NewBasketButton_Click(object sender, RoutedEventArgs e)
    {
        var basket = new Basket();
        Baskets.Add(basket);
        RenumberBaskets();
        BasketsTabControl.SelectedItem = basket;
    }

    private void CloseBasket_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Basket basket }) return;

        // Always keep at least one basket open.
        if (Baskets.Count <= 1) return;

        Baskets.Remove(basket);
        RenumberBaskets();
    }

    private void RenumberBaskets()
    {
        for (var i = 0; i < Baskets.Count; i++)
        {
            Baskets[i].Header = $"Basket {i + 1}";
        }
    }
}
