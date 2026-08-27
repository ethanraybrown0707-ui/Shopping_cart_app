using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ShoppingCartApp;

/// <summary>
/// Persists completed orders to a JSON file next to the exe (order-history.json, same
/// AppContext.BaseDirectory convention ShoppingCartAppVerifier uses for its Logs folder), so
/// order history survives app restarts. One process-wide, shared history - every BasketControl
/// checkout adds to it directly rather than routing through MainWindow, since there's exactly
/// one list regardless of which basket/tab placed the order (see Basket's remarks on why
/// history isn't per-basket).
///
/// Deliberately simple for an app this size: the whole list is re-read/re-written on every
/// change rather than an incremental append-only format or a real database. Load/Save failures
/// (corrupt file, no write permission, etc.) are swallowed rather than crashing the app -
/// history is a convenience feature, not core cart/checkout functionality.
/// </summary>
public static class OrderHistoryStore
{
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "order-history.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static ObservableCollection<OrderRecord> Orders { get; } = new(Load());

    /// <summary>Records a completed order and immediately persists the updated history.
    /// Newest-first, so the history window shows the most recent order at the top.</summary>
    public static void Add(OrderRecord order)
    {
        Orders.Insert(0, order);
        Save();
    }

    private static List<OrderRecord> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<OrderRecord>();

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<OrderRecord>>(json) ?? new List<OrderRecord>();
        }
        catch
        {
            // Missing/corrupt/unreadable history file - start with an empty history rather than
            // crash the app over what's meant to be a convenience feature.
            return new List<OrderRecord>();
        }
    }

    private static void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Orders.ToList(), JsonOptions);
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Best-effort persistence - a failed write shouldn't block checkout from completing.
        }
    }
}
