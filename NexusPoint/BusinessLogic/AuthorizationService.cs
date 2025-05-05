using NexusPoint.Models;
using NexusPoint.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace NexusPoint.BusinessLogic
{
    public class AuthorizationService
    {
        /// <summary>
        /// Показывает окно входа для авторизации действия.
        /// </summary>
        /// <param name="actionName">Название действия для заголовка окна.</param>
        /// <param name="allowedRoles">Массив ролей, которым разрешено это действие.</param>
        /// <param name="owner">Окно-владелец для модального диалога.</param>
        /// <returns>Объект User авторизованного пользователя или null, если авторизация не удалась или была отменена.</returns>
        public User AuthorizeAction(string actionName, string[] allowedRoles, Window owner)
        {
            if (allowedRoles == null || !allowedRoles.Any())
            {
                MessageBox.Show("Действие не настроено для выполнения.", "Ошибка конфигурации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            var authWindow = new LoginWindow(); // Создаем без предзаполнения логина
            authWindow.Owner = owner;
            authWindow.Title = $"Авторизация: {actionName}"; // Меняем заголовок

            if (authWindow.ShowDialog() == true)
            {
                if (authWindow.AuthenticatedUser != null &&
                    allowedRoles.Contains(authWindow.AuthenticatedUser.Role))
                {
                    return authWindow.AuthenticatedUser; // Успех
                }
                else
                {
                    string requiredRolesString = string.Join(", ", allowedRoles);
                    string usernameAttempted = authWindow.AuthenticatedUser?.Username ?? "???";
                    MessageBox.Show($"У пользователя '{usernameAttempted}' недостаточно прав для выполнения действия '{actionName}'.\nТребуемые роли: {requiredRolesString}",
                                    "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null; // Недостаточно прав
                }
            }
            else
            {
                return null; // Окно авторизации было закрыто (Отмена)
            }
        }
    }
}