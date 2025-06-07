using NexusPoint.Windows;
using System.Windows;

namespace NexusPoint
{
    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            InitializeComponent();
        }

        private void CashierModeButton_Click(object sender, RoutedEventArgs e)
        {
            LoginWindow loginWindow = new LoginWindow();
            if (loginWindow.ShowDialog() == true)
            {
                Models.User loggedInUser = loginWindow.AuthenticatedUser;
                if (loggedInUser.Role == "Cashier" || loggedInUser.Role == "Admin" || loggedInUser.Role == "Manager")
                {
                    CashierWindow cashierWindow = new CashierWindow(loggedInUser);
                    cashierWindow.Show();

                    this.Close();
                }
                else
                {
                    MessageBox.Show("У вас нет прав для доступа к режиму кассы.", "Ошибка доступа", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void ManagerModeButton_Click(object sender, RoutedEventArgs e)
        {
            LoginWindow loginWindow = new LoginWindow();
            if (loginWindow.ShowDialog() == true)
            {
                Models.User loggedInUser = loginWindow.AuthenticatedUser;
                if (loggedInUser.Role == "Manager" || loggedInUser.Role == "Admin")
                {
                    ManagerWindow managerWindow = new ManagerWindow(loggedInUser);
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