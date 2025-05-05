using NexusPoint.Data.Repositories;
using NexusPoint.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace NexusPoint.BusinessLogic
{
    public class UserManager
    {
        private readonly UserRepository _userRepository;

        public UserManager(UserRepository userRepository)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        }

        // --- Получение данных ---
        public async Task<IEnumerable<User>> GetUsersAsync()
        {
            try
            {
                return await Task.Run(() => _userRepository.GetAllUsers());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки списка пользователей: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<User>();
            }
        }

        public User GetUserByUsername(string username)
        {
            try
            {
                return _userRepository.GetUserByUsername(username);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка поиска пользователя '{username}': {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        // --- Операции CRUD ---
        public bool AddUser(User user, string plainPassword)
        {
            // Валидация
            if (string.IsNullOrWhiteSpace(user.Username) || string.IsNullOrWhiteSpace(user.FullName) || string.IsNullOrEmpty(user.Role))
            {
                MessageBox.Show("Логин, ФИО и Роль обязательны для заполнения.", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (string.IsNullOrWhiteSpace(plainPassword) || plainPassword.Length < 4)
            {
                MessageBox.Show("Пароль обязателен (мин. 4 символа) при создании пользователя.", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            // Проверка уникальности
            if (GetUserByUsername(user.Username) != null)
            {
                MessageBox.Show($"Пользователь с логином '{user.Username}' уже существует.", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            try
            {
                int newId = _userRepository.AddUser(user, plainPassword);
                return newId > 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка добавления пользователя: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public bool UpdateUser(User user, string newPlainPassword = null)
        {
            // Валидация
            if (string.IsNullOrWhiteSpace(user.Username) || string.IsNullOrWhiteSpace(user.FullName) || string.IsNullOrEmpty(user.Role))
            {
                MessageBox.Show("Логин, ФИО и Роль обязательны для заполнения.", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            // Проверка уникальности логина (кроме себя)
            var existingUser = GetUserByUsername(user.Username);
            if (existingUser != null && existingUser.UserId != user.UserId)
            {
                MessageBox.Show($"Пользователь с логином '{user.Username}' уже существует.", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            // Валидация нового пароля (если он введен)
            if (!string.IsNullOrEmpty(newPlainPassword) && newPlainPassword.Length < 4)
            {
                MessageBox.Show("Новый пароль должен содержать не менее 4 символов.", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            try
            {
                bool success = _userRepository.UpdateUser(user); // Обновляем основные данные
                if (success && !string.IsNullOrEmpty(newPlainPassword))
                {
                    // Обновляем пароль, если нужно и основные данные обновились
                    success = _userRepository.UpdateUserPassword(user.UserId, newPlainPassword);
                    if (!success)
                    {
                        MessageBox.Show($"Не удалось обновить пароль для пользователя {user.Username}.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        // Основные данные уже обновлены, но сообщаем об ошибке пароля
                    }
                }
                return success; // Возвращаем успех обновления основных данных
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обновления пользователя: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public bool ResetUserPassword(int userId, string newPlainPassword)
        {
            if (string.IsNullOrWhiteSpace(newPlainPassword) || newPlainPassword.Length < 4)
            {
                MessageBox.Show("Пароль не может быть пустым и должен содержать не менее 4 символов.", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            try
            {
                return _userRepository.UpdateUserPassword(userId, newPlainPassword);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сбросе пароля: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }


        public bool DeleteUser(int userId, int currentUserId)
        {
            if (userId == currentUserId)
            {
                MessageBox.Show("Нельзя удалить текущего пользователя.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            try
            {
                // Подумать об обработке FK ошибок, если пользователь связан с чеками/сменами
                // Показ подтверждения лучше делать в UI
                return _userRepository.DeleteUser(userId);
            }
            catch (Exception ex)
            {
                // TODO: Обработать специфичные ошибки FK (SQLiteException с кодом 19 = Constraint)
                MessageBox.Show($"Ошибка при удалении пользователя: {ex.Message}\nВозможно, пользователь связан с записями чеков или смен.", "Ошибка удаления", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}