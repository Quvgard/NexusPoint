using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace NexusPoint.Models
{
    public class ReturnItemView : INotifyPropertyChanged
    {
        public CheckItem OriginalItem { get; }
        public Product Product { get; }
        public int ProductId => OriginalItem.ProductId;

        private decimal _returnQuantity;
        /// <summary>
        /// Количество товара, выбранное для возврата.
        /// Не может быть больше, чем было продано (OriginalItem.Quantity).
        /// </summary>
        public decimal ReturnQuantity
        {
            get => _returnQuantity;
            set
            {
                decimal validatedValue = 0m;
                if (value >= 0 && value <= OriginalItem.Quantity)
                {
                    validatedValue = value;
                }
                else if (value > OriginalItem.Quantity)
                {
                    validatedValue = OriginalItem.Quantity; // Максимум - сколько было
                }
                // Оставляем 0, если value < 0

                // Уведомляем только если значение действительно изменилось
                if (_returnQuantity != validatedValue)
                {
                    _returnQuantity = validatedValue;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ReturnItemTotalAmount)); // Сумма тоже изменится
                }
            }
        }

        private bool _canEditReturnQuantity = true; // По умолчанию редактирование разрешено
        /// <summary>
        /// Указывает, можно ли редактировать количество возврата для этой позиции.
        /// (Может использоваться для маркированных товаров в будущем).
        /// </summary>
        public bool CanEditReturnQuantity
        {
            get => _canEditReturnQuantity;
            // Сделаем сеттер приватным, чтобы он не менялся снаружи без логики
            private set { _canEditReturnQuantity = value; OnPropertyChanged(); }
        }

        // --- Свойства для отображения из оригинального элемента ---
        public string ProductName => Product?.Name ?? "<Товар не найден>";
        /// <summary>
        /// Исходное количество товара в чеке продажи.
        /// </summary>
        public decimal Quantity => OriginalItem.Quantity;
        /// <summary>
        /// Цена товара на момент продажи.
        /// </summary>
        public decimal PriceAtSale => OriginalItem.PriceAtSale;
        /// <summary>
        /// Общая сумма скидки на всю позицию в чеке продажи.
        /// </summary>
        public decimal DiscountAmount => OriginalItem.DiscountAmount;
        /// <summary>
        /// Итоговая сумма по позиции в чеке продажи (Цена * Кол-во - Скидка).
        /// </summary>
        public decimal OriginalItemTotalAmount => Math.Round(OriginalItem.Quantity * OriginalItem.PriceAtSale - OriginalItem.DiscountAmount, 2);
        /// <summary>
        /// Скидка на единицу товара в чеке продажи.
        /// </summary>
        public decimal DiscountPerUnit => OriginalItem.Quantity > 0 ? Math.Round(OriginalItem.DiscountAmount / OriginalItem.Quantity, 2) : 0m;


        /// <summary>
        /// Рассчитываемая сумма для возвращаемого количества (ReturnQuantity).
        /// Учитывает пропорционально цену и скидку оригинала.
        /// </summary>
        public decimal ReturnItemTotalAmount
        {
            get
            {
                // Рассчитываем пропорционально от СУММЫ ОРИГИНАЛЬНОЙ ПОЗИЦИИ
                // Это проще и точнее, чем считать через скидку на единицу
                if (OriginalItem.Quantity == 0) return 0m;
                decimal originalTotal = OriginalItem.Quantity * OriginalItem.PriceAtSale - OriginalItem.DiscountAmount;
                decimal ratio = ReturnQuantity / OriginalItem.Quantity;
                return Math.Round(originalTotal * ratio, 2);

                // Альтернативный расчет (менее точный из-за двойного округления скидки):
                // decimal pricePerUnit = OriginalItem.PriceAtSale;
                // decimal discountPerUnit = DiscountPerUnit; // Используем уже рассчитанное свойство
                // return Math.Round((pricePerUnit - discountPerUnit) * ReturnQuantity, 2);
            }
        }


        // Конструктор
        public ReturnItemView(CheckItem baseItem, Product product)
        {
            OriginalItem = baseItem ?? throw new ArgumentNullException(nameof(baseItem));
            Product = product; // Может быть null, если товар удален

            // Изначально количество к возврату 0
            this._returnQuantity = 0m;
            // Устанавливаем возможность редактирования (пока всегда true)
            this.CanEditReturnQuantity = true; // Можно добавить логику позже
        }


        // --- Реализация INotifyPropertyChanged ---
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}