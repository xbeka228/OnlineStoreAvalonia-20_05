namespace OnlineStoreAvalonia.Models;

public sealed class Product
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public decimal Price { get; init; }

    public int Stock { get; init; }

    public string ImagePath { get; init; } = string.Empty;

    public string CategoryIcon => Category switch
    {
        "Масла" => "OIL",
        "Шины" => "TYR",
        "Запчасти" => "PRT",
        "Аксессуары" => "ACC",
        "Электрика" => "ELC",
        _ => "CAR"
    };

    public string AccentColor => Category switch
    {
        "Масла" => "#D9E8FF",
        "Шины" => "#E8E1D4",
        "Запчасти" => "#DFF7EA",
        "Аксессуары" => "#FFF0D6",
        "Электрика" => "#E9E0FF",
        _ => "#E9EEF7"
    };
}
