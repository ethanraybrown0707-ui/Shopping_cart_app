namespace ShoppingCartApp;

/// <summary>A catalog product. Also used directly as a cart line item - "Add to Cart" just
/// appends the same Product reference to the cart, so adding one product twice yields two
/// entries rather than a quantity count. Keeps cart state trivial for this first feature
/// slice; no INotifyPropertyChanged needed since a Product's own fields never change after
/// creation - only the containing ObservableCollections need to notify.</summary>
public class Product
{
    public required string Name { get; init; }
    public required decimal Price { get; init; }
    public string DisplayPrice => Price.ToString("C");
}
