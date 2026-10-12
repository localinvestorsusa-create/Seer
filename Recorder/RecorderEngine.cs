using System.Diagnostics;
using System.Globalization;

namespace Recorder;

// Starts capture, then combines video + both audio tracks into one MP4.
internal sealed class RecorderEngine : IDisposable
{
    private readonly string _ffmpeg;
    private readonly string _tempFolder;
    private readonly string _videoPath;
    private readonly AudioTrackCapture _systemAudio;
    private readonly AudioTrackCapture _microphoneAudio;
    private readonly Process _videoProcess;
    private readonly Task<string> _videoLog;
    private readonly long _videoStartTicks;
    private bool _finished;

    private RecorderEngine(string ffmpeg, string tempFolder, AudioTrackCapture systemAudio,
        AudioTrackCapture microphoneAudio, Process videoProcess, Task<string> videoLog,
        long videoStartTicks)
    {
        _ffmpeg = ffmpeg;
        _tempFolder = tempFolder;
        _videoPath = System.IO.Path.Combine(tempFolder, "screen.mp4");
        _systemAudio = systemAudio;
        _microphoneAudio = microphoneAudio;
        _videoProcess = videoProcess;
        _videoLog = videoLog;
        _videoStartTicks = videoStartTicks;
    }

    public static RecorderEngine Start(Screen screen, string ffmpeg)
    {
        if (!File.Exists(ffmpeg))
            throw new FileNotFoundException("FFmpeg was not found. Select ffmpeg.exe in the app window.", ffmpeg);

        string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Recorder", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        AudioTrackCapture? system = null;
        AudioTrackCapture? microphone = null;
        Process? process = null;
        try
        {
            system = new AudioTrackCapture(System.IO.Path.Combine(folder, "system.wav"), true);
            microphone = new AudioTrackCapture(System.IO.Path.Combine(folder, "microphone.wav"), false);
            system.Start();
            microphone.Start();

            var bounds = screen.Bounds;
            var info = NewFfmpeg(ffmpeg);
            Add(info, "-hide_banner", "-loglevel", "error", "-f", "gdigrab", "-framerate", "30",
                "-offset_x", bounds.X.ToString(CultureInfo.InvariantCulture),
                "-offset_y", bounds.Y.ToString(CultureInfo.InvariantCulture),
                "-video_size", $"{bounds.Width}x{bounds.Height}", "-i", "desktop",
                "-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2",
                "-c:v", "libx264", "-preset", "veryfast", "-crf", "23",
                "-pix_fmt", "yuv420p", "-an", "-movflags", "+faststart",
                System.IO.Path.Combine(folder, "screen.mp4"));
            info.RedirectStandardInput = true;

            long videoStartTicks = Stopwatch.GetTimestamp();
            process = Process.Start(info) ?? throw new IOException("FFmpeg did not start.");
            Task<string> log = process.StandardError.ReadToEndAsync();
            return new RecorderEngine(ffmpeg, folder, system, microphone, process, log, videoStartTicks);
        }
        catch
        {
            if (process is { HasExited: false }) process.Kill(true);
            try { system?.Dispose(); } catch { }
            try { microphone?.Dispose(); } catch { }
            process?.Dispose();
            throw;
        }
    }

    public async Task<string> StopAndSaveAsync(Action onCaptureStopped)
    {
        if (_finished) throw new InvalidOperationException("This recording has already been stopped.");
        _finished = true;
        long endTicks = Stopwatch.GetTimestamp();
        try
        {
            var systemStop = _systemAudio.StopAsync(endTicks);
            var microphoneStop = _microphoneAudio.StopAsync(endTicks);
            var videoStop = StopVideoAsync();
            await Task.WhenAll(systemStop, microphoneStop, videoStop);

            onCaptureStopped();

            _systemAudio.Dispose();
            _microphoneAudio.Dispose();

            string downloads = DownloadsFolder.GetPath();
            Directory.CreateDirectory(downloads);
            string name = $"Recording-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}-{Guid.NewGuid():N}";
            string combined = System.IO.Path.Combine(_tempFolder, "combined.mp4");
            await CombineAsync(combined);
            string saved = System.IO.Path.Combine(downloads, name + ".mp4");
            File.Move(combined, saved);
            Directory.Delete(_tempFolder, true);
            return saved;
        }
        catch (Exception error)
        {
            throw new IOException($"The recording could not be saved. Recovery files are in {_tempFolder}. {error.Message}", error);
        }
    }

    private async Task StopVideoAsync()
    {
        if (!_videoProcess.HasExited)
        {
            await _videoProcess.StandardInput.WriteLineAsync("q");
            await _videoProcess.StandardInput.FlushAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try { await _videoProcess.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                _videoProcess.Kill(true);
                throw new TimeoutException("FFmpeg did not finish the screen video.");
            }
        }
        string log = await _videoLog;
        if (_videoProcess.ExitCode != 0 || !File.Exists(_videoPath) || new FileInfo(_videoPath).Length == 0)
            throw new IOException("FFmpeg could not capture the display. " + Tail(log));
    }

    private async Task CombineAsync(string output)
    {
        var info = NewFfmpeg(_ffmpeg);
        Add(info, "-hide_banner", "-loglevel", "error", "-i", _videoPath);
        AddAudioInput(info, _systemAudio);
        AddAudioInput(info, _microphoneAudio);
        Add(info, "-filter_complex", "[1:a][2:a]amix=inputs=2:duration=longest:normalize=0,volume=0.5[a]",
            "-map", "0:v:0", "-map", "[a]", "-c:v", "copy", "-c:a", "aac", "-b:a", "192k",
            "-shortest", "-movflags", "+faststart", output);

        using var process = Process.Start(info) ?? throw new IOException("FFmpeg could not combine the video and audio.");
        var logTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            process.Kill(true);
            throw new TimeoutException("Combining the video and audio took too long.");
        }
        string log = await logTask;
        if (process.ExitCode != 0 || !File.Exists(output) || new FileInfo(output).Length == 0)
            throw new IOException("FFmpeg could not combine the video and audio. " + Tail(log));
    }

    private void AddAudioInput(ProcessStartInfo info, AudioTrackCapture track)
    {
        double skipSeconds = Math.Max(0, Stopwatch.GetElapsedTime(track.StartTicks, _videoStartTicks).TotalSeconds);
        Add(info, "-ss", skipSeconds.ToString("0.000000", CultureInfo.InvariantCulture), "-i", track.Path);
    }

    private static ProcessStartInfo NewFfmpeg(string path) => new(path)
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardError = true
    };

    private static void Add(ProcessStartInfo info, params string[] values)
    {
        foreach (string value in values) info.ArgumentList.Add(value);
    }

    private static string Tail(string value)
    {
        value = value.Trim();
        return value.Length > 1000 ? value[^1000..] : value;
    }

    public void Dispose()
    {
        _systemAudio.Dispose();
        _microphoneAudio.Dispose();
        if (!_videoProcess.HasExited) _videoProcess.Kill(true);
        _videoProcess.Dispose();
    }
}
