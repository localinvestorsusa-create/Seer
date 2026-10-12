# Privacy

Seer records the primary display, default microphone, and default computer
playback device only after the user starts a recording. The finished MP4 is
saved in that Windows user's Downloads folder. Temporary audio and video files
are written under `%TEMP%\Recorder` and are removed after a successful save.
If saving fails, Seer reports where those files can be recovered.

Seer does not upload recordings, audio, screenshots, or file names to a server.
It connects to GitHub Releases to check for software updates while an installed
copy is running. If the user clicks **Install FFmpeg**, it downloads FFmpeg from
the publisher's site and checks the downloaded archive against the publisher's
SHA-256 checksum. A user can instead choose an existing `ffmpeg.exe`.

The application does not contain an account system or analytics. A future
version that adds cloud storage will need an updated privacy notice before
recordings are uploaded.
