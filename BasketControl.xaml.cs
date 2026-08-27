using System;
using System.ComponentModel;
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
        UpdateTotal(basket);
        StatusText.Text = basket.StatusMessage;

        // Detached while syncing so this doesn't fire a redundant SelectionChanged - ApplySort
        // below is the single source of truth for actually applying the sort.
        SortComboBox.SelectionChanged -= SortComboBox_SelectionChanged;
        SortComboBox.SelectedIndex = (int)basket.SortOrder;
        SortComboBox.SelectionChanged += SortComboBox_SelectionChanged;
        ApplySort(basket, basket.SortOrder);
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
    /// every fresh Basket seeds from) is never mutated - only how this basket's list displays.</summary>
    private void ApplySort(Basket basket, ProductSortOrder order)
    {
        var view = CollectionViewSource.GetDefaultView(basket.Catalog);
        view.SortDescriptions.Clear();
        switch (order)
        {
            case ProductSortOrder.NameAscending:
                view.SortDescriptions.Add(new SortDescription(nameof(Product.Name), ListSortDirection.Ascending));
                break;
            case ProductSortOrder.PriceAscending:
                view.SortDescriptions.Add(new SortDescription(nameof(Product.Price), ListSortDirection.Ascending));
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
