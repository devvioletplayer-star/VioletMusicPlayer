# Companion apps: source

The source of the two Windows programs that go with Violet Music Player:

| Folder | App | What it does |
|---|---|---|
| [`VioletSongCheck`](VioletSongCheck) | Violet Song Check | Listens to every song and lists the ones with long silences, talking or a second song on the end. |
| [`VioletSongConverter`](VioletSongConverter) | Violet Song Converter | Trims a song and puts the fixed copy back in the mod, and can shrink songs to save space. |

Each folder has its own README that explains the app.

## The downloads are built here, from this source

The `.exe` files offered in the main README are not uploaded from anyone's PC. GitHub builds them from the files
in this folder with the workflow in
[`.github/workflows/build-companion-apps.yml`](../.github/workflows/build-companion-apps.yml), and the same run
attaches them to a release. You can follow the chain yourself:

1. The release page names the commit it was built from and links to the build log.
2. The release page lists each file's SHA-256. To compare it with your download, in PowerShell:
   ```
   Get-FileHash .\VioletSongCheck.exe -Algorithm SHA256
   ```
3. Each build also records signed build provenance with GitHub. With the [GitHub CLI](https://cli.github.com/):
   ```
   gh attestation verify VioletSongCheck.exe --repo devvioletplayer-star/VioletMusicPlayer
   ```

The apps are not code-signed, so your browser and Windows may still warn about them. The main README says what
to click.

## Building them yourself

You need Windows and the [.NET 10 SDK](https://dotnet.microsoft.com/download). In either app's folder,
double-click `BuildPortable.bat`. It runs the same command as the workflow and writes one self-contained `.exe`
to `publish-portable`. A build on your own PC will not be byte-for-byte the same file as GitHub's.

## What the apps do on your PC

Both apps work only on files on your own PC and make no network connections. They read Penumbra's settings to
find the music mod. Violet Song Check reads the songs and writes one Excel file where you choose. Violet Song
Converter changes song files inside the music mod, and first saves a backup of each to
`%AppData%\Violet\Song backups`, kept for 7 days.
Both keep one small settings file (the text size you chose) in `%AppData%\Violet` and read Windows' own text-size
setting. Violet Song Check opens the finished spreadsheet when it is done.

## Licence

The source in this folder (Violet Song Check and Violet Song Converter) is under the MIT License: see
[LICENSE](LICENSE). It covers these two apps only, not the Violet Music Player plugin. The libraries the apps use
remain under their own licences, listed in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
