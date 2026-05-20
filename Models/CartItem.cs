namespace OnlineStoreAvalonia.Models;

public sealed class CartItem
{
    public required Product Product { get; init; }

    public int Quantity { get; set; }

    public decimal LineTotal => Product.Price * Quantity;
}
