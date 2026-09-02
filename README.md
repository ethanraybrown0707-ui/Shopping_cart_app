# ShoppingCartApp

A small **WPF / .NET 8** desktop shopping-cart app, built as a practice target for UI
automation. It's deliberately feature-rich for its size so there's plenty to drive and assert
against.

## Related repositories

This is one of three repos that make up the project:

| repo | what it is |
|---|---|
| **[Shopping_cart_app](https://github.com/ethanraybrown0707-ui/Shopping_cart_app)** | this repo — the app itself |
| [App_Verifier](https://github.com/ethanraybrown0707-ui/App_Verifier) | a standalone diagnostic tool (2 GUIs + 2 CLIs) that drives this app — and Windows Explorer — through simulated UI actions with **FlaUI / UI Automation** |
| [App_UI_tests](https://github.com/ethanraybrown0707-ui/App_UI_tests) | an **xUnit + FlaUI** test project for this app |

Clone the three side by side (same parent folder) — `App_Verifier` and `App_UI_tests` look for
`..\ShoppingCartApp\bin\...` by default (both also take a `--app-dll` / `SHOPPING_CART_APP_DLL`
override).

## What it does

- **Catalog** of 15 products; **Add to Cart** / **Remove** build a quantity per line; live cart total.
- **Multiple basket tabs** — each its own independent catalog + cart. A `+` in the tab strip
  adds one; each tab has a close button; tabs renumber to their position ("Basket 1", "Basket
  2", …) so the leftmost is always "Basket 1". At least one stays open.
- **Search** filters the catalog (case-insensitive); **Sort** by Default / Name / Price /
  "★ First"; a **"★ only"** checkbox filters to favourites.
- **Favourites** — a star toggle per product, persisted to `favourites.json`, shared across
  every basket tab and across restarts.
- **Order History** window (History menu) — every checkout is snapshotted to
  `order-history.json`; the window lists them newest-first by default and sorts by date or total.
- **"Images ▾"** per catalog row — opens Google Images / Bing Images / Wikipedia searches for
  that product in the default browser.
- **Shipping address** — a per-basket, session-only text box; checkout echoes its first line
  in the confirmation.
- **Settings ▸ Interaction Delay** — the app puts a configurable 0.1–1 s pause between every
  interaction and its effect (persisted to `interaction-delay.json`), so automation has to
  wait for state the way a real user would.

## Build & run

```
dotnet build ShoppingCartApp.csproj
dotnet bin/Debug/net8.0-windows/ShoppingCartApp.dll
```

Run the app through `dotnet <dll>` rather than the built `.exe` — on a machine that enforces
**Smart App Control**, the freshly-built, self-signed exe is blocked from launching directly.
`sign-exe.ps1` Authenticode-signs the publish exe with a local `CN=Ethan Brown` dev cert
(local trust only; does **not** satisfy Smart App Control — see the script's comments).

Persisted state (`favourites.json`, `order-history.json`, `interaction-delay.json`) lives next
to the built dll and is git-ignored.
