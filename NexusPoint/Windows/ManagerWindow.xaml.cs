using NexusPoint.BusinessLogic;
using NexusPoint.Data.Repositories;
using NexusPoint.Models;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace NexusPoint.Windows
{
    public partial class ManagerWindow : Window
    {
        private readonly User CurrentUser;
        private readonly ProductManager _productManager;
        private readonly StockManager _stockManager;
        private readonly UserManager _userManager;
        private readonly DiscountManager _discountManager;
        public ManagerWindow(User user)
        {
            InitializeComponent();
            CurrentUser = user ?? throw new ArgumentNullException(nameof(user));
            var productRepository = new ProductRepository();
            var stockItemRepository = new StockItemRepository();
            var userRepository = new UserRepository();
            var discountRepository = new DiscountRepository();
            _productManager = new ProductManager(productRepository);
            _stockManager = new StockManager(stockItemRepository, productRepository);
            _userManager = new UserManager(userRepository);
            _discountManager = new DiscountManager(discountRepository);


            UserInfoStatusBarText.Text = $"Пользователь: {CurrentUser.FullName} ({CurrentUser.Role})";
        }
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.WhenAll(
                LoadProductsAsync(),
                LoadStockItemsAsync(),
                LoadUsersAsync(),
                LoadDiscountsAsync()
            );
        }
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
        private async Task LoadProductsAsync(string searchTerm = null)
        {
            ProductsDataGrid.ItemsSource = await _productManager.GetProductsAsync(searchTerm);
        }

        private async Task LoadStockItemsAsync(string searchTerm = null)
        {
            StockDataGrid.ItemsSource = null;
            StockDataGrid.ItemsSource = await _stockManager.GetStockItemsViewAsync(searchTerm);
        }

        private async Task LoadUsersAsync()
        {
            UsersDataGrid.ItemsSource = await _userManager.GetUsersAsync();
        }

        private async Task LoadDiscountsAsync()
        {
            DiscountsDataGrid.ItemsSource = await _discountManager.GetDiscountsAsync();
        }
        private async void RefreshProductsButton_Click(object sender, RoutedEventArgs e) => await LoadProductsAsync();
        private async void SearchProductButton_Click(object sender, RoutedEventArgs e) => await LoadProductsAsync(ProductSearchTextBox.Text);
        private async void ResetProductSearchButton_Click(object sender, RoutedEventArgs e)
        {
            ProductSearchTextBox.Clear();
            await LoadProductsAsync();
        }

        private async void AddProductButton_Click(object sender, RoutedEventArgs e)
        {
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
                    bool deleted = _productManager.DeleteProduct(selectedProduct.ProductId);
                    if (deleted)
                    {
                        MessageBox.Show("Товар успешно удален.", "Удаление", MessageBoxButton.OK, MessageBoxImage.Information);
                        await Task.WhenAll(LoadProductsAsync(), LoadStockItemsAsync());
                    }
                }
            }
            else { MessageBox.Show("Пожалуйста, выберите товар для удаления.", "Выбор товара", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
        private async void RefreshStockButton_Click(object sender, RoutedEventArgs e) => await LoadStockItemsAsync();
        private async void SearchStockButton_Click(object sender, RoutedEventArgs e) => await LoadStockItemsAsync(StockSearchTextBox.Text);
        private async void ResetStockSearchButton_Click(object sender, RoutedEventArgs e)
        {
            StockSearchTextBox.Clear();
            await LoadStockItemsAsync();
        }

        private async void AdjustStockButton_Click(object sender, RoutedEventArgs e)
        {
            var adjustStockDialog = new AdjustStockWindow(_productManager, _stockManager) { Owner = this };
            if (adjustStockDialog.ShowDialog() == true)
            {
                await LoadStockItemsAsync(StockSearchTextBox.Text);
            }
        }
        private async void RefreshUsersButton_Click(object sender, RoutedEventArgs e) => await LoadUsersAsync();

        private async void AddUserButton_Click(object sender, RoutedEventArgs e)
        {
            var addUserDialog = new AddEditUserWindow(_userManager) { Owner = this };
            if (addUserDialog.ShowDialog() == true)
            {
                await LoadUsersAsync();
            }
        }

        private async void EditUserButton_Click(object sender, RoutedEventArgs e)
        {
            if (UsersDataGrid.SelectedItem is User selectedUser)
            {
                var editUserDialog = new AddEditUserWindow(_userManager, selectedUser) { Owner = this };
                if (editUserDialog.ShowDialog() == true)
                {
                    await LoadUsersAsync();
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
                    bool success = _userManager.ResetUserPassword(selectedUser.UserId, newPlainPassword);
                    if (success)
                    {
                        MessageBox.Show("Пароль успешно сброшен.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            else { MessageBox.Show("Выберите пользователя для сброса пароля.", "Выбор", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private async void DeleteUserButton_Click(object sender, RoutedEventArgs e)
        {
            if (UsersDataGrid.SelectedItem is User selectedUser)
            {
                var result = MessageBox.Show($"Удалить пользователя {selectedUser.Username}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    bool deleted = _userManager.DeleteUser(selectedUser.UserId, CurrentUser.UserId);
                    if (deleted)
                    {
                        MessageBox.Show("Пользователь удален.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadUsersAsync();
                    }
                }
            }
            else { MessageBox.Show("Выберите пользователя для удаления.", "Выбор", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
        private async void RefreshDiscountsButton_Click(object sender, RoutedEventArgs e) => await LoadDiscountsAsync();

        private async void AddDiscountButton_Click(object sender, RoutedEventArgs e)
        {
            var addDiscountDialog = new AddEditDiscountWindow(_discountManager, _productManager) { Owner = this };
            if (addDiscountDialog.ShowDialog() == true)
            {
                await LoadDiscountsAsync();
            }
        }

        private async void EditDiscountButton_Click(object sender, RoutedEventArgs e)
        {
            if (DiscountsDataGrid.SelectedItem is Discount selectedDiscount)
            {
                var editDiscountDialog = new AddEditDiscountWindow(_discountManager, _productManager, selectedDiscount) { Owner = this };
                if (editDiscountDialog.ShowDialog() == true)
                {
                    await LoadDiscountsAsync();
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
                    bool deleted = _discountManager.DeleteDiscount(selectedDiscount.DiscountId);
                    if (deleted)
                    {
                        MessageBox.Show("Акция удалена.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadDiscountsAsync();
                    }
                }
            }
            else { MessageBox.Show("Выберите акцию для удаления.", "Выбор", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
    }
}