using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Hosting;
using Tagger.messages;

namespace Tagger
{
    public class BackgroundScaningService : BackgroundService
    {
        private readonly PeriodicTimer _scaningTimer = new(TimeSpan.FromMinutes(30));

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (await _scaningTimer.WaitForNextTickAsync(stoppingToken))
            {
                WeakReferenceMessenger.Default.Send(new StartBackroundScan());
            }
        }
    }
}
