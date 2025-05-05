using Humanizer.Configuration;
using Humanizer;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NexusPoint.Utils.Converters
{
    public static class AmountToWordsConverter
    {
        // Локаль для конвертации (можно вынести в настройки)
        private static readonly CultureInfo RussianCulture = new CultureInfo("ru-RU");

        /// <summary>
        /// Конвертирует денежную сумму в строку прописью на русском языке.
        /// </summary>
        /// <param name="amount">Сумма.</param>
        /// <param name="includePennies">Включать ли копейки в пропись.</param>
        /// <returns>Сумма прописью.</returns>
        public static string Convert(decimal amount, bool includePennies = true)
        {
            try
            {
                // Отделяем рубли и копейки
                long rubles = (long)Math.Truncate(amount);
                int pennies = (int)Math.Round((amount - rubles) * 100); // Округляем копейки

                // --- ИСПОЛЬЗУЕМ ТОЛЬКО УПРОЩЕННЫЙ ВАРИАНТ ---

                string rubleWord = ((long)rubles).ToWords(RussianCulture); // Получаем число прописью
                string rubleUnit = ChooseCurrencyForm(rubles, "рубль", "рубля", "рублей"); // Склоняем "рубль"
                string result = $"{rubleWord} {rubleUnit}"; // Собираем рубли

                if (includePennies)
                {
                    // string pennyWord = pennies.ToWords(RussianCulture); // Число копеек прописью обычно не нужно в РКО
                    string pennyUnit = ChooseCurrencyForm(pennies, "копейка", "копейки", "копеек"); // Склоняем "копейка"
                    result += $" {pennies:00} {pennyUnit}"; // Добавляем "XX копеек"
                }
                /* --- КОНЕЦ УПРОЩЕННОГО ВАРИАНТА --- */

                // Делаем первую букву заглавной
                if (!string.IsNullOrEmpty(result))
                {
                    result = char.ToUpper(result[0]) + result.Substring(1);
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AmountToWords conversion error: {ex.Message}");
                return amount.ToString("N2"); // Возвращаем число в случае ошибки
            }
        }


        // Перегрузка для int
        public static string Convert(int amount)
        {
            return Convert((decimal)amount, false);
        }

        // --- Вспомогательный метод для склонения единиц валюты ---
        // (Простая реализация, можно улучшить)
        private static string ChooseCurrencyForm(long number, string form1, string form2, string form5)
        {
            number = Math.Abs(number) % 100; // Берем последние две цифры
            long lastDigit = number % 10;

            if (number > 10 && number < 20) // 11-19 рублей/копеек
            {
                return form5;
            }
            if (lastDigit > 1 && lastDigit < 5) // 2, 3, 4 рубля/копейки
            {
                return form2;
            }
            if (lastDigit == 1) // 1 рубль/копейка
            {
                return form1;
            }
            return form5; // 0, 5, 6, 7, 8, 9 рублей/копеек
        }

        // --- Пример расширения для Humanizer (если хотите сделать "чище") ---
        // (Это нужно поместить в отдельный статический класс)
        /*
        public static class HumanizerRussianCurrencyExtensions
        {
            public static string ToRussianRublesString(this long number)
            {
                 // Логика склонения слова "рубль" как в ChooseCurrencyForm
                 number = Math.Abs(number) % 100;
                 long lastDigit = number % 10;
                 if (number > 10 && number < 20) return "рублей";
                 if (lastDigit > 1 && lastDigit < 5) return "рубля";
                 if (lastDigit == 1) return "рубль";
                 return "рублей";
            }
             public static string ToRussianKopecksString(this int number)
            {
                 // Логика склонения слова "копейка"
                 number = Math.Abs(number) % 100;
                 int lastDigit = number % 10;
                 if (number > 10 && number < 20) return "копеек";
                 if (lastDigit > 1 && lastDigit < 5) return "копейки";
                 if (lastDigit == 1) return "копейка";
                 return "копеек";
            }

             // Основной метод расширения (если нужен)
             public static string ToRussianCurrencyWords(this decimal amount, CultureInfo culture)
             {
                  long rubles = (long)Math.Truncate(amount);
                  int pennies = (int)Math.Round((amount - rubles) * 100);

                  string rubleWord = ((long)rubles).ToWords(culture);
                  string rubleUnit = ((long)rubles).ToRussianRublesString();
                  string pennyUnit = pennies.ToRussianKopecksString();

                  string result = $"{rubleWord} {rubleUnit} {pennies:00} {pennyUnit}";
                  result = char.ToUpper(result[0]) + result.Substring(1);
                  return result;
             }
        }
        */

    } // Конец класса AmountToWordsConverter
} // Конец namespace