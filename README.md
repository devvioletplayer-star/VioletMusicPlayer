# Violet Music Player

A Dalamud plugin for FINAL FANTASY XIV. Your character performs songs on the keyboard, guitar or violin,
one after another in shuffled order, and the same songs can play as background music or as a shared
playlist in a house.

One link installs everything. The plugin creates its own Penumbra mod, **[Violet] Music Solo Player**, the
first time you open it, and keeps that mod up to date with every plugin update.

📖 **[User guide (PDF)](Violet%20Music%20Player%20-%20User%20Guide.pdf)**: step-by-step instructions for everything below.

## What it does

| Part | What it does |
|---|---|
| **Perform** | Picks a song, selects it in Penumbra and performs the matching instrument emote, then moves on to the next. Filter by instrument or song group; leave songs out or delete them. |
| **Background music** | The plugin plays your songs itself, whole and back to back, inside houses or everywhere, at its own volume. Only you hear it. |
| **House music packs** | A playlist for one house. Everyone who has the pack hears the same song at the same moment in that house. Its volume is separate from background music. |
| **Organize** | Rename song groups and songs, move songs between groups, restore a backup. |

Songs are your own: add them to [Violet] Music Solo Player with **Soundy**.

## Requirements

- [XIVLauncher](https://goatcorp.github.io/) with Dalamud
- [Penumbra](https://github.com/xivdev/Penumbra)
- Soundy, to add songs

## Installing

1. In game, type `/xlsettings` and open the **Experimental** tab.
2. Under **Custom Plugin Repositories**, add this link, tick the box next to it, and click **Save and Close**:
   ```
   https://raw.githubusercontent.com/devvioletplayer-star/VioletMusicPlayer/main/repo.json
   ```
3. Type `/xlplugins`, search for **Violet Music Player**, and click **Install**.

If you tested an earlier build as a dev plugin, remove it first (`/xlsettings`, **Experimental**, **Dev Plugin Locations**)
and restart the game. Two copies of the plugin get in each other's way.

Updates arrive through Dalamud like any other plugin.

## First run

After you log in, the Violet Music Player window opens on **Set up the music mod**:

- **Create the music mod**: adds [Violet] Music Solo Player to your Penumbra mod folder (about 45 MB: the
  instruments, no songs) and turns it on for your character. Nothing you already have is changed.
- **I already have it**: lists your Penumbra mods with a search box. Pick the music mod, for example after
  renaming it. A mod that is not the music mod is refused.

The mod has two tabs in Penumbra. **Songs** holds your song groups and the Look Around tick box. **Instruments**
holds the Guitar, Keyboard and Violin lists: open it once and pick your race or size in each. Then add songs with
Soundy into song groups named after their instrument (for example "Keyboard Covers").

## Quick start

1. Type `/vmusic` to open the window, or `/vmusic start` to perform straight away. `/vmusic stop` stops.
2. `/vmusic help` lists every chat command. Everything is also in the window.
3. The plugin tells you a lot in chat. **Chat** in the window's header chooses how much: **All** (everything),
   **Mode** (only when performing starts or stops, and when a house music pack starts) or **None**. Errors, notes
   about a permanent delete and answers to commands you type are always shown. By chat: `/vmusic chat all`,
   `/vmusic chat mode` or `/vmusic chat none`.

## Backups

Every change made on the Organize page, and every update the plugin makes to the mod, saves a backup first
(Organize, **Backups** tab). A song removed with **Delete** is held in the plugin's `removed songs` folder. Both are
kept for 7 days and are then deleted for good the next time the plugin starts.

## Companion apps

Two Windows programs check and tidy the songs in your music mod. They need no install and no game running:
download, put them anywhere and double-click. Sections 3 and 4 of the user guide explain both.

The apps are not code-signed, so expect two warnings. Both are normal for a small program from a new publisher:

- **When downloading:** your browser may say the file "isn't commonly downloaded" and hold it back. In the browser's
  downloads list, click the three dots (**...**) beside the file and choose **Keep**. If it asks again, open the
  choice that offers **Delete** and pick **Save** (or **Keep anyway**) instead. The wording differs a little from
  browser to browser.
- **When running it the first time:** Windows may warn about an unknown publisher. Click **More info**, then
  **Run anyway**.

| App | What it does | Download |
|---|---|---|
| **Violet Song Check** 1.1.1 | Listens to every song and lists the ones with long silences, talking or a second song on the end. | [VioletSongCheck.exe](https://github.com/devvioletplayer-star/VioletMusicPlayer/releases/download/v1.18.4/VioletSongCheck.exe) |
| **Violet Song Converter** 1.3.1 | Trims a song and puts the fixed copy back in the mod, and can shrink songs to save space. | [VioletSongConverter.exe](https://github.com/devvioletplayer-star/VioletMusicPlayer/releases/download/v1.18.4/VioletSongConverter.exe) |

They need Windows 10 or 11 (64-bit) and [Violet] Music Solo Player, but not the game, Dalamud or the plugin.

## Third-party notices

The plugin's download includes libraries written by other people. Their licences and copyright notices are in
[THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
