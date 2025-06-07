using Humanizer;
using System;
using System.Globalization;

namespace NexusPoint.Utils.Converters
{
    public static class AmountToWordsConverter
    {
        private static readonly CultureInfo RussianCulture = new CultureInfo("ru-RU");
        public static string Convert(decimal amount, bool includePennies = true)
        {
            try
            {
                long rubles = (long)Math.Truncate(amount);
                int pennies = (int)Math.Round((amount - rubles) * 100);

                string rubleWord = ((long)rubles).ToWords(RussianCulture);
                string rubleUnit = ChooseCurrencyForm(rubles, "рубль", "рубля", "рублей");
                string result = $"{rubleWord} {rubleUnit}";

                if (includePennies)
                {
                    string pennyUnit = ChooseCurrencyForm(pennies, "копейка", "копейки", "копеек");
                    result += $" {pennies:00} {pennyUnit}";
                }
                if (!string.IsNullOrEmpty(result))
                {
                    result = char.ToUpper(result[0]) + result.Substring(1);
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AmountToWords conversion error: {ex.Message}");
                return amount.ToString("N2");
            }
        }
        public static string Convert(int amount)
        {
            return Convert((decimal)amount, false);
        }
        private static string ChooseCurrencyForm(long number, string form1, string form2, string form5)
        {
            number = Math.Abs(number) % 100;
            long lastDigit = number % 10;

            if (number > 10 && number < 20)
            {
                return form5;
            }
            if (lastDigit > 1 && lastDigit < 5)
            {
                return form2;
            }
            if (lastDigit == 1)
            {
                return form1;
            }
            return form5;
        }

    }
}