using ClosedXML.Excel;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Helpers;

public static class RekapBulananExcelExporter
{
    public static byte[] Build(RekapBulananVm vm)
    {
        using var wb = new XLWorkbook();

        var ws = wb.Worksheets.Add("Rekap Bulanan");
        ws.Style.Font.FontName = "Calibri";
        ws.Style.Font.FontSize = 11;

        ws.Cell(1, 1).Value = "Rekap Bulanan Permohonan PPID";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(2, 1).Value = vm.ScopeTitle;
        ws.Cell(3, 1).Value = vm.BulanLabel;
        ws.Cell(4, 1).Value = $"Total permohonan: {vm.TotalBulan}";
        ws.Range(2, 1, 4, 1).Style.Font.FontColor = XLColor.FromHtml("#374151");

        var row = 6;
        ws.Cell(row, 1).Value = "Ringkasan per Kategori";
        ws.Cell(row, 1).Style.Font.Bold = true;
        row++;

        var headerRow = row;
        ws.Cell(row, 1).Value = "No";
        ws.Cell(row, 2).Value = "Kategori";
        ws.Cell(row, 3).Value = "Jumlah";
        ws.Cell(row, 4).Value = "Status";
        StyleHeader(ws.Range(row, 1, row, 4));
        row++;

        foreach (var r in vm.Rows)
        {
            ws.Cell(row, 1).Value = r.No;
            ws.Cell(row, 2).Value = r.Kategori;
            ws.Cell(row, 3).Value = r.Jumlah;
            ws.Cell(row, 4).Value = r.Status;
            row++;
        }

        if (vm.Rows.Count == 0)
        {
            ws.Cell(row, 1).Value = "Belum ada permohonan pada bulan ini.";
            ws.Range(row, 1, row, 4).Merge();
            ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 1).Style.Font.FontColor = XLColor.Gray;
            row++;
        }
        else
        {
            ws.Cell(row, 2).Value = "TOTAL";
            ws.Cell(row, 2).Style.Font.Bold = true;
            ws.Cell(row, 3).Value = vm.TotalBulan;
            ws.Cell(row, 3).Style.Font.Bold = true;
            ws.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F4F6");
            row++;
        }

        var tableEnd = row - 1;
        if (tableEnd >= headerRow)
        {
            var tableRange = ws.Range(headerRow, 1, tableEnd, 4);
            tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            ws.Range(headerRow + 1, 1, tableEnd, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(headerRow + 1, 3, tableEnd, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        row += 2;
        ws.Cell(row, 1).Value = "Kalender Permohonan Masuk";
        ws.Cell(row, 1).Style.Font.Bold = true;
        row++;

        var calHeaderRow = row;
        var dayHeaders = new[] { "Sen", "Sel", "Rab", "Kam", "Jum", "Sab", "Min" };
        for (var c = 0; c < 7; c++)
        {
            ws.Cell(row, c + 1).Value = dayHeaders[c];
            ws.Cell(row, c + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, c + 1).Style.Font.Bold = true;
            ws.Cell(row, c + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#EFF6FF");
        }
        row++;

        var calStartRow = row;
        var col = 0;
        foreach (var day in vm.CalendarDays)
        {
            if (day.IsCurrentMonth)
            {
                var cell = ws.Cell(row, col + 1);
                cell.Value = day.Count > 0 ? $"{day.Date.Day} ({day.Count})" : day.Date.Day.ToString();
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                if (day.Count > 0)
                {
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontColor = XLColor.FromHtml("#047857");
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#ECFDF5");
                }
                if (day.Date == DateOnly.FromDateTime(DateTime.Today))
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#DBEAFE");
            }

            col++;
            if (col >= 7) { col = 0; row++; }
        }

        var calEndRow = row - 1;
        if (calEndRow >= calHeaderRow)
        {
            ws.Range(calHeaderRow, 1, calEndRow, 7).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(calHeaderRow, 1, calEndRow, 7).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        ws.Column(1).Width = 6;
        ws.Column(2).Width = 42;
        ws.Column(3).Width = 12;
        ws.Column(4).Width = 28;
        for (var c = 1; c <= 7; c++)
            ws.Column(c).Width = Math.Max(ws.Column(c).Width, 10);

        ws.SheetView.FreezeRows(5);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void StyleHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A8A");
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }
}
