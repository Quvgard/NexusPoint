using NexusPoint.Data.Repositories;
using NexusPoint.Models;
using System;
using System.Collections.Generic;
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
        public bool AddUser(User user, string plainPassword)
        {
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
            if (string.IsNullOrWhiteSpace(user.Username) || string.IsNullOrWhiteSpace(user.FullName) || string.IsNullOrEmpty(user.Role))
            {
                MessageBox.Show("Логин, ФИО и Роль обязательны для заполнения.", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            var existingUser = GetUserByUsername(user.Username);
            if (existingUser != null && existingUser.UserId != user.UserId)
            {
                MessageBox.Show($"Пользователь с логином '{user.Username}' уже существует.", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (!string.IsNullOrEmpty(newPlainPassword) && newPlainPassword.Length < 4)
            {
                MessageBox.Show("Новый пароль должен содержать не менее 4 символов.", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            try
            {
                bool success = _userRepository.UpdateUser(user);
                if (success && !string.IsNullOrEmpty(newPlainPassword))
                {
                    success = _userRepository.UpdateUserPassword(user.UserId, newPlainPassword);
                    if (!success)
                    {
                        MessageBox.Show($"Не удалось обновить пароль для пользователя {user.Username}.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                return success;
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
                return _userRepository.DeleteUser(userId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении пользователя: {ex.Message}\nВозможно, пользователь связан с записями чеков или смен.", "Ошибка удаления", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}