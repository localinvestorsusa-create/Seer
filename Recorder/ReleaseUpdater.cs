using Velopack;
using Velopack.Sources;

namespace Recorder;

// Checks public GitHub Releases while Seer runs and applies a downloaded update
// only when no recording or save is in progress.
internal sealed class ReleaseUpdater : IDisposable
{
    private readonly UpdateManager? _manager;
    private readonly Func<bool> _isBusy;
    private readonly System.Windows.Forms.Timer _timer;
    private UpdateInfo? _pending;
    private bool _checking;

    public ReleaseUpdater(Func<bool> isBusy)
    {
        _isBusy = isBusy;
        _timer = new System.Windows.Forms.Timer { Interval = (int)TimeSpan.FromHours(6).TotalMilliseconds };
        _timer.Tick += (_, _) => _ = CheckAsync();

        string config = Path.Combine(AppContext.BaseDirectory, "github-repo-url.txt");
        if (!File.Exists(config)) return;
        string url = File.ReadAllText(config).Trim();
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
            uri.Scheme == Uri.UriSchemeHttps && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
            uri.AbsolutePath.Trim('/').Split('/').Length == 2)
        {
            _manager = new UpdateManager(new GithubSource(url, accessToken: null, prerelease: false));
        }
    }

    public void Start()
    {
        if (_manager is null || !_manager.IsInstalled) return;
        _timer.Start();
        _ = CheckAsync();
    }

    private async Task CheckAsync()
    {
        if (_manager is null || _checking || _pending is not null || _isBusy()) return;
        _checking = true;
        try
        {
            UpdateInfo? update = await _manager.CheckForUpdatesAsync();
            if (update is null) return;
            await _manager.DownloadUpdatesAsync(update);
            _pending = update;
            TryApplyIfIdle();
        }
        catch (Exception error)
        {
            // Network problems should never stop screen recording. Retry at the next check.
            Log(error);
        }
        finally
        {
            _checking = false;
        }
    }

    public void TryApplyIfIdle()
    {
        if (_pending is null || _manager is null || _isBusy()) return;
        UpdateInfo update = _pending;
        _pending = null;
        try { _manager.ApplyUpdatesAndRestart(update); }
        catch (Exception error) { Log(error); }
    }

    private static void Log(Exception error)
    {
        try
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Recorder");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "updater.log"), $"{DateTimeOffset.Now:u} {error}\n");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void Dispose() => _timer.Dispose();
}
