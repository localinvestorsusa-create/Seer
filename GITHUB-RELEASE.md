# Publish Seer directly from Visual Studio Code

This route uses a public GitHub Releases page. People download one Windows
`Setup.exe` link. After installation, Seer checks that same release page for
new versions while it is running. It waits until recording and saving are idle
before restarting to apply a downloaded update. Recording files remain in each
user's Downloads folder; only app installers and update packages go to GitHub.

## One-time setup

1. Your public repository is already at
   <https://github.com/localinvestorsusa-create/Seer>. It can be empty: the
   first Publish task creates the short public README in
   `release/PUBLIC-README.md` if the repository has no commit yet.
2. `release/github-repo-url.txt` already contains this repository address.
   Keep using it for future releases; installed copies look here for updates.
   The public source lives in this same repository under the MIT license.
3. In VS Code choose **Terminal > Run Task... > Sign in to GitHub (one time)**.
   When asked to authenticate Git, press **Y** and **Enter**. GitHub CLI then
   shows a one-time code; press **Enter** to open the browser, enter that code
   on GitHub's page, and approve the sign-in. Return to VS Code and wait for a
   success message. Keep the code and password private. The local source ZIP
   includes the official GitHub CLI executable; a GitHub source clone needs
   [GitHub CLI](https://cli.github.com/) installed separately. The project
   does not store your GitHub password or token in source files.
4. Install the .NET 10 SDK if `dotnet --version` does not print a version.
   Internet access is needed to restore the Velopack packaging tool and NuGet
   packages on the first build.

## First release

1. In VS Code choose **Terminal > Run Task... > Build Seer installer**. The task
   creates `dist/Seer-.../packages/Seer.ScreenRecorder-win-Setup.exe` and an
   update package. You can review the installer locally before making it public.
2. Exit the old Seer ZIP copy through its tray icon, then run the new `Setup.exe`
   to check the installed version. The old extracted folder will remain on your
   computer until you remove it yourself; do not run both copies together.
3. Choose **Terminal > Run Task... > Publish Seer to GitHub**. This first
   commits and uploads the source, then builds again, uploads the installer and
   update files, and publishes the number in `release/next-version.txt`.
4. Share `https://github.com/localinvestorsusa-create/Seer/releases/latest`. Visitors
   download the `Setup.exe` listed under **Assets**. After publishing succeeds,
   the task advances `release/next-version.txt` to the next patch version.
   For a direct installer link, share
   `https://github.com/localinvestorsusa-create/Seer/releases/latest/download/Seer.ScreenRecorder-win-Setup.exe`.
   That address keeps pointing at the newest published installer.

## Every later update

Edit the `.cs` files and press **Ctrl+S**. Run **Build Recorder** or **Run
Recorder** to check your changes. Then run **Publish Seer to GitHub**. That
task uploads source changes first, then publishes the installable release. The
published release is what installed copies can download; saving a file alone
does not update anybody's app. Keep the version number increasing for each
release. If an upload fails, retry with the same version after resolving the
error; the version file advances only after a successful upload. To upload
source without making a new installer, use **Publish Seer source to GitHub**.

The older local ZIP copy does not receive these updates. Each person needs to
install the GitHub `Setup.exe` once. FFmpeg still needs the app's one-time
**Install FFmpeg** setup, or an existing `ffmpeg.exe` on that computer.

The current installer is unsigned. On Windows PCs with Smart App Control
enabled, Windows can **block it outright** because it cannot verify the
publisher. Smart App Control has no per-app allow option. Do not tell users to
turn off device-wide protection to install Seer. To make this direct download
usable on those PCs, obtain code signing from a trusted provider and configure
Velopack to sign the app binaries and installer during packaging. A
self-signed certificate does not provide public trust. Newly signed releases
may still need time to build SmartScreen reputation. The old extracted ZIP app
can continue to be used on a computer where it already works while signing is
being arranged.

## Files involved

| File | Role |
| --- | --- |
| `release/github-repo-url.txt` | Public release address built into the installed app |
| `release/PUBLIC-README.md` | Short README committed to an empty GitHub repository on first publish |
| `release/next-version.txt` | Version for the next public release |
| `Recorder/ReleaseUpdater.cs` | Checks, downloads, and applies app updates |
| `scripts/Build-GitHubInstaller.ps1` | Builds the Windows installer and update files |
| `scripts/Publish-GitHubRelease.ps1` | Uses your GitHub CLI sign-in to publish the release |
| `dotnet-tools.json` | Pins the Velopack packaging tool version |
| `tools/gh.exe` | Official GitHub CLI used to upload releases |

For Velopack's packaging details, see its [C# guide](https://docs.velopack.io/getting-started/csharp)
and [GitHub Releases source guide](https://docs.velopack.io/integrating/update-sources).
