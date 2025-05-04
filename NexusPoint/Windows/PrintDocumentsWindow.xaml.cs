using NexusPoint.Data.Repositories;
using NexusPoint.Models;
using NexusPoint.Utils;
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
    // Дополнительная модель для отображения в ListView с подгруженными данными
    public class CheckDisplayView : Check
    {
        public User Cashier { get; set; }
        public Shift Shift { get; set; } // Добавим смену
        // Унаследует все остальные свойства от Check
    }

    public partial class PrintDocumentsWindow : Window
    {
        private readonly CheckRepository _checkRepository;
        private readonly ShiftRepository _shiftRepository; // Нужен для поиска смены по номеру
        private readonly UserRepository _userRepository;
        private readonly ProductRepository _productRepository; // Нужен для товарного чека

        private CheckDisplayView _selectedCheck = null; // Храним выбранный чек для действий

        public PrintDocumentsWindow()
        {
            InitializeComponent();
            _checkRepository = new CheckRepository();
            _shiftRepository = new ShiftRepository();
            _userRepository = new UserRepository();
            _productRepository = new ProductRepository();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CheckNumberTextBox.Focus();
            UpdateActionButtonsState(); // Изначально кнопки неактивны
        }

        // Поиск чека
        private void FindCheckButton_Click(object sender, RoutedEventArgs e)
        {
            ClearError();
            ClearDisplay();

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
                // Ищем чек по номеру и номеру смены
                Check foundCheck = _checkRepository.FindCheckByNumberAndShift(checkNumber, shiftNumber);

                if (foundCheck == null)
                {
                    ShowError($"Чек №{checkNumber} в смене №{shiftNumber} не найден.");
                }
                else
                {
                    DisplayChecks(new List<Check> { foundCheck });
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при поиске чека: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Find check error: {ex}");
            }
        }

        // Копия последнего чека
        private void PrintLastCheckCopyButton_Click(object sender, RoutedEventArgs e)
        {
            ClearError();
            ClearDisplay();
            try
            {
                // Запрашиваем последний чек из репозитория (нужно добавить метод)
                Check lastCheck = _checkRepository.GetLastCheck(); // Добавьте этот метод в CheckRepository
                if (lastCheck == null)
                {
                    ShowError("Не найдено ни одного чека.");
                }
                else
                {
                    DisplayChecks(new List<Check> { lastCheck });
                    // Сразу печатаем копию
                    if (_selectedCheck != null) PrintCheckCopy(_selectedCheck);
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при получении последнего чека: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Get last check error: {ex}");
            }
        }

        // Отображение найденных чеков (или одного чека)
        private async void DisplayChecks(List<Check> checks)
        {
            var checkViews = new List<CheckDisplayView>();
            if (checks != null && checks.Any())
            {
                // Асинхронно подгружаем кассиров и смены для отображения
                var userIds = checks.Select(c => c.UserId).Distinct().ToList();
                var shiftIds = checks.Select(c => c.ShiftId).Distinct().ToList();

                var usersTask = System.Threading.Tasks.Task.Run(() => _userRepository.GetUsersByIds(userIds)); // Нужно добавить GetUsersByIds
                var shiftsTask = System.Threading.Tasks.Task.Run(() => _shiftRepository.GetShiftsByIds(shiftIds)); // Нужно добавить GetShiftsByIds

                await System.Threading.Tasks.Task.WhenAll(usersTask, shiftsTask);

                var users = usersTask.Result.ToDictionary(u => u.UserId);
                var shifts = shiftsTask.Result.ToDictionary(s => s.ShiftId);


                foreach (var check in checks)
                {
                    // Дозагружаем позиции, если они не были загружены ранее
                    if (check.Items == null || !check.Items.Any())
                    {
                        check.Items = _checkRepository.GetCheckItemsByCheckId(check.CheckId);
                    }

                    var view = new CheckDisplayView
                    {
                        // Копируем свойства из Check
                        CheckId = check.CheckId,
                        ShiftId = check.ShiftId,
                        CheckNumber = check.CheckNumber,
                        Timestamp = check.Timestamp,
                        UserId = check.UserId,
                        TotalAmount = check.TotalAmount,
                        PaymentType = check.PaymentType,
                        CashPaid = check.CashPaid,
                        CardPaid = check.CardPaid,
                        DiscountAmount = check.DiscountAmount,
                        IsReturn = check.IsReturn,
                        OriginalCheckId = check.OriginalCheckId,
                        Items = check.Items, // Копируем список позиций

                        // Добавляем связанные данные
                        Cashier = users.TryGetValue(check.UserId, out User u) ? u : null,
                        Shift = shifts.TryGetValue(check.ShiftId, out Shift s) ? s : null
                    };
                    checkViews.Add(view);
                }
            }

            ChecksListView.ItemsSource = checkViews;
            // Выбираем первый элемент, если он есть
            if (checkViews.Any())
            {
                ChecksListView.SelectedIndex = 0;
                _selectedCheck = checkViews.First();
            }
            else
            {
                _selectedCheck = null;
            }
            UpdateActionButtonsState(); // Обновляем доступность кнопок
        }

        // Обновление состояния кнопок действий
        private void UpdateActionButtonsState()
        {
            bool isCheckSelected = _selectedCheck != null;
            bool isSaleCheckSelected = isCheckSelected && !_selectedCheck.IsReturn;

            PrintCopyButton.IsEnabled = isCheckSelected;
            PrintTovarnyCheckButton.IsEnabled = isSaleCheckSelected; // Товарный чек только для продаж
            PrintDiscountDetailsButton.IsEnabled = isCheckSelected && _selectedCheck.DiscountAmount > 0; // Если были скидки
        }

        // Выбор чека в списке
        private void ChecksListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedCheck = ChecksListView.SelectedItem as CheckDisplayView;
            UpdateActionButtonsState();
        }


        // --- Кнопки действий ---

        private void PrintCopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCheck != null)
            {
                PrintCheckCopy(_selectedCheck);
            }
            else
            {
                ShowError("Сначала найдите или выберите чек.");
            }
        }

        private void PrintTovarnyCheckButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCheck != null && !_selectedCheck.IsReturn)
            {
                PrintTovarnyCheck(_selectedCheck);
            }
            else
            {
                ShowError("Выберите чек продажи для печати товарного чека.");
            }
        }

        private void PrintDiscountDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCheck != null)
            {
                PrintDiscountDetails(_selectedCheck);
            }
            else
            {
                ShowError("Сначала найдите или выберите чек.");
            }
        }


        // --- Логика "Печати" (Имитация) ---

        private async void PrintCheckCopy(CheckDisplayView check)
        {
            // Имитация печати копии фискального чека
            // Формируем текст, похожий на фискальный
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("--- КОПИЯ ЧЕКА ---");
            sb.AppendLine($"ООО \"NexusPoint\""); // Пример организации
            sb.AppendLine($"Кассир: {check.Cashier?.FullName ?? "Неизвестно"}");
            sb.AppendLine($"ИНН: 1234567890   ЗН ККТ: 00012345"); // Пример данных
            sb.AppendLine($"Смена №: {check.Shift?.ShiftNumber ?? check.ShiftId}   Чек №: {check.CheckNumber}");
            sb.AppendLine($"{check.Timestamp:G}");
            sb.AppendLine(check.IsReturn ? "*** ВОЗВРАТ ПРИХОДА ***" : "*** ПРИХОД ***");
            sb.AppendLine("---------------------------------");

            // Загружаем названия товаров для позиций
            var productIds = check.Items.Select(i => i.ProductId).Distinct().ToList();
            var products = (await System.Threading.Tasks.Task.Run(() => _productRepository.GetProductsByIds(productIds)))
                           .ToDictionary(p => p.ProductId);

            foreach (var item in check.Items)
            {
                string productName = products.TryGetValue(item.ProductId, out Product p) ? p.Name : "<Товар?>";
                sb.AppendLine($"{productName}");
                sb.AppendLine($"  {item.Quantity} x {item.PriceAtSale:N2} = {item.Quantity * item.PriceAtSale:N2}");
                if (item.DiscountAmount > 0)
                {
                    sb.AppendLine($"  Скидка: {item.DiscountAmount:N2}");
                }
                sb.AppendLine($"  ИТОГ ПО ПОЗИЦИИ: {item.ItemTotalAmount:N2}");
            }
            sb.AppendLine("---------------------------------");
            sb.AppendLine($"ПОДЫТОГ: {check.Items.Sum(i => i.Quantity * i.PriceAtSale):N2}");
            if (check.DiscountAmount > 0)
            {
                sb.AppendLine($"СКИДКА НА ЧЕК: {check.DiscountAmount:N2}");
            }
            sb.AppendLine($"ИТОГО: {check.TotalAmount:N2}");
            sb.AppendLine("---------------------------------");
            string paymentTypeText = check.PaymentType == "Cash" ? "НАЛИЧНЫМИ" :
                                      check.PaymentType == "Card" ? "КАРТОЙ" : "СМЕШАННАЯ";
            sb.AppendLine($"ОПЛАТА ({paymentTypeText}): {check.TotalAmount:N2}");
            if (check.PaymentType == "Cash" || check.PaymentType == "Mixed")
                sb.AppendLine($"  ПОЛУЧЕНО НАЛ: {check.CashPaid:N2}");
            if (check.PaymentType == "Card" || check.PaymentType == "Mixed")
                sb.AppendLine($"  ПОЛУЧЕНО КАРТОЙ: {check.CardPaid:N2}");
            decimal change = (check.CashPaid + check.CardPaid) - check.TotalAmount;
            // Корректный расчет сдачи для копии (если не хранится)
            if (!check.IsReturn && change > 0.001m) // Показываем сдачу для продаж
            {
                sb.AppendLine($"СДАЧА: {change:N2}");
            }
            sb.AppendLine("---------------------------------");
            sb.AppendLine($"ФН: 999900001111222   ФД: {check.CheckId + 10000}  ФП: 1234567890"); // Пример ФПД
            sb.AppendLine("--- КОНЕЦ КОПИИ ---");


            PrinterService.Print($"Копия чека №{check.CheckNumber}", sb.ToString());
            ShowError("Копия чека 'отправлена на печать'.", isInfo: true);
        }

        private async void PrintTovarnyCheck(CheckDisplayView check)
        {
            // Имитация печати товарного чека (нефискальный)
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"--- ТОВАРНЫЙ ЧЕК № {check.CheckNumber} от {check.Timestamp:d} ---");
            sb.AppendLine($"Продавец: ООО \"NexusPoint\"");
            sb.AppendLine($"Кассир: {check.Cashier?.FullName ?? "-"}");
            sb.AppendLine("--------------------------------------------------");
            sb.AppendLine("| № | Наименование товара          | Кол-во | Цена  | Сумма |");
            sb.AppendLine("--------------------------------------------------");

            var productIds = check.Items.Select(i => i.ProductId).Distinct().ToList();
            var products = (await System.Threading.Tasks.Task.Run(() => _productRepository.GetProductsByIds(productIds)))
                           .ToDictionary(p => p.ProductId);
            int index = 1;
            foreach (var item in check.Items)
            {
                string productName = products.TryGetValue(item.ProductId, out Product p) ? p.Name : "<Товар?>";
                // Форматируем строку таблицы (примерно)
                sb.AppendFormat("|{0,3}| {1,-28}|{2,8}|{3,7:N2}|{4,7:N2}|\n",
                               index++,
                               productName.Length > 28 ? productName.Substring(0, 28) : productName, // Обрезаем длинные названия
                               item.Quantity,
                               item.PriceAtSale, // Цена за единицу
                               item.ItemTotalAmount); // Сумма по позиции с учетом скидки
            }
            sb.AppendLine("--------------------------------------------------");
            sb.AppendLine($"Всего наименований: {check.Items.Count}, на сумму: {check.TotalAmount:N2} руб.");
            if (check.DiscountAmount > 0)
            {
                sb.AppendLine($"В том числе скидка: {check.DiscountAmount:N2} руб.");
            }
            sb.AppendLine("\nПодпись кассира: __________________");


            MessageBox.Show(sb.ToString(), "Имитация печати товарного чека", MessageBoxButton.OK, MessageBoxImage.Information);
            ShowError("Товарный чек 'отправлен на печать'.", isInfo: true);
        }

        private void PrintDiscountDetails(CheckDisplayView check)
        {
            // Имитация печати расшифровки скидок (как в PDF стр. 24)
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"--- Расшифровка скидок к чеку №{check.CheckNumber} ---");
            if (check.DiscountAmount > 0)
            {
                // TODO: Реализовать логику получения НАЗВАНИЙ скидок/акций,
                // примененных к позициям чека. Текущая БД хранит только сумму скидки.
                // Это потребует доработки БД (см. предыдущие обсуждения) или
                // сложной логики восстановления скидок по сумме (не рекомендуется).

                // Пока выводим общую информацию:
                sb.AppendLine("Применены скидки на общую сумму: " + check.DiscountAmount.ToString("C"));
                sb.AppendLine("\n(Детальная расшифровка по акциям не реализована в этой версии)");

            }
            else
            {
                sb.AppendLine("Скидки к данному чеку не применялись.");
            }


            MessageBox.Show(sb.ToString(), "Имитация печати скидок", MessageBoxButton.OK, MessageBoxImage.Information);
            ShowError("Расшифровка скидок 'отправлена на печать'.", isInfo: true);
        }


        // Очистка и сообщения
        private void ClearDisplay()
        {
            ChecksListView.ItemsSource = null;
            _selectedCheck = null;
            UpdateActionButtonsState();
        }
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