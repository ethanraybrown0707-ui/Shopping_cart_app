using System.ComponentModel;

namespace ShoppingCartApp;

/// <summary>A catalog product. No INotifyPropertyChanged needed since fields never change
/// after creation - only the containing ObservableCollection needs to notify.</summary>
public class Product
{
    public required string Name { get; init; }
    public required decimal Price { get; init; }
    public string DisplayPrice => Price.ToString("C");
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
