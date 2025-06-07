using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NexusPoint.Models
{
    public class CheckItemView : CheckItem, INotifyPropertyChanged
    {
        private Product _product;
        public Product Product
        {
            get => _product;
            set { _product = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProductName)); }
        }
        private decimal _quantity;
        public new decimal Quantity
        {
            get => _quantity;
            set
            {
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
                decimal maxDiscount = this._quantity * this._priceAtSale;
                _discountAmount = Math.Max(0, Math.Min(maxDiscount, value));
                OnPropertyChanged();
                OnPropertyChanged(nameof(CalculatedItemTotalAmount));
            }
        }
        private int? _appliedDiscountId;
        public new int? AppliedDiscountId
        {
            get => _appliedDiscountId;
            set { _appliedDiscountId = value; OnPropertyChanged(); }
        }
        public string ProductName => Product?.Name ?? "<Товар не найден>";
        public decimal CalculatedItemTotalAmount => Math.Round(Quantity * PriceAtSale - DiscountAmount, 2);
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public CheckItemView(CheckItem baseItem, Product product)
        {
            if (baseItem == null) throw new ArgumentNullException(nameof(baseItem));
            this.CheckItemId = baseItem.CheckItemId;
            this.CheckId = baseItem.CheckId;
            this.ProductId = baseItem.ProductId;
            this._quantity = baseItem.Quantity;
            this._priceAtSale = baseItem.PriceAtSale;
            this._discountAmount = baseItem.DiscountAmount;
            this._appliedDiscountId = baseItem.AppliedDiscountId;
            this.Product = product;
        }
        public CheckItemView() { }
    }
}