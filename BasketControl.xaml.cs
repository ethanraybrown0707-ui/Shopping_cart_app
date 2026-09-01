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
///
/// Every user-facing handler here is async: it awaits <see cref="InteractionDelay"/> before
/// applying its effect, so there's a deliberate 0.1-1s beat between clicking/typing and the
/// result (the control disables and the wait cursor shows meanwhile). The delay is a no-op
/// when SHOPPING_CART_DISABLE_INTERACTION_DELAY=1.
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

        // Live shaping keyed on IsFavourite so a favourite toggle re-shapes the view as soon as
        // Product.IsFavourite actually changes (which, with the interaction delay, is a beat
        // after the click) - and so a toggle in one basket tab re-shapes the others too:
        //  - live sorting: re-floats the item while the "Favourites first" sort is active;
        //  - live filtering: drops the item out while the "favourites only" filter is on.
        // Only IsFavourite is watched; Name/Price never change after creation.
        if (CollectionViewSource.GetDefaultView(basket.Catalog) is ICollectionViewLiveShaping live)
        {
            live.IsLiveSorting = true;
            if (!live.LiveSortingProperties.Contains(nameof(Product.IsFavourite)))
            {
                live.LiveSortingProperties.Add(nameof(Product.IsFavourite));
            }

            live.IsLiveFiltering = true;
            if (!live.LiveFilteringProperties.Contains(nameof(Product.IsFavourite)))
            {
                live.LiveFilteringProperties.Add(nameof(Product.IsFavourite));
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

        // Same detach/reattach reasoning as SortComboBox/SearchTextBox - Checked/Unchecked
        // (unlike Click) fire on a programmatic IsChecked change too.
        FavouritesOnlyCheckBox.Checked -= FavouritesOnlyCheckBox_Toggled;
        FavouritesOnlyCheckBox.Unchecked -= FavouritesOnlyCheckBox_Toggled;
        FavouritesOnlyCheckBox.IsChecked = basket.FavouritesOnly;
        FavouritesOnlyCheckBox.Checked += FavouritesOnlyCheckBox_Toggled;
        FavouritesOnlyCheckBox.Unchecked += FavouritesOnlyCheckBox_Toggled;

        ApplySort(basket, basket.SortOrder);
        ApplyFilter(basket);
    }

    private async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is not Basket basket) return;

        // The typed text is the interaction; recording it is immediate. Applying it to the
        // list is the "effect" and gets the delay. The box itself stays enabled (disabling a
        // control mid-type would eat keystrokes), so this only shows the wait cursor.
        basket.SearchText = SearchTextBox.Text;
        await InteractionDelay.Wait();
        ApplyFilter(basket);
    }

    private async void FavouritesOnlyCheckBox_Toggled(object sender, RoutedEventArgs e)
    {
        if (DataContext is not Basket basket) return;

        var showFavouritesOnly = FavouritesOnlyCheckBox.IsChecked == true;
        await InteractionDelay.Wait(FavouritesOnlyCheckBox);

        basket.FavouritesOnly = showFavouritesOnly;
        ApplyFilter(basket);
        SetStatus(basket, basket.FavouritesOnly ? "Showing favourites only" : "Showing all products");
    }

    /// <summary>Filters via the catalog's CollectionView (same mechanism ApplySort uses for
    /// sorting) rather than swapping ItemsSource, so filtering and sorting compose without
    /// interfering with each other and basket.Catalog itself is never mutated. Two conditions,
    /// AND-ed: the search text (if any) and, when "favourites only" is on, IsFavourite.</summary>
    private void ApplyFilter(Basket basket)
    {
        var view = CollectionViewSource.GetDefaultView(basket.Catalog);
        var search = basket.SearchText;

        if (string.IsNullOrWhiteSpace(search) && !basket.FavouritesOnly)
        {
            view.Filter = null;
            return;
        }

        view.Filter = item =>
            item is Product product
            && (string.IsNullOrWhiteSpace(search) || product.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            && (!basket.FavouritesOnly || product.IsFavourite);
    }

    private async void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not Basket basket) return;
        if (SortComboBox.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
        if (!Enum.TryParse<ProductSortOrder>(tag, out var order)) return;

        await InteractionDelay.Wait(SortComboBox);

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

    private async void AddToCart_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not Basket basket) return;
        if (sender is not FrameworkElement { Tag: Product product } button) return;

        await InteractionDelay.Wait(button);

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

    private async void FavouriteCheckBox_Toggled(object sender, RoutedEventArgs e)
    {
        if (DataContext is not Basket basket) return;
        if (sender is not CheckBox { Tag: Product product } box) return;

        // IsChecked is one-way from Product.IsFavourite, so this handler also fires when the
        // binding echoes a change back (or when another tab toggled the same product) - in
        // which case there's nothing to do and no delay to impose.
        var wantFavourite = box.IsChecked == true;
        if (product.IsFavourite == wantFavourite) return;

        await InteractionDelay.Wait(box);

        // Persists via FavouritesStore and raises PropertyChanged, which the catalog's live
        // sorting/filtering (see DataContextChanged) reacts to - so the re-float / drop-out
        // happens here, a beat after the click.
        product.IsFavourite = wantFavourite;
        SetStatus(basket, wantFavourite
            ? $"Added \"{product.Name}\" to favourites"
            : $"Removed \"{product.Name}\" from favourites");
    }

    private async void OpenImageLink_Click(object sender, RoutedEventArgs e)
    {
        // Each link MenuItem carries its target URL in Tag (bound to a Product.*Url property).
        if (sender is not MenuItem { Tag: string url } menuItem) return;

        await InteractionDelay.Wait(menuItem);

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

    private async void RemoveFromCart_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not Basket basket) return;
        if (sender is not FrameworkElement { Tag: CartLine line } button) return;

        await InteractionDelay.Wait(button);

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

    private async void Checkout_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not Basket basket) return;

        await InteractionDelay.Wait(sender as FrameworkElement);

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
