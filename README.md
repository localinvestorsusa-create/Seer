# Seer

Seer is an open-source Windows screen recorder. It records the primary display,
system playback sound, and the default microphone to a local MP4 file. The
source code is available under the [MIT license](LICENSE).

[Download the latest release](https://github.com/localinvestorsusa-create/Seer/releases/latest).
**Seer 1.0.0 is unsigned.** Windows Smart App Control may block its installer
because the publisher cannot be verified. Do not disable device-wide security
to install it. A signed installer is planned; see [the signing status](SIGNING.md).

Open `Recorder.sln` to work on the app in Visual Studio, or open this whole
folder in Visual Studio Code. The user interface is implemented in C# code.

For a beginner-friendly Visual Studio Code walkthrough, open `START-HERE.md`.
It explains where the UI and recording code live and how to use the VS Code
Build, Run, and Publish tasks. For a public Seer release with automatic updates,
see `GITHUB-RELEASE.md`.

## What the first version does

1. While the app window is open **or closed to the system tray**, press
   **right Alt** once to begin recording the full primary display.
2. A dotted outline appears around that display. A black square at the bottom
   right stops recording when clicked.
3. The app captures the default computer playback device and the default
   microphone, combines them with the video, and saves an MP4 in your real
   Windows **Downloads** folder.
4. **Open Downloads** in the app shows past recordings in File Explorer.

Clicking the window's **X** closes the window but leaves Seer running in
the system tray, so right Alt keeps working. Double-click the tray icon to
reopen the window. Right-click it for **Show Seer**, **Start recording**,
**Open Downloads**, and **Exit Seer**. After **Exit Seer**, the app is
fully stopped and the shortcut is unavailable until you run it again.

The app does not upload recordings. Temporary audio and video files are kept in
`%TEMP%\Recorder` while saving and removed after a successful save. If saving
fails, the app reports their recovery location.

## Open and run in Visual Studio

1. Open `Recorder.sln` in Visual Studio. This is **Visual Studio**, the full
   Windows IDE. If you prefer **Visual Studio Code**, open this folder and run
   `dotnet run --project Recorder/Recorder.csproj` in its terminal.
2. Build the solution. Visual Studio restores the `NAudio` NuGet package and
   compiles the app. The project targets .NET 10 for Windows.
3. Press **F5** to run the app. Click **Install FFmpeg** once. This downloads
   the Windows essentials build directly from its publisher and checks its
   SHA-256 hash. An internet connection is needed for this one-time setup.
   Alternatively, get `ffmpeg.exe` from the [FFmpeg Windows builds page](https://www.gyan.dev/ffmpeg/builds/),
   extract its `bin\ffmpeg.exe`, and click **Choose ffmpeg.exe** in the app.
   If FFmpeg is already on your `PATH`, the app finds it automatically.
4. Press right Alt to record. Click the black square to stop. Wait for the
   **Saved** message, then click **Open Downloads**. You can close the main
   window and keep using right Alt while Recorder remains in the tray.

FFmpeg is an external tool and is **not included** in this source project. The
Install button saves it in `%LOCALAPPDATA%\Recorder\tools`. The app remembers
the selected path in `%LOCALAPPDATA%\Recorder\ffmpeg-path.txt`.

The local ZIP and GitHub installer builds include the .NET runtime for Windows x64.
People who install the app do not need to install .NET separately. On first use,
they still need to click **Install FFmpeg** or choose an existing `ffmpeg.exe`.
See [Privacy](PRIVACY.md) for the app's local file and network behavior.

## Where to change the code

| File | Purpose |
| --- | --- |
| `Recorder/Program.cs` | Starts the app. Usually leave it alone. |
| `Recorder/MainForm.cs` | Main window, tray icon, buttons, status text, and user actions. Change this for the main UI. |
| `Recorder/GlobalRightAltInput.cs` | Detects right Alt anywhere while the app is running. Change this for the shortcut. |
| `Recorder/RecordingOverlay.cs` | Draws the dotted screen outline and black stop square. Change this for the recording indicator. |
| `Recorder/RecorderEngine.cs` | Starts and stops FFmpeg, combines audio with video, and saves the MP4. Change this for recording behavior. |
| `Recorder/AudioTrackCapture.cs` | Captures system sound and microphone into temporary WAV files. Change this for audio behavior. |
| `Recorder/DownloadsFolder.cs` | Asks Windows for the user's actual Downloads folder. Usually leave it alone. |
| `Recorder/FfmpegInstaller.cs` | One-time FFmpeg download and checksum check. Usually leave it alone. |
| `Recorder/ReleaseUpdater.cs` | Checks your public GitHub Releases page and installs updates when idle. |
| `Recorder/Recorder.csproj` | .NET project settings and the NAudio and Velopack dependencies. |

Do not put application code in `bin/` or `obj/`; Visual Studio generates those
folders when it builds. A `.cs` file contains C# source. The `.exe` built from
it is the program you run.

## Current limits

- Records one full display: the primary monitor. It does not yet capture all
  monitors together or choose a monitor.
- Uses the default speaker/output device and default microphone. It does not
  yet provide audio device selectors, pause, or cloud storage.
- Right Alt can also be used as AltGr on some keyboard layouts, so those
  layouts may trigger recording while typing.
- Closing the main window keeps the app running in the tray. Fully exiting the
  app stops the shortcut. Automatic start when signing in to Windows is not
  implemented yet.
- The recorder window is hidden while recording. On supported Windows
  versions, the outline and stop control are excluded from display capture;
  this may vary with Windows version and capture behavior.

## License

Seer's original source code is licensed under the [MIT license](LICENSE).
NAudio, Velopack, .NET, and FFmpeg are separate projects with their own
licenses. FFmpeg is downloaded separately at the user's request.
