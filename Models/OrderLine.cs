namespace OnlineStoreAvalonia.Models;

public sealed class OrderLine
{
    public int OrderId { get; init; }

    public int ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public decimal UnitPrice { get; init; }
}
