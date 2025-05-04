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


    // Модель для отображения в ListView (добавляем Product)
    public class CheckItemView : CheckItem, INotifyPropertyChanged
    {
        private Product _product;
        public Product Product
        {
            get => _product;
            set { _product = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProductName)); } // Уведомляем об изменении Product и ProductName
        }

        // --- Переопределяем свойства из CheckItem, чтобы вызывать OnPropertyChanged ---
        private decimal _quantity;
        public new decimal Quantity // Используем new для переопределения свойства базового класса
        {
            get => _quantity;
            set { _quantity = value; OnPropertyChanged(); OnPropertyChanged(nameof(CalculatedItemTotalAmount)); }  // Уведомляем об изменении Quantity и ItemTotalAmount
        }

        private decimal _priceAtSale;
        public new decimal PriceAtSale
        {
            get => _priceAtSale;
            set { _priceAtSale = value; OnPropertyChanged(); OnPropertyChanged(nameof(CalculatedItemTotalAmount)); }
        }

        private decimal _discountAmount;
        public new decimal DiscountAmount
        {
            get => _discountAmount;
            set { _discountAmount = value; OnPropertyChanged(); OnPropertyChanged(nameof(CalculatedItemTotalAmount)); }
        }

        // --- Конец переопределения свойств ---

        // Свойства только для отображения (зависят от других)
        public string ProductName => Product?.Name ?? "<Товар не найден>";
        // Это свойство теперь действительно ТОЛЬКО для чтения в UI,
        // оно будет обновляться автоматически при изменении Quantity, PriceAtSale, DiscountAmount
        public decimal CalculatedItemTotalAmount => Quantity * PriceAtSale - DiscountAmount;

        // --- Реализация INotifyPropertyChanged ---
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // --- Конструктор для копирования из CheckItem ---
        public CheckItemView(CheckItem baseItem, Product product)
        {
            // Копируем значения из базового CheckItem
            this.CheckItemId = baseItem.CheckItemId;
            this.CheckId = baseItem.CheckId;
            this.ProductId = baseItem.ProductId;
            // Важно: присваиваем значения приватным полям, чтобы не вызвать PropertyChanged рекурсивно
            this._quantity = baseItem.Quantity;
            this._priceAtSale = baseItem.PriceAtSale;
            this._discountAmount = baseItem.DiscountAmount;

            // Устанавливаем продукт
            this.Product = product; // Это вызовет OnPropertyChanged для Product и ProductName
        }
        // Добавим пустой конструктор (может быть нужен для некоторых операций WPF)
        public CheckItemView() { }
    }


    public partial class CashierWindow : Window
    {
        private readonly User CurrentUser;
        private Shift CurrentShift; // Текущая открытая смена

        private readonly ProductRepository _productRepository;
        private readonly StockItemRepository _stockItemRepository;
        private readonly CheckRepository _checkRepository;
        private readonly ShiftRepository _shiftRepository;
        private readonly CashDrawerOperationRepository _cashDrawerRepository;
        private readonly UserRepository _userRepository;
        private ContextMenu _originalCheckListViewContextMenu;

        // Используем ObservableCollection для автоматического обновления ListView
        private ObservableCollection<CheckItemView> CurrentCheckItems = new ObservableCollection<CheckItemView>();

        private decimal _subtotal = 0m;
        private decimal _totalDiscount = 0m;
        private decimal _totalAmount = 0m;

        private DispatcherTimer _clockTimer;
        private DispatcherTimer _inactivityTimer; // Таймер для автоблокировки
        private const int InactivityTimeoutMinutes = 15; // Время неактивности в минутах
        private bool _isLocked = false; // Флаг состояния блокировки

        public CashierWindow(User user)
        {
            InitializeComponent();
            CurrentUser = user;

            // Инициализация репозиториев
            _productRepository = new ProductRepository();
            _stockItemRepository = new StockItemRepository();
            _checkRepository = new CheckRepository();
            _shiftRepository = new ShiftRepository();
            _cashDrawerRepository = new CashDrawerOperationRepository();
            _userRepository = new UserRepository();

            // Привязка коллекции к ListView
            CheckListView.ItemsSource = CurrentCheckItems;

            // Настройка таймера неактивности
            InitializeInactivityTimer();

            // Подписка на события активности на уровне окна
            this.PreviewMouseMove += Window_ActivityDetected;
            this.PreviewKeyDown += Window_ActivityDetected;
            // Можно добавить PreviewMouseDown и т.д. по необходимости
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _originalCheckListViewContextMenu = CheckListView.ContextMenu; // <<--- СОХРАНЯЕМ ОРИГИНАЛ

            CheckOpenShift();
            UpdateCashierInfo();
            SetupClock(); // Запускаем часы
            ItemInputTextBox.Focus();

            // Запускаем таймер неактивности только если окно активно и не заблокировано
            if (!_isLocked)
            {
                ResetInactivityTimer(); // Запуск таймера при загрузке
            }
        }

        // --- Управление неактивностью и блокировкой ---

        private void InitializeInactivityTimer()
        {
            _inactivityTimer = new DispatcherTimer();
            _inactivityTimer.Interval = TimeSpan.FromMinutes(InactivityTimeoutMinutes);
            _inactivityTimer.Tick += InactivityTimer_Tick;
        }

        // Срабатывает при неактивности
        private void InactivityTimer_Tick(object sender, EventArgs e)
        {
            // Блокируем только если окно активно и еще не заблокировано
            if (this.IsActive && !_isLocked)
            {
                LockScreen("Экран заблокирован из-за неактивности.");
            }
        }

        // Сброс таймера неактивности при действии пользователя
        private void ResetInactivityTimer()
        {
            _inactivityTimer.Stop();
            _inactivityTimer.Start();
            // Debug.WriteLine("Inactivity timer reset."); // Для отладки
        }

        // Обработчик событий активности окна
        private void Window_ActivityDetected(object sender, InputEventArgs e)
        {
            // Сбрасываем таймер только если экран не заблокирован
            if (!_isLocked)
            {
                ResetInactivityTimer();
            }
        }

        // Метод для блокировки экрана
        private void LockScreen(string lockMessage = "Станция заблокирована.")
        {
            if (_isLocked) return; // Уже заблокировано

            _isLocked = true;
            _inactivityTimer.Stop(); // Останавливаем таймер неактивности

            DisableCheckoutControls();

            // Показываем оверлей с сообщением
            OverlayText.Text = $"{lockMessage}\nВведите пароль для разблокировки.";
            OverlayText.Foreground = Brushes.OrangeRed; // Цвет для блокировки
            OverlayBorder.Visibility = Visibility.Visible;

            // Отключаем основные контролы на всякий случай (хотя оверлей их перекрывает)
            // DisableCheckoutControls(); // Можно не вызывать, т.к. оверлей блокирует

            // Показываем окно входа МОДАЛЬНО
            // Передаем логин текущего пользователя для удобства
            var loginWindow = new LoginWindow(CurrentUser.Username, true); // Используем новый конструктор
            loginWindow.Owner = this; // Устанавливаем владельца

            // Цикл для повторного показа окна логина при неудаче
            bool unlocked = false;
            while (!unlocked && _isLocked) // Повторяем, пока не разблокировано и флаг блокировки стоит
            {
                if (loginWindow.ShowDialog() == true)
                {
                    // Проверяем, что вошел ТОТ ЖЕ пользователь
                    if (loginWindow.AuthenticatedUser != null && loginWindow.AuthenticatedUser.UserId == CurrentUser.UserId)
                    {
                        UnlockScreen();
                        unlocked = true; // Выходим из цикла
                    }
                    else
                    {
                        // Вошел другой пользователь или ошибка - остаемся заблокированными
                        MessageBox.Show("Для разблокировки необходимо войти под текущим пользователем.", "Ошибка разблокировки", MessageBoxButton.OK, MessageBoxImage.Warning);
                        // Создаем новое окно логина для следующей попытки
                        loginWindow = new LoginWindow(CurrentUser.Username, true);
                        loginWindow.Owner = this;
                    }
                }
                else
                {
                    // Пользователь нажал "Отмена" в окне логина - остаемся заблокированными
                    // Можно либо выйти из цикла и оставить заблокированным, либо показать снова.
                    // Пока оставляем заблокированным, выйти можно только через успешный логин.
                    // Если нужно дать возможность отменить блокировку по Esc - нужна другая логика.
                    // Выход из цикла, если окно логина закрыли крестиком или Esc
                    break;
                }
            }

            // Если после цикла так и не разблокировали (например, закрыли окно логина)
            if (!unlocked)
            {
                // Можно добавить доп. логику, например, полное закрытие кассы или ожидание.
                // Пока просто остаемся с видимым оверлеем.
                OverlayText.Text = $"{lockMessage}\nВход не выполнен. Станция остается заблокированной.";
            }
        }

        // Метод для разблокировки экрана
        private void UnlockScreen()
        {
            _isLocked = false;
            HideOverlay(); // Скрываем оверлей
            EnableCheckoutControls();
            ResetInactivityTimer(); // Перезапускаем таймер неактивности
            ItemInputTextBox.Focus(); // Возвращаем фокус
            ShowTemporaryStatusMessage("Станция разблокирована.", isInfo: true);
        }


        // --- Управление сменой и состоянием окна ---

        private void CheckOpenShift()
        {
            try
            {
                CurrentShift = _shiftRepository.GetCurrentOpenShift();
                if (CurrentShift == null)
                {
                    ShowOverlay("СМЕНА ЗАКРЫТА.\nНажмите F12 -> Открыть смену.");
                    DisableCheckoutControls();
                    UpdateMenuItemsState();
                }
                else
                {
                    HideOverlay();
                    EnableCheckoutControls();
                    UpdateShiftInfo();
                    UpdateMenuItemsState();
                }
            }
            catch (Exception ex)
            {
                ShowOverlay($"ОШИБКА ЗАГРУЗКИ СМЕНЫ:\n{ex.Message}");
                DisableCheckoutControls();
                MessageBox.Show($"Критическая ошибка при проверке смены: {ex.Message}", "Ошибка смены", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowOverlay(string message)
        {
            OverlayText.Text = message;
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
            ManualDiscountButton.IsEnabled = false; // И так выключена
            // PrintDocButton.IsEnabled = false; // Печать старых можно оставить?
            // LookupItemButton.IsEnabled = false; // Инфо можно оставить?
            CancelCheckButton.IsEnabled = false;
            CheckListView.ContextMenu = null; // Отключаем контекстное меню
        }

        private void EnableCheckoutControls()
        {
            ItemInputTextBox.IsEnabled = true;
            PaymentButton.IsEnabled = true;
            QuantityButton.IsEnabled = true;
            DeleteItemButton.IsEnabled = true;
            ReturnModeButton.IsEnabled = true;
            ManualDiscountButton.IsEnabled = true;
            // PrintDocButton.IsEnabled = true;
            // LookupItemButton.IsEnabled = true;
            CancelCheckButton.IsEnabled = true;
            // Восстанавливаем контекстное меню из сохраненного оригинала
            if (_originalCheckListViewContextMenu != null)
            {
                CheckListView.ContextMenu = _originalCheckListViewContextMenu; 
            }

            ItemInputTextBox.Focus();
        }


        private void UpdateCashierInfo()
        {
            CashierInfoStatusText.Text = $"Кассир: {CurrentUser.FullName}";
        }

        private void UpdateShiftInfo()
        {
            if (CurrentShift != null)
            {
                ShiftInfoStatusText.Text = $"Смена №: {CurrentShift.ShiftNumber} (от {CurrentShift.OpenTimestamp:dd.MM HH:mm})";
            }
            else
            {
                ShiftInfoStatusText.Text = "Смена: Закрыта";
            }
        }

        // --- Часы ---
        private void SetupClock()
        {
            _clockTimer = new DispatcherTimer();
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += ClockTimer_Tick;
            _clockTimer.Start();
            UpdateClock(); // Показать время сразу
        }

        private void ClockTimer_Tick(object sender, EventArgs e)
        {
            UpdateClock();
        }

        private void UpdateClock()
        {
            ClockTextBlock.Text = DateTime.Now.ToString("HH:mm");
        }


        // --- Логика Добавления/Обработки Товара ---

        private void ItemInputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(ItemInputTextBox.Text))
            {
                string codeOrBarcode = ItemInputTextBox.Text.Trim();
                ItemInputTextBox.Clear(); // Очищаем сразу
                ProcessItemInput(codeOrBarcode);
                e.Handled = true; // Поглощаем Enter
            }
        }

        private void ProcessItemInput(string codeOrBarcode)
        {
            if (CurrentShift == null)
            {
                ShowTemporaryStatusMessage("Ошибка: Смена не открыта!");
                return;
            }

            try
            {
                Product product = _productRepository.FindProductByCodeOrBarcode(codeOrBarcode);

                if (product == null)
                {
                    ShowTemporaryStatusMessage($"Товар с кодом/ШК '{codeOrBarcode}' не найден!");
                    LastItemInfoText.Text = "- Товар не найден -";
                    return;
                }

                // --- Проверка остатка (опционально, но рекомендуется) ---
                decimal currentStock = _stockItemRepository.GetStockQuantity(product.ProductId);
                if (currentStock <= 0) // Продаем по 1 шт по умолчанию
                {
                    ShowTemporaryStatusMessage($"Товар '{product.Name}' закончился на складе!", isError: true);
                    LastItemInfoText.Text = $"- Товар '{product.Name}' закончился -";
                    // Можно не добавлять товар или добавить с нулевым количеством и предупреждением
                    // return; // Пока блокируем добавление
                }

                // --- Добавление или обновление количества ---
                var existingItem = CurrentCheckItems.FirstOrDefault(item => item.ProductId == product.ProductId); // Ищем точно такой же товар (с такой же маркой, если есть)

                if (existingItem != null) // Увеличиваем кол-во только для немаркированных
                {
                    existingItem.Quantity += 1;
                }
                else // Добавляем новую позицию
                {
                    var newItem = new CheckItemView(
                        new CheckItem // Создаем "пустой" CheckItem с базовыми данными
                        {
                            ProductId = product.ProductId,
                            Quantity = 1,
                            PriceAtSale = product.Price,
                            DiscountAmount = 0
                        },
                        product // Передаем найденный продукт
                    );
                    CurrentCheckItems.Add(newItem);
                    // Прокрутить список к добавленному элементу
                    CheckListView.ScrollIntoView(newItem);
                    CheckListView.SelectedItem = newItem; // Выделить добавленнй
                }


                // Обновляем информацию о последнем товаре и итоги
                LastItemInfoText.Text = $"Добавлено: {product.Name}\nЦена: {product.Price:C}\nКод: {product.ProductCode}";
                UpdateTotals();
            }
            catch (Exception ex)
            {
                ShowTemporaryStatusMessage($"Ошибка при добавлении товара: {ex.Message}", isError: true);
                LastItemInfoText.Text = "- Ошибка добавления -";
                MessageBox.Show($"Произошла ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ItemInputTextBox.Focus(); // Возвращаем фокус на ввод
            }
        }

        // --- Обновление итогов ---
        private void UpdateTotals()
        {
            _subtotal = CurrentCheckItems.Sum(item => item.Quantity * item.PriceAtSale);
            _totalDiscount = CurrentCheckItems.Sum(item => item.DiscountAmount); // Суммируем скидки по позициям
            _totalAmount = _subtotal - _totalDiscount;

            // Форматируем и отображаем
            CultureInfo culture = CultureInfo.CurrentCulture; // Или CultureInfo.GetCultureInfo("ru-RU");
            SubtotalText.Text = _subtotal.ToString("C", culture);
            DiscountText.Text = _totalDiscount.ToString("C", culture);
            TotalAmountText.Text = _totalAmount.ToString("C", culture);

            // Кнопка оплаты активна только если есть товары и сумма > 0
            PaymentButton.IsEnabled = CurrentCheckItems.Any() && _totalAmount >= 0 && CurrentShift != null;
            CancelCheckButton.IsEnabled = CurrentCheckItems.Any();
        }

        // --- Очистка чека ---
        private void ClearCheck()
        {
            CurrentCheckItems.Clear(); // Очищаем коллекцию (UI обновится)
            _subtotal = 0m;
            _totalDiscount = 0m;
            _totalAmount = 0m;
            UpdateTotals(); // Обновляем UI и состояние кнопок
            LastItemInfoText.Text = "-";
            ItemInputTextBox.Clear();
            ItemInputTextBox.Focus();
            ShowTemporaryStatusMessage("Чек очищен.");
        }

        // --- Вспомогательное сообщение в статусной строке ---
        private async void ShowTemporaryStatusMessage(string message, bool isError = false, bool isInfo = false, int durationSeconds = 3)
        {
            var originalContent = CashierInfoStatusText.Text;
            var originalForeground = CashierInfoStatusText.Foreground; // Сохраняем исходный цвет

            CashierInfoStatusText.Text = message;
            // Устанавливаем цвет в зависимости от флагов
            if (isError)
            {
                CashierInfoStatusText.Foreground = Brushes.Red;
            }
            else if (isInfo) // Добавляем проверку на isInfo
            {
                CashierInfoStatusText.Foreground = Brushes.Blue; // Используем синий для информации
            }
            else // Обычное сообщение - зеленый
            {
                CashierInfoStatusText.Foreground = Brushes.Green;
            }


            await Task.Delay(TimeSpan.FromSeconds(durationSeconds));

            // Восстанавливаем, только если сообщение не изменилось
            if (CashierInfoStatusText.Text == message)
            {
                CashierInfoStatusText.Text = originalContent;
                CashierInfoStatusText.Foreground = originalForeground; // Восстанавливаем исходный цвет
            }
        }


        // --- Обработчики Кнопок и Меню ---

        private void PaymentButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CurrentCheckItems.Any())
            {
                ShowTemporaryStatusMessage("Нечего оплачивать.", isError: true);
                return;
            }
            if (CurrentShift == null)
            {
                ShowTemporaryStatusMessage("Ошибка: Смена не открыта!", isError: true);
                return;
            }

            // 1. Создаем и показываем диалог оплаты
            var paymentDialog = new PaymentDialog(_totalAmount); // Передаем сумму к оплате
            if (paymentDialog.ShowDialog() == true)
            {
                // 2. Оплата подтверждена, получаем детали
                string paymentType = paymentDialog.SelectedPaymentType;
                decimal cashPaid = paymentDialog.CashPaid;
                decimal cardPaid = paymentDialog.CardPaid;
                decimal change = paymentDialog.Change; // Сдача

                // 3. Формируем объект чека
                var checkToSave = new Check
                {
                    ShiftId = CurrentShift.ShiftId,
                    // CheckNumber нужно получить из репозитория ПЕРЕД вставкой
                    CheckNumber = _checkRepository.GetNextCheckNumber(CurrentShift.ShiftId), // Получаем следующий номер
                    Timestamp = DateTime.Now,
                    UserId = CurrentUser.UserId,
                    TotalAmount = _totalAmount,
                    PaymentType = paymentType,
                    CashPaid = cashPaid,
                    CardPaid = cardPaid,
                    DiscountAmount = _totalDiscount,
                    IsReturn = false, // Это продажа
                    OriginalCheckId = null,
                    Items = CurrentCheckItems.Select(civ => (CheckItem)civ).ToList() // Преобразуем CheckItemView в CheckItem для сохранения
                };

                try
                {
                    // 4. Сохраняем чек (репозиторий обработает транзакцию и остатки)
                    var savedCheck = _checkRepository.AddCheck(checkToSave);

                    // 5. "Печать" чека (пока просто сообщение)
                    string printMessage = $"Чек №{savedCheck.CheckNumber} сохранен.\n";
                    printMessage += $"Тип оплаты: {paymentType}\n";
                    if (paymentType == "Cash" || paymentType == "Mixed") printMessage += $"Получено наличными: {cashPaid:C}\n";
                    if (paymentType == "Card" || paymentType == "Mixed") printMessage += $"Оплачено картой: {cardPaid:C}\n";
                    if (change > 0) printMessage += $"Сдача: {change:C}\n";
                    printMessage += $"ИТОГО: {savedCheck.TotalAmount:C}";

                    PrinterService.Print($"Чек №{savedCheck.CheckNumber}", printMessage);

                    // 6. Очищаем текущий чек
                    ClearCheck();
                }
                catch (InvalidOperationException invEx) // Ошибка обновления остатков
                {
                    MessageBox.Show($"Не удалось сохранить чек:\n{invEx.Message}", "Ошибка остатков", MessageBoxButton.OK, MessageBoxImage.Warning);
                    // Чек не очищаем, чтобы можно было исправить
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Критическая ошибка при сохранении чека:\n{ex.Message}", "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
                    // Чек не очищаем
                }
            }
            else
            {
                // Оплата отменена
                ShowTemporaryStatusMessage("Оплата отменена.");
            }
        }

        private void QuantityButton_Click(object sender, RoutedEventArgs e)
        {
            ChangeSelectedItemQuantity();
        }

        private void ChangeQuantityMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ChangeSelectedItemQuantity();
        }

        // Общий метод для изменения кол-ва выбранного товара
        private void ChangeSelectedItemQuantity()
        {
            if (CheckListView.SelectedItem is CheckItemView selectedItem)
            {
                // Открываем диалог для ввода нового количества
                var quantityDialog = new InputDialog("Количество", $"Введите новое количество для '{selectedItem.ProductName}':", selectedItem.Quantity.ToString());

                if (quantityDialog.ShowDialog() == true && decimal.TryParse(quantityDialog.InputText, out decimal newQuantity) && newQuantity > 0)
                {
                    // TODO: Проверить остаток, если новое кол-во больше старого
                    // decimal stockNeeded = newQuantity - selectedItem.Quantity;
                    // ... проверка остатка ...

                    selectedItem.Quantity = newQuantity; // Просто меняем свойство
                    UpdateTotals(); // Пересчитать общие итоги нужно
                    ItemInputTextBox.Focus();
                }
                else if (quantityDialog.DialogResult == true) // Если ввели не число или <= 0
                {
                    MessageBox.Show("Некорректное количество.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                ShowTemporaryStatusMessage("Выберите товар в чеке для изменения количества.", isError: true);
            }
        }


        private void DeleteCheckItemButton_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedItem();
        }

        private void DeleteCheckItemMenuItem_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedItem();
        }

        // Общий метод удаления выбранной позиции
        private void DeleteSelectedItem()
        {
            if (CheckListView.SelectedItem is CheckItemView selectedItem)
            {

                CurrentCheckItems.Remove(selectedItem); // Удаляем из коллекции (UI обновится)
                UpdateTotals();
                LastItemInfoText.Text = $"- Удалено: {selectedItem.ProductName} -";
                ItemInputTextBox.Focus();
            }
            else
            {
                ShowTemporaryStatusMessage("Выберите товар в чеке для удаления.", isError: true);
            }
        }

        private void ReturnModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentShift == null) { ShowTemporaryStatusMessage("Смена не открыта!", true); return; }
            // Открываем окно возврата
            var returnWindow = new ReturnWindow(CurrentUser, CurrentShift); // Передаем пользователя и смену
            returnWindow.Owner = this; // Делаем это окно владельцем
            returnWindow.ShowDialog(); // Показываем как диалог
                                       // Логика возврата полностью в ReturnWindow
            ItemInputTextBox.Focus();
        }

        private void ManualDiscountButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CurrentCheckItems.Any()) return; // Нельзя применить к пустому чеку

            decimal currentTotal = CurrentCheckItems.Sum(i => i.Quantity * i.PriceAtSale); // Берем сумму до скидок

            var discountDialog = new DiscountDialog(currentTotal);
            if (discountDialog.ShowDialog() == true)
            {
                // Скидка подтверждена, применяем ее ко всем позициям
                decimal appliedAmount = DiscountCalculator.ApplyManualCheckDiscount(
                    CurrentCheckItems.ToList<CheckItem>(), // Передаем базовые CheckItem
                    discountDialog.DiscountValue,
                    discountDialog.IsPercentage);

                // Обновляем UI ListView (если нужно "перерисовать" элементы)
                var tempItems = CurrentCheckItems.ToList();
                CurrentCheckItems.Clear();
                tempItems.ForEach(CurrentCheckItems.Add);

                UpdateTotals(); // Пересчитываем общие итоги
                ShowTemporaryStatusMessage($"Применена ручная скидка: {appliedAmount:C}", isInfo: true);
            }
        }

        private void PrintDocButton_Click(object sender, RoutedEventArgs e)
        {
            // Открываем окно для печати документов (копий чеков и т.д.)
            var printDocsWindow = new PrintDocumentsWindow();
            printDocsWindow.Owner = this;
            printDocsWindow.ShowDialog();
            ItemInputTextBox.Focus();
        }

        private void LookupItemButton_Click(object sender, RoutedEventArgs e)
        {
            // Открываем окно информации о товаре
            var itemInfoWindow = new ItemInfoViewWindow();
            itemInfoWindow.Owner = this;
            itemInfoWindow.ShowDialog();
            ItemInputTextBox.Focus();
        }

        private void CancelCheckButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentCheckItems.Any())
            {
                var result = MessageBox.Show("Отменить текущий чек? Все позиции будут удалены.", "Отмена чека", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    ClearCheck();
                }
            }
        }

        // --- Обработчики для MenuItem в Popup для исправления подсветки ---

        // --- Новый метод для обновления состояния пунктов меню ---
        private void UpdateMenuItemsState()
        {
            bool isShiftOpen = CurrentShift != null && !CurrentShift.IsClosed;

            // Используем имена ListBoxItem из XAML
            OpenShiftItem.IsEnabled = !isShiftOpen; // Открыть можно только если смена закрыта
            CloseShiftItem.IsEnabled = isShiftOpen; // Закрыть можно только если открыта
            CashInItem.IsEnabled = isShiftOpen;     // Внесение/Изъятие только при открытой смене
            CashOutItem.IsEnabled = isShiftOpen;
            LockStationItem.IsEnabled = !_isLocked; // Блокировать можно если не заблокировано
            LogoutItem.IsEnabled = true; // Выход доступен всегда? Или тоже блокировать при открытой смене? Решите сами.
        }

        // Обработчик открытия Popup
        private void MainMenuPopup_Opened(object sender, EventArgs e)
        {
            UpdateMenuItemsState(); // Обновляем состояние перед показом

            // Устанавливаем фокус на ListBox, когда Popup открылся
            MenuListBox.Focus();
            // Выбираем первый ДОСТУПНЫЙ элемент
            ListBoxItem firstEnabledItem = MenuListBox.Items.OfType<ListBoxItem>().FirstOrDefault(item => item.IsEnabled);
            if (firstEnabledItem != null)
            {
                MenuListBox.SelectedItem = firstEnabledItem;
                // firstEnabledItem.Focus(); // Фокус на сам элемент может быть лучше
            }
            else
            {
                MenuListBox.SelectedIndex = -1; // Снимаем выбор, если ничего не доступно
            }
        }

        // Обработка нажатия Enter или двойного клика на элементе ListBox
        private void ExecuteSelectedMenuItem()
        {
            if (MenuListBox.SelectedItem is ListBoxItem selectedItem)
            {
                // Закрываем Popup перед выполнением действия
                MainMenuPopup.IsOpen = false;

                // Определяем, какой пункт был выбран, по его имени (x:Name)
                switch (selectedItem.Name)
                {
                    case "OpenShiftItem":
                        OpenShiftMenuItem_Click(selectedItem, null); // Вызываем старый обработчик
                        break;
                    case "CloseShiftItem":
                        CloseShiftMenuItem_Click(selectedItem, null);
                        break;
                    case "CashInItem":
                        CashInMenuItem_Click(selectedItem, null);
                        break;
                    case "CashOutItem":
                        CashOutMenuItem_Click(selectedItem, null);
                        break;
                    case "LockStationItem":
                        LockStationMenuItem_Click(selectedItem, null);
                        break;
                    case "LogoutItem":
                        LogoutMenuItem_Click(selectedItem, null);
                        break;
                }
            }
        }

        private void MenuListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Space) // Enter или Пробел для выбора
            {
                ExecuteSelectedMenuItem();
                e.Handled = true; // Поглощаем событие
            }
            else if (e.Key == Key.Escape) // Escape для закрытия Popup
            {
                MainMenuPopup.IsOpen = false;
                MenuButton.Focus(); // Возвращаем фокус на кнопку
                e.Handled = true;
            }
        }

        private void MenuListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Проверяем, что двойной клик был именно на элементе списка
            if (e.Source is FrameworkElement element &&
                (element.DataContext is ListBoxItem || // Клик на самом элементе
                element.TemplatedParent is ListBoxItem)) // Клик на элементе внутри шаблона
            {
                ExecuteSelectedMenuItem();
                e.Handled = true;
            }
        }




        // --- Меню (F12) ---
        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            MainMenuPopup.IsOpen = !MainMenuPopup.IsOpen;
            // Фокус будет установлен в обработчике MainMenuPopup_Opened
        }

        private void OpenShiftMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainMenuPopup.IsOpen = false; // Закрываем меню
            if (CurrentShift != null)
            {
                MessageBox.Show($"Смена №{CurrentShift.ShiftNumber} уже открыта.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Диалог для ввода начальной суммы
            var startCashDialog = new InputDialog("Открыть смену", "Введите сумму наличных в кассе на начало смены:", "0");
            if (startCashDialog.ShowDialog() == true && decimal.TryParse(startCashDialog.InputText, out decimal startCash) && startCash >= 0)
            {
                try
                {
                    var newShift = _shiftRepository.OpenShift(CurrentUser.UserId, startCash);
                    CurrentShift = newShift; // Обновляем текущую смену
                    UpdateShiftInfo();
                    HideOverlay();
                    EnableCheckoutControls();
                    UpdateMenuItemsState();
                    ShowTemporaryStatusMessage($"Смена №{CurrentShift.ShiftNumber} открыта.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось открыть смену: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else if (startCashDialog.DialogResult == true)
            {
                MessageBox.Show("Некорректная сумма.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CloseShiftMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainMenuPopup.IsOpen = false;
            if (CurrentShift == null)
            {
                MessageBox.Show("Нет открытой смены для закрытия.", "Информация", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Диалог для ввода фактической суммы в кассе
            var endCashDialog = new InputDialog("Закрыть смену", $"Смена №{CurrentShift.ShiftNumber}\nВведите фактическую сумму наличных в кассе:", "0");
            if (endCashDialog.ShowDialog() == true && decimal.TryParse(endCashDialog.InputText, out decimal endCashActual) && endCashActual >= 0)
            {
                try
                {
                    bool closed = _shiftRepository.CloseShift(CurrentShift.ShiftId, CurrentUser.UserId, endCashActual);
                    if (closed)
                    {
                        // Получаем данные закрытой смены для Z-отчета (имитация)
                        var closedShift = _shiftRepository.GetShiftById(CurrentShift.ShiftId);
                        string zReport = $"--- Z-Отчет (Имитация) ---\n";
                        zReport += $"Смена №: {closedShift.ShiftNumber}\n";
                        zReport += $"Открыта: {closedShift.OpenTimestamp:G}\n";
                        zReport += $"Закрыта: {closedShift.CloseTimestamp:G}\n";
                        zReport += $"Кассир откр.: {_userRepository.GetUserById(closedShift.OpeningUserId)?.FullName ?? "-"}\n";
                        zReport += $"Кассир закр.: {_userRepository.GetUserById(closedShift.ClosingUserId ?? -1)?.FullName ?? "-"}\n";
                        zReport += $"Начальный остаток: {closedShift.StartCash:C}\n";
                        zReport += $"Продажи (Итог): {closedShift.TotalSales ?? 0:C}\n";
                        zReport += $"Возвраты (Итог): {closedShift.TotalReturns ?? 0:C}\n";
                        zReport += $"Продажи нал.: {closedShift.CashSales ?? 0:C}\n";
                        zReport += $"Продажи карта: {closedShift.CardSales ?? 0:C}\n";
                        zReport += $"Внесения: {closedShift.CashAdded ?? 0:C}\n";
                        zReport += $"Изъятия: {closedShift.CashRemoved ?? 0:C}\n";
                        zReport += $"Остаток теор.: {closedShift.EndCashTheoretic ?? 0:C}\n";
                        zReport += $"Остаток факт.: {closedShift.EndCashActual ?? 0:C}\n";
                        zReport += $"Расхождение: {closedShift.Difference ?? 0:C}\n";

                        PrinterService.Print($"Z-Отчет (Смена №{closedShift.ShiftNumber})", zReport);

                        CurrentShift = null; // Сбрасываем текущую смену
                        UpdateShiftInfo();
                        ShowOverlay("СМЕНА ЗАКРЫТА");
                        DisableCheckoutControls();
                        UpdateMenuItemsState();
                        ClearCheck(); // Очищаем чек на всякий случай
                    }
                    else
                    {
                        MessageBox.Show("Не удалось закрыть смену (возможно, она уже была закрыта).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при закрытии смены: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else if (endCashDialog.DialogResult == true)
            {
                MessageBox.Show("Некорректная сумма.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CashInMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainMenuPopup.IsOpen = false;
            if (CurrentShift == null) { ShowTemporaryStatusMessage("Смена не открыта!", true); return; }

            var cashInDialog = new InputDialog("Внесение наличных", "Введите сумму для внесения:");
            if (cashInDialog.ShowDialog() == true && decimal.TryParse(cashInDialog.InputText, out decimal amount) && amount > 0)
            {
                var reasonDialog = new InputDialog("Внесение наличных", "Введите причину (необязательно):");
                reasonDialog.ShowDialog(); // Не важно, нажал ОК или Отмена

                try
                {
                    var op = new CashDrawerOperation
                    {
                        ShiftId = CurrentShift.ShiftId,
                        UserId = CurrentUser.UserId,
                        OperationType = "CashIn",
                        Amount = amount,
                        Reason = reasonDialog.InputText
                    };
                    _cashDrawerRepository.AddOperation(op);
                    ShowTemporaryStatusMessage($"Внесено {amount:C}.");
                }
                catch (Exception ex) { /* обработка ошибки */ }
            }
            else if (cashInDialog.DialogResult == true) { /* Некорректная сумма */ }
            ItemInputTextBox.Focus();
        }

        private void CashOutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainMenuPopup.IsOpen = false;
            if (CurrentShift == null) { ShowTemporaryStatusMessage("Смена не открыта!", true); return; }

            var cashOutDialog = new InputDialog("Изъятие наличных", "Введите сумму для изъятия:");
            if (cashOutDialog.ShowDialog() == true && decimal.TryParse(cashOutDialog.InputText, out decimal amount) && amount > 0)
            {
                // TODO: Проверить, достаточно ли наличных в кассе для изъятия (потребует расчета текущего нал. остатка)

                var reasonDialog = new InputDialog("Изъятие наличных", "Введите причину (необязательно):");
                reasonDialog.ShowDialog();

                try
                {
                    var op = new CashDrawerOperation
                    {
                        ShiftId = CurrentShift.ShiftId,
                        UserId = CurrentUser.UserId,
                        OperationType = "CashOut",
                        Amount = amount,
                        Reason = reasonDialog.InputText
                    };
                    _cashDrawerRepository.AddOperation(op);
                    ShowTemporaryStatusMessage($"Изъято {amount:C}.");
                }
                catch (Exception ex) { /* обработка ошибки */ }
            }
            else if (cashOutDialog.DialogResult == true) { /* Некорректная сумма */ }
            ItemInputTextBox.Focus();
        }

        private void LockStationMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainMenuPopup.IsOpen = false;
            LockScreen(); // Вызываем наш новый метод блокировки
        }

         protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
         {
             base.OnClosing(e);
             _clockTimer?.Stop();
             _inactivityTimer?.Stop();
         }

        // Важно: Останавливать таймер неактивности при потере фокуса окном? (Опционально)
        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            // _inactivityTimer?.Stop(); // Останавливать, если окно неактивно?
        }
        // Важно: Запускать таймер неактивности при активации окна? (Опционально)
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            // if (!_isLocked) ResetInactivityTimer(); // Запускать, если активно и не заблокировано?
        }


        private void LogoutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Закрыть это окно и показать MainWindow (выбор режима)
            var mainWindow = new MainWindow();
            mainWindow.Show();
            this.Close(); // Закрываем окно кассира
        }


        // --- Обработка горячих клавиш ---
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Проверяем, активно ли основное окно (не диалог)
            if (this.IsActive && !MainMenuPopup.IsOpen)
            {
                switch (e.Key)
                {
                    case Key.F2: // Кол-во
                        if (QuantityButton.IsEnabled) ChangeSelectedItemQuantity();
                        e.Handled = true;
                        break;
                    case Key.Delete: // Удалить позицию
                        if (DeleteItemButton.IsEnabled) DeleteSelectedItem();
                        e.Handled = true;
                        break;
                    case Key.F5: // Оплата
                        if (PaymentButton.IsEnabled) PaymentButton_Click(PaymentButton, new RoutedEventArgs());
                        e.Handled = true;
                        break;
                    case Key.F6: // Возврат
                        if (ReturnModeButton.IsEnabled) ReturnModeButton_Click(ReturnModeButton, new RoutedEventArgs());
                        e.Handled = true;
                        break;
                    case Key.F12: // Меню
                        MenuButton_Click(MenuButton, new RoutedEventArgs());
                        e.Handled = true;
                        break;
                        // Добавить другие клавиши по необходимости (F3, F4 и т.д.)
                }
            }
        }
    }
}