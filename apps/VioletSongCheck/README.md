# Violet Song Check

A small Windows app that checks every song in **[Violet] Music Solo Player** and saves the results as an Excel
spreadsheet: which songs have silence at the end, extra sound after the song (often talking), a replay of
the song after it ends, or an ending that stops suddenly (possibly cut off).

(The mod used to be called **[Violet] Music Mod**. Copies with the old name are found too.)

1. Start `VioletSongCheck.exe`. It finds [Violet] Music Solo Player through Penumbra's settings and shows how many
   songs it found. (If it can't, click **Browse…** and pick the mod's folder in your Penumbra mods folder.)
2. Choose where to save (**Save in folder**, Downloads by default) and type a **File name**.
3. Click **Check songs and create spreadsheet**. Each song is checked in turn; then the spreadsheet opens.

## The spreadsheet

One row per song, worst first, colour-coded:

| Status | Meaning |
|---|---|
| **Needs fixing** | The song starts again after it ends, or a minute or more of other audio follows it. |
| **Worth a listen** | A short stretch of sound after a silent gap, or an ending that stops suddenly. |
| **Silence at end** | 5 seconds or more of silence after the song (harmless; a pause before the next song). |
| **Clean** | Nothing found. |

Each row says what's in the file, a suggested fix and, where it applies, where the song really ends
(for the End marker in Violet Song Converter's editor). A **Done?** drop-down and a count at the top let you
tick songs off as you fix them.

The check measures loudness; it can't tell talking from music for certain, so "Worth a listen" songs need
your ears.

## Building

Needs the .NET 10 SDK. Double-click `Build.bat` (small, needs .NET installed) or `BuildPortable.bat`
(one larger file that runs on any 64-bit Windows 10/11 PC). The app ends up in `publish` or
`publish-portable`, and can be moved anywhere.
