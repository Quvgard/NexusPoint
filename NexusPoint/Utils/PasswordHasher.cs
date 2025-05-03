using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NexusPoint.Utils
{
    public static class PasswordHasher
    {
        public static string HashPassword(string password)
        {
            // Генерируем соль и хешируем пароль
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        public static bool VerifyPassword(string password, string hashedPassword)
        {
            try
            {
                // Проверяем соответствие пароля хешу
                return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
            }
            catch (BCrypt.Net.SaltParseException) // Обработка некорректного хеша
            {
                System.Diagnostics.Debug.WriteLine("Password hash verification failed: Invalid salt format.");
                return false;
            }
            catch (System.ArgumentException ex) // Обработка других ошибок верификации
            {
                System.Diagnostics.Debug.WriteLine($"Password hash verification error: {ex.Message}");
                return false;
            }
        }
    }
}