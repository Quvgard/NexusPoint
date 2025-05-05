using NexusPoint.Data.Repositories;
using NexusPoint.Models;
using NexusPoint.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NexusPoint.BusinessLogic
{
    // Результат аутентификации
    public enum AuthResultStatus { Success, UserNotFound, InvalidPassword, Error }

    public class AuthenticationResult
    {
        public AuthResultStatus Status { get; }
        public User AuthenticatedUser { get; }
        public string ErrorMessage { get; }

        // Конструкторы для разных результатов
        public static AuthenticationResult SuccessResult(User user) => new AuthenticationResult(AuthResultStatus.Success, user);
        public static AuthenticationResult UserNotFoundResult() => new AuthenticationResult(AuthResultStatus.UserNotFound, null, "Пользователь с таким именем не найден.");
        public static AuthenticationResult InvalidPasswordResult() => new AuthenticationResult(AuthResultStatus.InvalidPassword, null, "Неверный пароль.");
        public static AuthenticationResult ErrorResult(string message) => new AuthenticationResult(AuthResultStatus.Error, null, message);


        private AuthenticationResult(AuthResultStatus status, User user = null, string errorMessage = null)
        {
            Status = status;
            AuthenticatedUser = user;
            ErrorMessage = errorMessage ?? (status != AuthResultStatus.Success ? "Произошла ошибка аутентификации." : null);
        }
    }


    public class AuthManager
    {
        private readonly UserRepository _userRepository;

        public AuthManager(UserRepository userRepository)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        }

        /// <summary>
        /// Асинхронно аутентифицирует пользователя по имени и паролю.
        /// </summary>
        /// <param name="username">Имя пользователя.</param>
        /// <param name="password">Пароль в открытом виде.</param>
        /// <returns>Объект AuthenticationResult с результатом попытки входа.</returns>
        public async Task<AuthenticationResult> AuthenticateUserAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            {
                // Эта базовая валидация должна быть и в UI, но дублируем на всякий случай
                return AuthenticationResult.ErrorResult("Имя пользователя и пароль не могут быть пустыми.");
            }

            try
            {
                // Ищем пользователя асинхронно (оборачиваем синхронный вызов)
                User user = await Task.Run(() => _userRepository.GetUserByUsername(username));

                if (user == null)
                {
                    return AuthenticationResult.UserNotFoundResult();
                }

                // Проверяем пароль асинхронно (хотя сам хешер синхронный, обертка не помешает)
                bool passwordValid = await Task.Run(() => PasswordHasher.VerifyPassword(password, user.HashedPassword));

                if (passwordValid)
                {
                    return AuthenticationResult.SuccessResult(user);
                }
                else
                {
                    return AuthenticationResult.InvalidPasswordResult();
                }
            }
            catch (Exception ex)
            {
                // Логирование ошибки
                System.Diagnostics.Debug.WriteLine($"Authentication error for user '{username}': {ex}");
                return AuthenticationResult.ErrorResult($"Произошла ошибка при входе: {ex.Message}");
            }
        }
    }
}