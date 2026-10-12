using System.Diagnostics;

namespace Recorder;

// The app window: setup, status, and entry points. The capture work is in
// RecorderEngine.cs; the on-screen border and square are in RecordingOverlay.cs.
public sealed class MainForm : Form
{
    private readonly Label _status;
    private readonly Label _ffmpegLabel;
    private readonly Button _startButton;
    private readonly Button _installButton;
    private readonly Button _chooseButton;
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _trayMenu;
    private readonly ReleaseUpdater _updater;
    private GlobalRightAltInput? _rightAlt;
    private RecorderEngine? _recording;
    private RecordingOverlay? _overlay;
    private bool _busy;
    private bool _exitAfterSaving;
    private bool _allowExit;
    private bool _showWindowAfterRecording;
    private string? _selectedFfmpeg;

    public MainForm()
    {
        Text = "Seer";
        ClientSize = new Size(500, 365);
        MinimumSize = new Size(515, 400);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(248, 249, 251);
        Font = new Font("Segoe UI", 10);

        var title = new Label
        {
            Text = "Screen recorder",
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(22, 20)
        };
        var instructions = new Label
        {
            Text = "Press right Alt to record the full primary screen.\nClick the black square at the bottom right to stop.\nIncludes computer sound and microphone audio.",
            AutoSize = true,
            Location = new Point(24, 68)
        };
        _startButton = new Button
        {
            Text = "Start recording",
            Bounds = new Rectangle(24, 146, 185, 38)
        };
        _startButton.Click += async (_, _) => await StartRecordingAsync();

        var downloadsButton = new Button
        {
            Text = "Open Downloads",
            Bounds = new Rectangle(220, 146, 160, 38)
        };
        downloadsButton.Click += (_, _) => OpenDownloads();

        _installButton = new Button
        {
            Text = "Install FFmpeg",
            Bounds = new Rectangle(24, 205, 165, 34)
        };
        _installButton.Click += async (_, _) => await InstallFfmpegAsync();
        _chooseButton = new Button
        {
            Text = "Choose ffmpeg.exe",
            Bounds = new Rectangle(200, 205, 175, 34)
        };
        _chooseButton.Click += (_, _) => ChooseFfmpeg();

        _ffmpegLabel = new Label
        {
            AutoEllipsis = true,
            Bounds = new Rectangle(24, 248, 454, 24),
            ForeColor = Color.DimGray
        };
        _status = new Label
        {
            Text = "Ready",
            AutoEllipsis = true,
            Bounds = new Rectangle(24, 288, 454, 28)
        };
        var trayHint = new Label
        {
            Text = "Closing this window keeps Seer running in the system tray.",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Location = new Point(24, 326)
        };
        Controls.AddRange([title, instructions, _startButton, downloadsButton, _installButton,
            _chooseButton, _ffmpegLabel, _status, trayHint]);

        _trayMenu = new ContextMenuStrip();
        _trayMenu.Items.Add("Show Seer", null, (_, _) => ShowMainWindow());
        _trayMenu.Items.Add("Start recording", null, (_, _) => _ = StartRecordingAsync());
        _trayMenu.Items.Add("Open Downloads", null, (_, _) => OpenDownloads());
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add("Exit Seer", null, (_, _) => ExitRecorder());
        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Seer — Press right Alt to record",
            ContextMenuStrip = _trayMenu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();

        _updater = new ReleaseUpdater(() => _busy || _recording is not null);

        _selectedFfmpeg = FindFfmpeg();
        _ffmpegLabel.Text = _selectedFfmpeg is null ? "FFmpeg needed for video" : "FFmpeg ready";
        if (_selectedFfmpeg is null) _status.Text = "Install FFmpeg or choose ffmpeg.exe before recording.";

        Shown += (_, _) =>
        {
            InstallShortcut();
            _updater.Start();
        };
        FormClosing += OnClosing;
        FormClosed += (_, _) =>
        {
            _rightAlt?.Dispose();
            _trayIcon.Dispose();
            _trayMenu.Dispose();
            _updater.Dispose();
        };
    }

    private void InstallShortcut()
    {
        try
        {
            _rightAlt = new GlobalRightAltInput(Handle, () => BeginInvoke(new Action(() => _ = StartRecordingAsync())));
        }
        catch (Exception error)
        {
            _status.Text = "Right Alt could not be registered: " + error.Message;
        }
    }

    private async Task StartRecordingAsync()
    {
        if (_busy || _recording is not null) return;
        string? ffmpeg = _selectedFfmpeg;
        if (ffmpeg is null || !File.Exists(ffmpeg))
        {
            Show();
            Activate();
            _status.Text = "Install FFmpeg or choose ffmpeg.exe before recording.";
            return;
        }

        _busy = true;
        _startButton.Enabled = false;
        try
        {
            var screen = Screen.PrimaryScreen ?? throw new IOException("No primary display was found.");
            _showWindowAfterRecording = Visible;
            Hide();
            await Task.Yield(); // Give Windows a chance to remove this window from the recording.
            _recording = RecorderEngine.Start(screen, ffmpeg);
            _overlay = new RecordingOverlay(screen, () => BeginInvoke(new Action(() => _ = StopRecordingAsync())));
            _overlay.Show();
            _status.Text = "Recording...";
            _trayIcon.Text = "Seer — Recording";
        }
        catch (Exception error)
        {
            _recording?.Dispose();
            _recording = null;
            _overlay?.Dispose();
            _overlay = null;
            Show();
            Activate();
            _status.Text = "Could not start recording.";
            _trayIcon.Text = "Seer — Ready";
            MessageBox.Show(this, error.Message, "Seer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _busy = false;
            _startButton.Enabled = true;
            _updater.TryApplyIfIdle();
            if (_exitAfterSaving)
            {
                if (_recording is not null) _ = StopRecordingAsync();
                else Close();
            }
        }
    }

    private async Task StopRecordingAsync()
    {
        if (_busy || _recording is null) return;
        _busy = true;
        _overlay?.Dispose();
        _overlay = null;
        _startButton.Enabled = false;
        try
        {
            string saved = await _recording.StopAndSaveAsync(() =>
            {
                if (_showWindowAfterRecording && !_exitAfterSaving) ShowMainWindow();
                _status.Text = "Saving video to Downloads...";
                _trayIcon.Text = "Seer — Saving";
            });
            _status.Text = "Saved: " + Path.GetFileName(saved);
            _trayIcon.Text = "Seer — Ready";
            if (!_showWindowAfterRecording && !_exitAfterSaving)
                _trayIcon.ShowBalloonTip(3000, "Recording saved", Path.GetFileName(saved), ToolTipIcon.Info);
        }
        catch (Exception error)
        {
            Show();
            Activate();
            _status.Text = "Could not save the recording.";
            _trayIcon.Text = "Seer — Save failed";
            MessageBox.Show(this, error.Message, "Seer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _recording.Dispose();
            _recording = null;
            _startButton.Enabled = true;
            _busy = false;
            _updater.TryApplyIfIdle();
            if (_exitAfterSaving) Close();
        }
    }

    private void ChooseFfmpeg()
    {
        using var picker = new OpenFileDialog
        {
            Title = "Choose ffmpeg.exe",
            Filter = "FFmpeg executable (ffmpeg.exe)|ffmpeg.exe",
            CheckFileExists = true
        };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        _selectedFfmpeg = picker.FileName;
        SaveFfmpegPath(picker.FileName);
        _ffmpegLabel.Text = "FFmpeg ready";
        _status.Text = "Ready. Press right Alt to record.";
    }

    private async Task InstallFfmpegAsync()
    {
        if (_busy) return;
        _busy = true;
        _installButton.Enabled = false;
        _chooseButton.Enabled = false;
        _startButton.Enabled = false;
        _status.Text = "Downloading FFmpeg...";
        try
        {
            var progress = new Progress<int>(percent => _status.Text = $"Downloading FFmpeg... {percent}%");
            string installed = await FfmpegInstaller.InstallAsync(progress);
            SaveFfmpegPath(installed);
            _selectedFfmpeg = installed;
            _ffmpegLabel.Text = "FFmpeg ready";
            _status.Text = "Ready. Press right Alt to record.";
        }
        catch (Exception error)
        {
            _status.Text = "FFmpeg installation failed. You can choose an existing ffmpeg.exe.";
            MessageBox.Show(this, error.Message, "Seer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _busy = false;
            _installButton.Enabled = true;
            _chooseButton.Enabled = true;
            _startButton.Enabled = true;
            _updater.TryApplyIfIdle();
            if (_exitAfterSaving) Close();
        }
    }

    private static void SaveFfmpegPath(string path)
    {
        string settingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Recorder");
        Directory.CreateDirectory(settingsFolder);
        File.WriteAllText(Path.Combine(settingsFolder, "ffmpeg-path.txt"), path);
    }

    private static string? FindFfmpeg()
    {
        string settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Recorder", "ffmpeg-path.txt");
        if (File.Exists(settings))
        {
            string configured = File.ReadAllText(settings).Trim();
            if (File.Exists(configured)) return configured;
        }
        string besideApp = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
        if (File.Exists(besideApp)) return besideApp;
        foreach (string folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(folder)) continue;
            try
            {
                string candidate = Path.Combine(folder.Trim('"'), "ffmpeg.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException) { }
        }
        return null;
    }

    private void OpenDownloads()
    {
        try
        {
            var info = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
            info.ArgumentList.Add(DownloadsFolder.GetPath());
            Process.Start(info);
        }
        catch (Exception error)
        {
            MessageBox.Show(this, error.Message, "Seer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnClosing(object? sender, FormClosingEventArgs args)
    {
        if (!_allowExit)
        {
            args.Cancel = true;
            Hide();
            _trayIcon.ShowBalloonTip(3000, "Seer is still running",
                "Press right Alt to record. Use the tray icon to reopen or exit.", ToolTipIcon.Info);
            return;
        }
        if (_busy || _recording is not null)
        {
            args.Cancel = true;
            _exitAfterSaving = true;
            if (!_busy) _ = StopRecordingAsync();
            return;
        }
    }

    private void ShowMainWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void ExitRecorder()
    {
        _allowExit = true;
        _exitAfterSaving = true;
        Close();
    }

    protected override void WndProc(ref Message message)
    {
        _rightAlt?.ProcessMessage(ref message);
        base.WndProc(ref message);
    }
}
