using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Hosting;
using Tagger.messages;
using Tagger.services.interfaces;

namespace Tagger
{
    public class BackgroundScaningService : BackgroundService
    {
        private readonly PeriodicTimer _scaningTimer = new(TimeSpan.FromMinutes(30));
        private readonly IDialogService _dialogService;

        public BackgroundScaningService(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (await _scaningTimer.WaitForNextTickAsync(stoppingToken))
                {
                    WeakReferenceMessenger.Default.Send(new StartBackroundScan());
                }
            }
            catch (OperationCanceledException) { }
            catch(Exception ex)
            {
                _dialogService.ShowMessage($"Ошибка фонового таймера: {ex.Message}", "Ошибка!");
            }
        }
    }
}
