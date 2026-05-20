using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnlineStoreAvalonia.Models;
using OnlineStoreAvalonia.Services;

namespace OnlineStoreAvalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly StoreDatabase _database;
    private Customer? _currentCustomer;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isAuthenticated;

    [ObservableProperty]
    private bool _isStaff;

    [ObservableProperty]
    private bool _isRegisterMode;

    [ObservableProperty]
    private bool _isKazakh;

    [ObservableProperty]
    private string _clientLogin = string.Empty;

    [ObservableProperty]
    private string _clientPassword = string.Empty;

    [ObservableProperty]
    private string _registerLogin = string.Empty;

    [ObservableProperty]
    private string _registerPassword = string.Empty;

    [ObservableProperty]
    private string _registerFullName = string.Empty;

    [ObservableProperty]
    private string _registerPhone = string.Empty;

    [ObservableProperty]
    private string _staffLogin = string.Empty;

    [ObservableProperty]
    private string _staffPassword = string.Empty;

    [ObservableProperty]
    private string _newProductName = string.Empty;

    [ObservableProperty]
    private string _newProductCategory = string.Empty;

    [ObservableProperty]
    private string _newProductDescription = string.Empty;

    [ObservableProperty]
    private string _newProductPrice = string.Empty;

    [ObservableProperty]
    private string _newProductStock = string.Empty;

    [ObservableProperty]
    private string _authMessage = "Введите логин и пароль.";

    [ObservableProperty]
    private string _selectedCategory = "Все";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckoutCommand))]
    private string _customerName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckoutCommand))]
    private string _phone = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckoutCommand))]
    private string _address = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "База данных SQLite готова. Выберите товары из каталога.";

    public MainWindowViewModel()
    {
        _database = new StoreDatabase();

        ReloadCatalogData();
        LoadOrders();
    }

    public ObservableCollection<Product> Products { get; } = [];

    public ObservableCollection<string> Categories { get; } = [];

    public ObservableCollection<string> AdminCategories { get; } = [];

    public ObservableCollection<CartItem> CartItems { get; } = [];

    public ObservableCollection<Order> RecentOrders { get; } = [];

    public decimal CartTotal => CartItems.Sum(item => item.LineTotal);

    public int CartItemsCount => CartItems.Sum(item => item.Quantity);

    public string CartSummary => CartItemsCount == 0
        ? T("CartEmpty")
        : $"{CartItemsCount} {T("Pieces")} {CartTotal:N0} ₸";

    public string LanguageButtonText => IsKazakh ? "RU" : "KZ";

    public string AppTitle => T("AppTitle");

    public string LoginSubtitle => T("LoginSubtitle");

    public string LoginTitle => T("LoginTitle");

    public string LoginPlaceholder => T("LoginPlaceholder");

    public string PasswordPlaceholder => T("PasswordPlaceholder");

    public string LoginButtonText => T("LoginButtonText");

    public string NoAccountText => T("NoAccountText");

    public string RegisterTitle => T("RegisterTitle");

    public string FullNamePlaceholder => T("FullNamePlaceholder");

    public string Phone13Placeholder => T("Phone13Placeholder");

    public string RegisterButtonText => T("RegisterButtonText");

    public string AlreadyAccountText => T("AlreadyAccountText");

    public string HeaderSubtitle => T("HeaderSubtitle");

    public string LogoutText => T("LogoutText");

    public string SearchPlaceholder => T("SearchPlaceholder");

    public string CatalogTitle => T("CatalogTitle");

    public string AddToCartText => T("AddToCartText");

    public string DeleteProductText => T("DeleteProductText");

    public string CartTitle => T("CartTitle");

    public string TotalText => T("TotalText");

    public string CheckoutTitle => T("CheckoutTitle");

    public string CustomerFullNamePlaceholder => T("CustomerFullNamePlaceholder");

    public string AddressPlaceholder => T("AddressPlaceholder");

    public string CheckoutButtonText => T("CheckoutButtonText");

    public string AddProductTitle => T("AddProductTitle");

    public string ProductNamePlaceholder => T("ProductNamePlaceholder");

    public string CategoryPlaceholder => T("CategoryPlaceholder");

    public string DescriptionPlaceholder => T("DescriptionPlaceholder");

    public string PricePlaceholder => T("PricePlaceholder");

    public string StockPlaceholder => T("StockPlaceholder");

    public string AddProductButtonText => T("AddProductButtonText");

    public string OrdersTitle => T("OrdersTitle");

    public string ApproveText => T("ApproveText");

    public string DeleteText => T("DeleteText");

    public string CustomerCabinetTitle => T("CustomerCabinetTitle");

    public string CustomerCabinetHint => T("CustomerCabinetHint");

    public string PhoneHint => T("PhoneHint");

    public bool IsLoginVisible => !IsAuthenticated;

    public bool IsLoginFormVisible => !IsAuthenticated && !IsRegisterMode;

    public bool IsRegistrationFormVisible => !IsAuthenticated && IsRegisterMode;

    public bool IsStoreVisible => IsAuthenticated;

    public bool IsStaffPanelVisible => IsAuthenticated && IsStaff;

    public bool IsCustomerPanelVisible => IsAuthenticated && !IsStaff;

    public string CurrentUserTitle
    {
        get
        {
            if (!IsAuthenticated)
            {
                return string.Empty;
            }

            return IsStaff ? $"{T("StaffUser")}: admin" : $"{T("ClientUser")}: {_currentCustomer?.FullName}";
        }
    }

    partial void OnIsKazakhChanged(bool value)
    {
        RaiseLanguageProperties();
    }

    partial void OnIsAuthenticatedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsLoginVisible));
        OnPropertyChanged(nameof(IsLoginFormVisible));
        OnPropertyChanged(nameof(IsRegistrationFormVisible));
        OnPropertyChanged(nameof(IsStoreVisible));
        OnPropertyChanged(nameof(IsStaffPanelVisible));
        OnPropertyChanged(nameof(IsCustomerPanelVisible));
        OnPropertyChanged(nameof(CurrentUserTitle));
        CheckoutCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsRegisterModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsLoginFormVisible));
        OnPropertyChanged(nameof(IsRegistrationFormVisible));
    }

    partial void OnIsStaffChanged(bool value)
    {
        OnPropertyChanged(nameof(IsStaffPanelVisible));
        OnPropertyChanged(nameof(IsCustomerPanelVisible));
        OnPropertyChanged(nameof(CurrentUserTitle));
        CheckoutCommand.NotifyCanExecuteChanged();
    }

    partial void OnSearchTextChanged(string value)
    {
        LoadProducts();
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        LoadProducts();
    }

    partial void OnPhoneChanged(string value)
    {
        var normalized = DigitsOnly(value, 13);
        if (normalized != value)
        {
            Phone = normalized;
            return;
        }

        CheckoutCommand.NotifyCanExecuteChanged();
    }

    partial void OnRegisterPhoneChanged(string value)
    {
        var normalized = DigitsOnly(value, 13);
        if (normalized != value)
        {
            RegisterPhone = normalized;
        }
    }

    [RelayCommand]
    private void ToggleLanguage()
    {
        IsKazakh = !IsKazakh;
        AuthMessage = IsRegisterMode ? T("RegisterPrompt") : T("LoginPrompt");
        StatusMessage = T("ReadyStatus");
    }

    [RelayCommand]
    private void ShowRegistration()
    {
        IsRegisterMode = true;
        AuthMessage = T("RegisterPrompt");
    }

    [RelayCommand]
    private void ShowLogin()
    {
        IsRegisterMode = false;
        AuthMessage = T("LoginPrompt");
    }

    [RelayCommand]
    private void Login()
    {
        if (ClientLogin == "admin" && ClientPassword == "admin")
        {
            _currentCustomer = null;
            IsStaff = true;
            IsAuthenticated = true;
            IsRegisterMode = false;
            AuthMessage = string.Empty;
            StatusMessage = T("AdminLoginStatus");
            LoadProducts();
            LoadOrders();
            return;
        }

        LoginClient();
    }

    [RelayCommand]
    private void RegisterClient()
    {
        if (string.IsNullOrWhiteSpace(RegisterLogin)
            || string.IsNullOrWhiteSpace(RegisterPassword)
            || string.IsNullOrWhiteSpace(RegisterFullName)
            || RegisterPhone.Length != 13)
        {
            AuthMessage = T("RegisterValidation");
            return;
        }

        if (_database.CustomerLoginExists(RegisterLogin))
        {
            AuthMessage = T("LoginExists");
            return;
        }

        _currentCustomer = _database.RegisterCustomer(RegisterLogin, RegisterPassword, RegisterFullName, RegisterPhone);
        ClientLogin = RegisterLogin;
        ClientPassword = RegisterPassword;
        CustomerName = _currentCustomer.FullName;
        Phone = _currentCustomer.Phone;
        IsStaff = false;
        IsAuthenticated = true;
        IsRegisterMode = false;
        AuthMessage = string.Empty;
        StatusMessage = $"{T("RegisterSuccess")} {_currentCustomer.FullName}.";
    }

    [RelayCommand]
    private void LoginClient()
    {
        var customer = _database.ValidateCustomer(ClientLogin, ClientPassword);
        if (customer is null)
        {
            AuthMessage = T("WrongCredentials");
            return;
        }

        _currentCustomer = customer;
        IsStaff = false;
        IsAuthenticated = true;
        IsRegisterMode = false;
        CustomerName = customer.FullName;
        Phone = customer.Phone;
        AuthMessage = string.Empty;
        StatusMessage = $"{T("Welcome")} {customer.FullName}.";
    }

    [RelayCommand]
    private void LoginStaff()
    {
        if (StaffLogin == "admin" && StaffPassword == "admin")
        {
            _currentCustomer = null;
            IsStaff = true;
            IsAuthenticated = true;
            AuthMessage = string.Empty;
            StatusMessage = T("StaffLoginStatus");
            return;
        }

        AuthMessage = T("WrongStaffCredentials");
    }

    [RelayCommand]
    private void Logout()
    {
        IsAuthenticated = false;
        IsStaff = false;
        IsRegisterMode = false;
        _currentCustomer = null;
        CartItems.Clear();
        RefreshCartState();
        AuthMessage = T("LoginPrompt");
        StatusMessage = T("LogoutStatus");
    }

    [RelayCommand]
    private void AddProduct()
    {
        if (!IsStaff)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(NewProductName)
            || string.IsNullOrWhiteSpace(NewProductCategory)
            || string.IsNullOrWhiteSpace(NewProductDescription)
            || !decimal.TryParse(NewProductPrice, out var price)
            || !int.TryParse(NewProductStock, out var stock)
            || price <= 0
            || stock < 0)
        {
            StatusMessage = T("ProductValidation");
            return;
        }

        var category = NewProductCategory == "Все" ? string.Empty : NewProductCategory;
        _database.AddProduct(NewProductName, category, NewProductDescription, price, stock);
        NewProductName = string.Empty;
        NewProductDescription = string.Empty;
        NewProductPrice = string.Empty;
        NewProductStock = string.Empty;

        ReloadCatalogData();
        StatusMessage = T("ProductAdded");
    }

    [RelayCommand]
    private void DeleteProduct(Product product)
    {
        if (!IsStaff)
        {
            return;
        }

        _database.DeleteProduct(product.Id);
        ReloadCatalogData();
        StatusMessage = $"{T("ProductDeleted")} {product.Name}";
    }

    [RelayCommand]
    private void ApproveOrder(Order order)
    {
        if (!IsStaff)
        {
            return;
        }

        _database.ApproveOrder(order.Id);
        LoadOrders();
        StatusMessage = $"{T("Order")} #{order.Id} {T("ApprovedLower")}.";
    }

    [RelayCommand]
    private void DeleteOrder(Order order)
    {
        if (!IsStaff)
        {
            return;
        }

        _database.DeleteOrder(order.Id);
        LoadOrders();
        StatusMessage = $"{T("Order")} #{order.Id} {T("DeletedLower")}.";
    }

    [RelayCommand]
    private void AddToCart(Product product)
    {
        if (IsStaff)
        {
            StatusMessage = T("StaffCartBlocked");
            return;
        }

        if (product.Stock <= 0)
        {
            StatusMessage = T("OutOfStock");
            return;
        }

        var existing = CartItems.FirstOrDefault(item => item.Product.Id == product.Id);
        if (existing is null)
        {
            CartItems.Add(new CartItem { Product = product, Quantity = 1 });
        }
        else if (existing.Quantity < product.Stock)
        {
            existing.Quantity++;
        }
        else
        {
            StatusMessage = T("MaxQuantity");
            return;
        }

        StatusMessage = $"{T("Added")} {product.Name}";
        RefreshCartState();
    }

    [RelayCommand]
    private void IncreaseQuantity(CartItem item)
    {
        if (item.Quantity >= item.Product.Stock)
        {
            StatusMessage = T("MaxQuantity");
            return;
        }

        item.Quantity++;
        RefreshCartState();
    }

    [RelayCommand]
    private void DecreaseQuantity(CartItem item)
    {
        item.Quantity--;
        if (item.Quantity <= 0)
        {
            CartItems.Remove(item);
        }

        RefreshCartState();
    }

    [RelayCommand]
    private void RemoveFromCart(CartItem item)
    {
        CartItems.Remove(item);
        RefreshCartState();
    }

    [RelayCommand(CanExecute = nameof(CanCheckout))]
    private void Checkout()
    {
        var orderId = _database.CreateOrder(CustomerName, Phone, Address, CartItems.ToList());

        CartItems.Clear();
        CustomerName = string.Empty;
        Phone = string.Empty;
        Address = string.Empty;

        LoadProducts();
        LoadOrders();
        RefreshCartState();

        StatusMessage = $"{T("Order")} #{orderId} {T("OrderCreated")}";
    }

    private bool CanCheckout()
    {
        return CartItems.Count > 0
            && IsAuthenticated
            && !IsStaff
            && !string.IsNullOrWhiteSpace(CustomerName)
            && Phone.Length == 13
            && !string.IsNullOrWhiteSpace(Address);
    }

    private void LoadProducts()
    {
        Products.Clear();
        var category = SelectedCategory == "Все" ? string.Empty : SelectedCategory;

        foreach (var product in _database.GetProducts(SearchText, category))
        {
            Products.Add(product);
        }
    }

    private void ReloadCatalogData()
    {
        Categories.Clear();
        AdminCategories.Clear();
        foreach (var category in _database.GetCategories())
        {
            Categories.Add(category);
            if (category != "Все")
            {
                AdminCategories.Add(category);
            }
        }

        if (!Categories.Contains(SelectedCategory))
        {
            SelectedCategory = "Все";
        }

        if (!AdminCategories.Contains(NewProductCategory))
        {
            NewProductCategory = AdminCategories.FirstOrDefault() ?? string.Empty;
        }

        LoadProducts();
    }

    private void LoadOrders()
    {
        RecentOrders.Clear();

        foreach (var order in _database.GetRecentOrders())
        {
            RecentOrders.Add(order);
        }
    }

    private void RefreshCartState()
    {
        OnPropertyChanged(nameof(CartTotal));
        OnPropertyChanged(nameof(CartItemsCount));
        OnPropertyChanged(nameof(CartSummary));
        CheckoutCommand.NotifyCanExecuteChanged();

        var items = CartItems.ToList();
        CartItems.Clear();
        foreach (var item in items)
        {
            CartItems.Add(item);
        }
    }

    private static string DigitsOnly(string value, int maxLength)
    {
        var builder = new StringBuilder(maxLength);

        foreach (var character in value)
        {
            if (!char.IsDigit(character))
            {
                continue;
            }

            builder.Append(character);
            if (builder.Length == maxLength)
            {
                break;
            }
        }

        return builder.ToString();
    }

    private void RaiseLanguageProperties()
    {
        var properties = new[]
        {
            nameof(LanguageButtonText), nameof(AppTitle), nameof(LoginSubtitle), nameof(LoginTitle),
            nameof(LoginPlaceholder), nameof(PasswordPlaceholder), nameof(LoginButtonText), nameof(NoAccountText),
            nameof(RegisterTitle), nameof(FullNamePlaceholder), nameof(Phone13Placeholder), nameof(RegisterButtonText),
            nameof(AlreadyAccountText), nameof(HeaderSubtitle), nameof(LogoutText), nameof(SearchPlaceholder),
            nameof(CatalogTitle), nameof(AddToCartText), nameof(DeleteProductText), nameof(CartTitle),
            nameof(TotalText), nameof(CheckoutTitle), nameof(CustomerFullNamePlaceholder), nameof(AddressPlaceholder),
            nameof(CheckoutButtonText), nameof(AddProductTitle), nameof(ProductNamePlaceholder), nameof(CategoryPlaceholder),
            nameof(DescriptionPlaceholder), nameof(PricePlaceholder), nameof(StockPlaceholder), nameof(AddProductButtonText),
            nameof(OrdersTitle), nameof(ApproveText), nameof(DeleteText), nameof(CustomerCabinetTitle),
            nameof(CustomerCabinetHint), nameof(PhoneHint), nameof(CurrentUserTitle), nameof(CartSummary)
        };

        foreach (var property in properties)
        {
            OnPropertyChanged(property);
        }
    }

    private string T(string key)
    {
        return IsKazakh
            ? key switch
            {
                "AppTitle" => "Интернет дүкен",
                "LoginSubtitle" => "Клиент және қызметкер үшін кіру",
                "LoginTitle" => "Кіру",
                "LoginPlaceholder" => "Логин",
                "PasswordPlaceholder" => "Құпия сөз",
                "LoginButtonText" => "Кіру",
                "NoAccountText" => "Аккаунт жоқ па? Тіркелу",
                "RegisterTitle" => "Тіркелу",
                "FullNamePlaceholder" => "Аты-жөні",
                "Phone13Placeholder" => "Телефон: 13 цифр",
                "RegisterButtonText" => "Тіркелу",
                "AlreadyAccountText" => "Аккаунт бар ма? Кіру",
                "HeaderSubtitle" => "Тауарлар каталогы, себет, тапсырыс рәсімдеу және SQLite дерекқоры",
                "LogoutText" => "Шығу",
                "SearchPlaceholder" => "Тауар іздеу",
                "CatalogTitle" => "Каталог",
                "AddToCartText" => "Қосу",
                "DeleteProductText" => "Тауарды жою",
                "CartTitle" => "Себет",
                "TotalText" => "Барлығы",
                "CheckoutTitle" => "Тапсырысты рәсімдеу",
                "CustomerFullNamePlaceholder" => "Клиенттің аты-жөні",
                "AddressPlaceholder" => "Жеткізу мекенжайы",
                "CheckoutButtonText" => "Тапсырыс беру",
                "AddProductTitle" => "Тауар қосу",
                "ProductNamePlaceholder" => "Атауы",
                "CategoryPlaceholder" => "Санат",
                "DescriptionPlaceholder" => "Сипаттама",
                "PricePlaceholder" => "Баға",
                "StockPlaceholder" => "Саны",
                "AddProductButtonText" => "Тауар қосу",
                "OrdersTitle" => "Тапсырыстар",
                "ApproveText" => "Мақұлдау",
                "DeleteText" => "Жою",
                "CustomerCabinetTitle" => "Клиент кабинеті",
                "CustomerCabinetHint" => "Жеткізу мекенжайын толтырғаннан кейін тапсырысты рәсімдеңіз.",
                "PhoneHint" => "Телефон дәл 13 цифрдан тұруы керек.",
                "CartEmpty" => "Себет бос",
                "Pieces" => "дана, сомасы",
                "StaffUser" => "Қызметкер",
                "ClientUser" => "Клиент",
                "LoginPrompt" => "Логин мен құпия сөзді енгізіңіз.",
                "RegisterPrompt" => "Клиентті тіркеу деректерін толтырыңыз.",
                "ReadyStatus" => "SQLite дерекқоры дайын. Каталогтан тауар таңдаңыз.",
                "AdminLoginStatus" => "Әкімші кірді. Тауарлар мен тапсырыстарды басқаруға болады.",
                "RegisterValidation" => "Тіркелу үшін аты-жөні, логин, құпия сөз және дәл 13 цифрлық телефон қажет.",
                "LoginExists" => "Мұндай клиент логині бар.",
                "RegisterSuccess" => "Тіркелу аяқталды. Қош келдіңіз,",
                "WrongCredentials" => "Логин немесе құпия сөз қате.",
                "Welcome" => "Қош келдіңіз,",
                "StaffLoginStatus" => "Қызметкер кірді. Соңғы тапсырыстарды көруге болады.",
                "WrongStaffCredentials" => "Қызметкер логині немесе құпия сөзі қате.",
                "LogoutStatus" => "Сеанс аяқталды.",
                "ProductValidation" => "Тауарды толтырыңыз: атауы, санаты, сипаттамасы, бағасы және саны.",
                "ProductAdded" => "Тауар қосылды.",
                "ProductDeleted" => "Тауар жойылды:",
                "Order" => "Тапсырыс",
                "ApprovedLower" => "мақұлданды",
                "DeletedLower" => "жойылды",
                "StaffCartBlocked" => "Қызметкер тапсырыстарды қарайды, рәсімдеу клиенттерге қолжетімді.",
                "OutOfStock" => "Тауар қоймада жоқ.",
                "MaxQuantity" => "Бұл тауардың қолжетімді саны аяқталды.",
                "Added" => "Қосылды:",
                "OrderCreated" => "рәсімделді және SQLite ішінде сақталды.",
                _ => key
            }
            : key switch
            {
                "AppTitle" => "Интернет магазин",
                "LoginSubtitle" => "Вход для клиента и персонала",
                "LoginTitle" => "Вход",
                "LoginPlaceholder" => "Логин",
                "PasswordPlaceholder" => "Пароль",
                "LoginButtonText" => "Войти",
                "NoAccountText" => "Нет аккаунта? Зарегистрироваться",
                "RegisterTitle" => "Регистрация",
                "FullNamePlaceholder" => "ФИО",
                "Phone13Placeholder" => "Телефон: 13 цифр",
                "RegisterButtonText" => "Зарегистрироваться",
                "AlreadyAccountText" => "Уже есть аккаунт? Войти",
                "HeaderSubtitle" => "Каталог товаров, корзина, оформление заказа и хранение данных в SQLite",
                "LogoutText" => "Выйти",
                "SearchPlaceholder" => "Поиск товара",
                "CatalogTitle" => "Каталог",
                "AddToCartText" => "Добавить",
                "DeleteProductText" => "Удалить товар",
                "CartTitle" => "Корзина",
                "TotalText" => "Итого",
                "CheckoutTitle" => "Оформление заказа",
                "CustomerFullNamePlaceholder" => "ФИО клиента",
                "AddressPlaceholder" => "Адрес доставки",
                "CheckoutButtonText" => "Оформить заказ",
                "AddProductTitle" => "Добавить товар",
                "ProductNamePlaceholder" => "Название",
                "CategoryPlaceholder" => "Категория",
                "DescriptionPlaceholder" => "Описание",
                "PricePlaceholder" => "Цена",
                "StockPlaceholder" => "Количество",
                "AddProductButtonText" => "Добавить товар",
                "OrdersTitle" => "Заказы",
                "ApproveText" => "Одобрить",
                "DeleteText" => "Удалить",
                "CustomerCabinetTitle" => "Кабинет клиента",
                "CustomerCabinetHint" => "Оформляйте заказ после заполнения адреса доставки.",
                "PhoneHint" => "Телефон должен содержать ровно 13 цифр.",
                "CartEmpty" => "Корзина пустая",
                "Pieces" => "шт. на сумму",
                "StaffUser" => "Персонал",
                "ClientUser" => "Клиент",
                "LoginPrompt" => "Введите логин и пароль.",
                "RegisterPrompt" => "Заполните данные для регистрации клиента.",
                "ReadyStatus" => "База данных SQLite готова. Выберите товары из каталога.",
                "AdminLoginStatus" => "Вход администратора выполнен. Можно управлять товарами и заказами.",
                "RegisterValidation" => "Для регистрации заполните ФИО, логин, пароль и телефон ровно из 13 цифр.",
                "LoginExists" => "Такой логин клиента уже существует.",
                "RegisterSuccess" => "Регистрация выполнена. Добро пожаловать,",
                "WrongCredentials" => "Неверный логин или пароль.",
                "Welcome" => "Добро пожаловать,",
                "StaffLoginStatus" => "Вход персонала выполнен. Доступен просмотр последних заказов.",
                "WrongStaffCredentials" => "Неверный логин или пароль персонала.",
                "LogoutStatus" => "Сеанс завершен.",
                "ProductValidation" => "Заполните товар: название, категория, описание, цена и количество.",
                "ProductAdded" => "Товар добавлен.",
                "ProductDeleted" => "Товар удален:",
                "Order" => "Заказ",
                "ApprovedLower" => "одобрен",
                "DeletedLower" => "удален",
                "StaffCartBlocked" => "Персонал просматривает заказы, оформление доступно клиентам.",
                "OutOfStock" => "Товар закончился на складе.",
                "MaxQuantity" => "Больше единиц этого товара на складе нет.",
                "Added" => "Добавлено:",
                "OrderCreated" => "оформлен и сохранен в SQLite.",
                _ => key
            };
    }
}
