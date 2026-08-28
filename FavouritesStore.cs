using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ShoppingCartApp;

/// <summary>
/// Remembers which products the user has marked as favourites, persisted to favourites.json
/// next to the exe (same AppContext.BaseDirectory convention as OrderHistoryStore) so the set
/// survives app restarts. One process-wide set keyed by product name - favouriting is per-user,
/// not per-basket (basket tabs are transient; see Basket's remarks), and Product.IsFavourite
/// reads/writes straight through here.
///
/// Same deliberate simplicity as OrderHistoryStore: the whole set is re-written on every change,
/// and Load/Save failures are swallowed rather than crashing the app - favourites are a
/// convenience, not core cart/checkout functionality.
/// </summary>
public static class FavouritesStore
{
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "favourites.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static readonly HashSet<string> Favourites = new(Load(), StringComparer.Ordinal);

    public static bool Contains(string productName) => Favourites.Contains(productName);

    /// <summary>Adds or removes a product from favourites and persists the change. No-ops (and
    /// skips the write) if the product is already in the requested state.</summary>
    public static void Set(string productName, bool isFavourite)
    {
        var changed = isFavourite ? Favourites.Add(productName) : Favourites.Remove(productName);
        if (changed) Save();
    }

    private static List<string> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<string>();

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            // Missing/corrupt/unreadable file - start with no favourites rather than crash over
            // what's meant to be a convenience feature.
            return new List<string>();
        }
    }

    private static void Save()
    {
        try
        {
            // Sorted so the file is stable/diffable rather than in HashSet iteration order.
            var json = JsonSerializer.Serialize(Favourites.OrderBy(n => n, StringComparer.Ordinal).ToList(), JsonOptions);
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Best-effort persistence - a failed write shouldn't block anything.
        }
    }
}
