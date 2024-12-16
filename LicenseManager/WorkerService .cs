namespace LicenseManager
{
    public class WorkerService : BackgroundService
    {
        private readonly ILogger<WorkerService> _logger;
        private readonly ActivationManager _activationManager;
        private readonly ActivationState _activationState;

        public WorkerService(ActivationManager activationManager, ActivationState activationState, ILogger<WorkerService> logger)
        {
            _activationManager = activationManager;
            _logger = logger;
            _activationState = activationState;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                CheckHardwareIfActivated();
            }
            catch
            {
                _logger.LogWarning("Hardware checking Failed.");
                _activationManager.Deactivate();
            }
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Background worker running at: {time}", DateTimeOffset.Now);

                    //CheckHardwareIfActivated();

                    await Task.Delay(10000, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker service encountered an error and will stop.");
                throw;
            }
        }

        private void CheckHardwareIfActivated()
        {
            var activationState = _activationState.LoadFromFile();

            if (!activationState.IsActivated)
            {
                _logger.LogInformation("System is not activated. Hardware check is not required.");
                return;
            }

            var savedHardwareInfo = activationState.HardwareInfo;
            var currentHardwareInfo = HardwareInfo.GetHardwareInfo();

            if (string.IsNullOrEmpty(savedHardwareInfo) || string.IsNullOrEmpty(currentHardwareInfo) || !HardwareMatches(savedHardwareInfo, currentHardwareInfo))
            {
                _logger.LogWarning("Hardware mismatch detected. Deactivating the application.");
                _activationManager.Deactivate();
                return;
            }

            _logger.LogInformation("Hardware validation passed. Worker service continues.");
        }

        private bool HardwareMatches(string savedHardwareInfo, string currentHardwareInfo)
        {
            return string.Equals(savedHardwareInfo, currentHardwareInfo, StringComparison.OrdinalIgnoreCase);
        }
    }
}
