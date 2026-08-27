using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ShoppingCartApp;

/// <summary>A catalog product. No INotifyPropertyChanged needed since fields never change
/// after creation - only the containing ObservableCollection needs to notify.</summary>
public class Product
{
    public required string Name { get; init; }
    public required decimal Price { get; init; }
    public string DisplayPrice => Price.ToString("C");

    /// <summary>The catalog every new Basket seeds from. Product has no mutable state, so it's
    /// safe for every basket's Catalog to wrap the same Product instances.</summary>
    public static readonly Product[] DefaultCatalog =
    {
        new() { Name = "Wireless Mouse", Price = 24.99m },
        new() { Name = "Mechanical Keyboard", Price = 79.99m },
        new() { Name = "USB-C Hub", Price = 34.50m },
        new() { Name = "Desk Lamp", Price = 18.75m },
    };
}

/// <summary>
/// One line in the cart - one CartLine per distinct Product, with a Quantity that goes up when
/// the same product is added again (rather than adding a second row for it). Quantity needs
/// INotifyPropertyChanged since, unlike Product, it changes after the line already exists and
/// is on screen - the bound TextBlocks need to hear about that to refresh.
/// </summary>
public class CartLine : INotifyPropertyChanged
{
    private int _quantity = 1;

    public required Product Product { get; init; }

    public int Quantity
    {
        get => _quantity;
        set
        {
            if (_quantity == value) return;
            _quantity = value;
            OnPropertyChanged(nameof(Quantity));
            OnPropertyChanged(nameof(DisplayQuantity));
            OnPropertyChanged(nameof(LineTotal));
            OnPropertyChanged(nameof(DisplayLineTotal));
        }
    }

    public string Name => Product.Name;
    public string DisplayQuantity => $"x{Quantity}";
    public decimal LineTotal => Product.Price * Quantity;
    public string DisplayLineTotal => LineTotal.ToString("C");

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>How a Basket's Catalog should be ordered. Default preserves Product.DefaultCatalog's
/// own order (no sort applied at all, rather than sorting by some implicit key).</summary>
public enum ProductSortOrder
{
    Default,
    NameAscending,
    NameDescending,
    PriceAscending,
    PriceDescending,
}

/// <summary>
/// One tab's worth of state: its own catalog + cart, fully independent of every other Basket.
///
/// Header is positional, not a permanent identity - MainWindow renumbers every Basket's Header
/// to match its left-to-right position ("Basket 1", "Basket 2", ...) after every add/close, so
/// the leftmost tab always reads "Basket 1". That means Header can change after creation (unlike
/// Product, which never changes), so it needs INotifyPropertyChanged for the bound TabItem
/// header/AutomationProperties.Name to pick up a renumber.
///
/// StatusMessage and SortOrder live here, not just as transient control state (TextBlock.Text /
/// ComboBox.SelectedIndex), because WPF's TabControl reuses a single BasketControl visual
/// instance across tab switches (only rebinding DataContext) rather than creating a fresh one
/// per tab - without storing them per-Basket, both would leak whichever basket was viewed last
/// instead of reflecting this basket's own status/sort choice.
/// </summary>
public class Basket : INotifyPropertyChanged
{
    private string _header = "";

    public string Header
    {
        get => _header;
        set
        {
            if (_header == value) return;
            _header = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Header)));
        }
    }

    public ObservableCollection<Product> Catalog { get; } = new(Product.DefaultCatalog);
    public ObservableCollection<CartLine> Cart { get; } = new();
    public string StatusMessage { get; set; } = "Ready";
    public ProductSortOrder SortOrder { get; set; } = ProductSortOrder.Default;

    public event PropertyChangedEventHandler? PropertyChanged;
}
