using NexusPoint.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace NexusPoint
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Опционально: Хранение залогиненного пользователя
        // public Models.User LoggedInUser { get; set; }

        public MainWindow()
        {
            InitializeComponent();
        }

        private void CashierModeButton_Click(object sender, RoutedEventArgs e)
        {
            // --- Сначала Логин ---
            LoginWindow loginWindow = new LoginWindow();
            // Показываем окно логина как диалоговое (блокирует MainWindow)
            if (loginWindow.ShowDialog() == true)
            {
                // Логин успешен, loginWindow.AuthenticatedUser содержит данные пользователя
                Models.User loggedInUser = loginWindow.AuthenticatedUser;

                // Проверяем роль (если нужно)
                if (loggedInUser.Role == "Cashier" || loggedInUser.Role == "Admin" || loggedInUser.Role == "Manager") // Менеджеры тоже могут на кассу?
                {
                    // Открываем окно кассира
                    CashierWindow cashierWindow = new CashierWindow(loggedInUser); // Передаем пользователя
                    cashierWindow.Show(); // Показываем новое окно

                    this.Close(); // Закрываем текущее окно выбора режима
                }
                else
                {
                    MessageBox.Show("У вас нет прав для доступа к режиму кассы.", "Ошибка доступа", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            // Если ShowDialog() вернул false или null, окно логина просто закрыли
        }

        private void ManagerModeButton_Click(object sender, RoutedEventArgs e)
        {
            // --- Сначала Логин ---
            LoginWindow loginWindow = new LoginWindow();
            if (loginWindow.ShowDialog() == true)
            {
                Models.User loggedInUser = loginWindow.AuthenticatedUser;

                // Проверяем роль (только менеджер или админ)
                if (loggedInUser.Role == "Manager" || loggedInUser.Role == "Admin")
                {
                    // Открываем окно управления
                    ManagerWindow managerWindow = new ManagerWindow(loggedInUser); // Передаем пользователя
                    managerWindow.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show("У вас нет прав для доступа к режиму управления.", "Ошибка доступа", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }
}