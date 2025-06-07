using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NexusPoint.Models
{
    public class ReturnItemView : INotifyPropertyChanged
    {
        public CheckItem OriginalItem { get; }
        public Product Product { get; }
        public int ProductId => OriginalItem.ProductId;

        private decimal _returnQuantity;
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
                    validatedValue = OriginalItem.Quantity;
                }
                if (_returnQuantity != validatedValue)
                {
                    _returnQuantity = validatedValue;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ReturnItemTotalAmount));
                }
            }
        }

        private bool _canEditReturnQuantity = true;
        public bool CanEditReturnQuantity
        {
            get => _canEditReturnQuantity;
            private set { _canEditReturnQuantity = value; OnPropertyChanged(); }
        }
        public string ProductName => Product?.Name ?? "<Товар не найден>";
        public decimal Quantity => OriginalItem.Quantity;
        public decimal PriceAtSale => OriginalItem.PriceAtSale;
        public decimal DiscountAmount => OriginalItem.DiscountAmount;
        public decimal OriginalItemTotalAmount => Math.Round(OriginalItem.Quantity * OriginalItem.PriceAtSale - OriginalItem.DiscountAmount, 2);
        public decimal DiscountPerUnit => OriginalItem.Quantity > 0 ? Math.Round(OriginalItem.DiscountAmount / OriginalItem.Quantity, 2) : 0m;
        public decimal ReturnItemTotalAmount
        {
            get
            {
                if (OriginalItem.Quantity == 0) return 0m;
                decimal originalTotal = OriginalItem.Quantity * OriginalItem.PriceAtSale - OriginalItem.DiscountAmount;
                decimal ratio = ReturnQuantity / OriginalItem.Quantity;
                return Math.Round(originalTotal * ratio, 2);
            }
        }
        public ReturnItemView(CheckItem baseItem, Product product)
        {
            OriginalItem = baseItem ?? throw new ArgumentNullException(nameof(baseItem));
            Product = product;
            this._returnQuantity = 0m;
            this.CanEditReturnQuantity = true;
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}