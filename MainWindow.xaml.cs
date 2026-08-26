using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace ShoppingCartApp;

/// <summary>
/// A hardcoded product catalog, per-row "Add to Cart" buttons, a cart panel (one row per
/// distinct product, with a quantity, a per-row "Remove" button, and a running total), and a
/// Checkout button that clears the cart. Pure in-memory, no persistence - this is a UI test
/// fixture, not a real store.
/// </summary>
public partial class MainWindow : Window
{
    public ObservableCollection<Product> Catalog { get; } = new(new[]
    {
        new Product { Name = "Wireless Mouse", Price = 24.99m },
        new Product { Name = "Mechanical Keyboard", Price = 79.99m },
        new Product { Name = "USB-C Hub", Price = 34.50m },
        new Product { Name = "Desk Lamp", Price = 18.75m },
    });

    // At most one CartLine per Product - see the remarks on CartLine in Models.cs.
    public ObservableCollection<CartLine> Cart { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        CatalogListBox.ItemsSource = Catalog;
        CartListBox.ItemsSource = Cart;
        UpdateTotal();
    }

    private void AddToCart_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Product product })
        {
            var existingLine = Cart.FirstOrDefault(line => line.Product == product);
            if (existingLine is not null)
            {
                existingLine.Quantity++;
            }
            else
            {
                Cart.Add(new CartLine { Product = product });
            }

            UpdateTotal();
            StatusText.Text = $"Added \"{product.Name}\" to cart";
        }
    }

    private void RemoveFromCart_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: CartLine line })
        {
            // One click removes one unit; the row itself only disappears once its quantity
            // reaches 0, mirroring how Add to Cart builds the quantity up.
            if (line.Quantity > 1)
            {
                line.Quantity--;
            }
            else
            {
                Cart.Remove(line);
            }

            UpdateTotal();
            StatusText.Text = $"Removed \"{line.Name}\" from cart";
        }
    }

    private void Checkout_Click(object sender, RoutedEventArgs e)
    {
        if (Cart.Count == 0)
        {
            StatusText.Text = "Cart is empty";
            return;
        }

        var itemCount = Cart.Sum(line => line.Quantity);
        Cart.Clear();
        UpdateTotal();
        StatusText.Text = $"Order placed - {itemCount} item(s) checked out";
    }

    private void UpdateTotal()
    {
        var total = Cart.Sum(line => line.LineTotal);
        TotalText.Text = $"Total: {total:C}";
    }
}
