using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace ShoppingCartApp;

/// <summary>
/// A catalog product. Name/Price/CatalogPosition never change after creation; IsFavourite does
/// (it's a per-user toggle, persisted via FavouritesStore), so this needs INotifyPropertyChanged
/// - the bound checkbox and the "Favourites first" sort both have to hear about a change. Every
/// basket's Catalog wraps the same shared Product instances, so favouriting a product in one
/// basket tab shows up in all of them, which matches how favourites are meant to work.
/// </summary>
public class Product : INotifyPropertyChanged
{
    public required string Name { get; init; }
    public required decimal Price { get; init; }

    /// <summary>Position in DefaultCatalog (0-based). Used as the "Favourites first" sort's
    /// tie-breaker so, within and below the favourites, items stay in the original order.</summary>
    public int CatalogPosition { get; init; }

    public string DisplayPrice => Price.ToString("C");

    /// <summary>Whether this product is one of the user's favourites. Backed by FavouritesStore
    /// (persisted to favourites.json), not a plain field, so the value is shared across every
    /// basket's view of this same Product instance and survives app restarts.</summary>
    public bool IsFavourite
    {
        get => FavouritesStore.Contains(Name);
        set
        {
            if (value == FavouritesStore.Contains(Name)) return;
            FavouritesStore.Set(Name, value);
            OnPropertyChanged(nameof(IsFavourite));
        }
    }

    // Image links for the catalog row's "Images" dropdown. There's no real product-image
    // data, so these are just image searches built from the name - each opens in the default
    // browser (see BasketControl.OpenImageLink_Click). Computed, like DisplayPrice.
    public string GoogleImagesUrl => $"https://www.google.com/search?tbm=isch&q={Uri.EscapeDataString(Name)}";
    public string BingImagesUrl => $"https://www.bing.com/images/search?q={Uri.EscapeDataString(Name)}";
    public string WikipediaUrl => $"https://en.wikipedia.org/w/index.php?search={Uri.EscapeDataString(Name)}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>The catalog every new Basket seeds from. Product has no per-basket state, so it's
    /// safe for every basket's Catalog to wrap the same Product instances.</summary>
    public static readonly Product[] DefaultCatalog =
    {
        new() { CatalogPosition = 0, Name = "Wireless Mouse", Price = 24.99m },
        new() { CatalogPosition = 1, Name = "Mechanical Keyboard", Price = 79.99m },
        new() { CatalogPosition = 2, Name = "USB-C Hub", Price = 34.50m },
        new() { CatalogPosition = 3, Name = "Desk Lamp", Price = 18.75m },
        new() { CatalogPosition = 4, Name = "Webcam", Price = 45.00m },
        new() { CatalogPosition = 5, Name = "Laptop Stand", Price = 29.99m },
        new() { CatalogPosition = 6, Name = "Wireless Charger", Price = 22.50m },
        new() { CatalogPosition = 7, Name = "Bluetooth Speaker", Price = 39.99m },
        new() { CatalogPosition = 8, Name = "Monitor Arm", Price = 65.00m },
        new() { CatalogPosition = 9, Name = "Noise-Cancelling Headphones", Price = 129.99m },
        new() { CatalogPosition = 10, Name = "Portable SSD", Price = 89.99m },
        new() { CatalogPosition = 11, Name = "HDMI Cable", Price = 9.99m },
        new() { CatalogPosition = 12, Name = "Laptop Backpack", Price = 54.99m },
        new() { CatalogPosition = 13, Name = "Ergonomic Footrest", Price = 27.50m },
        new() { CatalogPosition = 14, Name = "USB Microphone", Price = 69.00m },
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

/// <summary>How a Basket's Catalog should be ordered. Default is Product.DefaultCatalog's own
/// order; Name/Price are strict sorts; FavouritesFirst is that Default order with the user's
/// favourites lifted to the top. New values must be appended (SortComboBox binds by index).</summary>
public enum ProductSortOrder
{
    Default,
    NameAscending,
    NameDescending,
    PriceAscending,
    PriceDescending,
    FavouritesFirst,
}

/// <summary>
/// One tab's worth of state: its own catalog + cart, fully independent of every other Basket.
///
/// Header is positional, not a permanent identity - MainWindow renumbers every Basket's Header
/// to match its left-to-right position ("Basket 1", "Basket 2", ...) after every add/close, so
/// the leftmost tab always reads "Basket 1". That means Header can change after creation, so it
/// needs INotifyPropertyChanged for the bound TabItem header/AutomationProperties.Name to pick
/// up a renumber.
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

    // Same reasoning as StatusMessage/SortOrder above - stored on the Basket, not just
    // SearchBox.Text / the checkbox, so switching tabs doesn't leak one basket's search text
    // or "favourites only" toggle into another's reused BasketControl instance.
    public string SearchText { get; set; } = "";
    public bool FavouritesOnly { get; set; }

    /// <summary>Where this basket's order ships. Free text the user types before checkout;
    /// stored per-basket (same reasoning as SearchText) and, like it, not persisted to disk -
    /// it lives only for the session. Checkout echoes its first non-blank line in the
    /// confirmation status.</summary>
    public string ShippingAddress { get; set; } = "";

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>One line of a completed order - a frozen snapshot of a CartLine at checkout time
/// (plain data, not a live reference to it), since the CartLine itself gets cleared/discarded
/// once the order is placed.</summary>
public class OrderLineRecord
{
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }

    // Computed, not persisted data - [JsonIgnore] keeps it out of order-history.json so the file
    // stays pure data and never carries a stale rendering of a format string that might change.
    [JsonIgnore]
    public string Display => $"{ProductName} x{Quantity} - {LineTotal:C}";
}

/// <summary>
/// A completed checkout, persisted to disk (see OrderHistoryStore) so it survives app restarts.
/// One shared history across every basket, not per-basket - basket tabs are transient
/// (renumbered, closable), so tying history to a specific tab wouldn't survive closing it.
/// </summary>
public class OrderRecord
{
    public DateTime PlacedAt { get; set; }
    public List<OrderLineRecord> Lines { get; set; } = new();
    public decimal Total { get; set; }

    [JsonIgnore]
    public string DisplaySummary =>
        $"{PlacedAt:yyyy-MM-dd HH:mm} - {Lines.Sum(l => l.Quantity)} item(s) - {Total:C}";
}
