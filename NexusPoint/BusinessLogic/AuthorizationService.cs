using NexusPoint.Models;
using NexusPoint.Windows;
using System;
using System.Linq;
using System.Windows;

namespace NexusPoint.BusinessLogic
{
    public class AuthorizationService
    {
        public User AuthorizeAction(string actionName, string[] allowedRoles, Window owner)
        {
            if (allowedRoles == null || !allowedRoles.Any())
            {
                MessageBox.Show("Действие не настроено для выполнения.", "Ошибка конфигурации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            var authWindow = new LoginWindow();
            authWindow.Owner = owner;
            authWindow.Title = $"Авторизация: {actionName}";

            if (authWindow.ShowDialog() == true)
            {
                if (authWindow.AuthenticatedUser != null &&
                    allowedRoles.Contains(authWindow.AuthenticatedUser.Role))
                {
                    return authWindow.AuthenticatedUser;
                }
                else
                {
                    string requiredRolesString = string.Join(", ", allowedRoles);
                    string usernameAttempted = authWindow.AuthenticatedUser?.Username ?? "???";
                    MessageBox.Show($"У пользователя '{usernameAttempted}' недостаточно прав для выполнения действия '{actionName}'.\nТребуемые роли: {requiredRolesString}",
                                    "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
            }
            else
            {
                return null;
            }
        }
    }
}