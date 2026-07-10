using System.Globalization;
using ClosedXML.Excel;
using HastaGeriBildirim.Models.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HastaGeriBildirim.Services;

public class ReportExportService
{
    private const string MinistryRed = "#E31F26";
    private const string OfficialHeaderFill = "#E5B8B7";
    private const string AlternateRowFill = "#F2F2F2";
    private const string Black = "#000000";
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    private readonly string _hospitalName;
    private readonly string _hospitalUnitName;
    private readonly string _healthDirectorateName;
    private readonly string _documentCode;
    private readonly string _publicationDate;
    private readonly string _revisionNumber;
    private readonly string _revisionDate;
    private readonly string _hospitalContactLine;
    private readonly byte[]? _ministryLogo;
    private readonly byte[]? _probelLogo;

    static ReportExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ReportExportService(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _hospitalName = StripTcPrefix(ReadSetting(configuration, "Branding:HospitalName", "Devlet Hastanesi"));
        _hospitalUnitName = ReadSetting(configuration, "Branding:HospitalUnitName", "Kalite ve Hasta Deneyimi Birimi");
        _healthDirectorateName = ResolveHealthDirectorateName(configuration);
        _documentCode = ReadSetting(configuration, "Branding:Reports:DocumentCode", "HGB.RP.01");
        _publicationDate = ReadSetting(configuration, "Branding:Reports:PublicationDate", "13.07.2026");
        _revisionNumber = ReadSetting(configuration, "Branding:Reports:RevisionNumber", "00");
        _revisionDate = ReadSetting(configuration, "Branding:Reports:RevisionDate", "-");
        _hospitalContactLine = BuildContactLine(configuration);
        _ministryLogo = ReadAsset(environment.WebRootPath, "img", "saglik-bakanligi-logo.png");
        _probelLogo = ReadAsset(environment.WebRootPath, "img", "probel-wordmark.png");
    }

    public byte[] BuildExcel(DashboardViewModel dashboard, DateTime startDate, DateTime endDate)
    {
        using var workbook = new XLWorkbook();
        workbook.Properties.Title = "Hasta Memnuniyet Raporu";
        workbook.Properties.Subject = $"{startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}";
        workbook.Properties.Company = _hospitalName;
        workbook.Properties.Author = _hospitalUnitName;

        AddSummarySheet(workbook, dashboard, startDate, endDate);
        AddDepartmentSheet(workbook, dashboard, startDate, endDate);
        AddDoctorSheet(workbook, dashboard, startDate, endDate);
        AddTrendSheet(workbook, dashboard, startDate, endDate);
        AddKpiSheet(workbook, dashboard, startDate, endDate);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] BuildPdf(DashboardViewModel dashboard, DateTime startDate, DateTime endDate)
    {
        var generatedAt = DateTime.Now;

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(18);
            page.DefaultTextStyle(style => style.FontFamily(Fonts.Arial).FontSize(8).FontColor(Black));
            page.Header().Element(container => ComposeControlledDocumentHeader(container, generatedAt));
            page.Content().PaddingTop(8).Element(container => ComposePdfContent(container, dashboard, startDate, endDate));
            page.Footer().Element(ComposePdfFooter);
        })).GeneratePdf();
    }

    private void AddSummarySheet(XLWorkbook workbook, DashboardViewModel dashboard, DateTime startDate, DateTime endDate)
    {
        var sheet = workbook.Worksheets.Add("Özet");
        ConfigureWorksheet(sheet, "HASTA MEMNUNİYET RAPORU");
        WriteExcelPeriodRow(sheet, 6, startDate, endDate);
        WriteExcelSectionTitle(sheet, 8, "ÖZET GÖSTERGELER");

        var row = 9;
        foreach (var (label, value) in GetSummaryRows(dashboard))
        {
            sheet.Range(row, 1, row, 2).Merge().Value = label;
            sheet.Range(row, 3, row, 4).Merge().Value = value;
            StyleExcelBodyRow(sheet.Range(row, 1, row, 4), row);
            sheet.Range(row, 1, row, 2).Style.Font.Bold = true;
            sheet.Range(row, 3, row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            row++;
        }

        AddTinyProbelMark(sheet, row + 1);
        sheet.PageSetup.PrintAreas.Add(1, 1, row + 2, 4);
    }

    private void AddDepartmentSheet(XLWorkbook workbook, DashboardViewModel dashboard, DateTime startDate, DateTime endDate)
    {
        var sheet = workbook.Worksheets.Add("Bölümler");
        ConfigureWorksheet(sheet, "BÖLÜM BAZLI HASTA MEMNUNİYET RAPORU");
        WriteExcelPeriodRow(sheet, 6, startDate, endDate);
        WriteExcelTableHeader(sheet, 8, "Bölüm", "Yanıt Sayısı", "Ortalama Puan", "Olumsuz Sayısı");

        var row = 9;
        foreach (var item in dashboard.DepartmentSummaries)
        {
            sheet.Cell(row, 1).Value = item.DepartmentName;
            sheet.Cell(row, 2).Value = item.ResponseCount;
            sheet.Cell(row, 3).Value = item.AverageScore;
            sheet.Cell(row, 4).Value = item.NegativeCount;
            sheet.Cell(row, 3).Style.NumberFormat.Format = "0.00";
            StyleExcelBodyRow(sheet.Range(row, 1, row, 4), row);
            row++;
        }

        FinalizeExcelDataSheet(sheet, row, 4, 2);
    }

    private void AddDoctorSheet(XLWorkbook workbook, DashboardViewModel dashboard, DateTime startDate, DateTime endDate)
    {
        var sheet = workbook.Worksheets.Add("Hekimler");
        ConfigureWorksheet(sheet, "HEKİM BAZLI HASTA MEMNUNİYET RAPORU");
        WriteExcelPeriodRow(sheet, 6, startDate, endDate);
        sheet.Range(8, 1, 8, 2).Merge().Value = "Hekim";
        sheet.Cell(8, 3).Value = "Yanıt Sayısı";
        sheet.Cell(8, 4).Value = "Ortalama Puan";
        StyleExcelTableHeader(sheet.Range(8, 1, 8, 4));

        var row = 9;
        foreach (var item in dashboard.DoctorSummaries)
        {
            sheet.Range(row, 1, row, 2).Merge().Value = item.DoctorName;
            sheet.Cell(row, 3).Value = item.ResponseCount;
            sheet.Cell(row, 4).Value = item.AverageScore;
            sheet.Cell(row, 4).Style.NumberFormat.Format = "0.00";
            StyleExcelBodyRow(sheet.Range(row, 1, row, 4), row);
            row++;
        }

        FinalizeExcelDataSheet(sheet, row, 4, 3);
    }

    private void AddTrendSheet(XLWorkbook workbook, DashboardViewModel dashboard, DateTime startDate, DateTime endDate)
    {
        var sheet = workbook.Worksheets.Add("Trend");
        ConfigureWorksheet(sheet, "DÖNEMSEL HASTA MEMNUNİYET TRENDİ");
        WriteExcelPeriodRow(sheet, 6, startDate, endDate);
        WriteExcelTableHeader(sheet, 8, "Tarih", "Yanıt Sayısı", "Ortalama Puan", "NPS");

        var row = 9;
        foreach (var item in dashboard.TrendData)
        {
            sheet.Cell(row, 1).Value = item.Date;
            sheet.Cell(row, 1).Style.DateFormat.Format = "dd.MM.yyyy";
            sheet.Cell(row, 2).Value = item.ResponseCount;
            sheet.Cell(row, 3).Value = item.AverageScore;
            sheet.Cell(row, 4).Value = item.NpsValue;
            sheet.Range(row, 3, row, 4).Style.NumberFormat.Format = "0.00";
            StyleExcelBodyRow(sheet.Range(row, 1, row, 4), row);
            row++;
        }

        FinalizeExcelDataSheet(sheet, row, 4, 2);
    }

    private void AddKpiSheet(XLWorkbook workbook, DashboardViewModel dashboard, DateTime startDate, DateTime endDate)
    {
        if (dashboard.KpiComparisons.Count == 0)
            return;

        var sheet = workbook.Worksheets.Add("KPI Hedefleri");
        ConfigureWorksheet(sheet, "KPI HEDEF VE GERÇEKLEŞME RAPORU");
        WriteExcelPeriodRow(sheet, 6, startDate, endDate);
        WriteExcelTableHeader(sheet, 8, "KPI", "Hedef", "Gerçekleşen", "Sapma");

        var row = 9;
        foreach (var item in dashboard.KpiComparisons)
        {
            sheet.Cell(row, 1).Value = item.KpiCode;
            sheet.Cell(row, 2).Value = item.TargetValue;
            sheet.Cell(row, 3).Value = item.ActualValue;
            sheet.Cell(row, 4).Value = item.Deviation;
            sheet.Range(row, 2, row, 4).Style.NumberFormat.Format = "+0.00;-0.00;0.00";
            StyleExcelBodyRow(sheet.Range(row, 1, row, 4), row);
            row++;
        }

        FinalizeExcelDataSheet(sheet, row, 4, 2);
    }

    private void ConfigureWorksheet(IXLWorksheet sheet, string title)
    {
        sheet.ShowGridLines = false;
        sheet.Style.Font.FontName = "Arial";
        sheet.Style.Font.FontSize = 9;
        sheet.Style.Font.FontColor = XLColor.Black;
        sheet.Column(1).Width = 15;
        sheet.Column(2).Width = 69;
        sheet.Column(3).Width = 16;
        sheet.Column(4).Width = 16;
        sheet.Row(1).Height = 14.3;
        sheet.Row(2).Height = 14.3;
        sheet.Row(3).Height = 14.3;
        sheet.Row(4).Height = 14.3;

        sheet.Range("A1:A4").Merge();
        sheet.Range("B1:B3").Merge();
        sheet.Cell("C1").Value = "Sayfa No";
        sheet.Cell("D1").Value = "1 / 1";
        sheet.Cell("C2").Value = "Doküman Kodu";
        sheet.Cell("D2").Value = _documentCode;
        sheet.Cell("C3").Value = "Yayın Tarihi";
        sheet.Cell("D3").Value = _publicationDate;
        sheet.Cell("B4").Value = title;
        sheet.Cell("C4").Value = "Revizyon No/Tarihi";
        sheet.Cell("D4").Value = $"{_revisionNumber} / {_revisionDate}";

        WriteExcelInstitutionIdentity(sheet.Cell("B1"));
        StyleExcelControlledHeader(sheet.Range("A1:D4"));
        AddExcelMinistryLogo(sheet);

        sheet.SheetView.FreezeRows(4);
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        sheet.PageSetup.Margins.Top = 0.35;
        sheet.PageSetup.Margins.Bottom = 0.35;
        sheet.PageSetup.Margins.Left = 0.25;
        sheet.PageSetup.Margins.Right = 0.25;
        sheet.PageSetup.Margins.Header = 0.1;
        sheet.PageSetup.Margins.Footer = 0.1;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.SetRowsToRepeatAtTop(1, 4);
        sheet.PageSetup.Footer.Left.AddText(_hospitalContactLine, XLHFOccurrence.AllPages);
    }

    private void WriteExcelInstitutionIdentity(IXLCell cell)
    {
        cell.Value = string.Join(Environment.NewLine,
            "T.C.",
            "SAĞLIK BAKANLIĞI",
            _healthDirectorateName,
            _hospitalName.ToUpper(TurkishCulture));
        cell.Style.Font.Bold = true;
        cell.Style.Font.FontSize = 7.5;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        cell.Style.Alignment.WrapText = true;
    }

    private static void StyleExcelControlledHeader(IXLRange range)
    {
        range.Style.Border.TopBorder = XLBorderStyleValues.Medium;
        range.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
        range.Style.Border.LeftBorder = XLBorderStyleValues.Medium;
        range.Style.Border.RightBorder = XLBorderStyleValues.Medium;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Medium;
        range.Style.Border.TopBorderColor = XLColor.Black;
        range.Style.Border.BottomBorderColor = XLColor.Black;
        range.Style.Border.LeftBorderColor = XLColor.Black;
        range.Style.Border.RightBorderColor = XLColor.Black;
        range.Style.Border.InsideBorderColor = XLColor.Black;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Alignment.WrapText = true;
        range.Style.Font.FontSize = 7.5;
        range.Style.Font.Bold = true;
    }

    private void AddExcelMinistryLogo(IXLWorksheet sheet)
    {
        if (_ministryLogo is null)
            return;

        using var stream = new MemoryStream(_ministryLogo);
        sheet.AddPicture(stream, $"SaglikBakanligi-{sheet.Name}")
            .MoveTo(sheet.Cell("A1"), 22, 2)
            .WithSize(54, 54);
    }

    private static void WriteExcelPeriodRow(IXLWorksheet sheet, int row, DateTime startDate, DateTime endDate)
    {
        sheet.Range(row, 1, row, 4).Merge().Value = $"Raporlama Dönemi: {startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}";
        var range = sheet.Range(row, 1, row, 4);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.Black;
        range.Style.Font.Bold = true;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Row(row).Height = 20;
    }

    private static void WriteExcelSectionTitle(IXLWorksheet sheet, int row, string title)
    {
        sheet.Range(row, 1, row, 4).Merge().Value = title;
        StyleExcelTableHeader(sheet.Range(row, 1, row, 4));
    }

    private static void WriteExcelTableHeader(IXLWorksheet sheet, int row, params string[] headers)
    {
        for (var index = 0; index < headers.Length; index++)
            sheet.Cell(row, index + 1).Value = headers[index];

        StyleExcelTableHeader(sheet.Range(row, 1, row, headers.Length));
    }

    private static void StyleExcelTableHeader(IXLRange range)
    {
        range.Style.Fill.BackgroundColor = XLColor.FromHtml(OfficialHeaderFill);
        range.Style.Font.Bold = true;
        range.Style.Font.FontSize = 9;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Alignment.WrapText = true;
        range.Style.Border.TopBorder = XLBorderStyleValues.Medium;
        range.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
        range.Style.Border.LeftBorder = XLBorderStyleValues.Medium;
        range.Style.Border.RightBorder = XLBorderStyleValues.Medium;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Medium;
        range.Style.Border.TopBorderColor = XLColor.Black;
        range.Style.Border.BottomBorderColor = XLColor.Black;
        range.Style.Border.LeftBorderColor = XLColor.Black;
        range.Style.Border.RightBorderColor = XLColor.Black;
        range.Style.Border.InsideBorderColor = XLColor.Black;
        range.Worksheet.Row(range.FirstRow().RowNumber()).Height = 23;
    }

    private static void StyleExcelBodyRow(IXLRange range, int row)
    {
        range.Style.Border.TopBorder = XLBorderStyleValues.Thin;
        range.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        range.Style.Border.LeftBorder = XLBorderStyleValues.Thin;
        range.Style.Border.RightBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.TopBorderColor = XLColor.Black;
        range.Style.Border.BottomBorderColor = XLColor.Black;
        range.Style.Border.LeftBorderColor = XLColor.Black;
        range.Style.Border.RightBorderColor = XLColor.Black;
        range.Style.Border.InsideBorderColor = XLColor.Black;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Alignment.WrapText = true;
        if (row % 2 == 0)
            range.Style.Fill.BackgroundColor = XLColor.FromHtml(AlternateRowFill);
        range.Worksheet.Row(row).Height = 19;
    }

    private void FinalizeExcelDataSheet(IXLWorksheet sheet, int nextRow, int columnCount, int firstNumericColumn)
    {
        var hasData = nextRow > 9;
        if (nextRow == 9)
        {
            sheet.Range(9, 1, 9, columnCount).Merge().Value = "Kayıt bulunmamaktadır.";
            StyleExcelBodyRow(sheet.Range(9, 1, 9, columnCount), 9);
            nextRow++;
        }

        if (hasData)
            sheet.Range(9, firstNumericColumn, nextRow - 1, columnCount).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        AddTinyProbelMark(sheet, nextRow + 1);
        sheet.PageSetup.PrintAreas.Add(1, 1, nextRow + 2, 4);
        sheet.AutoFilter.Clear();
        if (nextRow > 9)
            sheet.Range(8, 1, nextRow - 1, columnCount).SetAutoFilter();
    }

    private void AddTinyProbelMark(IXLWorksheet sheet, int row)
    {
        if (_probelLogo is null)
            return;

        using var stream = new MemoryStream(_probelLogo);
        sheet.AddPicture(stream, $"Probel-{sheet.Name}")
            .MoveTo(sheet.Cell(row, 4), 45, 2)
            .WithSize(42, 10);
    }

    private void ComposeControlledDocumentHeader(IContainer container, DateTime generatedAt)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(81.05f);
                columns.RelativeColumn(550.60f);
                columns.RelativeColumn(81f);
                columns.RelativeColumn(81.25f);
            });

            table.Cell().RowSpan(4).MinHeight(57.2f).Element(OfficialHeaderCell).Element(ComposeMinistryLogoCell);
            table.Cell().RowSpan(3).Element(OfficialHeaderCell).Element(ComposeInstitutionCell);
            table.Cell().Element(OfficialHeaderCell).Text("Sayfa No").FontSize(7).Bold();
            table.Cell().Element(OfficialHeaderCell).Text(text =>
            {
                text.CurrentPageNumber().FontSize(7);
                text.Span(" / ").FontSize(7);
                text.TotalPages().FontSize(7);
            });

            table.Cell().Element(OfficialHeaderCell).Text("Doküman Kodu").FontSize(7).Bold();
            table.Cell().Element(OfficialHeaderCell).Text(_documentCode).FontSize(7).Bold();

            table.Cell().Element(OfficialHeaderCell).Text("Yayın Tarihi").FontSize(7).Bold();
            table.Cell().Element(OfficialHeaderCell).Text(_publicationDate).FontSize(7);

            table.Cell().Element(OfficialHeaderCell).Text("HASTA MEMNUNİYET RAPORU").FontSize(9).Bold();
            table.Cell().Element(OfficialHeaderCell).Text("Revizyon No/Tarihi").FontSize(6.5f).Bold();
            table.Cell().Element(OfficialHeaderCell).Text($"{_revisionNumber} / {_revisionDate}").FontSize(7);
        });
    }

    private void ComposeMinistryLogoCell(IContainer container)
    {
        if (_ministryLogo is null)
        {
            container.AlignCenter().AlignMiddle().Text("T.C.\nSAĞLIK BAKANLIĞI").Bold().FontSize(7).FontColor(MinistryRed);
            return;
        }

        container.AlignCenter().AlignMiddle().Width(53).Height(53).Image(_ministryLogo).FitArea();
    }

    private void ComposeInstitutionCell(IContainer container)
    {
        container.AlignCenter().AlignMiddle().Column(column =>
        {
            column.Spacing(1);
            column.Item().AlignCenter().Text("T.C.").Bold().FontSize(7.5f);
            column.Item().AlignCenter().Text("SAĞLIK BAKANLIĞI").Bold().FontSize(7.5f);
            column.Item().AlignCenter().Text(_healthDirectorateName).Bold().FontSize(7.5f);
            column.Item().AlignCenter().Text(_hospitalName.ToUpper(TurkishCulture)).Bold().FontSize(8);
        });
    }

    private static IContainer OfficialHeaderCell(IContainer container) =>
        container.Border(1.5f).BorderColor(Black).PaddingHorizontal(2).PaddingVertical(1).AlignCenter().AlignMiddle();

    private static void ComposePdfContent(
        IContainer container,
        DashboardViewModel dashboard,
        DateTime startDate,
        DateTime endDate)
    {
        container.Column(column =>
        {
            column.Spacing(7);
            column.Item().Element(period => ComposePdfPeriod(period, startDate, endDate));
            column.Item().Element(summary => ComposePdfSummary(summary, dashboard));

            if (dashboard.DepartmentSummaries.Count > 0)
            {
                column.Item().Element(section => ComposePdfDataTable(
                    section,
                    "BÖLÜM BAZLI SONUÇLAR",
                    [3f, 1f, 1f, 1f],
                    ["Bölüm", "Yanıt Sayısı", "Ortalama Puan", "Olumsuz Sayısı"],
                    dashboard.DepartmentSummaries.Select(item => new[]
                    {
                        item.DepartmentName,
                        item.ResponseCount.ToString("N0", TurkishCulture),
                        item.AverageScore.ToString("N2", TurkishCulture),
                        item.NegativeCount.ToString("N0", TurkishCulture)
                    })));
            }

            if (dashboard.DoctorSummaries.Count > 0)
            {
                column.Item().Element(section => ComposePdfDataTable(
                    section,
                    "HEKİM BAZLI SONUÇLAR",
                    [3f, 1f, 1f],
                    ["Hekim", "Yanıt Sayısı", "Ortalama Puan"],
                    dashboard.DoctorSummaries.Select(item => new[]
                    {
                        item.DoctorName,
                        item.ResponseCount.ToString("N0", TurkishCulture),
                        item.AverageScore.ToString("N2", TurkishCulture)
                    })));
            }

            if (dashboard.TrendData.Count > 0)
            {
                column.Item().Element(section => ComposePdfDataTable(
                    section,
                    "DÖNEMSEL TREND",
                    [2f, 1f, 1f, 1f],
                    ["Tarih", "Yanıt Sayısı", "Ortalama Puan", "NPS"],
                    dashboard.TrendData.Select(item => new[]
                    {
                        item.Date.ToString("dd.MM.yyyy", TurkishCulture),
                        item.ResponseCount.ToString("N0", TurkishCulture),
                        item.AverageScore.ToString("N2", TurkishCulture),
                        item.NpsValue?.ToString("N2", TurkishCulture) ?? "-"
                    })));
            }

            if (dashboard.KpiComparisons.Count > 0)
            {
                column.Item().Element(section => ComposePdfDataTable(
                    section,
                    "KPI HEDEF VE GERÇEKLEŞMELERİ",
                    [2f, 1f, 1f, 1f],
                    ["KPI", "Hedef", "Gerçekleşen", "Sapma"],
                    dashboard.KpiComparisons.Select(item => new[]
                    {
                        item.KpiCode,
                        item.TargetValue.ToString("N2", TurkishCulture),
                        item.ActualValue?.ToString("N2", TurkishCulture) ?? "-",
                        item.Deviation?.ToString("+0.00;-0.00;0.00", TurkishCulture) ?? "-"
                    })));
            }
        });
    }

    private static void ComposePdfPeriod(IContainer container, DateTime startDate, DateTime endDate)
    {
        container.Border(1).BorderColor(Black).PaddingVertical(4).AlignCenter().Text(
            $"Raporlama Dönemi: {startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}").Bold().FontSize(8);
    }

    private static void ComposePdfSummary(IContainer container, DashboardViewModel dashboard)
    {
        var metrics = GetSummaryRows(dashboard);

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2);
                columns.RelativeColumn(1);
                columns.RelativeColumn(2);
                columns.RelativeColumn(1);
            });

            table.Cell().ColumnSpan(4).Element(OfficialSectionHeader).Text("ÖZET GÖSTERGELER").Bold().FontSize(8.5f);

            for (var index = 0; index < metrics.Count; index += 2)
            {
                var first = metrics[index];
                var second = metrics[index + 1];
                table.Cell().Element(OfficialLabelCell).Text(first.Label).Bold();
                table.Cell().Element(OfficialNumericCell).Text(first.Value);
                table.Cell().Element(OfficialLabelCell).Text(second.Label).Bold();
                table.Cell().Element(OfficialNumericCell).Text(second.Value);
            }
        });
    }

    private static void ComposePdfDataTable(
        IContainer container,
        string title,
        float[] widths,
        string[] headers,
        IEnumerable<string[]> sourceRows)
    {
        var rows = sourceRows.ToList();

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                foreach (var width in widths)
                    columns.RelativeColumn(width);
            });

            table.Header(header =>
            {
                header.Cell().ColumnSpan((uint)widths.Length).Element(OfficialSectionHeader).Text(title).Bold().FontSize(8.5f);
                foreach (var text in headers)
                    header.Cell().Element(OfficialColumnHeader).Text(text).Bold().FontSize(7.5f);
            });

            if (rows.Count == 0)
            {
                table.Cell().ColumnSpan((uint)widths.Length).Element(OfficialValueCell).AlignCenter().Text("Kayıt bulunmamaktadır.");
                return;
            }

            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var background = rowIndex % 2 == 0 ? "#FFFFFF" : AlternateRowFill;
                for (var columnIndex = 0; columnIndex < rows[rowIndex].Length; columnIndex++)
                {
                    var cell = table.Cell().Background(background);
                    var value = rows[rowIndex][columnIndex];
                    if (columnIndex == 0)
                        cell.Element(OfficialValueCell).Text(value).FontSize(7.2f);
                    else
                        cell.Element(OfficialNumericCell).Text(value).FontSize(7.2f);
                }
            }
        });
    }

    private void ComposePdfFooter(IContainer container)
    {
        container.PaddingTop(5).Row(row =>
        {
            row.RelativeItem().AlignMiddle().Text(_hospitalContactLine).FontFamily(Fonts.Arial).FontSize(6.5f);
            row.AutoItem().AlignMiddle().Text($"{_hospitalUnitName} · ").FontSize(6.5f);
            if (_probelLogo is not null)
            {
                row.ConstantItem(32).Height(8).AlignMiddle().Image(_probelLogo).FitArea();
            }
            else
            {
                row.AutoItem().AlignMiddle().Text("Probel HBYS").FontSize(6);
            }
        });
    }

    private static IContainer OfficialSectionHeader(IContainer container) =>
        container.Background(OfficialHeaderFill).Border(1.5f).BorderColor(Black).PaddingVertical(4).PaddingHorizontal(4).AlignCenter().AlignMiddle();

    private static IContainer OfficialColumnHeader(IContainer container) =>
        container.Background(OfficialHeaderFill).Border(1).BorderColor(Black).PaddingVertical(3).PaddingHorizontal(3).AlignCenter().AlignMiddle();

    private static IContainer OfficialLabelCell(IContainer container) =>
        container.Border(1).BorderColor(Black).PaddingVertical(3).PaddingHorizontal(4).AlignMiddle();

    private static IContainer OfficialValueCell(IContainer container) =>
        container.Border(1).BorderColor(Black).PaddingVertical(3).PaddingHorizontal(4).AlignMiddle();

    private static IContainer OfficialNumericCell(IContainer container) =>
        OfficialValueCell(container).AlignRight();

    private static List<(string Label, string Value)> GetSummaryRows(DashboardViewModel dashboard) =>
    [
        ("Toplam Yanıt", dashboard.TotalResponses.ToString("N0", TurkishCulture)),
        ("Ortalama Puan", dashboard.AverageOverallScore.ToString("N2", TurkishCulture)),
        ("NPS", dashboard.NpsScore.ToString("N2", TurkishCulture)),
        ("CSAT", dashboard.AverageCsat.ToString("N2", TurkishCulture)),
        ("CES", dashboard.AverageCes?.ToString("N2", TurkishCulture) ?? "-"),
        ("Olumsuz Oranı", $"%{dashboard.NegativePercentage.ToString("N2", TurkishCulture)}"),
        ("Gönderilen Davet", dashboard.InvitationsSent.ToString("N0", TurkishCulture)),
        ("Yanıt Oranı", $"%{dashboard.ResponseRate.ToString("N2", TurkishCulture)}")
    ];

    private static string ResolveHealthDirectorateName(IConfiguration configuration)
    {
        var configuredName = configuration["Branding:HealthDirectorateName"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configuredName))
            return configuredName.ToUpper(TurkishCulture);

        var provinceName = configuration["Branding:ProvinceName"]?.Trim();
        return string.IsNullOrWhiteSpace(provinceName)
            ? "İL SAĞLIK MÜDÜRLÜĞÜ"
            : $"{provinceName.ToUpper(TurkishCulture)} İL SAĞLIK MÜDÜRLÜĞÜ";
    }

    private static string StripTcPrefix(string hospitalName)
    {
        var trimmed = hospitalName.Trim();
        return trimmed.StartsWith("T.C. ", StringComparison.OrdinalIgnoreCase)
            ? trimmed[5..].Trim()
            : trimmed;
    }

    private static string ReadSetting(IConfiguration configuration, string key, string fallback)
    {
        var value = configuration[key]?.Trim();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string BuildContactLine(IConfiguration configuration)
    {
        var values = new[]
        {
            configuration["Branding:HospitalAddress"],
            configuration["Branding:HospitalPhone"],
            configuration["Branding:HospitalWebsite"]
        };

        return string.Join("  ·  ", values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim()));
    }

    private static byte[]? ReadAsset(string webRootPath, params string[] relativePath)
    {
        var path = relativePath.Aggregate(webRootPath, Path.Combine);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }
}
