using System;
using System.Collections.Generic;

namespace OnlineStoreAvalonia.Models;

public sealed class Order
{
    public int Id { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public string Phone { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    public decimal Total { get; init; }

    public DateTime CreatedAt { get; init; }

    public bool IsApproved { get; init; }

    public string StatusText => IsApproved ? "Одобрен" : "Ожидает";

    public IReadOnlyList<OrderLine> Lines { get; init; } = [];
}
