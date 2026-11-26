using ClosedXML.Excel;

namespace VioletSongCheck;

/// <summary>Writes the song check to an .xlsx file.</summary>
public static class ReportWriter
{
    private static readonly Dictionary<SongStatus, string> Fill = new()
    {
        [SongStatus.NeedsFixing] = "#F4C7C3",
        [SongStatus.WorthAListen] = "#FCE8B2",
        [SongStatus.SilenceAtEnd] = "#E8EAF6",
        [SongStatus.Clean] = "#D9EAD3",
        [SongStatus.CouldNotRead] = "#E0E0E0",
    };

    public static void Write(string path, string modName, IReadOnlyList<SongReport> reports)
    {
        var rows = reports.OrderBy(r => r.Status == SongStatus.CouldNotRead ? -1 : (int)r.Status)
                          .ThenBy(r => r.Song.Name, StringComparer.OrdinalIgnoreCase).ToList();

        using var wb = new XLWorkbook();
        wb.Style.Font.FontName = "Arial";
        wb.Style.Font.FontSize = 10;
        var ws = wb.Worksheets.Add("Song Check");

        ws.Cell("A1").Value = $"{modName} – song check";
        ws.Cell("A1").Style.Font.SetBold().Font.SetFontSize(14);
        ws.Cell("A2").Value =
            $"Checked {DateTime.Now:d MMM yyyy} by Violet Song Check, which measures each song's loudness ten times a second " +
            "(silence, extra sound after the song, replays of the start, sudden endings). It can't tell talking from music " +
            "for certain, so 'Worth a listen' rows need your ears.";
        var intro = ws.Range("A2:I2");
        intro.Merge();
        intro.Style.Alignment.SetWrapText().Alignment.SetVertical(XLAlignmentVerticalValues.Top);
        intro.Style.Font.SetItalic().Font.SetFontSize(9).Font.SetFontColor(XLColor.FromHtml("#555555"));
        ws.Row(2).Height = 30;

        const int hdr = 7;
        var first = hdr + 1;
        var last = hdr + Math.Max(1, rows.Count);

        // Summary
        ws.Cell("A4").Value = "Summary";
        ws.Cell("A4").Style.Font.SetBold();
        var labels = new[] { SongStatus.NeedsFixing, SongStatus.WorthAListen, SongStatus.SilenceAtEnd, SongStatus.Clean };
        for (var i = 0; i < labels.Length; i++)
        {
            var c = 1 + i * 2;
            var label = SongAnalyzer.Label(labels[i]);
            ws.Cell(5, c).Value = label;
            ws.Cell(5, c).Style.Fill.SetBackgroundColor(XLColor.FromHtml(Fill[labels[i]]));
            ws.Cell(5, c + 1).FormulaA1 = $"COUNTIF($D${first}:$D${last},\"{label}\")";
            ws.Cell(5, c + 1).Style.Font.SetBold();
        }
        ws.Cell(5, 9).FormulaA1 = $"COUNTIF($I${first}:$I${last},\"Yes\")&\" of \"&COUNTA($A${first}:$A${last})&\" done\"";
        ws.Cell(5, 9).Style.Font.SetBold();

        var heads = new[] { "Song", "Song group", "Length", "Status", "What's in the file", "Suggested fix", "Suggested end", "Silence at end (s)", "Done?" };
        for (var j = 0; j < heads.Length; j++)
        {
            var c = ws.Cell(hdr, j + 1);
            c.Value = heads[j];
            c.Style.Font.SetBold().Font.SetFontColor(XLColor.White)
                .Fill.SetBackgroundColor(XLColor.FromHtml("#3B2F5A"))
                .Alignment.SetWrapText().Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var row = first + i;
            ws.Cell(row, 1).Value = r.Song.Name;
            ws.Cell(row, 2).Value = r.Song.Group;
            ws.Cell(row, 3).Value = r.Duration / 86400.0;
            ws.Cell(row, 3).Style.NumberFormat.Format = "[m]:ss";
            ws.Cell(row, 4).Value = SongAnalyzer.Label(r.Status);
            ws.Cell(row, 4).Style.Fill.SetBackgroundColor(XLColor.FromHtml(Fill[r.Status]));
            ws.Cell(row, 5).Value = r.Details;
            ws.Cell(row, 6).Value = r.Fix;
            if (r.SuggestedEnd is { } endAt)
            {
                ws.Cell(row, 7).Value = endAt / 86400.0;
                ws.Cell(row, 7).Style.NumberFormat.Format = "\"End at \"m:ss";
            }
            else ws.Cell(row, 7).Value = "—";
            ws.Cell(row, 8).Value = Math.Round(r.TrailSilence, 1);
            ws.Cell(row, 8).Style.NumberFormat.Format = "0.0";
            ws.Range(row, 1, row, 9).Style
                .Alignment.SetWrapText().Alignment.SetVertical(XLAlignmentVerticalValues.Top)
                .Border.SetBottomBorder(XLBorderStyleValues.Thin).Border.SetBottomBorderColor(XLColor.FromHtml("#CCCCCC"));
            ws.Range(row, 7, row, 9).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        }

        // Done? drop-down
        ws.Range(first, 9, last, 9).CreateDataValidation().List("\"Yes,No\"", true);

        var note = last + 2;
        ws.Cell(note, 1).Value = "How to fix a song: open it in Violet Song Converter (Edit song…), move the markers or click Auto-trim silence, " +
                                 "Save as MP3…, then upload it with Soundy.";
        ws.Cell(note + 1, 1).Value = "Done?: pick Yes as you finish each song; the count at the top updates. Click a column header's arrow to filter.";
        ws.Range(note, 1, note + 1, 1).Style.Font.SetFontSize(9);

        var widths = new[] { 30, 18, 9, 15, 52, 52, 14, 12, 8 };
        for (var j = 0; j < widths.Length; j++) ws.Column(j + 1).Width = widths[j];
        ws.SheetView.FreezeRows(hdr);
        ws.Range(hdr, 1, last, 9).SetAutoFilter();

        wb.SaveAs(path);
    }
}
