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
/// One line in the cart. Deliberately a distinct object per "Add to Cart" click - even for the
/// same Product added twice - rather than putting Product objects straight into the cart
/// collection. WPF's ItemsControlAutomationPeer keys its automation-peer cache by item
/// reference, so two ListBoxItems bound to the very same object collapse into a single
/// UI-Automation element (the visual tree still renders both rows correctly, but FlaUI/UIA
/// callers can only see and count one of them). Wrapping each addition in its own CartLine
/// gives every row distinct identity, which is what makes duplicate cart entries reliably
/// testable via UI Automation.
/// </summary>
public class CartLine
{
    public required Product Product { get; init; }
    public string Name => Product.Name;
    public string DisplayPrice => Product.DisplayPrice;
}
