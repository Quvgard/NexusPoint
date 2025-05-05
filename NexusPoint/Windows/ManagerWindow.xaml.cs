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
    /// <summary>
    /// Логика взаимодействия для ManagerWindow.xaml
    /// </summary>
    public partial class ManagerWindow : Window
    {
        private readonly User CurrentUser; // Текущий вошедший пользователь
        private readonly ProductRepository _productRepository;
        private readonly StockItemRepository _stockItemRepository;
        private readonly UserRepository _userRepository;
        private readonly DiscountRepository _discountRepository;

        // Временное хранилище для полных данных остатков (для отображения названий)
        // Лучше создать ViewModel или использовать JOIN в репозитории
        private class StockItemView
        {
            public int StockItemId { get; set; }
            public int ProductId { get; set; }
            public string ProductCode { get; set; } = "..."; // Добавим и Код товара
            public string Barcode { get; set; } = "...";     // <<--- ДОБАВЛЕНО: Штрих-код
            public string ProductName { get; set; } = "Загрузка...";
            public decimal Quantity { get; set; }
            public DateTime LastUpdated { get; set; }
        }
        private List<StockItemView> currentStockView = new List<StockItemView>();


        public ManagerWindow(User user)
        {
            InitializeComponent();
            CurrentUser = user;
            _productRepository = new ProductRepository();
            _stockItemRepository = new StockItemRepository();
            _userRepository = new UserRepository();
            _discountRepository = new DiscountRepository();

            // Отображаем информацию о пользователе в статусной строке
            UserInfoStatusBarText.Text = $"Пользователь: {CurrentUser.FullName} ({CurrentUser.Role})";
        }

        // Загрузка данных при открытии окна
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadProducts();
            LoadStockItems(); // Используем новый метод
            LoadUsers();
            LoadDiscounts();
        }


        // --- Обработчик кнопки выхода ---
        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            // Простое подтверждение выхода
            MessageBoxResult result = MessageBox.Show("Вы уверены, что хотите выйти из системы?",
                                                     "Выход",
                                                     MessageBoxButton.YesNo,
                                                     MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Создаем и показываем главное окно выбора режима
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();

                // Закрываем текущее окно управления
                this.Close();
            }
        }

        // --- Методы загрузки данных ---

        private void LoadProducts(string searchTerm = null)
        {
            try
            {
                IEnumerable<Product> products;
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    products = _productRepository.GetAllProducts();
                }
                else
                {
                    products = _productRepository.SearchProductsByName(searchTerm);
                }
                ProductsDataGrid.ItemsSource = products;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки списка товаров: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Обновленный метод загрузки остатков с именами товаров
        private async void LoadStockItems(string searchTerm = null)
        {
            try
            {
                // 1. Получаем все StockItems
                var stockItems = await Task.Run(() => _stockItemRepository.GetAllStockItems()); // Делаем асинхронным

                // Фильтрация (пока простая, по ProductId, если введено число)
                // TODO: Реализовать более сложную фильтрацию по коду, ШК, названию, если нужно
                List<StockItem> filteredStockItems;
                if (!string.IsNullOrWhiteSpace(searchTerm) && int.TryParse(searchTerm, out int searchProductId))
                {
                    filteredStockItems = stockItems.Where(si => si.ProductId == searchProductId).ToList();
                }
                // Здесь можно добавить else if для поиска по строке (коду, ШК, имени),
                // но это потребует сначала загрузить все продукты.
                // Пока фильтруем только по ID.
                else
                {
                    filteredStockItems = stockItems.ToList();
                }


                // 2. Создаем список для отображения StockItemView
                currentStockView = filteredStockItems.Select(si => new StockItemView
                {
                    StockItemId = si.StockItemId,
                    ProductId = si.ProductId,
                    Quantity = si.Quantity,
                    LastUpdated = si.LastUpdated
                    // Остальные поля (Code, Barcode, Name) будут загружены позже
                }).ToList();

                StockDataGrid.ItemsSource = null;
                StockDataGrid.ItemsSource = currentStockView;

                // 3. Асинхронно подгружаем детали товаров
                if (currentStockView.Any()) // Загружаем детали, только если есть что отображать
                {
                    await LoadProductDetailsForStockViewAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки остатков: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Переименованный и обновленный асинхронный метод для подгрузки деталей
        private async Task LoadProductDetailsForStockViewAsync()
        {
            List<int> productIds = currentStockView.Select(si => si.ProductId).Distinct().ToList();
            if (!productIds.Any()) return;

            try
            {
                // Загружаем нужные продукты одним запросом
                var products = (await Task.Run(() => _productRepository.GetProductsByIds(productIds)))
                               .ToDictionary(p => p.ProductId);

                // Обновляем объекты StockItemView в текущем списке
                foreach (var stockViewItem in currentStockView)
                {
                    if (products.TryGetValue(stockViewItem.ProductId, out Product product))
                    {
                        stockViewItem.ProductCode = product.ProductCode; // Добавляем Код
                        stockViewItem.Barcode = product.Barcode;         // Добавляем ШК
                        stockViewItem.ProductName = product.Name;          // Добавляем Имя
                    }
                    else // Если товар вдруг не найден в каталоге (маловероятно, но возможно)
                    {
                        stockViewItem.ProductCode = "<Н/Д>";
                        stockViewItem.Barcode = "<Н/Д>";
                        stockViewItem.ProductName = "<Товар не найден>";
                    }
                }

                // Обновляем DataGrid в потоке UI
                // Так как мы меняли свойства существующих объектов в currentStockView,
                // а StockItemView не реализует INotifyPropertyChanged,
                // простой Refresh может не сработать для обновления колонок.
                // Надежнее перепривязать источник.
                StockDataGrid.ItemsSource = null;
                StockDataGrid.ItemsSource = currentStockView;
                // Если StockItemView реализует INotifyPropertyChanged, можно было бы использовать:
                // StockDataGrid.Items.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки деталей товаров для остатков: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Асинхронный метод для подгрузки имен в StockDataGrid
        private async Task LoadProductNamesForStockViewAsync()
        {
            List<int> productIds = currentStockView.Select(si => si.ProductId).Distinct().ToList();
            if (!productIds.Any()) return;

            // Можно оптимизировать, получая сразу словарь ProductId -> ProductName
            var products = (await Task.Run(() => _productRepository.GetProductsByIds(productIds))).ToDictionary(p => p.ProductId); // Добавьте GetProductsByIds в ProductRepository

            foreach (var stockViewItem in currentStockView)
            {
                if (products.TryGetValue(stockViewItem.ProductId, out Product product))
                {
                    stockViewItem.ProductName = product.Name;
                }
                else
                {
                    stockViewItem.ProductName = "<Товар не найден>";
                }
            }

            // Обновляем DataGrid в потоке UI
            StockDataGrid.ItemsSource = null;
            StockDataGrid.ItemsSource = currentStockView;
            // StockDataGrid.Items.Refresh(); // Можно и так, если не менять ItemsSource
        }


        private void LoadUsers()
        {
            try
            {
                UsersDataGrid.ItemsSource = _userRepository.GetAllUsers();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки списка пользователей: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadDiscounts()
        {
            try
            {
                DiscountsDataGrid.ItemsSource = _discountRepository.GetAllDiscounts(); // Или GetAllActiveDiscounts() по умолчанию?
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки списка акций: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- Обработчики кнопок вкладки "Товары" ---

        private void RefreshProductsButton_Click(object sender, RoutedEventArgs e) => LoadProducts();
        private void SearchProductButton_Click(object sender, RoutedEventArgs e) => LoadProducts(ProductSearchTextBox.Text);
        private void ResetProductSearchButton_Click(object sender, RoutedEventArgs e)
        {
            ProductSearchTextBox.Clear();
            LoadProducts();
        }

        private void AddProductButton_Click(object sender, RoutedEventArgs e)
        {
            var addProductDialog = new AddEditProductWindow(); // Вызываем конструктор добавления
            addProductDialog.Owner = this; // Устанавливаем владельца
            if (addProductDialog.ShowDialog() == true)
            {
                LoadProducts(); // Обновляем список, если товар добавлен
                // Можно также обновить список остатков, т.к. для нового товара создается запись
                LoadStockItems();
            }
        }

        private void EditProductButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProductsDataGrid.SelectedItem is Product selectedProduct)
            {
                var editProductDialog = new AddEditProductWindow(selectedProduct); // Вызываем конструктор редактирования
                editProductDialog.Owner = this;
                if (editProductDialog.ShowDialog() == true)
                {
                    LoadProducts(); // Обновляем список
                    // Обновление остатков не требуется, т.к. меняется только каталог
                }
            }
            else
            {
                MessageBox.Show("Пожалуйста, выберите товар для редактирования.", "Выбор товара", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DeleteProductButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProductsDataGrid.SelectedItem is Product selectedProduct)
            {
                var result = MessageBox.Show($"Вы уверены, что хотите удалить товар '{selectedProduct.Name}' (ID: {selectedProduct.ProductId})?\nЭто действие также удалит запись об остатках!",
                                             "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        bool deleted = _productRepository.DeleteProduct(selectedProduct.ProductId);
                        if (deleted)
                        {
                            MessageBox.Show("Товар успешно удален.", "Удаление", MessageBoxButton.OK, MessageBoxImage.Information);
                            LoadProducts(); // Обновляем список товаров
                            LoadStockItems(); // Обновляем список остатков
                        }
                        else
                        {
                            MessageBox.Show("Не удалось удалить товар.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Обработка возможных ошибок внешнего ключа, если товар используется в чеках (хотя FK нет)
                        MessageBox.Show($"Ошибка при удалении товара: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Пожалуйста, выберите товар для удаления.", "Выбор товара", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // --- Обработчики кнопок вкладки "Остатки" ---
        private void RefreshStockButton_Click(object sender, RoutedEventArgs e) => LoadStockItems(); 

        private void SearchStockButton_Click(object sender, RoutedEventArgs e) => LoadStockItems(StockSearchTextBox.Text); 

        private void ResetStockSearchButton_Click(object sender, RoutedEventArgs e)
        {
            StockSearchTextBox.Clear();
            LoadStockItems();
        }


        private void AdjustStockButton_Click(object sender, RoutedEventArgs e)
        {
            var adjustStockDialog = new AdjustStockWindow();
            adjustStockDialog.Owner = this;
            if (adjustStockDialog.ShowDialog() == true) // Или можно не проверять результат, если окно само сообщает об успехе
            {
                // Обновляем список остатков ПОСЛЕ успешной корректировки
                LoadStockItems(StockSearchTextBox.Text); // Обновляем с учетом текущего поиска
            }
        }

        // --- Обработчики кнопок вкладки "Пользователи" ---
        private void RefreshUsersButton_Click(object sender, RoutedEventArgs e) => LoadUsers();

        private void AddUserButton_Click(object sender, RoutedEventArgs e)
        {
            var addUserDialog = new AddEditUserWindow();
            addUserDialog.Owner = this;
            if (addUserDialog.ShowDialog() == true)
            {
                LoadUsers(); // Обновляем список
            }
        }

        private void EditUserButton_Click(object sender, RoutedEventArgs e)
        {
            if (UsersDataGrid.SelectedItem is User selectedUser)
            {
                var editUserDialog = new AddEditUserWindow(selectedUser); // Передаем пользователя
                editUserDialog.Owner = this;
                if (editUserDialog.ShowDialog() == true)
                {
                    LoadUsers(); // Обновляем список
                }
            }
            else
            {
                MessageBox.Show("Выберите пользователя для редактирования.", "Выбор", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ResetUserPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            if (UsersDataGrid.SelectedItem is User selectedUser)
            {
                // Можно использовать AddEditUserWindow для сброса, но это может быть неудобно
                // Лучше сделать отдельный ResetPasswordDialog или простой InputDialog

                var passwordDialog = new InputDialog($"Сброс пароля для {selectedUser.Username}", "Введите НОВЫЙ пароль:", isPassword: true); // Добавить isPassword в InputDialog?
                passwordDialog.Owner = this;
                if (passwordDialog.ShowDialog() == true)
                {
                    string newPlainPassword = passwordDialog.InputText; // InputText вернет пароль
                    if (string.IsNullOrEmpty(newPlainPassword) || newPlainPassword.Length < 4)
                    {
                        MessageBox.Show("Пароль не может быть пустым и должен содержать не менее 4 символов.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    try
                    {
                        if (_userRepository.UpdateUserPassword(selectedUser.UserId, newPlainPassword))
                        {
                            MessageBox.Show("Пароль успешно сброшен.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("Не удалось сбросить пароль.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при сбросе пароля: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите пользователя для сброса пароля.", "Выбор", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DeleteUserButton_Click(object sender, RoutedEventArgs e)
        {
            if (UsersDataGrid.SelectedItem is User selectedUser)
            {
                if (selectedUser.UserId == CurrentUser.UserId)
                {
                    MessageBox.Show("Нельзя удалить текущего пользователя.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Удалить пользователя {selectedUser.Username}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Подумать про FK в чеках и сменах! Удаление может вызвать ошибку.
                        // Лучше добавить поле IsActive в Users и деактивировать.
                        if (_userRepository.DeleteUser(selectedUser.UserId))
                        {
                            MessageBox.Show("Пользователь удален.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                            LoadUsers();
                        }
                        else { /*...*/ }
                    }
                    catch (Exception ex) { /*...*/ }
                }
            }
            else
            {
                MessageBox.Show("Выберите пользователя для удаления.", "Выбор", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }


        // --- Обработчики кнопок вкладки "Акции и Скидки" ---
        private void RefreshDiscountsButton_Click(object sender, RoutedEventArgs e) => LoadDiscounts();

        private void AddDiscountButton_Click(object sender, RoutedEventArgs e)
        {
            var addDiscountDialog = new AddEditDiscountWindow();
            addDiscountDialog.Owner = this;
            if (addDiscountDialog.ShowDialog() == true)
            {
                LoadDiscounts(); // Обновляем список
            }
        }

        private void EditDiscountButton_Click(object sender, RoutedEventArgs e)
        {
            if (DiscountsDataGrid.SelectedItem is Discount selectedDiscount)
            {
                var editDiscountDialog = new AddEditDiscountWindow(selectedDiscount);
                editDiscountDialog.Owner = this;
                if (editDiscountDialog.ShowDialog() == true)
                {
                    LoadDiscounts(); // Обновляем список
                }
            }
            else
            {
                MessageBox.Show("Выберите акцию для редактирования.", "Выбор", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DeleteDiscountButton_Click(object sender, RoutedEventArgs e)
        {
            if (DiscountsDataGrid.SelectedItem is Discount selectedDiscount)
            {
                var result = MessageBox.Show($"Удалить акцию {selectedDiscount.Name}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        if (_discountRepository.DeleteDiscount(selectedDiscount.DiscountId))
                        {
                            MessageBox.Show("Акция удалена.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                            LoadDiscounts();
                        }
                        else { /*...*/ }
                    }
                    catch (Exception ex) { /*...*/ }
                }
            }
            else
            {
                MessageBox.Show("Выберите акцию для удаления.", "Выбор", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}