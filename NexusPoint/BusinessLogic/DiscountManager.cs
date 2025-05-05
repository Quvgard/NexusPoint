using NexusPoint.Data.Repositories;
using NexusPoint.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace NexusPoint.BusinessLogic
{
    public class DiscountManager
    {
        private readonly DiscountRepository _discountRepository;

        public DiscountManager(DiscountRepository discountRepository)
        {
            _discountRepository = discountRepository ?? throw new ArgumentNullException(nameof(discountRepository));
        }

        // --- Получение данных ---
        public async Task<IEnumerable<Discount>> GetDiscountsAsync()
        {
            try
            {
                return await Task.Run(() => _discountRepository.GetAllDiscounts());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки списка акций: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<Discount>();
            }
        }

        // --- Операции CRUD ---
        public bool AddDiscount(Discount discount)
        {
            // Дополнительная валидация (если нужна, базовая делается в окне)
            try
            {
                return _discountRepository.AddDiscount(discount) > 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка добавления акции: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public bool UpdateDiscount(Discount discount)
        {
            // Дополнительная валидация (если нужна)
            try
            {
                return _discountRepository.UpdateDiscount(discount);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обновления акции: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public bool DeleteDiscount(int discountId)
        {
            try
            {
                // Показ подтверждения лучше делать в UI
                return _discountRepository.DeleteDiscount(discountId);
            }
            catch (Exception ex)
            {
                // TODO: Обработать FK ошибки, если скидка используется в CheckItems
                MessageBox.Show($"Ошибка удаления акции: {ex.Message}\nВозможно, акция уже применена в каких-то чеках.", "Ошибка удаления", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}