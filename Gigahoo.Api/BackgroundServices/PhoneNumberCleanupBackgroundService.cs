using Gigahoo.Api.Services;

namespace Gigahoo.Api.BackgroundServices;

public class PhoneNumberCleanupBackgroundService(
    IServiceProvider serviceProvider,
    ILogger<PhoneNumberCleanupBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Phone number cleanup background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Run cleanup every 24 hours
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);

                using var scope = serviceProvider.CreateScope();
                var cleanupService = scope.ServiceProvider.GetRequiredService<IPhoneNumberCleanupService>();

                // Release numbers from accounts inactive for 30 days
                await cleanupService.CleanupInactiveNumbersAsync(inactiveDays: 30);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during phone number cleanup");
                // Wait before retrying
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        logger.LogInformation("Phone number cleanup background service stopped");
    }
}
