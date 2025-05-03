using NexusPoint.Data.Repositories;
using NexusPoint.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// Логика взаимодействия для ReturnWindow.xaml
    /// </summary>
    public partial class ReturnWindow : Window
    {
        private readonly User CurrentUser;
        private readonly Shift CurrentShift; // Принимаем текущую смену

        private readonly CheckRepository _checkRepository;
        private readonly ProductRepository _productRepository;
        // StockItemRepository не нужен напрямую, т.к. CheckRepository сам обновляет остатки

        private Check _originalCheck = null; // Найденный чек продажи
        // Используем ObservableCollection, чтобы ListView обновлялся
        private ObservableCollection<CheckItemView> _originalCheckItemsView = new ObservableCollection<CheckItemView>();
        private List<CheckItemView> _itemsToReturn = new List<CheckItemView>(); // Какие позиции выбраны для возврата

        public ReturnWindow(User user, Shift shift)
        {
            InitializeComponent();
            CurrentUser = user;
            CurrentShift = shift; // Сохраняем текущую смену

            _checkRepository = new CheckRepository();
            _productRepository = new ProductRepository();

            OriginalCheckListView.ItemsSource = _originalCheckItemsView; // Привязываем коллекцию
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Можно предзаполнить номер смены текущей сменой
            if (CurrentShift != null)
            {
                ShiftNumberTextBox.Text = CurrentShift.ShiftNumber.ToString();
            }
            CheckNumberTextBox.Focus();
        }

        // Поиск чека продажи
        private void FindCheckButton_Click(object sender, RoutedEventArgs e)
        {
            ClearError();
            ClearCheckDetails();

            if (!int.TryParse(CheckNumberTextBox.Text, out int checkNumber) || checkNumber <= 0)
            {
                ShowError("Введите корректный номер чека.");
                return;
            }
            if (!int.TryParse(ShiftNumberTextBox.Text, out int shiftNumber) || shiftNumber <= 0)
            {
                ShowError("Введите корректный номер смены.");
                return;
            }

            try
            {
                _originalCheck = _checkRepository.FindCheckByNumberAndShift(checkNumber, shiftNumber);

                if (_originalCheck == null)
                {
                    ShowError($"Чек продажи №{checkNumber} в смене №{shiftNumber} не найден.");
                    return;
                }

                if (_originalCheck.IsReturn)
                {
                    ShowError($"Чек №{checkNumber} уже является чеком возврата.");
                    _originalCheck = null; // Сбрасываем, чтобы нельзя было оформить возврат
                    return;
                }

                // Чек найден, отображаем информацию
                PopulateCheckDetails();
                ActionPanel.Visibility = Visibility.Visible; // Показываем кнопки действий
                // ProcessReturnButton пока неактивна
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при поиске чека: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Check find error: {ex}");
            }
        }

        // Заполнение деталей найденного чека
        private async void PopulateCheckDetails()
        {
            if (_originalCheck == null) return;

            CheckInfoPanel.Visibility = Visibility.Visible;
            OriginalCheckNumberText.Text = $"{_originalCheck.CheckNumber} (ID: {_originalCheck.CheckId})";
            OriginalCheckDateText.Text = _originalCheck.Timestamp.ToString("g"); // dd.MM.yyyy HH:mm

            _originalCheckItemsView.Clear(); // Очищаем предыдущие

            // Асинхронно загружаем продукты для отображения имен
            List<int> productIds = _originalCheck.Items.Select(i => i.ProductId).Distinct().ToList();
            var products = (await System.Threading.Tasks.Task.Run(() => _productRepository.GetProductsByIds(productIds)))
                           .ToDictionary(p => p.ProductId);

            foreach (var item in _originalCheck.Items)
            {
                var itemView = new CheckItemView
                {
                    // Копируем основные свойства из CheckItem
                    CheckItemId = item.CheckItemId,
                    CheckId = item.CheckId,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    PriceAtSale = item.PriceAtSale,
                    // ItemTotalAmount не присваиваем, оно рассчитается само
                    DiscountAmount = item.DiscountAmount,
                    MarkingCode = item.MarkingCode,
                    // Добавляем найденный продукт
                    Product = products.TryGetValue(item.ProductId, out Product p) ? p : null
                };
                _originalCheckItemsView.Add(itemView);
            }
        }

        // Очистка деталей чека
        private void ClearCheckDetails()
        {
            _originalCheck = null;
            _originalCheckItemsView.Clear();
            _itemsToReturn.Clear();
            CheckInfoPanel.Visibility = Visibility.Collapsed;
            ActionPanel.Visibility = Visibility.Collapsed;
            ProcessReturnButton.IsEnabled = false;
        }

        // Кнопка "Вернуть весь чек"
        private void ReturnAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_originalCheck == null) return;

            _itemsToReturn = _originalCheckItemsView.ToList(); // Все позиции идут на возврат
            ProcessReturnButton.IsEnabled = true; // Активируем кнопку оформления
            ShowError($"Готово к возврату всего чека ({_itemsToReturn.Count} поз.). Нажмите 'Оформить возврат'.", isInfo: true);
            // Выделяем все строки в списке для наглядности
            OriginalCheckListView.SelectAll();
        }

        // Кнопка "Вернуть выбранное"
        private void ReturnSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (_originalCheck == null || OriginalCheckListView.SelectedItems.Count == 0)
            {
                ShowError("Выберите одну или несколько позиций для возврата.");
                return;
            }

            _itemsToReturn = OriginalCheckListView.SelectedItems.Cast<CheckItemView>().ToList();
            ProcessReturnButton.IsEnabled = true;
            ShowError($"Готово к возврату {_itemsToReturn.Count} выбранных позиций. Нажмите 'Оформить возврат'.", isInfo: true);
        }

        // Кнопка "Оформить возврат"
        private void ProcessReturnButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentShift == null || CurrentShift.IsClosed)
            {
                ShowError("Невозможно оформить возврат: текущая смена не открыта.");
                return;
            }
            if (_originalCheck == null || !_itemsToReturn.Any())
            {
                ShowError("Нет данных для оформления возврата.");
                return;
            }

            // --- Проверка и сканирование марок ---
            List<CheckItem> returnCheckItems = new List<CheckItem>();
            foreach (var itemToReturnView in _itemsToReturn)
            {
                string scannedMarkingCode = itemToReturnView.MarkingCode; // По умолчанию берем старую марку (если она была)

                // Если товар маркированный, ЗАПРАШИВАЕМ скан марки
                if (itemToReturnView.Product != null && itemToReturnView.Product.IsMarked)
                {
                    var markingDialog = new InputDialog("Возврат маркированного товара",
                        $"Отсканируйте/введите код маркировки для возвращаемого товара:\n'{itemToReturnView.ProductName}'",
                        ""); // Поле ввода пустое

                    if (markingDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(markingDialog.InputText))
                    {
                        scannedMarkingCode = markingDialog.InputText.Trim();
                        // Здесь можно добавить валидацию сканированной марки, если требуется
                        // Например, сравнить с оригинальной (itemToReturnView.MarkingCode)
                        // Или проверить формат/длину
                    }
                    else
                    {
                        // Пользователь отменил ввод марки
                        ShowError($"Возврат отменен: не указан код маркировки для '{itemToReturnView.ProductName}'.");
                        return; // Прерываем весь процесс возврата
                    }
                }

                // Создаем объект CheckItem для чека возврата
                returnCheckItems.Add(new CheckItem
                {
                    // CheckId будет установлен при сохранении чека возврата
                    ProductId = itemToReturnView.ProductId,
                    Quantity = itemToReturnView.Quantity, // Возвращаем то же количество
                    PriceAtSale = itemToReturnView.PriceAtSale, // По той же цене
                    // ItemTotalAmount будет рассчитана или взята из PriceAtSale/Quantity/DiscountAmount
                    DiscountAmount = itemToReturnView.DiscountAmount, // С той же скидкой
                    MarkingCode = scannedMarkingCode // Записываем ОТСКАННИРОВАННУЮ марку
                });
            }

            // --- Формирование чека возврата ---
            decimal returnTotalAmount = returnCheckItems.Sum(i => i.ItemTotalAmount);
            decimal returnDiscountAmount = returnCheckItems.Sum(i => i.DiscountAmount);

            var returnCheck = new Check
            {
                ShiftId = CurrentShift.ShiftId, // Текущая смена
                CheckNumber = _checkRepository.GetNextCheckNumber(CurrentShift.ShiftId), // Следующий номер чека
                Timestamp = DateTime.Now,
                UserId = CurrentUser.UserId,
                TotalAmount = returnTotalAmount, // Общая сумма возврата (положительное число)
                PaymentType = "Cash", // Упрощенно: возврат всегда наличными
                CashPaid = 0, // Мы не получаем деньги
                CardPaid = 0,
                DiscountAmount = returnDiscountAmount, // Сумма скидок возвращаемых товаров
                IsReturn = true, // Флаг возврата
                OriginalCheckId = _originalCheck.CheckId, // Ссылка на чек продажи
                Items = returnCheckItems // Список возвращаемых позиций
            };

            try
            {
                // --- Сохранение чека возврата и обновление остатков ---
                var savedReturnCheck = _checkRepository.AddCheck(returnCheck);

                // --- Имитация печати чека возврата ---
                string printMessage = $"--- Чек Возврата №{savedReturnCheck.CheckNumber} ---\n";
                printMessage += $"Основание: Чек продажи №{_originalCheck.CheckNumber} (Смена №{ShiftNumberTextBox.Text})\n";
                printMessage += $"Сумма возврата: {savedReturnCheck.TotalAmount:C}\n";
                printMessage += $"Возврат наличными."; // По нашему упрощению

                MessageBox.Show(printMessage, "Возврат оформлен (Имитация печати)", MessageBoxButton.OK, MessageBoxImage.Information);

                this.DialogResult = true; // Успешно, закрываем окно
            }
            catch (InvalidOperationException invEx) // Ошибка обновления остатков
            {
                MessageBox.Show($"Не удалось оформить возврат:\n{invEx.Message}", "Ошибка остатков", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Критическая ошибка при оформлении возврата:\n{ex.Message}", "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Return check save error: {ex}");
            }
        }


        // Показ/Скрытие ошибок
        private void ShowError(string message, bool isInfo = false)
        {
            StatusText.Text = message;
            StatusText.Foreground = isInfo ? System.Windows.Media.Brushes.Blue : System.Windows.Media.Brushes.Red;
        }

        private void ClearError()
        {
            StatusText.Text = string.Empty;
        }
    }
}