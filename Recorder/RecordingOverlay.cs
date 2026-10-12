using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Recorder;

internal sealed class RecordingOverlay : IDisposable
{
    private readonly BorderForm _border;
    private readonly StopForm _stop;

    public RecordingOverlay(Screen screen, Action onStop)
    {
        _border = new BorderForm(screen.Bounds);
        _stop = new StopForm(screen.Bounds, onStop);
    }

    public void Show()
    {
        _border.Show();
        _stop.Show();
    }

    public void Dispose()
    {
        _stop.Close();
        _border.Close();
        _stop.Dispose();
        _border.Dispose();
    }

    private static void TryExcludeFromCapture(Form form)
    {
        // Supported by Windows 10 2004+ for display capture. It keeps the
        // recording controls visible to the user but usually out of the video.
        SetWindowDisplayAffinity(form.Handle, 0x11);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(nint window, uint affinity);

    private sealed class BorderForm : Form
    {
        private const int WsExTransparent = 0x20;
        private const int WsExToolWindow = 0x80;

        public BorderForm(Rectangle bounds)
        {
            Bounds = bounds;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.Fuchsia;
            TransparencyKey = Color.Fuchsia;
            DoubleBuffered = true;
            Shown += (_, _) => TryExcludeFromCapture(this);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= WsExTransparent | WsExToolWindow;
                return parameters;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var white = new Pen(Color.White, 5);
            using var black = new Pen(Color.Black, 3) { DashStyle = DashStyle.Dot };
            var line = new Rectangle(4, 4, ClientSize.Width - 9, ClientSize.Height - 9);
            e.Graphics.DrawRectangle(white, line);
            e.Graphics.DrawRectangle(black, line);
        }
    }

    private sealed class StopForm : Form
    {
        private readonly Action _onStop;

        public StopForm(Rectangle screenBounds, Action onStop)
        {
            _onStop = onStop;
            Bounds = new Rectangle(screenBounds.Right - 70, screenBounds.Bottom - 70, 58, 58);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.Black;
            Cursor = Cursors.Hand;
            AccessibleName = "Stop recording";
            Shown += (_, _) => TryExcludeFromCapture(this);
            MouseClick += (_, _) => _onStop();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.FillRectangle(Brushes.White, 18, 18, 22, 22);
        }
    }
}
