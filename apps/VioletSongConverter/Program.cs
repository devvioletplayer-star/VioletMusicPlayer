// Violet Song Converter — version 1.3.1
//   1.3.1  Edit song and Restore backup work only on the highlighted song (before, with nothing highlighted, Edit
//          opened the one ticked song). The note beside them names the highlighted song and says tick boxes are
//          only for Shrink.
//   1.3.0  Replace song in mod: the editor puts the trimmed song straight back in the music mod, in place of the
//          old one (no MP3 or Soundy step). Songs are saved at about 192 kbps (no quality choice), much smaller
//          than Soundy's ~500 kbps. The old song is backed up first and the backup is kept for 7 days after the last
//          replace, then deleted automatically. Restore backup (main window) puts it back; greyed out with no backup.
//          Shrink selected songs replaces Convert to MP3: re-saves the ticked songs smaller, in place (same backups);
//          songs already small are skipped. A Size column shows each song's size. MP3 saving is removed completely
//          (bulk and the editor's Save as MP3): adding music is Soundy's job; this app only edits songs in place.
//          Ticking songs works at larger text sizes: the whole drawn tick box takes the click.
//          Remove unused files: takes out song files no group uses (old copies Soundy leaves behind), kept as 7-day
//          backups; Put back removed files brings them back. Biggest first button. Drop-downs fit at large text and
//          no longer change on a mouse-wheel scroll.
//   1.2.0  The Violet Music Player look: violet header, headings, buttons, song list and progress bar; dark window.
//          Finds the music mod as [Violet] Music Solo Player (its new name) or [Violet] Music Mod.
//          Bigger text: follows Windows' Text size (Accessibility), plus a Text size list in the header (100-250%,
//          shared with Violet Song Check). The whole layout grows with the text, and windows stay on screen.
//   1.1.0  Song editor: play a song, set its start and end, fade in/out, save the trimmed song as MP3.
// Shrinks and trims the songs in [Violet] Music Solo Player (FFXIV .scd files).

namespace VioletSongConverter;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        // Changing the header's Text size closes the window; it opens again here at the new size.
        do
        {
            VioletTheme.ReopenRequested = false;
            VioletTheme.LoadScale();
            Application.Run(new MainForm());
        } while (VioletTheme.ReopenRequested);
    }
}
