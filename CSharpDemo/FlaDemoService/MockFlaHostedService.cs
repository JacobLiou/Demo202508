public class MockFlaHostedService : BackgroundService
{
    private readonly IConfiguration _cfg; private MockFlaServer? _server;

    public MockFlaHostedService(IConfiguration cfg)
    {
        _cfg = cfg;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _cfg.GetValue("Mock:Enabled", false);
        if (!enabled) return;
        var port = _cfg.GetValue("Mock:Port", 4300);
        var res = _cfg.GetValue("Mock:ResolutionM", 0.005);
        var pts = _cfg.GetValue("Mock:Points", 200);
        _server = new MockFlaServer(port, res, pts, true);
        await _server.RunAsync(stoppingToken);
    }
}