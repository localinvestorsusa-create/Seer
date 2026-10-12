using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Recorder;

// Writes a time-aligned WAV track. WASAPI loopback does not deliver buffers
// during silence, so gaps must be written explicitly before the MP4 is mixed.
internal sealed class AudioTrackCapture : IDisposable
{
    private readonly IWaveIn _capture;
    private readonly WaveFileWriter _writer;
    private readonly object _gate = new();
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _started;
    private bool _disposed;
    private long _bytesWritten;
    private Exception? _captureError;

    public string Path { get; }
    public long StartTicks { get; private set; }

    public AudioTrackCapture(string path, bool systemAudio)
    {
        Path = path;
        _capture = systemAudio ? new WasapiLoopbackCapture() : new WasapiCapture();
        _writer = new WaveFileWriter(path, _capture.WaveFormat);
        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;
    }

    public void Start()
    {
        StartTicks = Stopwatch.GetTimestamp();
        _capture.StartRecording();
        _started = true;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs args)
    {
        lock (_gate)
        {
            if (_disposed) return;

            var bytesPerSecond = _capture.WaveFormat.AverageBytesPerSecond;
            double secondsOfBuffer = (double)args.BytesRecorded / bytesPerSecond;
            double secondsSinceStart = Stopwatch.GetElapsedTime(StartTicks).TotalSeconds;
            // The callback happens after the buffer was captured.
            WriteSilenceUntil(Math.Max(0, secondsSinceStart - secondsOfBuffer));
            _writer.Write(args.Buffer, 0, args.BytesRecorded);
            _bytesWritten += args.BytesRecorded;
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs args)
    {
        _captureError = args.Exception;
        _stopped.TrySetResult();
    }

    public async Task StopAsync(long endTicks)
    {
        if (!_started) return;
        _capture.StopRecording();
        await _stopped.Task.WaitAsync(TimeSpan.FromSeconds(8));
        lock (_gate)
        {
            double duration = Stopwatch.GetElapsedTime(StartTicks, endTicks).TotalSeconds;
            WriteSilenceUntil(duration);
            _writer.Flush();
        }
        if (_captureError is not null)
            throw new IOException("An audio device stopped unexpectedly.", _captureError);
    }

    private void WriteSilenceUntil(double seconds)
    {
        int block = _capture.WaveFormat.BlockAlign;
        long target = (long)(seconds * _capture.WaveFormat.AverageBytesPerSecond);
        target -= target % block;
        long gap = target - _bytesWritten;
        if (gap <= 0) return;

        byte[] zero = new byte[16 * 1024];
        while (gap > 0)
        {
            int count = (int)Math.Min(gap, zero.Length);
            count -= count % block;
            if (count == 0) break;
            _writer.Write(zero, 0, count);
            _bytesWritten += count;
            gap -= count;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _capture.DataAvailable -= OnDataAvailable;
            _capture.RecordingStopped -= OnRecordingStopped;
            _writer.Dispose();
        }
        _capture.Dispose();
    }
}
