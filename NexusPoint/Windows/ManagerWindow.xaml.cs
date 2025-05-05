using NexusPoint.BusinessLogic;
using NexusPoint.Data.Repositories;
using NexusPoint.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace NexusPoint.Windows
{
    public partial class ManagerWindow : Window
    {
        private readonly User CurrentUser;

        // --- Business Logic Managers ---
        private readonly ProductManager _productManager;
        private readonly StockManager _stockManager;
        private readonly UserManager _userManager;
        private readonly DiscountManager _discountManager;

        // --- Конструктор ---
        public ManagerWindow(User user)
        {
            InitializeComponent();
            CurrentUser = user ?? throw new ArgumentNullException(nameof(user));

            // Инициализация репозиториев
            var productRepository = new ProductRepository();
            var stockItemRepository = new StockItemRepository();
            var userRepository = new UserRepository();
            var discountRepository = new DiscountRepository();

            // Инициализация менеджеров
            _productManager = new ProductManager(productRepository);
            _stockManager = new StockManager(stockItemRepository, productRepository); // Передаем оба репо
            _userManager = new UserManager(userRepository);
            _discountManager = new DiscountManager(discountRepository);


            UserInfoStatusBarText.Text = $"Пользователь: {CurrentUser.FullName} ({CurrentUser.Role})";
        }

        // --- Загрузка окна ---
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Используем Task.WhenAll для параллельной загрузки
            await Task.WhenAll(
                LoadProductsAsync(),
                LoadStockItemsAsync(),
                LoadUsersAsync(),
                LoadDiscountsAsync()
            );
        }

        // --- Выход ---
        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("Вы уверены, что хотите выйти из системы?", "Выход", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();
                this.Close();
            }
        }

        // --- Методы загрузки данных (асинхронные) ---
        private async Task LoadProductsAsync(string searchTerm = null)
        {
            // Используем ProductManager
            ProductsDataGrid.ItemsSource = await _productManager.GetProductsAsync(searchTerm);
        }

        private async Task LoadStockItemsAsync(string searchTerm = null)
        {
            // Используем StockManager
            StockDataGrid.ItemsSource = null; // Сбрасываем перед загрузкой
            StockDataGrid.ItemsSource = await _stockManager.GetStockItemsViewAsync(searchTerm);
            // Обновление UI произойдет после await
        }

        private async Task LoadUsersAsync()
        {
            // Используем UserManager
            UsersDataGrid.ItemsSource = await _userManager.GetUsersAsync();
        }

        private async Task LoadDiscountsAsync()
        {
            // Используем DiscountManager
            DiscountsDataGrid.ItemsSource = await _discountManager.GetDiscountsAsync();
        }


        // --- Обработчики кнопок вкладки "Товары" ---
        private async void RefreshProductsButton_Click(object sender, RoutedEventArgs e) => await LoadProductsAsync();
        private async void SearchProductButton_Click(object sender, RoutedEventArgs e) => await LoadProductsAsync(ProductSearchTextBox.Text);
        private async void ResetProductSearchButton_Click(object sender, RoutedEventArgs e)
        {
            ProductSearchTextBox.Clear();
            await LoadProductsAsync();
        }

        private async void AddProductButton_Click(object sender, RoutedEventArgs e)
        {
            // Передаем _productManager в конструктор
            var addProductDialog = new AddEditProductWindow(_productManager) { Owner = this };
            if (addProductDialog.ShowDialog() == true)
            {
                await Task.WhenAll(LoadProductsAsync(), LoadStockItemsAsync());
            }
        }

        private async void EditProductButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProductsDataGrid.SelectedItem is Product selectedProduct)
            {
                // Передаем _productManager и редактируемый товар
                var editProductDialog = new AddEditProductWindow(_productManager, selectedProduct) { Owner = this };
                if (editProductDialog.ShowDialog() == true)
                {
                    await Task.WhenAll(LoadProductsAsync(), LoadStockItemsAsync());
                }
            }
            else { MessageBox.Show("Пожалуйста, выберите товар для редактирования.", "Выбор товара", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private async void DeleteProductButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProductsDataGrid.SelectedItem is Product selectedProduct)
            {
                var result = MessageBox.Show($"Вы уверены, что хотите удалить товар '{selectedProduct.Name}' (ID: {selectedProduct.ProductId})?\nЭто действие необратимо и также удалит запись об остатках!",
                                             "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    // Используем ProductManager для удаления
                    bool deleted = _productManager.DeleteProduct(selectedProduct.ProductId);
                    if (deleted)
                    {
                        MessageBox.Show("Товар успешно удален.", "Удаление", MessageBoxButton.OK, MessageBoxImage.Information);
                        // Перезагружаем оба списка
                        await Task.WhenAll(LoadProductsAsync(), LoadStockItemsAsync());
                    }
                    // Сообщение об ошибке покажется внутри DeleteProduct
                }
            }
            else { MessageBox.Show("Пожалуйста, выберите товар для удаления.", "Выбор товара", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }


        // --- Обработчики кнопок вкладки "Остатки" ---
        private async void RefreshStockButton_Click(object sender, RoutedEventArgs e) => await LoadStockItemsAsync();
        private async void SearchStockButton_Click(object sender, RoutedEventArgs e) => await LoadStockItemsAsync(StockSearchTextBox.Text);
        private async void ResetStockSearchButton_Click(object sender, RoutedEventArgs e)
        {
            StockSearchTextBox.Clear();
            await LoadStockItemsAsync();
        }

        private async void AdjustStockButton_Click(object sender, RoutedEventArgs e)
        {
            // Передаем нужные менеджеры
            var adjustStockDialog = new AdjustStockWindow(_productManager, _stockManager) { Owner = this };
            if (adjustStockDialog.ShowDialog() == true)
            {
                // Обновляем список остатков ПОСЛЕ успешной корректировки
                await LoadStockItemsAsync(StockSearchTextBox.Text);
            }
        }


        // --- Обработчики кнопок вкладки "Пользователи" ---
        private async void RefreshUsersButton_Click(object sender, RoutedEventArgs e) => await LoadUsersAsync();

       private async void AddUserButton_Click(object sender, RoutedEventArgs e)
{
    // Передаем _userManager
    var addUserDialog = new AddEditUserWindow(_userManager) { Owner = this };
    if (addUserDialog.ShowDialog() == true)
    {
        await LoadUsersAsync(); // Обновляем список
    }
}

private async void EditUserButton_Click(object sender, RoutedEventArgs e)
{
    if (UsersDataGrid.SelectedItem is User selectedUser)
    {
        // Передаем _userManager и пользователя
        var editUserDialog = new AddEditUserWindow(_userManager, selectedUser) { Owner = this };
        if (editUserDialog.ShowDialog() == true)
        {
            await LoadUsersAsync(); // Обновляем список
        }
    }
    else { MessageBox.Show("Выберите пользователя для редактирования.", "Выбор", MessageBoxButton.OK, MessageBoxImage.Warning); }
}

        private async void ResetUserPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            if (UsersDataGrid.SelectedItem is User selectedUser)
            {
                var passwordDialog = new InputDialog($"Сброс пароля для {selectedUser.Username}", "Введите НОВЫЙ пароль:", isPassword: true) { Owner = this };
                if (passwordDialog.ShowDialog() == true)
                {
                    string newPlainPassword = passwordDialog.InputText;
                    // Используем UserManager для сброса пароля
                    bool success = _userManager.ResetUserPassword(selectedUser.UserId, newPlainPassword);
                    if (success)
                    {
                        MessageBox.Show("Пароль успешно сброшен.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        // Список пользователей обновлять не нужно, изменился только хеш пароля
                    }
                    // Сообщение об ошибке покажется внутри ResetUserPassword
                }
            }
            else { MessageBox.Show("Выберите пользователя для сброса пароля.", "Выбор", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private async void DeleteUserButton_Click(object sender, RoutedEventArgs e)
        {
            if (UsersDataGrid.SelectedItem is User selectedUser)
            {
                // Проверка удаления текущего пользователя выполняется в менеджере
                var result = MessageBox.Show($"Удалить пользователя {selectedUser.Username}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    // Используем UserManager для удаления
                    bool deleted = _userManager.DeleteUser(selectedUser.UserId, CurrentUser.UserId);
                    if (deleted)
                    {
                        MessageBox.Show("Пользователь удален.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadUsersAsync(); // Обновляем список
                    }
                    // Сообщение об ошибке покажется внутри DeleteUser
                }
            }
            else { MessageBox.Show("Выберите пользователя для удаления.", "Выбор", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }


        // --- Обработчики кнопок вкладки "Акции и Скидки" ---
        private async void RefreshDiscountsButton_Click(object sender, RoutedEventArgs e) => await LoadDiscountsAsync();

        private async void AddDiscountButton_Click(object sender, RoutedEventArgs e)
        {
            // Передаем менеджеры
            var addDiscountDialog = new AddEditDiscountWindow(_discountManager, _productManager) { Owner = this };
            if (addDiscountDialog.ShowDialog() == true)
            {
                await LoadDiscountsAsync(); // Обновляем список
            }
        }

        private async void EditDiscountButton_Click(object sender, RoutedEventArgs e)
        {
            if (DiscountsDataGrid.SelectedItem is Discount selectedDiscount)
            {
                // Передаем менеджеры и скидку
                var editDiscountDialog = new AddEditDiscountWindow(_discountManager, _productManager, selectedDiscount) { Owner = this };
                if (editDiscountDialog.ShowDialog() == true)
                {
                    await LoadDiscountsAsync(); // Обновляем список
                }
            }
            else { MessageBox.Show("Выберите акцию для редактирования.", "Выбор", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private async void DeleteDiscountButton_Click(object sender, RoutedEventArgs e)
        {
            if (DiscountsDataGrid.SelectedItem is Discount selectedDiscount)
            {
                var result = MessageBox.Show($"Удалить акцию '{selectedDiscount.Name}'?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    // Используем DiscountManager
                    bool deleted = _discountManager.DeleteDiscount(selectedDiscount.DiscountId);
                    if (deleted)
                    {
                        MessageBox.Show("Акция удалена.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadDiscountsAsync(); // Обновляем список
                    }
                    // Сообщение об ошибке покажется внутри DeleteDiscount
                }
            }
            else { MessageBox.Show("Выберите акцию для удаления.", "Выбор", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
    }
}