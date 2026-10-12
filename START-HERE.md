# Edit Recorder in Visual Studio Code

This folder contains the **source code** for the working Windows Recorder app.
Open this whole folder in VS Code so you can see the files, run the app, and
publish a new Windows package after editing it.

## 1. Open the right folder

In VS Code, choose **File > Open Folder...** and select this folder: the one
containing `Recorder.sln`, `START-HERE.md`, and the `Recorder` folder. Do not
open the `Recorder-app` folder; it contains the built program, not the source.

Install the recommended **C# Dev Kit** extension if VS Code asks. You also
need the **.NET 10 SDK** on this computer. The full Visual Studio installation
may already have installed it. In VS Code's terminal, `dotnet --version` should
print a version number.

## 2. Find the part you want to change

| What you want to change | Open this file |
| --- | --- |
| Text, buttons, layout, tray menu, and what happens when you click | `Recorder/MainForm.cs` |
| Dotted screen border and black stop square | `Recorder/RecordingOverlay.cs` |
| Right Alt shortcut | `Recorder/GlobalRightAltInput.cs` |
| Screen capture, MP4 creation, and save process | `Recorder/RecorderEngine.cs` |
| Computer sound and microphone capture | `Recorder/AudioTrackCapture.cs` |
| Actual Windows Downloads folder | `Recorder/DownloadsFolder.cs` |
| FFmpeg download/setup | `Recorder/FfmpegInstaller.cs` |
| App updates from GitHub | `Recorder/ReleaseUpdater.cs` |
| App startup | `Recorder/Program.cs` |
| .NET version and NuGet packages | `Recorder/Recorder.csproj` |

The window is created in C# code in `MainForm.cs`. There is no separate visual
designer file in this project. The recording outline and stop button are
separate small windows defined in `RecordingOverlay.cs`.

`Recorder.sln` groups the project for editors. `.vscode/tasks.json` contains
the VS Code Build, Run, and Publish commands. `scripts/Build-GitHubInstaller.ps1`
builds the installer; `scripts/Publish-GitHubRelease.ps1` uploads a release.
Folders named `bin`, `obj`, and `dist` are generated; make
changes in the `.cs` source files instead. The first NuGet and Velopack restore
uses the internet. A downloaded source ZIP may also include a `packages` cache;
the public GitHub repository contains only the project source.

## 3. Try a change

For a simple first edit, open `Recorder/MainForm.cs` and change the words in
the `instructions` label. Save with **Ctrl+S**. In VS Code, choose
**Terminal > Run Task... > Run Recorder**. The app opens with your new text.
Exit the old Recorder from its tray icon before running another copy, or both
copies will listen for right Alt.

You can also press **Ctrl+Shift+B** to build without opening the app. The
working source has to compile before a new package can be made.

## 4. Build or publish the Windows app

For a local copy, run **Terminal > Run Task... > Build local ZIP (manual install)**.
Extract the new `dist/Seer-app-*.zip` and run `Recorder.exe`. This ZIP does not
install or update itself.

For the public installer and automatic updates, follow `GITHUB-RELEASE.md`.
After the one-time GitHub setup, **Terminal > Run Task... > Publish Seer to GitHub**
builds a new version and publishes it to your public GitHub Releases page.
Installed copies check for updates while Seer is running and restart to apply
an update when recording and saving are idle.

For additional details, see `README.md`.

## 5. Understand what saving does

**Ctrl+S** saves source code on your computer. **Run Recorder** shows your edits
locally. **Publish Seer to GitHub** creates a new public release after you have
configured GitHub. To receive automatic updates, people must install Seer using
that release's `Setup.exe` once. The older extracted ZIP copy remains separate.
