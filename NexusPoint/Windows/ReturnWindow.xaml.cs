using NexusPoint.Data.Repositories;
using NexusPoint.Models;
using NexusPoint.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
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

namespace NexusPoint.Windows
{
    // Новая модель для отображения и редактирования возвращаемых позиций
    public class ReturnItemView : INotifyPropertyChanged
    {
        public CheckItem OriginalItem { get; } // Храним оригинальный CheckItem
        public Product Product { get; } // Храним связанный Product
        public int ProductId => OriginalItem.ProductId;

        private decimal _returnQuantity;
        public decimal ReturnQuantity
        {
            get => _returnQuantity;
            set
            {
                // Валидация: не больше, чем было, и не меньше нуля
                if (value >= 0 && value <= OriginalItem.Quantity)
                {
                    _returnQuantity = value;
                }
                else if (value > OriginalItem.Quantity)
                {
                    _returnQuantity = OriginalItem.Quantity; // Максимум - сколько было
                }
                else
                {
                    _returnQuantity = 0; // Минимум - ноль
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(ReturnItemTotalAmount)); // Сумма тоже изменится
            }
        }

        private bool _canEditReturnQuantity = true;
        public bool CanEditReturnQuantity
        {
            get => _canEditReturnQuantity;
            private set { _canEditReturnQuantity = value; OnPropertyChanged(); } // Сеттер приватный
        }

        // --- Свойства для отображения из оригинального элемента ---
        public string ProductName => Product?.Name ?? "<Товар не найден>";
        public decimal Quantity => OriginalItem.Quantity;
        public decimal PriceAtSale => OriginalItem.PriceAtSale;
        public decimal DiscountAmount => OriginalItem.DiscountAmount;
        public decimal OriginalItemTotalAmount => OriginalItem.Quantity * OriginalItem.PriceAtSale - OriginalItem.DiscountAmount; // Рассчитываем для показа
        public string MarkingCode => OriginalItem.MarkingCode;

        // --- Рассчитываемая сумма для возвращаемого количества ---
        public decimal ReturnItemTotalAmount
        {
            get
            {
                if (OriginalItem.Quantity == 0) return 0; // Деление на ноль
                // Рассчитываем пропорционально
                decimal pricePerUnit = OriginalItem.PriceAtSale;
                decimal discountPerUnit = OriginalItem.DiscountAmount / OriginalItem.Quantity;
                return Math.Round((pricePerUnit - discountPerUnit) * ReturnQuantity, 2);
            }
        }


        // Конструктор
        public ReturnItemView(CheckItem baseItem, Product product)
        {
            OriginalItem = baseItem ?? throw new ArgumentNullException(nameof(baseItem));
            Product = product; // Может быть null, если товар удален
            // Изначально предлагаем вернуть все количество
            this._returnQuantity = baseItem.Quantity;
            // Нельзя редактировать кол-во маркированного товара
            // ВАЖНО: Если маркированных > 1 шт, то частичный возврат невозможен без скана КАЖДОЙ марки.
            // Упрощаем: если позиция маркирована - возвращаем только всю позицию целиком (кол-во нельзя менять).
            this.CanEditReturnQuantity = string.IsNullOrEmpty(baseItem.MarkingCode);
        }


        // --- Реализация INotifyPropertyChanged ---
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    } // Конец ReturnItemView


    public partial class ReturnWindow : Window
    {
        private readonly User CurrentUser;
        private readonly Shift CurrentShift; // Принимаем текущую смену

        private readonly CheckRepository _checkRepository;
        private readonly ProductRepository _productRepository;
        // StockItemRepository не нужен напрямую, т.к. CheckRepository сам обновляет остатки

        private Check _originalCheck = null; // Найденный чек продажи
        // Используем новую модель и ObservableCollection
        private ObservableCollection<ReturnItemView> _originalCheckItemsView = new ObservableCollection<ReturnItemView>();

        public ReturnWindow(User user, Shift shift)
        {
            InitializeComponent();
            CurrentUser = user ?? throw new ArgumentNullException(nameof(user));
            CurrentShift = shift; // Может быть null, если вдруг вызвали без смены (добавить проверку)

            _checkRepository = new CheckRepository();
            _productRepository = new ProductRepository();

            OriginalCheckListView.ItemsSource = _originalCheckItemsView; // Привязываем коллекцию
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Предзаполняем номер смены, если она есть
            if (CurrentShift != null)
            {
                ShiftNumberTextBox.Text = CurrentShift.ShiftNumber.ToString();
            }
            else // Если смена не передана или закрыта - блокируем
            {
                ShowError("Ошибка: Не удалось определить текущую открытую смену. Возврат невозможен.");
                FindCheckButton.IsEnabled = false;
                CheckNumberTextBox.IsEnabled = false;
                ShiftNumberTextBox.IsEnabled = false;
                return;
            }
            CheckNumberTextBox.Focus();
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Вызываем тот же метод, что и кнопка "Найти чек"
                FindCheckButton_Click(FindCheckButton, new RoutedEventArgs());
                e.Handled = true; // Поглощаем Enter
            }
        }

        // Поиск чека продажи
        private async void FindCheckButton_Click(object sender, RoutedEventArgs e)
        {
            ClearError();
            ClearCheckDetails();

            if (!int.TryParse(CheckNumberTextBox.Text, out int checkNumber) || checkNumber <= 0)
            { ShowError("Введите корректный номер чека."); return; }
            if (!int.TryParse(ShiftNumberTextBox.Text, out int shiftNumber) || shiftNumber <= 0)
            { ShowError("Введите корректный номер смены."); return; }

            try
            {
                StatusText.Text = "Поиск чека..."; // Индикатор загрузки
                // Ищем чек (метод FindCheckByNumberAndShift УЖЕ загружает позиции)
                _originalCheck = await Task.Run(() => _checkRepository.FindCheckByNumberAndShift(checkNumber, shiftNumber));
                StatusText.Text = ""; // Снимаем индикатор

                if (_originalCheck == null)
                { ShowError($"Чек продажи №{checkNumber} в смене №{shiftNumber} не найден."); return; }

                if (_originalCheck.IsReturn)
                { ShowError($"Чек №{checkNumber} уже является чеком возврата."); _originalCheck = null; return; }

                // Чек найден, отображаем информацию
                await PopulateCheckDetails(); // Делаем асинхронным
                ActionPanel.Visibility = Visibility.Visible;
                ProcessReturnButton.IsEnabled = false; // Кнопка возврата активна только после выбора
            }
            catch (Exception ex)
            {
                StatusText.Text = "";
                ShowError($"Ошибка при поиске чека: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Check find error: {ex}");
            }
        }

        // Заполнение деталей найденного чека
        private async Task PopulateCheckDetails()
        {
            if (_originalCheck == null) return;

            CheckInfoPanel.Visibility = Visibility.Visible;
            OriginalCheckNumberText.Text = $"{_originalCheck.CheckNumber} (ID: {_originalCheck.CheckId})";
            OriginalCheckDateText.Text = _originalCheck.Timestamp.ToString("g");

            _originalCheckItemsView.Clear();

            StatusText.Text = "Загрузка названий товаров...";
            List<int> productIds = _originalCheck.Items.Select(i => i.ProductId).Distinct().ToList();
            var products = (await Task.Run(() => _productRepository.GetProductsByIds(productIds)))
                           .ToDictionary(p => p.ProductId);
            StatusText.Text = "";

            foreach (var item in _originalCheck.Items)
            {
                Product product = products.TryGetValue(item.ProductId, out Product p) ? p : null;
                var itemView = new ReturnItemView(item, product);
                _originalCheckItemsView.Add(itemView);
            }
        }

        // Очистка деталей чека
        private void ClearCheckDetails()
        {
            _originalCheck = null;
            _originalCheckItemsView.Clear();
            CheckInfoPanel.Visibility = Visibility.Collapsed;
            ActionPanel.Visibility = Visibility.Collapsed;
            ProcessReturnButton.IsEnabled = false;
        }

        // Кнопка "Вернуть весь чек"
        private void ReturnAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_originalCheck == null) return;
            foreach (var item in _originalCheckItemsView)
            {
                // Сбрасываем количество на полное, если можно редактировать (не маркированный)
                // Или оставляем как есть (полное), если нельзя редактировать
                if (item.CanEditReturnQuantity)
                {
                    item.ReturnQuantity = item.Quantity;
                }
                else
                {
                    // Убедимся, что ReturnQuantity = Quantity для нередактируемых
                    item.ReturnQuantity = item.Quantity;
                }

            }
            // Активируем кнопку, если есть хоть что-то к возврату (всегда будет, если чек не пустой)
            ProcessReturnButton.IsEnabled = _originalCheckItemsView.Any(i => i.ReturnQuantity > 0);
            ShowError($"Выбраны все позиции для возврата. Проверьте количество и нажмите 'Оформить возврат'.", isInfo: true);
            OriginalCheckListView.SelectAll(); // Выделяем все для наглядности
        }

        // Кнопка "Вернуть выбранное" - теперь просто активирует кнопку оформления
        private void ReturnSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (_originalCheck == null || OriginalCheckListView.SelectedItems.Count == 0)
            {
                ShowError("Сначала выберите одну или несколько позиций в списке.");
                // Не меняем состояние ProcessReturnButton здесь, т.к. оно зависит от введенного кол-ва
                return;
            }

            // Проходим по всем ВЫДЕЛЕННЫМ элементам
            foreach (ReturnItemView selectedItem in OriginalCheckListView.SelectedItems)
            {
                // Устанавливаем ReturnQuantity равным исходному Quantity,
                // если это не маркированный товар (т.к. для маркированных мы и так возвращаем все)
                // или если пользователь может редактировать количество
                if (selectedItem.CanEditReturnQuantity) // Проверяем, можно ли менять кол-во
                {
                    selectedItem.ReturnQuantity = selectedItem.Quantity;
                }
                else
                {
                    // Если редактировать нельзя (маркированный), убедимся, что стоит полное кол-во
                    selectedItem.ReturnQuantity = selectedItem.Quantity;
                }
            }

            // После изменения количеств, проверяем, есть ли что возвращать и активируем кнопку
            ProcessReturnButton.IsEnabled = _originalCheckItemsView.Any(i => i.ReturnQuantity > 0);
            ShowError($"Для выбранных позиций установлено максимальное количество к возврату. Проверьте и нажмите 'Оформить возврат'.", isInfo: true);
        }


        // Валидация ввода для колонки "Возврат кол-во"
        private void QuantityTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            string currentText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
            string decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            string pattern = $@"^\d*({Regex.Escape(decimalSeparator)}?\d*)?$"; // Только положительные
            Regex regex = new Regex(pattern);
            if (!regex.IsMatch(currentText)) e.Handled = true;

            // После валидации сразу активируем кнопку "Оформить возврат", если есть хотя бы одно значение > 0
            // (Можно сделать и по событию TextChanged, но это проще)
            Dispatcher.BeginInvoke(new Action(() => { // Отложенный вызов после обновления текста
                ProcessReturnButton.IsEnabled = _originalCheckItemsView.Any(i => i.ReturnQuantity > 0);
            }), DispatcherPriority.ContextIdle);

        }


        // Кнопка "Оформить возврат"
        private async void ProcessReturnButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. --- Базовые проверки ---
            if (CurrentShift == null || CurrentShift.IsClosed)
            { ShowError("Невозможно оформить возврат: текущая смена не открыта."); return; }
            if (_originalCheck == null)
            { ShowError("Нет данных оригинального чека для оформления возврата."); return; }

            // 2. --- Сбор позиций для возврата ---
            var itemsToReturnProcessing = _originalCheckItemsView
                .Where(item => item.ReturnQuantity > 0) // Берем те, где указано кол-во > 0
                .ToList();

            if (!itemsToReturnProcessing.Any())
            { ShowError("Не указано количество для возврата ни по одной позиции."); return; }

            // 3. --- Проверка количества и марок ---
            List<CheckItem> returnCheckItems = new List<CheckItem>(); // Список для нового чека
            foreach (var itemToReturnView in itemsToReturnProcessing)
            {
                // Валидация введенного количества
                if (itemToReturnView.ReturnQuantity > itemToReturnView.Quantity)
                { ShowError($"Нельзя вернуть {itemToReturnView.ReturnQuantity} шт. товара '{itemToReturnView.ProductName}', продано было {itemToReturnView.Quantity}."); return; }
                if (itemToReturnView.ReturnQuantity <= 0) continue; // Пропускаем нулевые (хотя where выше должен был отсеять)

                string scannedMarkingCode = itemToReturnView.MarkingCode; // Марка по умолчанию

                // Запрос марки, если товар маркированный и частичный возврат не разрешен
                if (itemToReturnView.Product != null && itemToReturnView.Product.IsMarked)
                {
                    if (itemToReturnView.ReturnQuantity != itemToReturnView.Quantity)
                    {
                        ShowError($"Частичный возврат маркированного товара '{itemToReturnView.ProductName}' не поддерживается. Верните всю позицию или не возвращайте ее.");
                        return;
                    }

                    // Запрашиваем скан марки
                    var markingDialog = new InputDialog("Возврат маркированного товара",
                        $"Отсканируйте/введите КОД МАРКИРОВКИ возвращаемого товара:\n'{itemToReturnView.ProductName}'", "");
                    if (markingDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(markingDialog.InputText))
                    {
                        scannedMarkingCode = markingDialog.InputText.Trim();
                        // TODO: Здесь может быть дополнительная валидация марки
                    }
                    else
                    { ShowError($"Возврат отменен: не указан код маркировки для '{itemToReturnView.ProductName}'."); return; }
                }

                // Создаем позицию для чека возврата
                decimal quantityRatio = itemToReturnView.Quantity > 0 ? itemToReturnView.ReturnQuantity / itemToReturnView.Quantity : 0;
                returnCheckItems.Add(new CheckItem
                {
                    ProductId = itemToReturnView.ProductId,
                    Quantity = itemToReturnView.ReturnQuantity,
                    PriceAtSale = itemToReturnView.PriceAtSale,
                    DiscountAmount = Math.Round(itemToReturnView.DiscountAmount * quantityRatio, 2),
                    ItemTotalAmount = Math.Round(itemToReturnView.OriginalItemTotalAmount * quantityRatio, 2), // Используем OriginalItemTotalAmount для пропорции
                    MarkingCode = scannedMarkingCode
                });
            }

            // 4. --- Формирование чека возврата ---
            if (!returnCheckItems.Any())
            { ShowError("Нет корректных позиций для оформления возврата."); return; }

            decimal returnTotalAmount = returnCheckItems.Sum(i => i.ItemTotalAmount);
            decimal returnDiscountAmount = returnCheckItems.Sum(i => i.DiscountAmount);
            bool returnByCard = ReturnCardRadio.IsChecked == true; // Определяем способ возврата

            var returnCheck = new Check
            {
                ShiftId = CurrentShift.ShiftId,
                CheckNumber = _checkRepository.GetNextCheckNumber(CurrentShift.ShiftId),
                Timestamp = DateTime.Now,
                UserId = CurrentUser.UserId,
                TotalAmount = returnTotalAmount,
                PaymentType = returnByCard ? "CardReturn" : "Cash", // Можно использовать для отчетности
                CashPaid = 0,
                CardPaid = 0, // При возврате не получаем деньги
                DiscountAmount = returnDiscountAmount,
                IsReturn = true,
                OriginalCheckId = _originalCheck.CheckId,
                Items = returnCheckItems
            };

            // 5. --- Сохранение и Печать ---
            try
            {
                ProcessReturnButton.IsEnabled = false; // Блокируем кнопку
                FindCheckButton.IsEnabled = false; // Блокируем поиск на время сохранения
                StatusText.Text = "Сохранение чека возврата...";

                // Сохраняем чек возврата (это также обновит остатки)
                var savedReturnCheck = await Task.Run(() => _checkRepository.AddCheck(returnCheck));
                StatusText.Text = "";

                // --- Печать чека возврата ---
                StringBuilder checkSb = new StringBuilder();
                checkSb.AppendLine($"--- Чек Возврата №{savedReturnCheck.CheckNumber} ---");
                checkSb.AppendLine($"Основание: Чек продажи №{_originalCheck.CheckNumber} от {_originalCheck.Timestamp:d}");
                checkSb.AppendLine($"Кассир: {CurrentUser.FullName}");
                checkSb.AppendLine($"Дата: {savedReturnCheck.Timestamp:G}");
                checkSb.AppendLine("---------------------------------");
                var productsInfo = await Task.Run(() => _productRepository.GetProductsByIds(savedReturnCheck.Items.Select(i => i.ProductId)))
                                         .ContinueWith(t => t.Result.ToDictionary(p => p.ProductId)); // Загружаем инфо о товарах
                foreach (var item in savedReturnCheck.Items)
                {
                    string name = productsInfo.TryGetValue(item.ProductId, out var p) ? p.Name : "<Товар?>";
                    checkSb.AppendLine($"{name} x {item.Quantity}");
                    checkSb.AppendLine($"  Сумма: {item.ItemTotalAmount:C}");
                }
                checkSb.AppendLine("---------------------------------");
                checkSb.AppendLine($"ИТОГО К ВОЗВРАТУ: {savedReturnCheck.TotalAmount:C}");
                checkSb.AppendLine(returnByCard ? "ВОЗВРАТ НА КАРТУ" : "ВОЗВРАТ НАЛИЧНЫМИ");
                checkSb.AppendLine("--- Конец чека возврата ---");
                PrinterService.Print($"Чек возврата №{savedReturnCheck.CheckNumber}", checkSb.ToString());

                // --- Печать "рыбы" РКО ---
                StringBuilder rkoSb = new StringBuilder();
                rkoSb.AppendLine($"          Расходный Кассовый Ордер № {savedReturnCheck.CheckNumber}-В");
                rkoSb.AppendLine($"                          от {savedReturnCheck.Timestamp:d} г.");
                rkoSb.AppendLine("-------------------------------------------------------------");
                rkoSb.AppendLine($"Организация:    ООО \"NexusPoint\"");
                rkoSb.AppendLine("-------------------------------------------------------------");
                rkoSb.AppendLine($"Выдать:         _____________________________________________");
                rkoSb.AppendLine($"                              (Ф.И.О.)");
                rkoSb.AppendLine($"Основание:      Возврат товара по чеку №{_originalCheck.CheckNumber} от {_originalCheck.Timestamp:d}");
                rkoSb.AppendLine($"Сумма:          {savedReturnCheck.TotalAmount:N2} руб.");
                rkoSb.AppendLine($"                ({Utils.Converters.AmountToWordsConverter.Convert(savedReturnCheck.TotalAmount)})"); // СУММА ПРОПИСЬЮ (НУЖЕН КОНВЕРТЕР!)
                rkoSb.AppendLine($"Приложение:     Чек №{savedReturnCheck.CheckNumber}");
                rkoSb.AppendLine("\n");
                rkoSb.AppendLine("Получил:        ____________________  _____________________");
                rkoSb.AppendLine("                      (сумма прописью)");
                rkoSb.AppendLine($"                {savedReturnCheck.Timestamp:d} г.             _____________________");
                rkoSb.AppendLine($"                                        (подпись)");
                rkoSb.AppendLine("\nПредъявлен документ: Паспорт серия _______ № _______________");
                rkoSb.AppendLine($"Выдан:          _____________________________________________");
                rkoSb.AppendLine($"                          (кем и когда)");
                rkoSb.AppendLine("\n");
                rkoSb.AppendLine("Главный бухгалтер _________ /_______________/");
                rkoSb.AppendLine($"Кассир            _________ / {CurrentUser.FullName} /");

                PrinterService.Print($"РКО №{savedReturnCheck.CheckNumber}-В", rkoSb.ToString());

                // --- Взаимодействие с оборудованием ---
                if (returnByCard)
                {
                    MessageBox.Show($"Имитация банковского терминала:\nПриложите/вставьте карту для возврата\nСумма: {savedReturnCheck.TotalAmount:C}",
                                    "Возврат на карту", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    PrinterService.OpenCashDrawer(); // Открываем ящик для возврата наличных
                }

                this.DialogResult = true; // Успешно, закрываем окно
            }
            catch (InvalidOperationException invEx)
            { ShowError($"Не удалось оформить возврат (остатки): {invEx.Message}"); }
            catch (Exception ex)
            { ShowError($"Критическая ошибка при оформлении возврата: {ex.Message}"); System.Diagnostics.Debug.WriteLine($"Return check save error: {ex}"); }
            finally
            {
                ProcessReturnButton.IsEnabled = _originalCheckItemsView.Any(i => i.ReturnQuantity > 0); // Разблокируем кнопку в случае ошибки
                FindCheckButton.IsEnabled = true; // Разблокируем поиск
                StatusText.Text = "";
            }
        } // Конец ProcessReturnButton_Click


        // --- Вспомогательные ---
        private void ShowError(string message, bool isInfo = false)
        {
            StatusText.Text = message;
            StatusText.Foreground = isInfo ? Brushes.Blue : Brushes.Red;
        }
        private void ClearError() { StatusText.Text = string.Empty; }

    } // Конец класса ReturnWindow
} // Конец namespace