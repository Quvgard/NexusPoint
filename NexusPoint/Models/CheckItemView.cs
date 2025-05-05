using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace NexusPoint.Models
{
    public class CheckItemView : CheckItem, INotifyPropertyChanged // Наследуем от CheckItem
    {
        private Product _product;
        public Product Product
        {
            get => _product;
            set { _product = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProductName)); }
        }

        // --- Переопределяем свойства из CheckItem ---
        private decimal _quantity;
        public new decimal Quantity // Используем new
        {
            get => _quantity;
            set
            {
                // Добавим базовую валидацию > 0 при установке
                if (value > 0)
                {
                    _quantity = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CalculatedItemTotalAmount));
                }
            }
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
            set
            {
                // Убедимся, что скидка не больше суммы до скидки при установке
                decimal maxDiscount = this._quantity * this._priceAtSale;
                _discountAmount = Math.Max(0, Math.Min(maxDiscount, value)); // Ограничиваем 0 <= discount <= total
                OnPropertyChanged();
                OnPropertyChanged(nameof(CalculatedItemTotalAmount));
            }
        }

        // Добавляем AppliedDiscountId с уведомлением
        private int? _appliedDiscountId;
        public new int? AppliedDiscountId
        {
            get => _appliedDiscountId;
            set { _appliedDiscountId = value; OnPropertyChanged(); }
        }


        // --- Свойства только для отображения ---
        public string ProductName => Product?.Name ?? "<Товар не найден>";
        public decimal CalculatedItemTotalAmount => Math.Round(Quantity * PriceAtSale - DiscountAmount, 2); // Округляем здесь

        // --- Реализация INotifyPropertyChanged ---
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // --- Конструктор для копирования ---
        public CheckItemView(CheckItem baseItem, Product product)
        {
            if (baseItem == null) throw new ArgumentNullException(nameof(baseItem));

            // Копируем ID
            this.CheckItemId = baseItem.CheckItemId;
            this.CheckId = baseItem.CheckId;
            this.ProductId = baseItem.ProductId;

            // Присваиваем значения приватным полям напрямую, чтобы не вызвать лишние OnPropertyChanged в конструкторе
            this._quantity = baseItem.Quantity;
            this._priceAtSale = baseItem.PriceAtSale;
            this._discountAmount = baseItem.DiscountAmount;
            this._appliedDiscountId = baseItem.AppliedDiscountId;

            // Устанавливаем продукт (это вызовет OnPropertyChanged для Product и ProductName)
            this.Product = product;

            // Пересчитываем ItemTotalAmount один раз после инициализации всех полей
            // (Хотя CalculatedItemTotalAmount делает это сам, но на всякий случай)
            // baseItem.ItemTotalAmount не копируем, т.к. оно вычисляемое
        }

        // Пустой конструктор (может быть нужен WPF)
        public CheckItemView() { }
    }
}