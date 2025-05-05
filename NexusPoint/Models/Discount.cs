using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NexusPoint.Models
{
    public class Discount
    {
        public int DiscountId { get; set; }
        public string Name { get; set; } // Название акции
        public string Type { get; set; } // Тип ("Процент", "Сумма", "Подарок", "Фикс. цена", "N+M Подарок", "Скидка на N-ный", "Скидка на сумму чека")

        // --- Общие поля ---
        public string Description { get; set; }
        public bool IsActive { get; set; } = true; // Активна ли
        public DateTime? StartDate { get; set; } // Дата начала
        public DateTime? EndDate { get; set; }   // Дата окончания

        // --- Поля для разных типов ---

        // Для: Процент, Сумма, Фикс. цена, Скидка на N-ный (сумма/%), Скидка на сумму чека (сумма/%)
        public decimal? Value { get; set; } // Значение (процент, сумма, фикс.цена)

        // Для: Процент, Сумма, Фикс. цена, Подарок (опц.), N+M Подарок, Скидка на N-ный
        // Если NULL - скидка/условие действует на весь чек или любой товар (зависит от типа)
        public int? RequiredProductId { get; set; } // ID основного товара (условия)

        // Для: Подарок, N+M Подарок
        public int? GiftProductId { get; set; } // ID товара-подарка

        // Для: N+M Подарок
        public int? RequiredQuantityN { get; set; } // Сколько нужно купить (N)
        public int? GiftQuantityM { get; set; }     // Сколько дается в подарок (M)

        // Для: Скидка на N-ный
        public int? NthItemNumber { get; set; } // Номер N-ного товара, к которому применяется скидка
        public bool IsNthDiscountPercentage { get; set; } // Скидка на N-ный в процентах (true) или суммой (false)?

        // Для: Скидка на сумму чека
        public decimal? CheckAmountThreshold { get; set; } // Пороговая сумма чека
        public bool IsCheckDiscountPercentage { get; set; } // Скидка на чек в процентах (true) или суммой (false)?
    }
}