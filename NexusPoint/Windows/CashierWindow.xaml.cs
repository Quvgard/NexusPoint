using NexusPoint.BusinessLogic;
using NexusPoint.Data.Repositories;
using NexusPoint.Models;
using NexusPoint.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
using System.Windows.Threading;
using System.ComponentModel; 
using System.Runtime.CompilerServices; 

namespace NexusPoint.Windows
{


    public partial class CashierWindow : Window, INotifyPropertyChanged
    {
        private readonly User CurrentUser;

        // --- Business Logic Services ---
        private readonly ShiftManager _shiftManager;
        private readonly SaleManager _saleManager;
        private readonly PaymentProcessor _paymentProcessor;
        private readonly AuthorizationService _authorizationService;
        private readonly ReportService _reportService;
        private readonly ProductManager _productManager; 
        private readonly StockManager _stockManager;
        private readonly FileReportLogger _fileReportLogger;
        // ReturnManager не нужен здесь, используется в ReturnWindow

        // --- UI State & Timers ---
        private ContextMenu _originalCheckListViewContextMenu;
        private DispatcherTimer _clockTimer;
        private DispatcherTimer _inactivityTimer;
        private const int InactivityTimeoutMinutes = 15;
        private bool _isLocked = false;

        // --- Свойства для привязки к UI (делегируем SaleManager) ---
        public decimal Subtotal => _saleManager?.Subtotal ?? 0m;
        public decimal TotalDiscount => _saleManager?.TotalDiscount ?? 0m;
        public decimal TotalAmount => _saleManager?.TotalAmount ?? 0m;

        // --- Конструктор ---
        public CashierWindow(User user)
        {
            InitializeComponent();
            CurrentUser = user ?? throw new ArgumentNullException(nameof(user));

            // Инициализация репозиториев (необходимы для BL)
            var productRepository = new ProductRepository();
            var stockItemRepository = new StockItemRepository();
            var checkRepository = new CheckRepository();
            var shiftRepository = new ShiftRepository();
            var cashDrawerRepository = new CashDrawerOperationRepository();
            var userRepository = new UserRepository(); // Нужен для отчетов и ShiftManager

            // Инициализация Business Logic классов
            _authorizationService = new AuthorizationService();
            _reportService = new ReportService(checkRepository, cashDrawerRepository, userRepository);
            _shiftManager = new ShiftManager(shiftRepository, cashDrawerRepository, _reportService, userRepository);
            _saleManager = new SaleManager(productRepository, stockItemRepository);
            _paymentProcessor = new PaymentProcessor(checkRepository, productRepository, stockItemRepository);
            _productManager = new ProductManager(productRepository); 
            _stockManager = new StockManager(stockItemRepository, productRepository); 
            _authorizationService = new AuthorizationService();
            _fileReportLogger = new FileReportLogger();

            // Подписка на события изменения SaleManager для обновления UI
            _saleManager.PropertyChanged += SaleManager_PropertyChanged;
            _shiftManager.ShiftOpened += ShiftManager_ShiftStateChanged;
            _shiftManager.ShiftClosed += ShiftManager_ShiftStateChanged;


            // Привязка данных к UI
            this.DataContext = this; // Для привязки Subtotal, TotalDiscount, TotalAmount
            CheckListView.ItemsSource = _saleManager.CurrentCheckItems; // Привязка списка чека

            InitializeInactivityTimer();
            this.PreviewMouseMove += Window_ActivityDetected;
            this.PreviewKeyDown += Window_ActivityDetected;
        }

        // --- Обработчик загрузки окна ---
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _originalCheckListViewContextMenu = CheckListView.ContextMenu;
            _shiftManager.CheckCurrentShiftState(); // Проверяем состояние смены при запуске
            UpdateUIBasedOnShiftState(); // Обновляем UI (оверлей, контролы, меню)
            UpdateCashierInfo();
            SetupClock();
            ItemInputTextBox.Focus();

            if (!_isLocked && _shiftManager.CurrentOpenShift != null)
            {
                ResetInactivityTimer();
            }
        }

        // --- Обновление UI при изменении состояния смены ---
        private void ShiftManager_ShiftStateChanged(object sender, EventArgs e)
        {
            // Обновляем UI из потока UI
            Dispatcher.Invoke(() => {
                UpdateUIBasedOnShiftState();
                if (_shiftManager.CurrentOpenShift != null && !_isLocked)
                {
                    ResetInactivityTimer();
                }
                else
                {
                    _inactivityTimer?.Stop();
                }
            });
        }

        // --- Обновление UI при изменении данных чека ---
        private void SaleManager_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Обновляем свойства, связанные с итогами, когда они меняются в SaleManager
            if (e.PropertyName == nameof(SaleManager.Subtotal)) OnPropertyChanged(nameof(Subtotal));
            if (e.PropertyName == nameof(SaleManager.TotalDiscount)) OnPropertyChanged(nameof(TotalDiscount));
            if (e.PropertyName == nameof(SaleManager.TotalAmount)) OnPropertyChanged(nameof(TotalAmount));

            // Обновляем состояние кнопок и инфо о последнем товаре
            if (e.PropertyName == nameof(SaleManager.HasItems)) UpdateCheckoutButtonsState();
            if (e.PropertyName == nameof(SaleManager.LastAddedProduct)) UpdateLastItemInfo();
        }

        // --- Управление состоянием UI (Оверлей, Контролы, Меню) ---
        private void UpdateUIBasedOnShiftState()
        {
            bool isShiftOpen = _shiftManager.CurrentOpenShift != null;

            if (!isShiftOpen && !_isLocked) // Показываем оверлей только если не заблокировано
            {
                ShowOverlay("СМЕНА ЗАКРЫТА.\nНажмите F12 -> Открыть смену.", Brushes.White);
                DisableCheckoutControls();
            }
            else if (!_isLocked) // Если не заблокировано и смена открыта
            {
                HideOverlay();
                EnableCheckoutControls();
            }
            // Если заблокировано (_isLocked == true), оверлей и контролы управляются Lock/Unlock методами

            UpdateShiftInfo(); // Обновляем текст статуса смены
            UpdateMenuItemsState(); // Обновляем доступность пунктов меню
            UpdateCheckoutButtonsState(); // Обновляем доступность кнопок оплаты и т.д.

            if (!isShiftOpen)
            {
                _saleManager.ClearCheck(); // Очищаем чек, если смена закрылась
            }
        }

        private void UpdateCheckoutButtonsState()
        {
            bool isShiftOpen = _shiftManager.CurrentOpenShift != null;
            bool hasItems = _saleManager.HasItems;
            bool canPay = isShiftOpen && hasItems && _saleManager.TotalAmount >= 0 && !_isLocked;
            bool canModifyCheck = isShiftOpen && hasItems && !_isLocked;
            bool canStartActions = isShiftOpen && !_isLocked;


            PaymentButton.IsEnabled = canPay;
            CancelCheckButton.IsEnabled = canModifyCheck;
            QuantityButton.IsEnabled = canModifyCheck; // Зависит от выбора элемента в списке
            DeleteItemButton.IsEnabled = canModifyCheck; // Зависит от выбора элемента в списке
            ManualDiscountButton.IsEnabled = canModifyCheck; // Плюс проверка роли

            ReturnModeButton.IsEnabled = canStartActions;
            PrintDocButton.IsEnabled = !_isLocked; // Печать доков можно разрешить всегда?
            LookupItemButton.IsEnabled = !_isLocked; // Инфо о товаре тоже?
            ItemInputTextBox.IsEnabled = canStartActions;

            // Контекстное меню
            if (CheckListView != null)
            { // Добавим проверку на null
                CheckListView.ContextMenu = canModifyCheck ? _originalCheckListViewContextMenu : null;
            }

            // Фокус
            if (canStartActions)
            {
                // Проверяем, где сейчас фокус, чтобы не перескакивал без нужды
                if (!ItemInputTextBox.IsFocused && !CheckListView.IsFocused)
                {
                    ItemInputTextBox.Focus();
                }
            }
        }

        private void ShowOverlay(string message, Brush foregroundBrush = null)
        {
            OverlayText.Text = message;
            OverlayText.Foreground = foregroundBrush ?? Brushes.White;
            OverlayBorder.Visibility = Visibility.Visible;
        }

        private void HideOverlay()
        {
            OverlayBorder.Visibility = Visibility.Collapsed;
        }

        private void DisableCheckoutControls()
        {
            ItemInputTextBox.IsEnabled = false;
            PaymentButton.IsEnabled = false;
            QuantityButton.IsEnabled = false;
            DeleteItemButton.IsEnabled = false;
            ReturnModeButton.IsEnabled = false;
            ManualDiscountButton.IsEnabled = false;
            CancelCheckButton.IsEnabled = false;
            if (CheckListView != null) CheckListView.ContextMenu = null;
        }

        private void EnableCheckoutControls()
        {
            // Доступность кнопок будет уточнена в UpdateCheckoutButtonsState
            ItemInputTextBox.IsEnabled = true;
            if (CheckListView != null && _originalCheckListViewContextMenu != null)
            {
                CheckListView.ContextMenu = _originalCheckListViewContextMenu;
            }
            UpdateCheckoutButtonsState(); // Пересчитываем доступность на основе состояния
        }

        private void UpdateCashierInfo()
        {
            CashierInfoStatusText.Text = $"Кассир: {CurrentUser.FullName}";
            CashierInfoStatusText.Foreground = Brushes.Black;
        }

        private void UpdateShiftInfo()
        {
            if (_shiftManager.CurrentOpenShift != null)
            {
                ShiftInfoStatusText.Text = $"Смена №: {_shiftManager.CurrentOpenShift.ShiftNumber} (от {_shiftManager.CurrentOpenShift.OpenTimestamp:dd.MM HH:mm})";
            }
            else
            {
                ShiftInfoStatusText.Text = "Смена: Закрыта";
            }
        }

        private void UpdateLastItemInfo()
        {
            var product = _saleManager.LastAddedProduct;
            if (product != null)
            {
                LastItemInfoText.Text = $"Добавлено: {product.Name}\nЦена: {product.Price:C}\nКод: {product.ProductCode}";
            }
            else if (!string.IsNullOrWhiteSpace(ItemInputTextBox.Text)) // Если товар не найден после ввода
            {
                LastItemInfoText.Text = $"- Товар с кодом/ШК '{ItemInputTextBox.Text}' не найден -";
            }
            else // Если чек пуст
            {
                LastItemInfoText.Text = "-";
            }
        }

        // --- Часы ---
        private void SetupClock()
        {
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) => UpdateClock();
            _clockTimer.Start();
            UpdateClock();
        }

        private void UpdateClock() => ClockTextBlock.Text = DateTime.Now.ToString("HH:mm");

        // --- Управление неактивностью и блокировкой ---
        private void InitializeInactivityTimer()
        {
            _inactivityTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(InactivityTimeoutMinutes) };
            _inactivityTimer.Tick += (s, e) => { if (this.IsActive && !_isLocked) LockScreen("Экран заблокирован из-за неактивности."); };
        }

        private void ResetInactivityTimer() => _inactivityTimer?.Start(); // Просто перезапускаем

        private void Window_ActivityDetected(object sender, InputEventArgs e)
        {
            if (!_isLocked && _shiftManager.CurrentOpenShift != null) ResetInactivityTimer();
        }

        private void LockScreen(string lockMessage = "Станция заблокирована.")
        {
            if (_isLocked) return;
            _isLocked = true;
            _inactivityTimer.Stop();
            DisableCheckoutControls();
            ShowOverlay($"{lockMessage}\nВведите пароль для разблокировки.", Brushes.OrangeRed);
            AttemptUnlock(); // Начинаем процесс разблокировки
        }

        private void AttemptUnlock()
        {
            // Используем AuthorizationService для получения пользователя
            // Важно: LoginWindow должен уметь блокировать кнопку "Отмена", если это требуется при разблокировке
            var loginWindow = new LoginWindow(CurrentUser.Username, true); // true - блокировать отмену
            loginWindow.Owner = this;

            if (loginWindow.ShowDialog() == true)
            {
                // Проверяем, что вошел ТОТ ЖЕ пользователь
                if (loginWindow.AuthenticatedUser != null && loginWindow.AuthenticatedUser.UserId == CurrentUser.UserId)
                {
                    UnlockScreen();
                }
                else
                {
                    MessageBox.Show("Для разблокировки необходимо войти под текущим пользователем.", "Ошибка разблокировки", MessageBoxButton.OK, MessageBoxImage.Warning);
                    // Повторяем попытку разблокировки
                    ShowOverlay($"Станция заблокирована.\nВход не выполнен. Повторите ввод пароля.", Brushes.OrangeRed);
                    Dispatcher.BeginInvoke(new Action(AttemptUnlock), DispatcherPriority.Background); // Вызываем асинхронно, чтобы не блокировать UI
                }
            }
            else
            {
                // Пользователь нажал "Отмена" (если она была доступна) или закрыл окно
                // Остаемся заблокированными
                ShowOverlay($"Станция заблокирована.\nВход не выполнен. Станция остается заблокированной.", Brushes.OrangeRed);
                // Можно запланировать повторный вызов AttemptUnlock через какое-то время или оставить так
            }
        }


        private void UnlockScreen()
        {
            _isLocked = false;
            _shiftManager.CheckCurrentShiftState(); // Перепроверяем состояние смены
            UpdateUIBasedOnShiftState(); // Обновляем UI в соответствии с состоянием смены
            if (_shiftManager.CurrentOpenShift != null)
            {
                ResetInactivityTimer(); // Запускаем таймер, если смена открыта
                ItemInputTextBox.Focus();
                ShowTemporaryStatusMessage("Станция разблокирована.", isInfo: true);
            }
            else
            {
                ShowTemporaryStatusMessage("Станция разблокирована, но смена закрыта.", isInfo: true);
            }

        }

        // --- Обработка ввода товара ---
        private void ItemInputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(ItemInputTextBox.Text))
            {
                if (_shiftManager.CurrentOpenShift == null)
                {
                    ShowTemporaryStatusMessage("Ошибка: Смена не открыта!", isError: true);
                    return;
                }
                if (_isLocked) return;

                string codeOrBarcode = ItemInputTextBox.Text.Trim();

                decimal stockBeforeAdding = -1; 
                Product productInfo = null;
                try
                {

                    productInfo = new ProductRepository().FindProductByCodeOrBarcode(codeOrBarcode);
                    if (productInfo != null)
                    {
                        stockBeforeAdding = new StockItemRepository().GetStockQuantity(productInfo.ProductId);
                    }
                }
                catch { } 

                ItemInputTextBox.Clear(); 

                bool added = _saleManager.AddItem(codeOrBarcode);

                UpdateLastItemInfo(); 

                if (!added && _saleManager.LastAddedProduct == null)
                {
                    ShowTemporaryStatusMessage($"Товар с кодом/ШК '{codeOrBarcode}' не найден!", isError: true);
                }

                else if (added && stockBeforeAdding <= 0 && productInfo != null) 
                {
                    ShowTemporaryStatusMessage($"Товар '{productInfo.Name}' добавлен (Остаток <= 0!)", isInfo: true); 
                }

                e.Handled = true;
            }
        }

        // --- Обработка кнопок чека ---
        private async void PaymentButton_Click(object sender, RoutedEventArgs e)
        {
            if (!PaymentButton.IsEnabled) return;

            // --- Добавляем блок расчета авто-скидок ПЕРЕД диалогом оплаты ---
            if (!_saleManager.IsManualDiscountApplied)
            {
                PaymentButton.IsEnabled = false; // Блокируем кнопку на время расчета
                ShowTemporaryStatusMessage("Расчет автоматических скидок...", isInfo: true, durationSeconds: 15);
                bool discountApplied = await _saleManager.CalculateAndApplyAutoDiscountsAsync();
                ClearTemporaryStatusMessage();
                // Обновляем состояние кнопки после расчета (TotalAmount мог измениться)
                UpdateCheckoutButtonsState();


                if (!discountApplied)
                {
                    // Если при расчете скидок произошла ошибка, не продолжаем оплату
                    PaymentButton.IsEnabled = true; // Разблокируем кнопку
                    ItemInputTextBox.Focus();
                    return;
                }
                // Если скидки применены, UI уже обновился через PropertyChanged из SaleManager
            }
            // --- Конец блока расчета авто-скидок ---


            // Проверка, остались ли вообще товары к оплате после скидок
            if (!_saleManager.HasItems)
            {
                ShowTemporaryStatusMessage("Чек пуст после применения скидок/подарков.", isInfo: true);
                ItemInputTextBox.Focus();
                return;
            }

            // Используем обновленную сумму из SaleManager
            var paymentDialog = new PaymentDialog(_saleManager.TotalAmount); // <--- ИЗМЕНЕНО: Используем актуальный TotalAmount
            paymentDialog.Owner = this;

            if (paymentDialog.ShowDialog() == true)
            {
                PaymentButton.IsEnabled = false;
                ShowTemporaryStatusMessage("Обработка оплаты...", isInfo: true, durationSeconds: 15);

                // Передаем ТЕКУЩИЕ (уже со скидками) данные из SaleManager в процессор
                var savedCheck = await _paymentProcessor.ProcessPaymentAsync(
                    _saleManager.CurrentCheckItems, // Передаем текущие элементы
                    _saleManager.IsManualDiscountApplied, // Передаем актуальный флаг
                    _shiftManager.CurrentOpenShift,
                    CurrentUser,
                    paymentDialog.SelectedPaymentType,
                    paymentDialog.CashPaid,
                    paymentDialog.CardPaid,
                    paymentDialog.Change);

                ClearTemporaryStatusMessage();

                if (savedCheck != null)
                {
                    _saleManager.ClearCheck(); // Очищаем чек в SaleManager
                    ItemInputTextBox.Focus();
                }
                else
                {
                    // Оставляем чек как есть для исправления
                    UpdateCheckoutButtonsState(); // Восстанавливаем состояние кнопок
                }
            }
            else
            {
                ShowTemporaryStatusMessage("Оплата отменена.");
                // Скидки, примененные на предыдущем шаге, остаются в чеке
                ItemInputTextBox.Focus();
            }
        }

        private void QuantityButton_Click(object sender, RoutedEventArgs e) => ChangeSelectedItemQuantity();
        private void ChangeQuantityMenuItem_Click(object sender, RoutedEventArgs e) => ChangeSelectedItemQuantity();

        private void ChangeSelectedItemQuantity()
        {
            if (CheckListView.SelectedItem is CheckItemView selectedItem)
            {
                var quantityDialog = new InputDialog("Количество", $"Введите новое количество для '{selectedItem.ProductName}':", selectedItem.Quantity.ToString());
                quantityDialog.Owner = this;
                if (quantityDialog.ShowDialog() == true && decimal.TryParse(quantityDialog.InputText, out decimal newQuantity))
                {
                    bool success = _saleManager.ChangeItemQuantity(selectedItem, newQuantity);
                    if (success) ItemInputTextBox.Focus();
                }
                else if (quantityDialog.DialogResult == true)
                {
                    MessageBox.Show("Некорректное количество.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                ShowTemporaryStatusMessage("Выберите товар в чеке для изменения количества.", isError: true);
            }
        }

        private void StornoCheckItemButton_Click(object sender, RoutedEventArgs e) => InitiateStorno();
        private void StornoCheckItemMenuItem_Click(object sender, RoutedEventArgs e) => InitiateStorno();

        private void InitiateStorno()
        {
            if (!(CheckListView.SelectedItem is CheckItemView selectedItem))
            {
                ShowTemporaryStatusMessage("Выберите позицию в чеке для сторнирования.", isError: true);
                return;
            }

            User supervisor = _authorizationService.AuthorizeAction("Сторнирование позиции", new[] { "Admin", "Manager" }, this);
            if (supervisor == null)
            {
                ShowTemporaryStatusMessage("Операция сторно отменена или не авторизована.", isError: true);
                ItemInputTextBox.Focus();
                return;
            }

            decimal quantityToStorno = 0;
            if (selectedItem.Quantity <= 1)
            {
                MessageBoxResult confirmResult = MessageBox.Show($"Сторнировать позицию '{selectedItem.ProductName}'?", "Полное сторно", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                if (confirmResult == MessageBoxResult.Yes) quantityToStorno = selectedItem.Quantity;
                else { ShowTemporaryStatusMessage("Сторно отменено.", isInfo: true); ItemInputTextBox.Focus(); return; }
            }
            else
            {
                var stornoQtyDialog = new InputDialog("Частичное сторно", $"Введите количество товара '{selectedItem.ProductName}',\nкоторое нужно СТОРНИРОВАТЬ (убрать) из чека.\n(Макс: {selectedItem.Quantity}, 0 = отмена):", "1");
                stornoQtyDialog.Owner = this;
                if (stornoQtyDialog.ShowDialog() == true)
                {
                    if (!decimal.TryParse(stornoQtyDialog.InputText, NumberStyles.Any, CultureInfo.CurrentCulture, out quantityToStorno) || quantityToStorno < 0)
                    { ShowTemporaryStatusMessage("Некорректное количество для сторно.", isError: true); ItemInputTextBox.Focus(); return; }
                    if (quantityToStorno == 0) { ShowTemporaryStatusMessage("Сторно отменено (введено 0).", isInfo: true); ItemInputTextBox.Focus(); return; }
                    if (quantityToStorno > selectedItem.Quantity) { ShowTemporaryStatusMessage($"Нельзя сторнировать {quantityToStorno} шт., в позиции только {selectedItem.Quantity} шт.", isError: true); ItemInputTextBox.Focus(); return; }
                    if (quantityToStorno == selectedItem.Quantity)
                    {
                        MessageBoxResult confirmFullResult = MessageBox.Show("Вы ввели количество для полного сторнирования. Продолжить?", "Полное сторно", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes);
                        if (confirmFullResult == MessageBoxResult.No) { ShowTemporaryStatusMessage("Сторно отменено.", isInfo: true); ItemInputTextBox.Focus(); return; }
                    }
                }
                else { ShowTemporaryStatusMessage("Сторно отменено.", isInfo: true); ItemInputTextBox.Focus(); return; }
            }

            if (quantityToStorno > 0)
            {
                bool success = _saleManager.StornoItem(selectedItem, quantityToStorno);
                if (success)
                {
                    string stornoType = (quantityToStorno >= selectedItem.Quantity) ? "полностью" : "частично";
                    string message = $"Позиция '{selectedItem.ProductName}' сторнирована {stornoType}.";
                    LastItemInfoText.Text = $"- СТОРНО: {selectedItem.ProductName} (Авториз.: {supervisor.Username}) -";
                    ShowTemporaryStatusMessage(message, isInfo: true);
                }
            }
            ItemInputTextBox.Focus();
        }

        private void ReturnModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_shiftManager.CurrentOpenShift == null) { ShowTemporaryStatusMessage("Смена не открыта!", true); return; }
            var returnWindow = new ReturnWindow(CurrentUser, _shiftManager.CurrentOpenShift);
            returnWindow.Owner = this;
            returnWindow.ShowDialog();
            ItemInputTextBox.Focus();
        }

        private void ManualDiscountButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentUser.Role != "Manager" && CurrentUser.Role != "Admin")
            {
                MessageBox.Show("У вас недостаточно прав для применения ручной скидки.", "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!_saleManager.HasItems) return;

            var discountDialog = new DiscountDialog(Subtotal); // Используем свойство Subtotal
            discountDialog.Owner = this;
            if (discountDialog.ShowDialog() == true)
            {
                bool success = _saleManager.ApplyManualDiscount(discountDialog.DiscountValue, discountDialog.IsPercentage);
                if (success)
                {
                    ShowTemporaryStatusMessage($"Применена ручная скидка: {discountDialog.CalculatedDiscountAmount:C}", isInfo: true);
                }
            }
        }

        private void PrintDocButton_Click(object sender, RoutedEventArgs e)
        {
            var printDocsWindow = new PrintDocumentsWindow();
            printDocsWindow.Owner = this;
            printDocsWindow.ShowDialog();
            ItemInputTextBox.Focus();
        }

        private void LookupItemButton_Click(object sender, RoutedEventArgs e)
        {
            var itemInfoWindow = new ItemInfoViewWindow(_productManager, _stockManager) { Owner = this };
            itemInfoWindow.ShowDialog();
            ItemInputTextBox.Focus();
        }

        private void CancelCheckButton_Click(object sender, RoutedEventArgs e)
        {
            if (_saleManager.HasItems)
            {
                var result = MessageBox.Show("Отменить текущий чек? Все позиции будут удалены.", "Отмена чека", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    _saleManager.ClearCheck();
                }
            }
        }


        // --- Меню (F12) ---
        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            MainMenuPopup.IsOpen = !MainMenuPopup.IsOpen;
        }

        private void UpdateMenuItemsState()
        {
            bool isShiftOpen = _shiftManager.CurrentOpenShift != null;
            OpenShiftItem.IsEnabled = !isShiftOpen;
            CloseShiftItem.IsEnabled = isShiftOpen;
            XReportItem.IsEnabled = isShiftOpen;
            CashInItem.IsEnabled = isShiftOpen;
            CashOutItem.IsEnabled = isShiftOpen;
            LockStationItem.IsEnabled = !_isLocked;
            LogoutItem.IsEnabled = true;
        }

        private void MainMenuPopup_Opened(object sender, EventArgs e)
        {
            UpdateMenuItemsState();
            ListBoxItem firstEnabledItem = MenuListBox.Items.OfType<ListBoxItem>()
                                            .FirstOrDefault(item => item.IsEnabled);
            if (firstEnabledItem != null)
            {
                firstEnabledItem.Focus();
                MenuListBox.SelectedItem = firstEnabledItem;
            }
            else
            {
                MenuListBox.SelectedIndex = -1;
            }
        }

        private void ExecuteSelectedMenuItem()
        {
            if (MenuListBox.SelectedItem is ListBoxItem selectedItem)
            {
                MainMenuPopup.IsOpen = false;
                switch (selectedItem.Name)
                {
                    case "OpenShiftItem": OpenShiftMenuItem_Click(selectedItem, null); break;
                    case "CloseShiftItem": CloseShiftMenuItem_Click(selectedItem, null); break;
                    case "XReportItem": PrintXReportMenuItem_Click(selectedItem, null); break;
                    case "CashInItem": CashInMenuItem_Click(selectedItem, null); break;
                    case "CashOutItem": CashOutMenuItem_Click(selectedItem, null); break;
                    case "LockStationItem": LockStationMenuItem_Click(selectedItem, null); break;
                    case "LogoutItem": LogoutMenuItem_Click(selectedItem, null); break;
                }
            }
        }

        private void MenuListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Space) { ExecuteSelectedMenuItem(); e.Handled = true; }
            else if (e.Key == Key.Escape) { MainMenuPopup.IsOpen = false; MenuButton.Focus(); e.Handled = true; }
        }

        private void MenuListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.Source is ListBoxItem clickedItem && clickedItem.IsEnabled)
            {
                MenuListBox.SelectedItem = clickedItem;
                ExecuteSelectedMenuItem();
                e.Handled = true;
            }
            else if (VisualTreeHelper.GetParent(e.OriginalSource as DependencyObject) is TextBlock tb && VisualTreeHelper.GetParent(tb) is ListBoxItem parentItem && parentItem.IsEnabled)
            {
                // Клик по TextBlock внутри активного ListBoxItem
                MenuListBox.SelectedItem = parentItem;
                ExecuteSelectedMenuItem();
                e.Handled = true;
            }
        }


        // --- Обработчики пунктов меню ---
        private void OpenShiftMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var startCashDialog = new InputDialog("Открыть смену", "Введите сумму наличных в кассе на начало смены:", "0");
            startCashDialog.Owner = this;
            if (startCashDialog.ShowDialog() == true && decimal.TryParse(startCashDialog.InputText, out decimal startCash))
            {
                bool opened = _shiftManager.OpenShift(CurrentUser, startCash);
                if (opened) ShowTemporaryStatusMessage($"Смена №{_shiftManager.CurrentOpenShift.ShiftNumber} открыта.");
            }
            else if (startCashDialog.DialogResult == true)
            {
                MessageBox.Show("Некорректная сумма.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void CloseShiftMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем, есть ли что закрывать
            if (_shiftManager.CurrentOpenShift == null) return;

            var endCashDialog = new InputDialog("Закрыть смену", $"Смена №{_shiftManager.CurrentOpenShift.ShiftNumber}\nВведите фактическую сумму наличных в кассе:", "0");
            endCashDialog.Owner = this;

            if (endCashDialog.ShowDialog() == true && decimal.TryParse(endCashDialog.InputText, out decimal endCashActual))
            {
                ShowTemporaryStatusMessage("Закрытие смены и формирование Z-отчета...", isInfo: true, durationSeconds: 15);

                // Вызываем обновленный метод, который вернет данные о закрытой смене
                Shift closedShift = await _shiftManager.CloseShiftAsync(CurrentUser, endCashActual);

                ClearTemporaryStatusMessage();

                // Если метод вернул данные (т.е. смена успешно закрыта)
                if (closedShift != null)
                {
                    try
                    {
                        string reportTitle = $"Z-Отчет (Смена №{closedShift.ShiftNumber})";
                        // Используем полученный объект для генерации отчета
                        string reportContent = await _reportService.GenerateZReportAsync(closedShift);

                        // 1. Автоматически сохраняем в лог-файл
                        _fileReportLogger.AppendReportToFile(reportContent);

                        // 2. Показываем отчет в нашем кастомном окне
                        var reportViewer = new ReportViewerWindow(reportTitle, reportContent);
                        reportViewer.Owner = this;
                        reportViewer.ShowDialog();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Смена закрыта, но произошла ошибка при формировании Z-отчета: {ex.Message}", "Ошибка отчета", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    _saleManager.ClearCheck(); // Очищаем чек
                }
                // Если closedShift == null, значит, была ошибка, и сообщение об этом уже показано из ShiftManager.
            }
            else if (endCashDialog.DialogResult == true)
            {
                MessageBox.Show("Некорректная сумма.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void PrintXReportMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_shiftManager.CurrentOpenShift == null)
            {
                MessageBox.Show("Для печати X-отчета необходимо открыть смену.", "Смена закрыта", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                ShowTemporaryStatusMessage("Формирование X-отчета...", isInfo: true, durationSeconds: 10); 
                string reportTitle = $"X-Отчет (Смена №{_shiftManager.CurrentOpenShift.ShiftNumber})"; 
                string reportContent = await _reportService.GenerateXReportAsync(_shiftManager.CurrentOpenShift); 


                // 1. Автоматически сохраняем в текстовый лог-файл
                _fileReportLogger.AppendReportToFile(reportContent);

                // 2. Показываем отчет в новом окне вместо печати
                var reportViewer = new ReportViewerWindow(reportTitle, reportContent);
                reportViewer.Owner = this;
                reportViewer.ShowDialog();


                ClearTemporaryStatusMessage();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при формировании X-отчета: {ex.Message}", "Ошибка отчета", MessageBoxButton.OK, MessageBoxImage.Error);
                ClearTemporaryStatusMessage();
            }
        }

        private void CashInMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var cashInDialog = new InputDialog("Внесение наличных", "Введите сумму для внесения:");
            cashInDialog.Owner = this;
            if (cashInDialog.ShowDialog() == true && decimal.TryParse(cashInDialog.InputText, out decimal amount))
            {
                var reasonDialog = new InputDialog("Внесение наличных", "Введите причину (необязательно):");
                reasonDialog.Owner = this;
                reasonDialog.ShowDialog(); // Не важно, ОК или Отмена
                bool success = _shiftManager.PerformCashIn(CurrentUser, amount, reasonDialog.InputText);
                if (success) ShowTemporaryStatusMessage($"Внесено {amount:C}.");
            }
            else if (cashInDialog.DialogResult == true) { MessageBox.Show("Некорректная сумма.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning); }
            ItemInputTextBox.Focus();
        }

        private async void CashOutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var cashOutDialog = new InputDialog("Изъятие наличных", "Введите сумму для изъятия:");
            cashOutDialog.Owner = this;
            if (cashOutDialog.ShowDialog() == true && decimal.TryParse(cashOutDialog.InputText, out decimal amount))
            {
                var reasonDialog = new InputDialog("Изъятие наличных", "Введите причину (необязательно):");
                reasonDialog.Owner = this;
                reasonDialog.ShowDialog();

                bool success = await _shiftManager.PerformCashOut(CurrentUser, amount, reasonDialog.InputText);

                if (success) ShowTemporaryStatusMessage($"Изъято {amount:C}.");
            }
            else if (cashOutDialog.DialogResult == true)
            {
                MessageBox.Show("Некорректная сумма.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            ItemInputTextBox.Focus();
        }

        private void LockStationMenuItem_Click(object sender, RoutedEventArgs e) => LockScreen();

        private void LogoutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();
            this.Close();
        }

        // --- Горячие клавиши ---
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (this.IsActive && !MainMenuPopup.IsOpen && !_isLocked)
            {
                switch (e.Key)
                {
                    case Key.F2: if (QuantityButton.IsEnabled) ChangeSelectedItemQuantity(); e.Handled = true; break;
                    case Key.F4: if (ManualDiscountButton.IsEnabled) ManualDiscountButton_Click(null, null); e.Handled = true; break;
                    case Key.Delete: if (DeleteItemButton.IsEnabled) InitiateStorno(); e.Handled = true; break;
                    case Key.F5: if (PaymentButton.IsEnabled) PaymentButton_Click(null, null); e.Handled = true; break;
                    case Key.F6: if (ReturnModeButton.IsEnabled) ReturnModeButton_Click(null, null); e.Handled = true; break;
                    case Key.F12: MenuButton_Click(null, null); e.Handled = true; break;
                }
            }
            else if (e.Key == Key.Escape && MainMenuPopup.IsOpen) // Закрыть меню по Esc
            {
                MainMenuPopup.IsOpen = false;
                MenuButton.Focus();
                e.Handled = true;
            }
        }

        // --- Управление окном ---
        protected override void OnClosing(CancelEventArgs e)
        {
            _clockTimer?.Stop();
            _inactivityTimer?.Stop();
            // Отписываемся от событий
            if (_saleManager != null) _saleManager.PropertyChanged -= SaleManager_PropertyChanged;
            if (_shiftManager != null)
            {
                _shiftManager.ShiftOpened -= ShiftManager_ShiftStateChanged;
                _shiftManager.ShiftClosed -= ShiftManager_ShiftStateChanged;
            }
            base.OnClosing(e);
        }

        // Опционально: управление таймером при активации/деактивации окна
        protected override void OnDeactivated(EventArgs e) { base.OnDeactivated(e); /* _inactivityTimer?.Stop(); */ }
        protected override void OnActivated(EventArgs e) { base.OnActivated(e); /* if (!_isLocked && _shiftManager.CurrentOpenShift != null) ResetInactivityTimer(); */ }

        // --- Вспомогательные методы для статуса ---
        private async void ShowTemporaryStatusMessage(string message, bool isError = false, bool isInfo = false, int durationSeconds = 3)
        {
            var originalContent = $"Кассир: {CurrentUser.FullName}";
            var originalForeground = Brushes.Black;

            CashierInfoStatusText.Text = message;
            if (isError) { CashierInfoStatusText.Foreground = Brushes.Red; }
            else if (isInfo) { CashierInfoStatusText.Foreground = Brushes.Blue; }
            else { CashierInfoStatusText.Foreground = Brushes.Green; }

            try { await Task.Delay(TimeSpan.FromSeconds(durationSeconds)); }
            finally
            {
                // Восстанавливаем, только если сообщение не было изменено снова
                if (CashierInfoStatusText.Text == message)
                {
                    CashierInfoStatusText.Text = originalContent;
                    CashierInfoStatusText.Foreground = originalForeground;
                }
            }
        }
        private void ClearTemporaryStatusMessage()
        {
            string defaultText = $"Кассир: {CurrentUser.FullName}";
            if (CashierInfoStatusText.Text != defaultText)
            {
                CashierInfoStatusText.Text = defaultText;
                CashierInfoStatusText.Foreground = Brushes.Black;
            }
        }

        // --- Реализация INotifyPropertyChanged ---
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}