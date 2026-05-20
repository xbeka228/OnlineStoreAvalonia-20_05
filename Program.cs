using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using OnlineStoreAvalonia.Models;
using OnlineStoreAvalonia.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<StoreDatabase>();
var uploadsRoot = GetUploadsRoot();
Directory.CreateDirectory(uploadsRoot);
builder.WebHost.UseWebRoot("wwwroot");

var app = builder.Build();
var db = app.Services.GetRequiredService<StoreDatabase>();
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsRoot),
    RequestPath = "/uploads"
});

app.MapGet("/", (HttpRequest request) =>
{
    var lang = GetLang(request);
    var products = db.GetProducts(request.Query["q"], request.Query["category"]);
    var categories = db.GetCategories().Where(category => category != "Все").ToList();
    var cart = ReadCart(request, db);
    var user = GetUser(request);
    var message = request.Query["message"].ToString();

    return Html(Layout(lang, user, BuildStore(lang, products, categories, cart, message, request.Query["q"].ToString(), request.Query["category"].ToString())));
});

app.MapPost("/cart/add", (HttpRequest request) =>
{
    var form = request.Form;
    var cart = ParseCartCookie(request);
    var productId = int.Parse(form["productId"]!);
    cart[productId] = cart.TryGetValue(productId, out var quantity) ? quantity + 1 : 1;

    var response = Results.Redirect("/?message=added");
    return WithCartCookie(response, cart);
});

app.MapPost("/cart/remove", (HttpRequest request) =>
{
    var cart = ParseCartCookie(request);
    if (int.TryParse(request.Form["productId"], out var productId))
    {
        cart.Remove(productId);
    }

    return WithCartCookie(Results.Redirect("/"), cart);
});

app.MapPost("/checkout", (HttpRequest request) =>
{
    var cart = ReadCart(request, db);
    if (cart.Count == 0)
    {
        return Results.Redirect("/?message=empty");
    }

    var form = request.Form;
    var name = form["name"].ToString();
    var phone = DigitsOnly(form["phone"].ToString(), 13);
    var address = form["address"].ToString();

    if (string.IsNullOrWhiteSpace(name) || phone.Length != 13 || string.IsNullOrWhiteSpace(address))
    {
        return Results.Redirect("/?message=checkout-error");
    }

    var orderId = db.CreateOrder(name, phone, address, cart);
    var result = Results.Redirect($"/receipt/{orderId}");
    return WithCartCookie(result, []);
});

app.MapGet("/receipt/{id:int}", (HttpRequest request, int id) =>
{
    var lang = GetLang(request);
    var order = db.GetOrder(id);
    if (order is null)
    {
        return Html(Layout(lang, GetUser(request), $"<section class='panel'><h1>{T(lang, "ReceiptNotFound")}</h1><a class='btn' href='/'>{T(lang, "Back")}</a></section>"));
    }

    return Html(Layout(lang, GetUser(request), BuildReceipt(lang, order)));
});

app.MapGet("/admin", (HttpRequest request) =>
{
    var lang = GetLang(request);
    var user = GetUser(request);
    if (user != "admin")
    {
        return Html(Layout(lang, user, BuildAdminLogin(lang, request.Query["error"] == "1")));
    }

    return Html(Layout(lang, user, BuildAdmin(lang, db.GetProducts(null, null), db.GetRecentOrders(), request.Query["message"].ToString())));
});

app.MapPost("/admin/login", (HttpRequest request) =>
{
    var form = request.Form;
    if (form["login"] == "admin" && form["password"] == "admin")
    {
        var result = Results.Redirect("/admin");
        return WithCookie(result, "user", "admin");
    }

    return Results.Redirect("/admin?error=1");
});

app.MapPost("/admin/logout", () =>
{
    return WithCookie(Results.Redirect("/"), "user", string.Empty);
});

app.MapPost("/admin/products", async (HttpRequest request) =>
{
    if (GetUser(request) != "admin")
    {
        return Results.Redirect("/admin");
    }

    var form = await request.ReadFormAsync();
    if (TryParseMoney(form["price"], out var price)
        && int.TryParse(form["stock"], out var stock)
        && !string.IsNullOrWhiteSpace(form["name"])
        && !string.IsNullOrWhiteSpace(form["category"])
        && !string.IsNullOrWhiteSpace(form["description"])
        && price > 0
        && stock >= 0)
    {
        var imagePath = await SaveUploadedImageAsync(form.Files["image"], request);
        db.AddProduct(form["name"]!, form["category"]!, form["description"]!, price, stock, imagePath);
        return Results.Redirect("/admin?message=product-added");
    }

    return Results.Redirect("/admin?message=product-error");
});

app.MapPost("/admin/products/delete", (HttpRequest request) =>
{
    if (GetUser(request) == "admin" && int.TryParse(request.Form["productId"], out var id))
    {
        db.DeleteProduct(id);
    }

    return Results.Redirect("/admin");
});

app.MapPost("/admin/orders/approve", (HttpRequest request) =>
{
    if (GetUser(request) == "admin" && int.TryParse(request.Form["orderId"], out var id))
    {
        db.ApproveOrder(id);
    }

    return Results.Redirect("/admin");
});

app.MapPost("/admin/orders/delete", (HttpRequest request) =>
{
    if (GetUser(request) == "admin" && int.TryParse(request.Form["orderId"], out var id))
    {
        db.DeleteOrder(id);
    }

    return Results.Redirect("/admin");
});

app.MapGet("/language/{lang}", (HttpRequest request, string lang) =>
{
    var culture = lang == "kk" ? "kk" : "ru";
    var returnUrl = request.Query["returnUrl"].ToString();
    if (string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith('/'))
    {
        returnUrl = "/";
    }

    return WithCookie(Results.Redirect(returnUrl), "lang", culture);
});

app.Run();

static IResult Html(string body) => Results.Content(body, "text/html; charset=utf-8");

static string Layout(string lang, string? user, string content)
{
    var currentPath = "/";
    return $$"""
        <!doctype html>
        <html lang="{{lang}}">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{{T(lang, "Title")}}</title>
            <style>
                * { box-sizing: border-box; }
                body { margin: 0; font-family: Inter, Arial, sans-serif; background: #f3f5f8; color: #172033; }
                header { background: #152238; color: white; padding: 18px 28px; display: flex; align-items: center; justify-content: space-between; gap: 16px; }
                header a { color: white; text-decoration: none; }
                .brand { font-size: 26px; font-weight: 800; letter-spacing: 0; }
                .nav { display: flex; gap: 10px; align-items: center; flex-wrap: wrap; }
                .nav a, .nav button, .btn { border: 0; border-radius: 8px; background: #eef2f8; color: #172033; padding: 10px 14px; font-weight: 700; text-decoration: none; cursor: pointer; display: inline-block; }
                .nav a.active { background: #2f80ed; color: white; }
                main { max-width: 1220px; margin: 0 auto; padding: 22px; }
                .grid { display: grid; grid-template-columns: minmax(0, 1fr) 360px; gap: 18px; align-items: start; }
                .panel, .product, .receipt { background: white; border: 1px solid #dce3ee; border-radius: 8px; padding: 16px; }
                .toolbar { display: grid; grid-template-columns: minmax(0, 1fr) 220px auto; gap: 10px; margin-bottom: 14px; }
                input, select, textarea { width: 100%; min-height: 42px; border: 1px solid #cfd8e6; border-radius: 8px; padding: 10px 12px; font: inherit; }
                button.primary, .btn.primary { background: #1f6feb; color: white; }
                button.danger { background: #b42318; color: white; }
                button { border: 0; border-radius: 8px; padding: 10px 12px; font-weight: 700; cursor: pointer; }
                .category-list { display: flex; flex-wrap: wrap; gap: 8px; margin: 0 0 14px; }
                .category-chip { border: 1px solid #cfd8e6; border-radius: 8px; background: white; color: #172033; padding: 9px 12px; font-weight: 700; text-decoration: none; }
                .category-chip.active { background: #1f6feb; border-color: #1f6feb; color: white; }
                .products { display: grid; grid-template-columns: repeat(auto-fill, minmax(230px, 1fr)); gap: 12px; }
                .product h3 { margin: 8px 0 6px; font-size: 17px; }
                .product-media { width: 100%; aspect-ratio: 4 / 3; border-radius: 8px; background: #eef2f8; object-fit: cover; display: block; margin-bottom: 12px; }
                .product-placeholder { display: flex; align-items: center; justify-content: center; font-size: 24px; font-weight: 800; color: #172033; }
                .thumb { width: 76px; height: 56px; border-radius: 8px; object-fit: cover; background: #eef2f8; display: inline-flex; align-items: center; justify-content: center; font-weight: 800; margin-right: 10px; vertical-align: middle; }
                .muted { color: #687083; font-size: 13px; }
                .price { color: #0b7a53; font-size: 20px; font-weight: 800; margin: 12px 0; }
                .badge { display: inline-flex; align-items: center; justify-content: center; min-width: 56px; height: 42px; border-radius: 8px; background: #d9e8ff; font-weight: 800; }
                .line { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: 10px; border-bottom: 1px solid #e5eaf2; padding: 10px 0; }
                .stack { display: grid; gap: 10px; }
                .admin-grid { display: grid; grid-template-columns: 360px minmax(0, 1fr); gap: 18px; align-items: start; }
                table { width: 100%; border-collapse: collapse; }
                th, td { text-align: left; padding: 10px; border-bottom: 1px solid #e5eaf2; vertical-align: top; }
                .receipt { max-width: 720px; margin: 0 auto; }
                .receipt-head { display: flex; justify-content: space-between; gap: 16px; border-bottom: 2px solid #172033; padding-bottom: 14px; margin-bottom: 14px; }
                @media (max-width: 880px) { .grid, .admin-grid, .toolbar { grid-template-columns: 1fr; } header { align-items: flex-start; flex-direction: column; } }
                @media print { header, .no-print { display: none; } body { background: white; } main { padding: 0; } .receipt { border: 0; } }
            </style>
        </head>
        <body>
            <header>
                <a class="brand" href="/">AutoDuken</a>
                <nav class="nav">
                    <a href="/" class="{{(currentPath == "/" ? "active" : "")}}">{{T(lang, "Catalog")}}</a>
                    <a href="/admin">{{T(lang, "Admin")}}</a>
                    <a href="/language/ru?returnUrl=/">RU</a>
                    <a href="/language/kk?returnUrl=/">KZ</a>
                </nav>
            </header>
            <main>{{content}}</main>
        </body>
        </html>
        """;
}

static string BuildStore(string lang, IReadOnlyList<Product> products, IReadOnlyList<string> categories, IReadOnlyList<CartItem> cart, string message, string q, string selectedCategory)
{
    var total = cart.Sum(item => item.LineTotal);
    var categoryLinks = new StringBuilder();
    categoryLinks.Append($"""<a class="category-chip{(string.IsNullOrWhiteSpace(selectedCategory) ? " active" : "")}" href="/?q={Uri.EscapeDataString(q)}">{T(lang, "All")}</a>""");
    foreach (var category in categories)
    {
        categoryLinks.Append($"""<a class="category-chip{(selectedCategory == category ? " active" : "")}" href="/?q={Uri.EscapeDataString(q)}&category={Uri.EscapeDataString(category)}">{H(category)}</a>""");
    }

    var builder = new StringBuilder();
    builder.Append($$"""
        <div class="grid">
            <section>
                <form class="toolbar" method="get">
                    <input name="q" value="{{H(q)}}" placeholder="{{T(lang, "Search")}}">
                    <select name="category" onchange="this.form.submit()">
                        <option value="">{{T(lang, "All")}}</option>
        """);

    foreach (var category in categories)
    {
        builder.Append($"<option value=\"{H(category)}\"{(selectedCategory == category ? " selected" : "")}>{H(category)}</option>");
    }

    builder.Append($$"""
                    </select>
                    <button class="primary">{{T(lang, "Find")}}</button>
                </form>
                <div class="category-list">{{categoryLinks}}</div>
                <div class="products">
        """);

    foreach (var product in products)
    {
        builder.Append($$"""
            <article class="product">
                {{ProductMedia(product)}}
                <p class="muted">{{H(product.Category)}} · {{T(lang, "Stock")}}: {{product.Stock}}</p>
                <h3>{{H(product.Name)}}</h3>
                <p>{{H(product.Description)}}</p>
                <div class="price">{{Money(product.Price)}}</div>
                <form method="post" action="/cart/add">
                    <input type="hidden" name="productId" value="{{product.Id}}">
                    <button class="primary" {{(product.Stock <= 0 ? "disabled" : "")}}>{{T(lang, "Add")}}</button>
                </form>
            </article>
            """);
    }

    builder.Append($$"""
                </div>
            </section>
            <aside class="stack">
                <section class="panel">
                    <h2>{{T(lang, "Cart")}}</h2>
        """);

    if (cart.Count == 0)
    {
        builder.Append($"<p class='muted'>{T(lang, "CartEmpty")}</p>");
    }
    else
    {
        foreach (var item in cart)
        {
            builder.Append($$"""
                <div class="line">
                    <div><strong>{{H(item.Product.Name)}}</strong><br><span class="muted">{{item.Quantity}} x {{Money(item.Product.Price)}}</span></div>
                    <form method="post" action="/cart/remove">
                        <input type="hidden" name="productId" value="{{item.Product.Id}}">
                        <button>{{T(lang, "Remove")}}</button>
                    </form>
                </div>
                """);
        }
    }

    builder.Append($$"""
                    <h3>{{T(lang, "Total")}}: {{Money(total)}}</h3>
                </section>
                <section class="panel">
                    <h2>{{T(lang, "Checkout")}}</h2>
                    {{Message(lang, message)}}
                    <form class="stack" method="post" action="/checkout">
                        <input name="name" placeholder="{{T(lang, "Name")}}" required>
                        <input name="phone" placeholder="{{T(lang, "Phone")}}" maxlength="13" required>
                        <textarea name="address" placeholder="{{T(lang, "Address")}}" required></textarea>
                        <button class="primary">{{T(lang, "CreateOrder")}}</button>
                    </form>
                </section>
            </aside>
        </div>
        """);

    return builder.ToString();
}

static string BuildReceipt(string lang, Order order)
{
    var rows = string.Join("", order.Lines.Select(line => $$"""
        <tr>
            <td>{{H(line.ProductName)}}</td>
            <td>{{line.Quantity}}</td>
            <td>{{Money(line.UnitPrice)}}</td>
            <td>{{Money(line.UnitPrice * line.Quantity)}}</td>
        </tr>
        """));

    return $$"""
        <section class="receipt">
            <div class="receipt-head">
                <div>
                    <h1>{{T(lang, "Receipt")}} #{{order.Id}}</h1>
                    <p class="muted">AutoDuken · SQLite · ASP.NET Core</p>
                </div>
                <div>
                    <strong>{{order.CreatedAt:dd.MM.yyyy HH:mm}}</strong><br>
                    <span class="muted">{{T(lang, "Status")}}: {{H(order.StatusText)}}</span>
                </div>
            </div>
            <p><strong>{{T(lang, "Name")}}:</strong> {{H(order.CustomerName)}}<br>
            <strong>{{T(lang, "PhoneLabel")}}:</strong> {{H(order.Phone)}}<br>
            <strong>{{T(lang, "AddressLabel")}}:</strong> {{H(order.Address)}}</p>
            <table>
                <thead><tr><th>{{T(lang, "Product")}}</th><th>{{T(lang, "Qty")}}</th><th>{{T(lang, "Price")}}</th><th>{{T(lang, "Sum")}}</th></tr></thead>
                <tbody>{{rows}}</tbody>
            </table>
            <h2>{{T(lang, "Total")}}: {{Money(order.Total)}}</h2>
            <div class="no-print stack">
                <button class="primary" onclick="window.print()">{{T(lang, "Print")}}</button>
                <a class="btn" href="/">{{T(lang, "Back")}}</a>
            </div>
        </section>
        """;
}

static string BuildAdminLogin(string lang, bool hasError)
{
    return $$"""
        <section class="panel" style="max-width:420px;margin:40px auto;">
            <h1>{{T(lang, "Admin")}}</h1>
            {{(hasError ? $"<p style='color:#b42318'>{T(lang, "WrongAdmin")}</p>" : "")}}
            <form class="stack" method="post" action="/admin/login">
                <input name="login" placeholder="{{T(lang, "Login")}}" required>
                <input name="password" type="password" placeholder="{{T(lang, "Password")}}" required>
                <button class="primary">{{T(lang, "Enter")}}</button>
            </form>
        </section>
        """;
}

static string BuildAdmin(string lang, IReadOnlyList<Product> products, IReadOnlyList<Order> orders, string message)
{
    var productRows = string.Join("", products.Select(product => $$"""
        <tr>
            <td>{{ProductThumb(product)}}{{H(product.Name)}}<br><span class="muted">{{H(product.Category)}}</span></td>
            <td>{{Money(product.Price)}}</td>
            <td>{{product.Stock}}</td>
            <td><form method="post" action="/admin/products/delete"><input type="hidden" name="productId" value="{{product.Id}}"><button class="danger">{{T(lang, "Delete")}}</button></form></td>
        </tr>
        """));

    var orderRows = string.Join("", orders.Select(order => $$"""
        <tr>
            <td>#{{order.Id}}<br><span class="muted">{{order.CreatedAt:dd.MM.yyyy HH:mm}}</span></td>
            <td>{{H(order.CustomerName)}}<br><span class="muted">{{H(order.Phone)}}</span></td>
            <td>{{Money(order.Total)}}<br><span class="muted">{{H(order.StatusText)}}</span></td>
            <td>
                <form method="post" action="/admin/orders/approve"><input type="hidden" name="orderId" value="{{order.Id}}"><button>{{T(lang, "Approve")}}</button></form>
                <form method="post" action="/admin/orders/delete" style="margin-top:6px;"><input type="hidden" name="orderId" value="{{order.Id}}"><button class="danger">{{T(lang, "Delete")}}</button></form>
                <a class="btn" style="margin-top:6px;" href="/receipt/{{order.Id}}">{{T(lang, "Receipt")}}</a>
            </td>
        </tr>
        """));

    return $$"""
        <div class="admin-grid">
            <section class="panel">
                <h2>{{T(lang, "AddProduct")}}</h2>
                {{AdminMessage(lang, message)}}
                <form class="stack" method="post" action="/admin/products" enctype="multipart/form-data">
                    <input name="name" placeholder="{{T(lang, "ProductName")}}" required>
                    <select name="category">
                        <option>Масла</option><option>Шины</option><option>Запчасти</option><option>Аксессуары</option><option>Электрика</option>
                    </select>
                    <textarea name="description" placeholder="{{T(lang, "Description")}}" required></textarea>
                    <input name="image" type="file" accept="image/png,image/jpeg,image/webp,image/gif">
                    <input name="price" placeholder="{{T(lang, "Price")}}" required>
                    <input name="stock" placeholder="{{T(lang, "Stock")}}" required>
                    <button class="primary">{{T(lang, "AddProduct")}}</button>
                </form>
            </section>
            <section class="panel">
                <h2>{{T(lang, "Products")}}</h2>
                <table><tbody>{{productRows}}</tbody></table>
                <h2>{{T(lang, "Orders")}}</h2>
                <table><tbody>{{orderRows}}</tbody></table>
            </section>
        </div>
        """;
}

static IReadOnlyList<CartItem> ReadCart(HttpRequest request, StoreDatabase db)
{
    var products = db.GetProducts(null, null).ToDictionary(product => product.Id);
    return ParseCartCookie(request)
        .Where(item => products.ContainsKey(item.Key))
        .Select(item => new CartItem { Product = products[item.Key], Quantity = Math.Min(item.Value, products[item.Key].Stock) })
        .Where(item => item.Quantity > 0)
        .ToList();
}

static string ProductMedia(Product product)
{
    if (!string.IsNullOrWhiteSpace(product.ImagePath))
    {
        return $"<img class=\"product-media\" src=\"{H(product.ImagePath)}\" alt=\"{H(product.Name)}\">";
    }

    return $"<div class=\"product-media product-placeholder\">{H(product.CategoryIcon)}</div>";
}

static string ProductThumb(Product product)
{
    if (!string.IsNullOrWhiteSpace(product.ImagePath))
    {
        return $"<img class=\"thumb\" src=\"{H(product.ImagePath)}\" alt=\"{H(product.Name)}\">";
    }

    return $"<span class=\"thumb\">{H(product.CategoryIcon)}</span>";
}

static async Task<string> SaveUploadedImageAsync(IFormFile? image, HttpRequest request)
{
    if (image is null || image.Length == 0)
    {
        return string.Empty;
    }

    if (image.Length > 5 * 1024 * 1024)
    {
        return string.Empty;
    }

    var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
    if (extension is not ".jpg" and not ".jpeg" and not ".png" and not ".webp" and not ".gif")
    {
        return string.Empty;
    }

    var uploadsPath = GetUploadsRoot();
    Directory.CreateDirectory(uploadsPath);

    var fileName = $"{Guid.NewGuid():N}{extension}";
    var filePath = Path.Combine(uploadsPath, fileName);

    await using var stream = File.Create(filePath);
    await image.CopyToAsync(stream);

    return $"/uploads/{fileName}";
}

static Dictionary<int, int> ParseCartCookie(HttpRequest request)
{
    var cart = new Dictionary<int, int>();
    if (!request.Cookies.TryGetValue("cart", out var raw) || string.IsNullOrWhiteSpace(raw))
    {
        return cart;
    }

    foreach (var pair in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
    {
        var parts = pair.Split(':', 2);
        if (parts.Length == 2 && int.TryParse(parts[0], out var id) && int.TryParse(parts[1], out var quantity))
        {
            cart[id] = Math.Clamp(quantity, 1, 99);
        }
    }

    return cart;
}

static IResult WithCartCookie(IResult result, Dictionary<int, int> cart)
{
    return result.WithCookie("cart", string.Join(",", cart.Select(item => $"{item.Key}:{item.Value}")));
}

static IResult WithCookie(IResult result, string name, string value)
{
    return result.WithCookie(name, value);
}

static string GetLang(HttpRequest request) => request.Cookies.TryGetValue("lang", out var lang) && lang == "kk" ? "kk" : "ru";

static string? GetUser(HttpRequest request) => request.Cookies.TryGetValue("user", out var user) ? user : null;

static string H(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);

static string Money(decimal value) => $"{value:N0} ₸";

static string DigitsOnly(string value, int maxLength)
{
    var builder = new StringBuilder(maxLength);
    foreach (var character in value)
    {
        if (char.IsDigit(character))
        {
            builder.Append(character);
            if (builder.Length == maxLength)
            {
                break;
            }
        }
    }

    return builder.ToString();
}

static string Message(string lang, string message) => message switch
{
    "added" => $"<p class='muted'>{T(lang, "Added")}</p>",
    "empty" => $"<p style='color:#b42318'>{T(lang, "EmptyOrder")}</p>",
    "checkout-error" => $"<p style='color:#b42318'>{T(lang, "CheckoutError")}</p>",
    _ => string.Empty
};

static string AdminMessage(string lang, string message) => message switch
{
    "product-added" => $"<p class='muted'>{T(lang, "ProductAdded")}</p>",
    "product-error" => $"<p style='color:#b42318'>{T(lang, "ProductError")}</p>",
    _ => string.Empty
};

static bool TryParseMoney(string? raw, out decimal value)
{
    value = 0;
    if (string.IsNullOrWhiteSpace(raw))
    {
        return false;
    }

    var normalized = raw.Trim().Replace(" ", "").Replace("\u00a0", "");
    var cultures = new[]
    {
        CultureInfo.CurrentCulture,
        CultureInfo.GetCultureInfo("ru-RU"),
        CultureInfo.InvariantCulture
    };

    foreach (var culture in cultures)
    {
        if (decimal.TryParse(normalized, NumberStyles.Number, culture, out value))
        {
            return true;
        }
    }

    normalized = normalized.Replace(',', '.');
    return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
}

static string GetUploadsRoot()
{
    var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    if (string.IsNullOrWhiteSpace(appData))
    {
        appData = AppContext.BaseDirectory;
    }

    return Path.Combine(appData, "OnlineStoreAvalonia", "uploads");
}

static string T(string lang, string key)
{
    var kk = lang == "kk";
    return key switch
    {
        "Title" => kk ? "Автодукен" : "Автодукен",
        "Catalog" => kk ? "Каталог" : "Каталог",
        "Admin" => kk ? "Әкімші панелі" : "Админ панель",
        "Search" => kk ? "Тауар іздеу" : "Поиск товара",
        "All" => kk ? "Барлығы" : "Все",
        "Find" => kk ? "Іздеу" : "Найти",
        "Stock" => kk ? "Қойма" : "Склад",
        "Add" => kk ? "Себетке қосу" : "В корзину",
        "Cart" => kk ? "Себет" : "Корзина",
        "CartEmpty" => kk ? "Себет бос" : "Корзина пустая",
        "Remove" => kk ? "Алу" : "Убрать",
        "Total" => kk ? "Барлығы" : "Итого",
        "Checkout" => kk ? "Тапсырыс рәсімдеу" : "Оформление заказа",
        "Name" => kk ? "Аты-жөні" : "ФИО",
        "Phone" => kk ? "Телефон: 13 цифр" : "Телефон: 13 цифр",
        "PhoneLabel" => kk ? "Телефон" : "Телефон",
        "Address" => kk ? "Жеткізу мекенжайы" : "Адрес доставки",
        "AddressLabel" => kk ? "Мекенжай" : "Адрес",
        "CreateOrder" => kk ? "Тапсырыс беру" : "Оформить заказ",
        "Receipt" => kk ? "Чек" : "Чек",
        "ReceiptNotFound" => kk ? "Чек табылмады" : "Чек не найден",
        "Status" => kk ? "Күйі" : "Статус",
        "Product" => kk ? "Тауар" : "Товар",
        "Qty" => kk ? "Саны" : "Кол-во",
        "Price" => kk ? "Баға" : "Цена",
        "Sum" => kk ? "Сома" : "Сумма",
        "Print" => kk ? "Басып шығару" : "Печать",
        "Back" => kk ? "Артқа" : "Назад",
        "WrongAdmin" => kk ? "Логин немесе құпия сөз қате." : "Неверный логин или пароль.",
        "Login" => kk ? "Логин" : "Логин",
        "Password" => kk ? "Құпия сөз" : "Пароль",
        "Enter" => kk ? "Кіру" : "Войти",
        "AddProduct" => kk ? "Тауар қосу" : "Добавить товар",
        "ProductName" => kk ? "Тауар атауы" : "Название товара",
        "Description" => kk ? "Сипаттама" : "Описание",
        "Products" => kk ? "Тауарлар" : "Товары",
        "Orders" => kk ? "Тапсырыстар" : "Заказы",
        "Delete" => kk ? "Жою" : "Удалить",
        "Approve" => kk ? "Мақұлдау" : "Одобрить",
        "Added" => kk ? "Тауар себетке қосылды." : "Товар добавлен в корзину.",
        "EmptyOrder" => kk ? "Себет бос." : "Корзина пустая.",
        "CheckoutError" => kk ? "Аты-жөні, 13 цифрлық телефон және мекенжай керек." : "Нужно заполнить ФИО, телефон из 13 цифр и адрес.",
        "ProductAdded" => kk ? "Тауар қосылды." : "Товар добавлен.",
        "ProductError" => kk ? "Тауарды қосу үшін атауын, санатын, сипаттамасын, дұрыс баға мен санын толтырыңыз." : "Заполните название, категорию, описание, корректную цену и количество.",
        _ => key
    };
}

static class CookieResults
{
    public static IResult WithCookie(this IResult result, string name, string value)
    {
        return new CookieResult(result, name, value);
    }
}

sealed class CookieResult(IResult inner, string name, string value) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        if (string.IsNullOrEmpty(value))
        {
            httpContext.Response.Cookies.Delete(name);
        }
        else
        {
            httpContext.Response.Cookies.Append(name, value, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });
        }

        await inner.ExecuteAsync(httpContext);
    }
}
