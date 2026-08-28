using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ShoppingCartApp;

/// <summary>
/// One basket tab's catalog + cart panel - the same Add/Remove/Checkout/UpdateTotal logic that
/// used to live directly in MainWindow, now operating on whichever Basket is bound as this
/// control's DataContext. When TabControl.ContentTemplate wraps this control in a DataTemplate,
/// WPF automatically sets its DataContext to the bound Basket - no manual wiring needed beyond
/// reacting to that assignment via DataContextChanged.
///
/// Every place that used to write straight to StatusText.Text also writes through
/// SetStatus(basket, ...), which stores the message on the Basket itself - see Basket.
/// StatusMessage's remarks for why (TabControl reuses this control instance across tabs).
/// SortComboBox follows the same pattern via ApplySort/basket.SortOrder.
/// </summary>
public partial class BasketControl : UserControl
{
    public BasketControl()
    {
        InitializeComponent();
        DataContextChanged += BasketControl_DataContextChanged;
    }

    private void BasketControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not Basket basket) return;

        CatalogListBox.ItemsSource = basket.Catalog;
        CartListBox.ItemsSource = basket.Cart;

        // Live sorting so that, while the "Favourites first" sort is active, ticking a
        // product's checkbox re-floats it immediately - without routing through the checkbox's
        // Click event, which only fires for a real user click, not a programmatic/AutomationPeer
        // toggle. Only IsFavourite is watched; Name/Price never change after creation.
        if (CollectionViewSource.GetDefaultView(basket.Catalog) is ICollectionViewLiveShaping live)
        {
            live.IsLiveSorting = true;
            if (!live.LiveSortingProperties.Contains(nameof(Product.IsFavourite)))
            {
                live.LiveSortingProperties.Add(nameof(Product.IsFavourite));
            }
        }

        UpdateTotal(basket);
        StatusText.Text = basket.StatusMessage;

        // Detached while syncing so this doesn't fire a redundant SelectionChanged - ApplySort
        // below is the single source of truth for actually applying the sort.
        SortComboBox.SelectionChanged -= SortComboBox_SelectionChanged;
        SortComboBox.SelectedIndex = (int)basket.SortOrder;
        SortComboBox.SelectionChanged += SortComboBox_SelectionChanged;

        // Same detach/reattach reasoning as SortComboBox above - avoids a redundant
        // TextChanged firing while syncing the box to this basket's own search text.
        SearchTextBox.TextChanged -= SearchTextBox_TextChanged;
        SearchTextBox.Text = basket.SearchText;
        SearchTextBox.TextChanged += SearchTextBox_TextChanged;

        ApplySort(basket, basket.SortOrder);
        ApplyFilter(basket, basket.SearchText);
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is not Basket basket) return;

        basket.SearchText = SearchTextBox.Text;
        ApplyFilter(basket, basket.SearchText);
    }

    /// <summary>Filters via the catalog's CollectionView (same mechanism ApplySort uses for
    /// sorting) rather than swapping ItemsSource, so filtering and sorting compose without
    /// interfering with each other and basket.Catalog itself is never mutated.</summary>
    private void ApplyFilter(Basket basket, string searchText)
    {
        var view = CollectionViewSource.GetDefaultView(basket.Catalog);
        view.Filter = string.IsNullOrWhiteSpace(searchText)
            ? null
            : item => item is Product product &&
                      product.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not Basket basket) return;
        if (SortComboBox.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
        if (!Enum.TryParse<ProductSortOrder>(tag, out var order)) return;

        basket.SortOrder = order;
        ApplySort(basket, order);
    }

    /// <summary>Sorts via the catalog's CollectionView rather than reordering basket.Catalog
    /// itself, so the underlying collection (and Product.DefaultCatalog's original order that
    /// every fresh Basket seeds from) is never mutated - only how this basket's list displays.
    /// Default and the Name/Price sorts ignore favourites; "Favourites first" is Default order
    /// with the favourited products lifted to the top.</summary>
    private void ApplySort(Basket basket, ProductSortOrder order)
    {
        var view = CollectionViewSource.GetDefaultView(basket.Catalog);
        view.SortDescriptions.Clear();
        switch (order)
        {
            case ProductSortOrder.NameAscending:
                view.SortDescriptions.Add(new SortDescription(nameof(Product.Name), ListSortDirection.Ascending));
                break;
            case ProductSortOrder.NameDescending:
                view.SortDescriptions.Add(new SortDescription(nameof(Product.Name), ListSortDirection.Descending));
                break;
            case ProductSortOrder.PriceAscending:
                view.SortDescriptions.Add(new SortDescription(nameof(Product.Price), ListSortDirection.Ascending));
                break;
            case ProductSortOrder.PriceDescending:
                view.SortDescriptions.Add(new SortDescription(nameof(Product.Price), ListSortDirection.Descending));
                break;
            case ProductSortOrder.FavouritesFirst:
                // Favourites (persisted) on top, then everything in the original catalog order.
                view.SortDescriptions.Add(new SortDescription(nameof(Product.IsFavourite), ListSortDirection.Descending));
                view.SortDescriptions.Add(new SortDescription(nameof(Product.CatalogPosition), ListSortDirection.Ascending));
                break;
            case ProductSortOrder.Default:
            default:
                break; // no SortDescriptions -> original catalog order
        }
    }

    private void AddToCart_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not Basket basket) return;
        if (sender is not FrameworkElement { Tag: Product product }) return;

        var existingLine = basket.Cart.FirstOrDefault(line => line.Product == product);
        if (existingLine is not null)
        {
            existingLine.Quantity++;
        }
        else
        {
            basket.Cart.Add(new CartLine { Product = product });
        }

        UpdateTotal(basket);
        SetStatus(basket, $"Added \"{product.Name}\" to cart");
    }

    private void FavouriteCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not Basket basket) return;
        if (sender is not FrameworkElement { Tag: Product product }) return;

        // IsChecked's two-way binding has already written the new value through to
        // Product.IsFavourite (and FavouritesStore) by the time Click fires. The re-float is
        // handled by live sorting (see DataContextChanged), not here - this only adds the
        // status-line feedback a real click deserves.
        SetStatus(basket, product.IsFavourite
            ? $"Added \"{product.Name}\" to favourites"
            : $"Removed \"{product.Name}\" from favourites");
    }

    private void OpenImageLink_Click(object sender, RoutedEventArgs e)
    {
        // Each link MenuItem carries its target URL in Tag (bound to a Product.*Url property).
        if (sender is not MenuItem { Tag: string url }) return;

        try
        {
            // UseShellExecute so the OS opens it in the default browser - a bare
            // Process.Start(string) throws on .NET without it.
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            // Opening a link is a convenience - a missing browser or bad URL shouldn't crash.
            if (DataContext is Basket basket) SetStatus(basket, $"Couldn't open image link: {ex.Message}");
        }
    }

    private void RemoveFromCart_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not Basket basket) return;
        if (sender is not FrameworkElement { Tag: CartLine line }) return;

        // One click removes one unit; the row itself only disappears once its quantity
        // reaches 0, mirroring how Add to Cart builds the quantity up.
        if (line.Quantity > 1)
        {
            line.Quantity--;
        }
        else
        {
            basket.Cart.Remove(line);
        }

        UpdateTotal(basket);
        SetStatus(basket, $"Removed \"{line.Name}\" from cart");
    }

    private void Checkout_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not Basket basket) return;

        if (basket.Cart.Count == 0)
        {
            SetStatus(basket, "Cart is empty");
            return;
        }

        var itemCount = basket.Cart.Sum(line => line.Quantity);

        // Snapshot the cart into an OrderRecord before clearing it - CartLine itself is about to
        // be discarded, and OrderHistoryStore needs plain data it can serialize independently of
        // this basket's live state.
        OrderHistoryStore.Add(new OrderRecord
        {
            PlacedAt = DateTime.Now,
            Total = basket.Cart.Sum(line => line.LineTotal),
            Lines = basket.Cart.Select(line => new OrderLineRecord
            {
                ProductName = line.Name,
                Quantity = line.Quantity,
                LineTotal = line.LineTotal,
            }).ToList(),
        });

        basket.Cart.Clear();
        UpdateTotal(basket);
        SetStatus(basket, $"Order placed - {itemCount} item(s) checked out");
    }

    private void UpdateTotal(Basket basket)
    {
        var total = basket.Cart.Sum(line => line.LineTotal);
        TotalText.Text = $"Total: {total:C}";
    }

    private void SetStatus(Basket basket, string message)
    {
        basket.StatusMessage = message;
        StatusText.Text = message;
    }
}
