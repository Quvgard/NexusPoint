using System.Windows;

namespace NexusPoint
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Data.DatabaseHelper.InitializeDatabaseIfNotExists();
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}