// Violet Song Check — version 1.1.1
//   1.1.1  Ticking songs works at larger text sizes: the whole drawn tick box takes the click (before, only a small
//          corner of it did).
//   1.1.0  The Violet Music Player look: violet header, headings, buttons and progress bar; dark window.
//          Finds the music mod as [Violet] Music Solo Player (its new name) or [Violet] Music Mod.
//          Bigger text: follows Windows' Text size (Accessibility), plus a Text size list in the header (100-250%,
//          shared with Violet Song Converter). The whole layout grows with the text, and windows stay on screen.
// Checks every song in [Violet] Music Solo Player for silence, extra audio after the song, replays and sudden
// endings, and writes the results to an Excel spreadsheet.

namespace VioletSongCheck;

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
