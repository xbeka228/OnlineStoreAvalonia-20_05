namespace OnlineStoreAvalonia.Models;

public sealed class Customer
{
    public int Id { get; init; }

    public string Login { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public string Phone { get; init; } = string.Empty;
}
