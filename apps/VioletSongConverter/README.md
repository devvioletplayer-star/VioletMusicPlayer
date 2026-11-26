# Violet Song Converter

A small Windows app for the songs in **[Violet] Music Solo Player** (FFXIV `.scd` files): it shrinks them to save
disk space, and trims a song (cuts junk off its start or end) and puts it straight back in the mod.

(The mod used to be called **[Violet] Music Mod**. Copies with the old name are found too.)

Start `VioletSongConverter.exe`. It finds [Violet] Music Solo Player through Penumbra's settings and lists every
song with its group, length and size. (If it can't find it, click **Browse…** next to *Music mod folder* and pick
the mod's folder inside your Penumbra mods folder.) Click a column header to sort.

## Shrinking songs

Soundy saves songs at about 500 kbps, so each one takes 10–20 MB. Songs from YouTube are at most about
160 kbps, so that extra size adds nothing you can hear.

1. Tick the songs to shrink (**Select all** / **Select none** help; **Biggest first** puts the largest songs at the top).
2. Click **Shrink selected songs** and confirm. Each song is re-saved in place, with the same name and group.
   Songs that are already small are skipped, so shrinking twice doesn't lower the quality again.

The old copies are kept as backups for 7 days (see below), so the space is freed once those are deleted.

## Removing unused files

When a song is replaced in Soundy, Soundy adds the new file but leaves the old one in the mod, where no song
group uses it. **Remove unused files…** finds every song file that no song group uses, shows them with their
sizes, and after you confirm, takes them out of the mod. Nothing you hear in game changes.

The removed files are kept as backups for 7 days (see below). Until then, **Put back removed files** brings them
all back; it's greyed out when there's nothing to put back.

## Trimming a song

Click a song's name to highlight it, then click **Edit song…** (or double-click the song). Edit works on the
highlighted song only; the tick boxes are only for shrinking. The note next to the buttons names the highlighted song. The editor shows the song's waveform:

- Drag the **green Start** and **red End** markers, or type exact times (`4:50.5`). The parts that will be
  cut off are greyed out. **Start at playhead** / **End at playhead** put a marker where the white playhead is.
- **Auto-trim silence** cuts silence at both ends. Talking or other sound after the song needs the End marker
  moved by hand: use **Hear the ending** to check.
- Click the waveform to move the playhead; **Play**, **Stop** and **Play from start** play only the part
  you're keeping. Space plays and pauses.
- **Fade in / Fade out** (seconds) smooth the cut. Fade out is 2 seconds by default; set it to 0 for none.
- **Replace song in mod…** puts the trimmed song straight back in [Violet] Music Solo Player, in place of the
  old one, with the same name and group. There's no Soundy step. The song is saved at about 192 kbps:
  songs from YouTube are at most about 160 kbps, so Soundy's much larger files (about 500 kbps) add nothing you
  can hear, and the replaced song is usually less than half the size.

If the game is running, Violet Music Player notices the new song by itself and reads the mod again.

## Backups and Restore backup

Before a song is replaced or shrunk, or an unused file is removed, the old one is saved to `%AppData%\Violet\Song backups`. The backup is kept for
**7 days** after the song's last replace and is then deleted automatically, because Soundy's songs are large.
Replacing the same song again within the week keeps the first backup (the song before any of your edits) and
starts the week again.

To undo, click the song's name in the main window to highlight it, then click **Restore backup…**. It's greyed out when the song has
no backup; next to it you can see when the backup was made and how long it's kept. Restoring puts the old song
back and removes the backup.

The app only edits songs that are already in the mod; adding new music is done with Soundy. It uses the
OggVorbisEncoder library (built into the app), so nothing else needs installing. Songs made with Soundy (Ogg
Vorbis) can be shrunk and replaced; plain PCM and Microsoft ADPCM sounds can only be played in the editor. The
game's own encrypted music files aren't supported.

## Building

Needs the .NET 10 SDK. Double-click `Build.bat`, or run:

```
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

The app is `publish\VioletSongConverter.exe`. It needs the .NET 10 Desktop Runtime, which the SDK
includes.

To share it with people who don't have .NET, double-click `BuildPortable.bat` instead. It makes
`publish-portable\VioletSongConverter.exe`: one larger file (roughly 50–70 MB) with .NET built in, which runs
on any 64-bit Windows 10/11 PC without installing anything.
