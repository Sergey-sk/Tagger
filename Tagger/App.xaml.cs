using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Configuration;
using System.Data;
using System.Windows;
using Tagger.services.implementations;
using Tagger.services.interfaces;
using Tagger.viewmodel;

namespace Tagger
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly IHost _host;

        public App()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddDbContextFactory<ApplicationDbContext>(options =>
                    {
                        var connectionString = context.Configuration.GetConnectionString("DefaultConnection");
                        options.UseSqlite(connectionString);
                    });

                    services.AddSingleton<IDialogService, DialogService>();
                    services.AddSingleton<MainViewModel>();

                    services.AddSingleton<ITagService, TagService>();
                    services.AddSingleton<IFileIndexingService, FileIndexingService>();
                    services.AddSingleton<IScanningService, ScanningService>();
                    services.AddSingleton<ISavedSearchService, SavedSearchService>();
                    services.AddSingleton<IFileService, FileService>();
                    services.AddSingleton<IFileTagService, FileTagService>();

                    services.AddSingleton<MainWindow>();
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                await _host.StartAsync();

                var contextFactory = _host.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();

                using (var context = await contextFactory.CreateDbContextAsync())
                {
                    await context.Database.EnsureDeletedAsync();
                    await context.Database.EnsureCreatedAsync();
                }

                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при запуске приложения: {ex.Message}", "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            using (_host)
            {
                await _host.StopAsync();
            }

            base.OnExit(e);
        }
    }

}
