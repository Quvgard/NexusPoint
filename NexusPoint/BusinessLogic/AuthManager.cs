using NexusPoint.Data.Repositories;
using NexusPoint.Models;
using NexusPoint.Utils;
using System;
using System.Threading.Tasks;

namespace NexusPoint.BusinessLogic
{
    public enum AuthResultStatus { Success, UserNotFound, InvalidPassword, Error }

    public class AuthenticationResult
    {
        public AuthResultStatus Status { get; }
        public User AuthenticatedUser { get; }
        public string ErrorMessage { get; }

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

        public async Task<AuthenticationResult> AuthenticateUserAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            {
                return AuthenticationResult.ErrorResult("Имя пользователя и пароль не могут быть пустыми.");
            }

            try
            {
                User user = await Task.Run(() => _userRepository.GetUserByUsername(username));

                if (user == null)
                {
                    return AuthenticationResult.UserNotFoundResult();
                }

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
                System.Diagnostics.Debug.WriteLine($"Authentication error for user '{username}': {ex}");
                return AuthenticationResult.ErrorResult($"Произошла ошибка при входе: {ex.Message}");
            }
        }
    }
}