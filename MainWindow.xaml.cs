using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace ShoppingCartApp;

/// <summary>
/// Feature 1: a hardcoded product catalog, per-row "Add to Cart" buttons, a cart panel with a
/// running total, and a Checkout button that clears the cart. Pure in-memory, no persistence -
/// this is a UI test fixture, not a real store.
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

    public ObservableCollection<Product> Cart { get; } = new();

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
            Cart.Add(product);
            UpdateTotal();
            StatusText.Text = $"Added \"{product.Name}\" to cart";
        }
    }

    private void Checkout_Click(object sender, RoutedEventArgs e)
    {
        if (Cart.Count == 0)
        {
            StatusText.Text = "Cart is empty";
            return;
        }

        var itemCount = Cart.Count;
        Cart.Clear();
        UpdateTotal();
        StatusText.Text = $"Order placed - {itemCount} item(s) checked out";
    }

    private void UpdateTotal()
    {
        var total = Cart.Sum(p => p.Price);
        TotalText.Text = $"Total: {total:C}";
    }
}
