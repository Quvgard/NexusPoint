﻿using NexusPoint.Data.Repositories;
using NexusPoint.Models;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace NexusPoint.BusinessLogic
{
    public class ShiftManager
    {
        private readonly ShiftRepository _shiftRepository;
        private readonly CashDrawerOperationRepository _cashDrawerRepository;
        private readonly ReportService _reportService;
        private readonly UserRepository _userRepository;

        public Shift CurrentOpenShift { get; private set; }

        public event EventHandler ShiftOpened;
        public event EventHandler ShiftClosed;

        public ShiftManager(ShiftRepository shiftRepository, CashDrawerOperationRepository cashDrawerRepository, ReportService reportService, UserRepository userRepository)
        {
            _shiftRepository = shiftRepository ?? throw new ArgumentNullException(nameof(shiftRepository));
            _cashDrawerRepository = cashDrawerRepository ?? throw new ArgumentNullException(nameof(cashDrawerRepository));
            _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        }

        public void CheckCurrentShiftState()
        {
            try
            {
                CurrentOpenShift = _shiftRepository.GetCurrentOpenShift();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Критическая ошибка при проверке смены: {ex.Message}", "Ошибка смены", MessageBoxButton.OK, MessageBoxImage.Error);
                CurrentOpenShift = null;
            }
        }

        public bool OpenShift(User openingUser, decimal startCash)
        {
            if (CurrentOpenShift != null)
            {
                MessageBox.Show($"Смена №{CurrentOpenShift.ShiftNumber} уже открыта.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            if (openingUser == null)
            {
                MessageBox.Show("Ошибка: Не определен пользователь для открытия смены.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (startCash < 0)
            {
                MessageBox.Show("Начальная сумма наличных не может быть отрицательной.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            try
            {
                CurrentOpenShift = _shiftRepository.OpenShift(openingUser.UserId, startCash);
                ShiftOpened?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (InvalidOperationException invEx)
            {
                MessageBox.Show(invEx.Message, "Ошибка открытия смены", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть смену: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public async Task<Shift> CloseShiftAsync(User closingUser, decimal endCashActual)
        {
            if (CurrentOpenShift == null)
            {
                MessageBox.Show("Нет открытой смены для закрытия.", "Информация", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
            if (closingUser == null)
            {
                MessageBox.Show("Ошибка: Не определен пользователь для закрытия смены.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
            if (endCashActual < 0)
            {
                MessageBox.Show("Фактическая сумма наличных не может быть отрицательной.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            try
            {
                int shiftIdToClose = CurrentOpenShift.ShiftId;
                bool closed = await Task.Run(() => _shiftRepository.CloseShift(shiftIdToClose, closingUser.UserId, endCashActual));

                if (closed)
                {
                    var closedShiftData = _shiftRepository.GetShiftById(shiftIdToClose);

                    CurrentOpenShift = null;
                    ShiftClosed?.Invoke(this, EventArgs.Empty);

                    return closedShiftData;
                }
                else
                {
                    MessageBox.Show("Не удалось закрыть смену (возможно, она уже была закрыта или произошла ошибка).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }
            }
            catch (InvalidOperationException invEx)
            {
                MessageBox.Show(invEx.Message, "Ошибка закрытия смены", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при закрытии смены: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        public bool PerformCashIn(User user, decimal amount, string reason)
        {
            if (CurrentOpenShift == null)
            {
                MessageBox.Show("Внесение невозможно: смена не открыта.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (user == null)
            {
                MessageBox.Show("Внесение невозможно: пользователь не определен.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (amount <= 0)
            {
                MessageBox.Show("Сумма внесения должна быть положительной.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            try
            {
                var operation = new CashDrawerOperation
                {
                    ShiftId = CurrentOpenShift.ShiftId,
                    UserId = user.UserId,
                    OperationType = "CashIn",
                    Amount = amount,
                    Reason = reason,
                    Timestamp = DateTime.Now
                };
                _cashDrawerRepository.AddOperation(operation);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при внесении наличных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public async Task<bool> PerformCashOut(User user, decimal amount, string reason)
        {
            if (CurrentOpenShift == null)
            {
                MessageBox.Show("Изъятие невозможно: смена не открыта.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (user == null)
            {
                MessageBox.Show("Изъятие невозможно: пользователь не определен.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (amount <= 0)
            {
                MessageBox.Show("Сумма изъятия должна быть положительной.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            try
            {

                decimal currentCash = await _reportService.CalculateCurrentCashInDrawerAsync(CurrentOpenShift);

                if (amount > currentCash)
                {
                    MessageBox.Show($"Изъятие невозможно. Сумма изъятия ({amount:C}) превышает количество наличных в кассе ({currentCash:C}).",
                                    "Недостаточно средств", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                var operation = new CashDrawerOperation
                {
                    ShiftId = CurrentOpenShift.ShiftId,
                    UserId = user.UserId,
                    OperationType = "CashOut",
                    Amount = amount,
                    Reason = reason,
                    Timestamp = DateTime.Now
                };
                _cashDrawerRepository.AddOperation(operation);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при изъятии наличных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}