using ClosedXML.Excel;
using HastaGeriBildirim.Models.ViewModels;
using HastaGeriBildirim.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace HastaGeriBildirim.Tests;

public class ReportExportServiceTests
{
    [Fact]
    public void BuildPdfAndExcel_CreateValidBrandedDocuments()
    {
        var service = CreateService();
        var dashboard = new DashboardViewModel
        {
            TotalResponses = 125,
            AverageOverallScore = 4.32m,
            AverageCsat = 88.5m,
            NpsScore = 42.1m,
            NegativePercentage = 7.2m,
            AverageCes = 4.1m,
            InvitationsSent = 200,
            ResponseRate = 62.5m,
            DepartmentSummaries = [new() { DepartmentName = "Dahiliye", ResponseCount = 35, AverageScore = 4.4m, NegativeCount = 2 }],
            DoctorSummaries = [new() { DoctorName = "Dr. Test Hekimi", ResponseCount = 20, AverageScore = 4.5m }],
            TrendData = [new() { Date = new DateTime(2026, 7, 1), ResponseCount = 15, AverageScore = 4.25m, NpsValue = 41m }],
            KpiComparisons = [new() { KpiCode = "CSAT", TargetValue = 85m, ActualValue = 88.5m }]
        };

        var pdf = service.BuildPdf(dashboard, new DateTime(2026, 7, 1), new DateTime(2026, 7, 13));
        var excel = service.BuildExcel(dashboard, new DateTime(2026, 7, 1), new DateTime(2026, 7, 13));

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.Equal("PK", System.Text.Encoding.ASCII.GetString(excel, 0, 2));
        Assert.True(pdf.Length > 10_000);
        Assert.True(excel.Length > 10_000);

        var pdfText = System.Text.Encoding.Latin1.GetString(pdf);
        var mediaBox = System.Text.RegularExpressions.Regex.Match(
            pdfText,
            @"/MediaBox\s*\[\s*0\s+0\s+([\d.]+)\s+([\d.]+)\s*\]");
        Assert.True(mediaBox.Success, "PDF sayfa ölçüsü bulunamadı.");
        var pageWidth = double.Parse(mediaBox.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        var pageHeight = double.Parse(mediaBox.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(pageHeight > pageWidth, "PDF tamamen A4 dikey olmalıdır.");

        using var workbook = new XLWorkbook(new MemoryStream(excel));
        var summarySheet = workbook.Worksheet("Özet");
        Assert.Equal("Sayfa No", summarySheet.Cell("C1").GetString());
        Assert.Equal("HGB.RP.01", summarySheet.Cell("D2").GetString());
        Assert.Contains("SAĞLIK BAKANLIĞI", summarySheet.Cell("B1").GetString());
        Assert.Equal("ÖZET GÖSTERGELER", summarySheet.Cell("A8").GetString());
        Assert.True(summarySheet.Pictures.Count >= 2);
        Assert.Equal(XLAlignmentHorizontalValues.Right, summarySheet.Cell("C9").Style.Alignment.Horizontal);
        Assert.Equal(XLAlignmentHorizontalValues.Right, workbook.Worksheet("Bölümler").Cell("B9").Style.Alignment.Horizontal);
        Assert.Equal(XLAlignmentHorizontalValues.Right, workbook.Worksheet("Hekimler").Cell("C9").Style.Alignment.Horizontal);
        Assert.Equal(XLAlignmentHorizontalValues.Right, workbook.Worksheet("Trend").Cell("B9").Style.Alignment.Horizontal);
        Assert.Equal(XLAlignmentHorizontalValues.Right, workbook.Worksheet("KPI Hedefleri").Cell("B9").Style.Alignment.Horizontal);

        if (Environment.GetEnvironmentVariable("HGB_WRITE_REPORT_PREVIEW") == "1")
        {
            File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "hgb-official-report-preview.pdf"), pdf);
            File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "hgb-official-report-preview.xlsx"), excel);
        }
    }

    private static ReportExportService CreateService()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HastaGeriBildirim"));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Branding:HospitalName"] = "T.C. Test Devlet Hastanesi",
            ["Branding:HospitalUnitName"] = "Kalite ve Hasta Deneyimi Birimi",
            ["Branding:HealthDirectorateName"] = "ANKARA İL SAĞLIK MÜDÜRLÜĞÜ"
        }).Build();
        return new ReportExportService(configuration, new FakeEnvironment
        {
            ContentRootPath = projectRoot,
            WebRootPath = Path.Combine(projectRoot, "wwwroot")
        });
    }

    private sealed class FakeEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "HastaGeriBildirim.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
