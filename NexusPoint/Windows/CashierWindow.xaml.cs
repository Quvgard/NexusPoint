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
        private bool _isManualDiscountApplied = false;

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
            if (_isLocked) return;

            _isLocked = true;
            _inactivityTimer.Stop();

            DisableCheckoutControls(); // <<--- Убедимся, что вызвано здесь

            // Показываем оверлей с сообщением БЛОКИРОВКИ
            ShowOverlay($"{lockMessage}\nВведите пароль для разблокировки.", Brushes.OrangeRed);


            // Показываем окно входа МОДАЛЬНО
            var loginWindow = new LoginWindow(CurrentUser.Username, true);
            loginWindow.Owner = this;

            bool unlocked = false;
            while (!unlocked && _isLocked) // Повторяем, пока не разблокировано и флаг блокировки стоит
            {
                // Проверяем состояние смены ПОСЛЕ попытки разблокировки
                bool isShiftStillOpen = CurrentShift != null && !CurrentShift.IsClosed;
                if (isShiftStillOpen) // Если смена открыта, но не разблокировали
                {
                    // Оставляем сообщение о блокировке (красное)
                    ShowOverlay($"{lockMessage}\nВход не выполнен. Станция остается заблокированной.", Brushes.OrangeRed);
                }

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
            if (!unlocked && _isLocked) // Добавил проверку _isLocked, чтобы не переписывать сообщение, если UnlockScreen уже сработал и сменил его
            {
                // Если смена все еще закрыта, UnlockScreen уже поставил правильное сообщение
                // Если смена открыта, но вышли из цикла (нажали отмену в login) - ставим сообщение о блокировке
                if (CurrentShift != null && !CurrentShift.IsClosed)
                {
                    OverlayText.Text = $"{lockMessage}\nВход не выполнен. Станция остается заблокированной.";
                }
                // Иначе сообщение о закрытой смене уже установлено в UnlockScreen или CheckOpenShift
            }
        }

        // Метод для разблокировки экрана
        private void UnlockScreen()
        {
            _isLocked = false;
            // --- ПОВТОРНО ПРОВЕРЯЕМ СОСТОЯНИЕ СМЕНЫ ---
            // Эта логика похожа на CheckOpenShift, но без обработки ошибок БД
            bool isShiftOpen = CurrentShift != null && !CurrentShift.IsClosed;

            if (isShiftOpen)
            {
                // Если смена открыта - разблокируем полностью
                HideOverlay();
                EnableCheckoutControls();
                ResetInactivityTimer();
                ItemInputTextBox.Focus();
                ShowTemporaryStatusMessage("Станция разблокирована.", isInfo: true);
            }
            else
            {
                // Если смена все еще ЗАКРЫТА - оставляем оверлей с сообщением о смене
                ShowOverlay("СМЕНА ЗАКРЫТА.\nНажмите F12 -> Открыть смену.", Brushes.White); // Используем белый цвет
                // Контролы уже должны быть выключены через DisableCheckoutControls,
                // который был вызван в LockScreen или при первоначальной проверке CheckOpenShift.
                // На всякий случай, можно вызвать еще раз:
                DisableCheckoutControls();
                // Таймер неактивности не запускаем, пока смена закрыта
                _inactivityTimer.Stop();
                // Фокус можно оставить на кнопке меню или где-то еще
                MenuButton.Focus(); // Например, на кнопке меню
                ShowTemporaryStatusMessage("Станция разблокирована, но смена закрыта.", isInfo: true);
            }
        }


        // --- Управление сменой и состоянием окна ---

        private void CheckOpenShift()
        {
            try
            {
                CurrentShift = _shiftRepository.GetCurrentOpenShift();
                UpdateMenuItemsState(); // Обновляем меню сразу

                if (CurrentShift == null)
                {
                    ShowOverlay("СМЕНА ЗАКРЫТА.\nНажмите F12 -> Открыть смену.", Brushes.White);
                    DisableCheckoutControls(); // Используем общий метод
                }
                else
                {
                    HideOverlay();
                    EnableCheckoutControls(); // Используем общий метод
                    UpdateShiftInfo();
                    // Таймер неактивности будет запущен в Window_Loaded или OnActivated
                }
            }
            catch (Exception ex)
            {
                ShowOverlay($"ОШИБКА ЗАГРУЗКИ СМЕНЫ:\n{ex.Message}", Brushes.OrangeRed);
                DisableCheckoutControls(); // Блокируем контролы при ошибке
                MessageBox.Show($"Критическая ошибка при проверке смены: {ex.Message}", "Ошибка смены", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowOverlay(string message, Brush foregroundBrush = null) // Добавляем параметр цвета
        {
            OverlayText.Text = message;
            // Устанавливаем цвет или используем белый по умолчанию
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
            // Устанавливаем стандартный текст и цвет
            CashierInfoStatusText.Text = $"Кассир: {CurrentUser.FullName}";
            CashierInfoStatusText.Foreground = Brushes.Black;
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
                _isManualDiscountApplied = false;
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
            _isManualDiscountApplied = false;
            UpdateTotals(); // Обновляем UI и состояние кнопок
            LastItemInfoText.Text = "-";
            ItemInputTextBox.Clear();
            ItemInputTextBox.Focus();
            ShowTemporaryStatusMessage("Чек очищен.");
        }

        // --- Вспомогательное сообщение в статусной строке ---
        private async void ShowTemporaryStatusMessage(string message, bool isError = false, bool isInfo = false, int durationSeconds = 3)
        {
            // --- ЗАПОМИНАЕМ ИСХОДНОЕ СОСТОЯНИЕ ---
            var originalContent = $"Кассир: {CurrentUser.FullName}"; // <<--- Всегда восстанавливаем имя кассира
            var originalForeground = Brushes.Black; // <<--- Стандартный цвет текста StatusBarItem (или возьмите из ресурса)

            // --- УСТАНАВЛИВАЕМ ВРЕМЕННОЕ СООБЩЕНИЕ ---
            CashierInfoStatusText.Text = message; // Устанавливаем новое сообщение
            // Устанавливаем цвет в зависимости от флагов
            if (isError) { CashierInfoStatusText.Foreground = Brushes.Red; }
            else if (isInfo) { CashierInfoStatusText.Foreground = Brushes.Blue; }
            else { CashierInfoStatusText.Foreground = Brushes.Green; }

            // --- ОЖИДАНИЕ ---
            // Используем try-finally, чтобы гарантировать восстановление, даже если задача будет отменена
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(durationSeconds));
            }
            finally // Выполняется всегда после Delay
            {
                // --- ВОССТАНАВЛИВАЕМ ИСХОДНОЕ СОСТОЯНИЕ ---
                // Проверяем, не изменилось ли сообщение ЗА ВРЕМЯ ожидания (например, другим вызовом этого же метода)
                // Если изменилось - НЕ восстанавливаем, чтобы не затереть более новое сообщение.
                // НО: Если это было сообщение об ошибке, а теперь должно быть имя кассира, то нужно восстановить.
                // Упрощенная логика: Всегда восстанавливаем имя кассира после задержки.
                CashierInfoStatusText.Text = originalContent;
                CashierInfoStatusText.Foreground = originalForeground;
            }
        }


        // --- Обработчики Кнопок и Меню ---

        private async void PaymentButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. --- Базовые проверки ---
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
            if (_isLocked) // Нельзя оплачивать, если заблокировано
            {
                ShowTemporaryStatusMessage("Станция заблокирована. Разблокируйте для оплаты.", isError: true);
                return;
            }

            // 2. --- Применение автоматических скидок и подарков (если не было ручной) ---
            DiscountCalculationResult discountResult = null;
            // Инициализируем список для сохранения текущим состоянием UI
            List<CheckItem> itemsToSave = CurrentCheckItems.Select(civ => new CheckItem
            {
                ProductId = civ.ProductId,
                Quantity = civ.Quantity,
                PriceAtSale = civ.PriceAtSale,
                DiscountAmount = civ.DiscountAmount,
                AppliedDiscountId = civ.AppliedDiscountId,
                ItemTotalAmount = civ.CalculatedItemTotalAmount
            }).ToList();

            if (!_isManualDiscountApplied) // <<--- ПРОВЕРЯЕМ ФЛАГ РУЧНОЙ СКИДКИ
            {
                Console.WriteLine("Applying automatic discounts...");
                try
                {
                    ShowTemporaryStatusMessage("Применение скидок...", isInfo: true, durationSeconds: 10);

                    // Создаем список базовых CheckItem из текущих CheckItemView для передачи калькулятору
                    List<CheckItem> originalItemsForCalc = CurrentCheckItems
                        .Select(civ => new CheckItem
                        {
                            ProductId = civ.ProductId,
                            Quantity = civ.Quantity,
                            PriceAtSale = civ.PriceAtSale,
                            DiscountAmount = 0, // Сбрасываем скидку перед авто-расчетом
                            AppliedDiscountId = null,
                            ItemTotalAmount = civ.Quantity * civ.PriceAtSale // Начальная сумма
                        }).ToList();

                    // Вызываем основной метод калькулятора в фоновом потоке
                    discountResult = await Task.Run(() => DiscountCalculator.ApplyAllAutoDiscounts(originalItemsForCalc));

                    // --- Обработка результата расчета ---
                    CurrentCheckItems.Clear(); // Очищаем UI перед заполнением новыми данными
                    itemsToSave.Clear();      // Очищаем список для сохранения
                    decimal newTotalDiscount = 0m; // Переменная для суммы скидок из расчета

                    var productCache = new Dictionary<int, Product>(); // Кэш для названий товаров

                    // Добавляем основные позиции с рассчитанными скидками
                    foreach (var discountedItem in discountResult.DiscountedItems)
                    {
                        // Загрузка Product (оптимизированно)
                        if (!productCache.TryGetValue(discountedItem.ProductId, out Product product))
                        {
                            product = await Task.Run(() => _productRepository.FindProductById(discountedItem.ProductId));
                            productCache[discountedItem.ProductId] = product;
                        }

                        CurrentCheckItems.Add(new CheckItemView(discountedItem, product)); // Добавляем в UI
                        itemsToSave.Add(discountedItem); // Добавляем в список для сохранения
                        newTotalDiscount += discountedItem.DiscountAmount;
                    }

                    // Добавляем подарки (если есть и доступны)
                    if (discountResult.GiftsToAdd.Any())
                    {
                        bool giftsAvailable = true;
                        var giftsToAddFiltered = new List<CheckItem>(); // Подарки, прошедшие проверку
                        foreach (var gift in discountResult.GiftsToAdd)
                        {
                            decimal giftStock = await Task.Run(() => _stockItemRepository.GetStockQuantity(gift.ProductId));
                            if (giftStock < gift.Quantity)
                            {
                                giftsAvailable = false;
                                Product giftProdInfo = productCache.ContainsKey(gift.ProductId) ? productCache[gift.ProductId] : await Task.Run(() => _productRepository.FindProductById(gift.ProductId));
                                ShowTemporaryStatusMessage($"Недостаточно подарка '{giftProdInfo?.Name ?? "ID:" + gift.ProductId}' (ост: {giftStock}). Акция не применена.", isError: true);
                                // Пропускаем этот подарок
                            }
                            else
                            {
                                giftsToAddFiltered.Add(gift); // Добавляем подарок, если он есть
                            }
                        }


                        if (giftsToAddFiltered.Any())
                        {
                            // Догружаем инфо о товарах-подарках, если нужно
                            var giftProductIds = giftsToAddFiltered.Select(g => g.ProductId).Distinct().ToList();
                            foreach (var pid in giftProductIds)
                            {
                                if (!productCache.ContainsKey(pid))
                                {
                                    productCache[pid] = await Task.Run(() => _productRepository.FindProductById(pid));
                                }
                            }

                            // Добавляем доступные подарки в UI и список для сохранения
                            foreach (var giftItem in giftsToAddFiltered)
                            {
                                Product giftProduct = productCache.TryGetValue(giftItem.ProductId, out var p) ? p : null;
                                CurrentCheckItems.Add(new CheckItemView(giftItem, giftProduct) { PriceAtSale = 0 });
                                itemsToSave.Add(giftItem);
                                ShowTemporaryStatusMessage($"Добавлен подарок: {giftProduct?.Name ?? "ID:" + giftItem.ProductId} x {giftItem.Quantity}", isInfo: true);
                            }
                        }
                    }

                    // Применяем скидку на чек (если она есть)
                    if (discountResult.AppliedCheckDiscount != null)
                    {
                        decimal checkDiscountAmount = 0m;
                        decimal currentTotalBeforeCheckDiscount = itemsToSave.Where(i => i.PriceAtSale > 0)
                                                                          .Sum(i => i.ItemTotalAmount); // Сумма НЕПОДАРКОВ до скидки на чек

                        if (discountResult.AppliedCheckDiscount.Value.HasValue && currentTotalBeforeCheckDiscount > 0)
                        {
                            if (discountResult.AppliedCheckDiscount.IsCheckDiscountPercentage)
                            {
                                checkDiscountAmount = currentTotalBeforeCheckDiscount * (Math.Min(100, discountResult.AppliedCheckDiscount.Value.Value) / 100m);
                            }
                            else
                            {
                                checkDiscountAmount = Math.Min(currentTotalBeforeCheckDiscount, discountResult.AppliedCheckDiscount.Value.Value);
                            }
                            checkDiscountAmount = Math.Round(checkDiscountAmount, 2);

                            // Распределяем скидку
                            if (checkDiscountAmount > 0)
                            {
                                decimal checkTotalForDistribution = itemsToSave.Where(i => i.PriceAtSale > 0).Sum(i => i.ItemTotalAmount);
                                if (checkTotalForDistribution > 0)
                                {
                                    decimal distributedSum = 0m;
                                    var itemsForCheckDiscount = itemsToSave.Where(i => i.PriceAtSale > 0).ToList();

                                    for (int i = 0; i < itemsForCheckDiscount.Count; i++)
                                    {
                                        var item = itemsForCheckDiscount[i];
                                        decimal itemTotalBeforeCheckDiscount = item.ItemTotalAmount;
                                        if (itemTotalBeforeCheckDiscount <= 0) continue;

                                        decimal itemShare = itemTotalBeforeCheckDiscount / checkTotalForDistribution;
                                        decimal discountPortion = (i == itemsForCheckDiscount.Count - 1)
                                                                 ? checkDiscountAmount - distributedSum
                                                                 : Math.Round(checkDiscountAmount * itemShare, 2);

                                        decimal discountToAdd = Math.Max(0, Math.Min(itemTotalBeforeCheckDiscount, discountPortion));

                                        item.DiscountAmount += discountToAdd;
                                        item.ItemTotalAmount -= discountToAdd; // Корректируем итоговую сумму
                                        item.AppliedDiscountId = discountResult.AppliedCheckDiscount.DiscountId;
                                        distributedSum += discountToAdd;

                                        // Обновляем UI
                                        var viewItem = CurrentCheckItems.FirstOrDefault(v => v.ProductId == item.ProductId && v.PriceAtSale > 0);
                                        if (viewItem != null)
                                        {
                                            viewItem.DiscountAmount = item.DiscountAmount;
                                            viewItem.AppliedDiscountId = item.AppliedDiscountId;
                                        }
                                    }
                                    newTotalDiscount += distributedSum;
                                }
                                ShowTemporaryStatusMessage($"Применена скидка на чек: {discountResult.AppliedCheckDiscount.Name}", isInfo: true);
                            }
                        }
                    }

                    UpdateTotals(); // Финальное обновление итогов на экране
                    ClearTemporaryStatusMessage(); // Убираем сообщение о применении скидок
                    await Task.Delay(500); // Небольшая пауза перед диалогом оплаты

                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка применения скидок:\n{ex.Message}", "Ошибка скидок", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ShowError("Ошибка применения скидок"); // Показываем ошибку
                                                           // Используем исходные данные без авто-скидок для оплаты
                    itemsToSave = CurrentCheckItems.Select(civ => (CheckItem)civ).ToList();
                    UpdateTotals(); // Обновляем итоги по данным из UI
                }
            }
            else // Ручная скидка была применена
            {
                // itemsToSave уже содержит результат ручной скидки
                ShowTemporaryStatusMessage("Применена ручная скидка, авто-скидки пропущены.", isInfo: true);
                await Task.Delay(500); // Пауза
            }
            // --- КОНЕЦ БЛОКА СКИДОК ---


            // Пересчитываем финальные итоги по списку itemsToSave на всякий случай
            _totalDiscount = Math.Round(itemsToSave.Sum(i => i.DiscountAmount), 2);
            _totalAmount = Math.Round(itemsToSave.Sum(i => i.ItemTotalAmount), 2);
            UpdateTotals(); // Обновляем UI итогов


            // 3. --- Проверка нулевой суммы ---
            if (_totalAmount <= 0 && itemsToSave.Any(i => i.PriceAtSale > 0)) // Если есть не только бесплатные подарки
            {
                MessageBoxResult freeResult = MessageBox.Show($"Итоговая сумма чека {(_totalAmount < 0 ? "отрицательная" : "равна нулю")} из-за скидок. Завершить продажу бесплатно?",
                                    "Нулевая сумма", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (freeResult == MessageBoxResult.No)
                {
                    ClearCheck(); // Отмена - очищаем чек
                    return;
                }
                // Если Да - сохраняем чек как оплаченный Наличными 0
                await SaveCheck(itemsToSave, "Cash", 0, 0); // Вызываем асинхронно
                return; // Завершаем обработку клика
            }
            else if (!itemsToSave.Any(i => i.PriceAtSale > 0)) // Если в чеке только подарки (или пустой)
            {
                MessageBox.Show("В чеке нет позиций для оплаты.", "Пустой чек", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearCheck();
                return;
            }


            // 4. --- Показ диалога оплаты ---
            var paymentDialog = new PaymentDialog(_totalAmount);
            paymentDialog.Owner = this; // Устанавливаем владельца
            if (paymentDialog.ShowDialog() == true)
            {
                // 5. --- Получение деталей оплаты ---
                string paymentType = paymentDialog.SelectedPaymentType;
                decimal cashPaid = paymentDialog.CashPaid;
                decimal cardPaid = paymentDialog.CardPaid;
                decimal change = paymentDialog.Change;

                // 6. --- Сохранение чека и печать ---
                await SaveCheck(itemsToSave, paymentType, cashPaid, cardPaid, change); // Вызываем асинхронно
            }
            else
            {
                // Оплата отменена
                ShowTemporaryStatusMessage("Оплата отменена.");
                // Скидки и подарки остаются в чеке
            }
        }


        // --- Новый метод для сохранения чека ---
        private async Task SaveCheck(List<CheckItem> itemsToSave, string paymentType, decimal cashPaid, decimal cardPaid, decimal change = 0m)
        {
            // Формируем объект чека
            var checkToSave = new Check
            {
                ShiftId = CurrentShift.ShiftId,
                CheckNumber = _checkRepository.GetNextCheckNumber(CurrentShift.ShiftId),
                Timestamp = DateTime.Now,
                UserId = CurrentUser.UserId,
                TotalAmount = itemsToSave.Sum(i => i.ItemTotalAmount), // Пересчитываем по финальному списку
                PaymentType = paymentType,
                CashPaid = cashPaid,
                CardPaid = cardPaid,
                DiscountAmount = Math.Round(itemsToSave.Sum(i => i.DiscountAmount), 2),
                IsReturn = false,
                OriginalCheckId = null,
                Items = itemsToSave
            };

            try
            {
                PaymentButton.IsEnabled = false;
                ShowTemporaryStatusMessage("Сохранение чека...", isInfo: true, durationSeconds: 10);

                // Сохраняем чек асинхронно
                var savedCheck = await Task.Run(() => _checkRepository.AddCheck(checkToSave));
                ClearTemporaryStatusMessage(); // Убираем сообщение о сохранении

                // "Печать" чека
                StringBuilder printSb = new StringBuilder();
                // ... (Код формирования printMessage как раньше, используя savedCheck) ...
                string printMessage = $"Чек №{savedCheck.CheckNumber} сохранен.\n";
                printMessage += $"Тип оплаты: {paymentType}\n";
                if (paymentType == "Cash" || paymentType == "Mixed") printMessage += $"Получено наличными: {cashPaid:C}\n";
                if (paymentType == "Card" || paymentType == "Mixed") printMessage += $"Оплачено картой: {cardPaid:C}\n";
                if (change > 0) printMessage += $"Сдача: {change:C}\n"; // Используем переданную сдачу
                printMessage += $"ИТОГО: {savedCheck.TotalAmount:C}";
                PrinterService.Print($"Чек №{savedCheck.CheckNumber}", printMessage);

                // Открываем ящик, если была оплата наличными (или сдача)
                if (paymentType == "Cash" || (paymentType == "Mixed" && cashPaid > 0) || change > 0)
                {
                    PrinterService.OpenCashDrawer();
                }

                // Очищаем текущий чек
                ClearCheck();
            }
            catch (InvalidOperationException invEx)
            {
                MessageBox.Show($"Не удалось сохранить чек (остатки):\n{invEx.Message}", "Ошибка остатков", MessageBoxButton.OK, MessageBoxImage.Warning);
                ShowError("Ошибка сохранения (остатки)");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Критическая ошибка при сохранении чека:\n{ex.Message}", "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
                ShowError("Критическая ошибка сохранения");
            }
            finally
            {
                // Разблокируем кнопку оплаты только если чек НЕ был успешно очищен (т.е. была ошибка)
                PaymentButton.IsEnabled = CurrentCheckItems.Any();
                ClearTemporaryStatusMessage(); // Убедимся, что статус очищен
            }
        }

        // Метод для очистки временного сообщения в статус баре
        private void ClearTemporaryStatusMessage()
        {
            // Проверяем, что текущий текст - не стандартное имя кассира
            string defaultText = $"Кассир: {CurrentUser.FullName}";
            if (CashierInfoStatusText.Text != defaultText)
            {
                CashierInfoStatusText.Text = defaultText;
                CashierInfoStatusText.Foreground = Brushes.Black;
            }
        }
        private void ShowError(string message)
        {
            // Можно выводить в ErrorText над кнопками или в статус бар
            // Пока выведем в статус бар красным
            ShowTemporaryStatusMessage(message, isError: true, durationSeconds: 5); // Показываем на 5 секунд
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
                    _isManualDiscountApplied = false;
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


        private void StornoCheckItemButton_Click(object sender, RoutedEventArgs e) // Переименовано
        {
            InitiateStorno();
        }

        private void StornoCheckItemMenuItem_Click(object sender, RoutedEventArgs e) // Переименовано
        {
            InitiateStorno();
        }

        // Общий метод инициирования сторно выбранной позиции
        private void InitiateStorno()
        {
            if (!(CheckListView.SelectedItem is CheckItemView selectedItem))
            {
                ShowTemporaryStatusMessage("Выберите позицию в чеке для сторнирования.", isError: true);
                return;
            }

            // 1. Запрашиваем авторизацию (как раньше)
            User supervisor = AuthorizeAction("Сторнирование позиции", new[] { "Admin", "Manager" });
            if (supervisor == null)
            {
                ShowTemporaryStatusMessage("Операция сторно отменена или не авторизована.", isError: true);
                ItemInputTextBox.Focus();
                return;
            }

            // 2. --- Выбор типа сторно: Полное или Частичное ---
            decimal quantityToStorno = 0; // Количество к удалению/уменьшению

            // Если в позиции только 1 шт, сразу предлагаем полное удаление
            if (selectedItem.Quantity <= 1)
            {
                // Подтверждение полного удаления
                MessageBoxResult confirmResult = MessageBox.Show(
                    $"Вы уверены, что хотите полностью сторнировать позицию:\n'{selectedItem.ProductName}' (Кол-во: {selectedItem.Quantity})?",
                    "Полное сторно", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

                if (confirmResult == MessageBoxResult.Yes)
                {
                    quantityToStorno = selectedItem.Quantity; // Удаляем всё
                }
                else
                {
                    ShowTemporaryStatusMessage("Сторно отменено.", isInfo: true);
                    ItemInputTextBox.Focus();
                    return; // Отмена
                }
            }
            else // Если количество > 1, даем выбор
            {
                // Используем InputDialog для ввода количества к сторнированию
                var stornoQtyDialog = new InputDialog(
                    "Частичное сторно",
                    $"Введите количество товара '{selectedItem.ProductName}',\nкоторое нужно СТОРНИРОВАТЬ (убрать) из чека.\n(Макс: {selectedItem.Quantity}, 0 = отмена):",
                    "1"); // Предлагаем убрать 1 по умолчанию
                stornoQtyDialog.Owner = this;

                if (stornoQtyDialog.ShowDialog() == true)
                {
                    if (!decimal.TryParse(stornoQtyDialog.InputText, NumberStyles.Any, CultureInfo.CurrentCulture, out quantityToStorno) || quantityToStorno < 0)
                    {
                        ShowTemporaryStatusMessage("Некорректное количество для сторно.", isError: true);
                        ItemInputTextBox.Focus();
                        return; // Ошибка ввода
                    }
                    if (quantityToStorno == 0)
                    {
                        ShowTemporaryStatusMessage("Сторно отменено (введено 0).", isInfo: true);
                        ItemInputTextBox.Focus();
                        return; // Отмена
                    }
                    if (quantityToStorno > selectedItem.Quantity)
                    {
                        ShowTemporaryStatusMessage($"Нельзя сторнировать {quantityToStorno} шт., в позиции только {selectedItem.Quantity} шт.", isError: true);
                        ItemInputTextBox.Focus();
                        return; // Ошибка - ввели больше, чем есть
                    }
                    // Если ввели полное количество - уточняем
                    if (quantityToStorno == selectedItem.Quantity)
                    {
                        MessageBoxResult confirmFullResult = MessageBox.Show(
                            $"Вы ввели количество для полного сторнирования позиции. Продолжить?",
                            "Полное сторно", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes);
                        if (confirmFullResult == MessageBoxResult.No)
                        {
                            ShowTemporaryStatusMessage("Сторно отменено.", isInfo: true);
                            ItemInputTextBox.Focus();
                            return; // Отмена полного сторно
                        }
                    }
                }
                else // Нажали "Отмена" в диалоге ввода количества
                {
                    ShowTemporaryStatusMessage("Сторно отменено.", isInfo: true);
                    ItemInputTextBox.Focus();
                    return; // Отмена
                }
            } // Конец блока else (если количество > 1)


            // 3. --- Выполнение сторно (Полное или Частичное) ---
            if (quantityToStorno > 0)
            {
                if (quantityToStorno >= selectedItem.Quantity) // Полное удаление
                {
                    CurrentCheckItems.Remove(selectedItem);
                    LastItemInfoText.Text = $"- СТОРНО (полностью): {selectedItem.ProductName} (Авториз.: {supervisor.Username}) -";
                    ShowTemporaryStatusMessage($"Позиция '{selectedItem.ProductName}' полностью сторнирована.", isInfo: true);
                }
                else // Частичное сторно (уменьшение количества)
                {
                    // Уменьшаем количество у существующего объекта CheckItemView
                    selectedItem.Quantity -= quantityToStorno;
                    LastItemInfoText.Text = $"- СТОРНО (частично): {selectedItem.ProductName} убрано {quantityToStorno} шт. (Авториз.: {supervisor.Username}) -";
                    ShowTemporaryStatusMessage($"Количество '{selectedItem.ProductName}' уменьшено на {quantityToStorno}.", isInfo: true);
                    // UI должен обновиться сам из-за INotifyPropertyChanged в CheckItemView
                }

                _isManualDiscountApplied = false; // Сбрасываем флаг ручной скидки в любом случае
                UpdateTotals(); // Пересчитываем итоги
            }

            ItemInputTextBox.Focus(); // Возвращаем фокус
        }


        // --- Метод для авторизации действия ---
        /// <summary>
        /// Показывает окно входа для авторизации действия.
        /// </summary>
        /// <param name="actionName">Название действия для заголовка окна.</param>
        /// <param name="allowedRoles">Массив ролей, которым разрешено это действие.</param>
        /// <returns>Объект User авторизованного пользователя или null, если авторизация не удалась.</returns>
        private User AuthorizeAction(string actionName, string[] allowedRoles)
        {
            if (allowedRoles == null || !allowedRoles.Any())
            {
                // Если роли не заданы, действие запрещено по умолчанию
                MessageBox.Show("Действие не настроено для выполнения.", "Ошибка конфигурации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            // Создаем окно логина БЕЗ предзаполнения логина и БЕЗ блокировки отмены
            var authWindow = new LoginWindow();
            authWindow.Owner = this;
            authWindow.Title = $"Авторизация: {actionName}"; // Меняем заголовок

            if (authWindow.ShowDialog() == true)
            {
                // Проверяем роль вошедшего пользователя
                if (authWindow.AuthenticatedUser != null &&
                    allowedRoles.Contains(authWindow.AuthenticatedUser.Role)) // Проверяем наличие роли в списке разрешенных
                {
                    return authWindow.AuthenticatedUser; // Авторизация успешна
                }
                else
                {
                    MessageBox.Show($"У пользователя '{authWindow.AuthenticatedUser?.Username ?? "???"}' недостаточно прав для выполнения действия '{actionName}'.\nТребуемые роли: {string.Join(", ", allowedRoles)}",
                                    "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null; // Недостаточно прав
                }
            }
            else
            {
                return null; // Окно авторизации было закрыто (Отмена)
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
            if (CurrentUser.Role != "Manager" && CurrentUser.Role != "Admin")
            {
                MessageBox.Show("У вас недостаточно прав для применения ручной скидки.", "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // Выходим из метода
            }

            if (!CurrentCheckItems.Any()) return;

            decimal currentTotal = CurrentCheckItems.Sum(i => i.Quantity * i.PriceAtSale);

            var discountDialog = new DiscountDialog(currentTotal);
            discountDialog.Owner = this; // Устанавливаем владельца
            if (discountDialog.ShowDialog() == true)
            {
                // --- Применяем ручную скидку (ПЕРЕЗАПИСЫВАЯ автоматические) ---
                // Создаем копию базовых CheckItem
                List<CheckItem> itemsForManualDiscount = CurrentCheckItems
                    .Select(civ => new CheckItem
                    {
                        ProductId = civ.ProductId,
                        Quantity = civ.Quantity,
                        PriceAtSale = civ.PriceAtSale,
                        DiscountAmount = 0, // Сбрасываем перед применением ручной
                        AppliedDiscountId = null // Ручная скидка без ID
                    }).ToList();

                decimal appliedAmount = DiscountCalculator.ApplyManualCheckDiscount(
                    itemsForManualDiscount,
                    discountDialog.DiscountValue,
                    discountDialog.IsPercentage);

                // Обновляем CurrentCheckItems на основе результата
                bool changed = false;
                for (int i = 0; i < CurrentCheckItems.Count; i++)
                {
                    // Сравниваем ДО и ПОСЛЕ расчета
                    if (CurrentCheckItems[i].DiscountAmount != itemsForManualDiscount[i].DiscountAmount ||
                        CurrentCheckItems[i].AppliedDiscountId != itemsForManualDiscount[i].AppliedDiscountId)
                    {
                        CurrentCheckItems[i].DiscountAmount = itemsForManualDiscount[i].DiscountAmount;
                        CurrentCheckItems[i].AppliedDiscountId = itemsForManualDiscount[i].AppliedDiscountId;
                        changed = true;
                    }
                }

                if (changed)
                {
                    UpdateTotals();
                    ShowTemporaryStatusMessage($"Применена ручная скидка: {appliedAmount:C}", isInfo: true);
                    _isManualDiscountApplied = true; 
                }
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
            XReportItem.IsEnabled = isShiftOpen;
            CashInItem.IsEnabled = isShiftOpen;     // Внесение/Изъятие только при открытой смене
            CashOutItem.IsEnabled = isShiftOpen;
            LockStationItem.IsEnabled = !_isLocked; // Блокировать можно если не заблокировано
            LogoutItem.IsEnabled = true; // Выход доступен всегда? Или тоже блокировать при открытой смене? Решите сами.
        }

        // Обработчик открытия Popup
        private void MainMenuPopup_Opened(object sender, EventArgs e)
        {
            UpdateMenuItemsState(); // Обновляем состояние перед показом

            // --- ИЗМЕНЕНО: Устанавливаем фокус на ПЕРВЫЙ ЭЛЕМЕНТ ---
            // Находим первый ВКЛЮЧЕННЫЙ элемент
            ListBoxItem firstEnabledItem = MenuListBox.Items.OfType<ListBoxItem>()
                                            .Select(item => MenuListBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem)
                                            .FirstOrDefault(container => container != null && container.IsEnabled);

            if (firstEnabledItem != null)
            {
                // Устанавливаем фокус на найденный элемент
                firstEnabledItem.Focus();
                // Выделяем его (опционально, но улучшает вид)
                MenuListBox.SelectedItem = firstEnabledItem;
            }
            else
            {
                // Если нет активных элементов, фокус остается на кнопке или уходит куда-то еще
                MenuListBox.SelectedIndex = -1; // Снимаем выбор
                // Можно попробовать установить фокус обратно на кнопку Меню?
                // MenuButton.Focus();
            }
            // --- КОНЕЦ ИЗМЕНЕНИЯ ---
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
                    case "XReportItem": 
                        PrintXReportMenuItem_Click(selectedItem, null); 
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

        // --- Новый обработчик для пункта X-Отчет ---
        private async void PrintXReportMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentShift == null || CurrentShift.IsClosed)
            {
                MessageBox.Show("Для печати X-отчета необходимо открыть смену.", "Смена закрыта", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                ShowTemporaryStatusMessage("Формирование X-отчета...", isInfo: true, durationSeconds: 5);
                string report = await GenerateXReportAsync(CurrentShift); // Вызываем асинхронный генератор
                PrinterService.Print($"X-Отчет (Смена №{CurrentShift.ShiftNumber})", report);
                ClearTemporaryStatusMessage();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при формировании X-отчета: {ex.Message}", "Ошибка отчета", MessageBoxButton.OK, MessageBoxImage.Error);
                ClearTemporaryStatusMessage();
            }
        }

        // --- Новый АСИНХРОННЫЙ метод для генерации X-Отчета ---
        private async Task<string> GenerateXReportAsync(Shift shift)
        {
            // Получаем актуальные данные по чекам и операциям ящика для ТЕКУЩЕЙ смены
            var checksTask = Task.Run(() => _checkRepository.GetChecksByShiftId(shift.ShiftId).ToList());
            var cashOpsTask = Task.Run(() => _cashDrawerRepository.GetOperationsByShiftId(shift.ShiftId).ToList());
            var cashierTask = Task.Run(() => _userRepository.GetUserById(shift.OpeningUserId)); // Кассир, открывший смену

            await Task.WhenAll(checksTask, cashOpsTask, cashierTask);

            var checks = checksTask.Result;
            var cashOps = cashOpsTask.Result;
            var openingCashier = cashierTask.Result;

            // Расчет итогов (аналогично CloseShift, но без сохранения в БД смены)
            decimal totalSales = checks.Where(c => !c.IsReturn).Sum(c => c.TotalAmount);
            decimal totalReturns = checks.Where(c => c.IsReturn).Sum(c => c.TotalAmount);
            decimal cashSales = checks.Where(c => !c.IsReturn).Sum(c => c.PaymentType == "Cash" ? c.TotalAmount : c.PaymentType == "Mixed" ? c.CashPaid : 0);
            decimal cardSales = checks.Where(c => !c.IsReturn).Sum(c => c.PaymentType == "Card" ? c.TotalAmount : c.PaymentType == "Mixed" ? c.CardPaid : 0);
            decimal cashReturns = checks.Where(c => c.IsReturn && (c.PaymentType == "Cash" || c.PaymentType == "Mixed")).Sum(c => c.TotalAmount - c.CardPaid); // Возвраты наличными (упрощенно)
                                                                                                                                                               // Или используем способ, который был определен при возврате? Если мы его храним. Пока считаем все наличные возвраты.

            decimal cashAdded = cashOps.Where(co => co.OperationType == "CashIn").Sum(co => co.Amount);
            decimal cashRemoved = cashOps.Where(co => co.OperationType == "CashOut").Sum(co => co.Amount);
            decimal currentCashTheoretic = shift.StartCash + cashSales + cashAdded - cashRemoved - cashReturns;

            // Формирование текста отчета
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"--- X-Отчет (Промежуточный) ---");
            sb.AppendLine($"Смена №: {shift.ShiftNumber}");
            sb.AppendLine($"Открыта: {shift.OpenTimestamp:G}");
            sb.AppendLine($"Текущее время: {DateTime.Now:G}");
            sb.AppendLine($"Кассир: {openingCashier?.FullName ?? "-"} (Открыл)");
            sb.AppendLine($"---------------------------------");
            sb.AppendLine($"Начальный остаток нал.: {shift.StartCash:C}");
            sb.AppendLine($"Внесения: {cashAdded:C}");
            sb.AppendLine($"Изъятия: {cashRemoved:C}");
            sb.AppendLine($"---------------------------------");
            sb.AppendLine($"Продажи (Итог): {totalSales:C}");
            sb.AppendLine($"  в т.ч. наличными: {cashSales:C}");
            sb.AppendLine($"  в т.ч. картой: {cardSales:C}");
            sb.AppendLine($"Возвраты (Итог): {totalReturns:C}");
            sb.AppendLine($"  (возвращено наличными: {cashReturns:C})"); // Примерно
            sb.AppendLine($"---------------------------------");
            sb.AppendLine($"Наличных в кассе (теор.): {currentCashTheoretic:C}");
            sb.AppendLine($"=================================");
            sb.AppendLine("(Отчет без гашения)");

            return sb.ToString();
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

        private async void CloseShiftMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainMenuPopup.IsOpen = false;
            if (CurrentShift == null || CurrentShift.IsClosed)
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
                        string zReport = $"--- Z-Отчет ---\n";
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

        // --- ОБРАБОТЧИК КЛИКА МЫШИ ---
        private void MenuListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            DependencyObject dep = (DependencyObject)e.OriginalSource;
            while ((dep != null) && !(dep is ListBoxItem))
            {
                dep = VisualTreeHelper.GetParent(dep);
            }

            if (dep is ListBoxItem clickedItem && clickedItem.IsEnabled) // Проверяем, что элемент активен
            {
                // Выделяем кликнутый элемент
                MenuListBox.SelectedItem = clickedItem;
                // Выполняем действие
                ExecuteSelectedMenuItem();
                e.Handled = true;
            }
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
                    case Key.F4:
                        if (ManualDiscountButton.IsEnabled) ManualDiscountButton_Click(ManualDiscountButton, new RoutedEventArgs());
                        e.Handled = true;
                        break;
                    case Key.Delete: // Удалить позицию
                        if (DeleteItemButton.IsEnabled) InitiateStorno();
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
                }
            }
        }
    }
}