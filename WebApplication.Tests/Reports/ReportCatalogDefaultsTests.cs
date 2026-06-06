using Microsoft.Extensions.Options;
using WebApplication.Models.Configuration;
using WebApplication.Models.Reports.Configuration;
using WebApplication.Models.Reports.Constants;
using WebApplication.Services.Reports;
using Xunit;

namespace WebApplication.Tests.Reports;

public sealed class ReportCatalogDefaultsTests
{
    [Theory]
    [InlineData(ReportIds.LoadAndDowntime, ReportGeneratorKind.LoadAndDowntime)]
    [InlineData(ReportIds.WaitingBeforeAppointment, ReportGeneratorKind.WaitingBeforeAppointment)]
    [InlineData(ReportIds.AppointmentDuration, ReportGeneratorKind.AppointmentDuration)]
    [InlineData(ReportIds.StagesAndWaiting, ReportGeneratorKind.StagesAndWaiting)]
    [InlineData(ReportIds.ServiceRouteOutcomes, ReportGeneratorKind.ServiceRouteOutcomes)]
    [InlineData(ReportIds.ServiceCategoriesComparison, ReportGeneratorKind.ServiceCategoriesComparison)]
    [InlineData(ReportIds.ServiceDelays, ReportGeneratorKind.ServiceDelays)]
    public void TryResolveGeneratorKind_maps_all_report_ids(string reportId, ReportGeneratorKind expected)
    {
        Assert.True(ReportCatalogDefaults.TryResolveGeneratorKind(reportId, out var kind));
        Assert.Equal(expected, kind);
    }

    [Fact]
    public void GetPresentationDefaults_serviceRouteOutcomes_uses_arrivedCompleted_detail_row_kind()
    {
        var presentation = ReportCatalogDefaults.GetPresentationDefaults(ReportGeneratorKind.ServiceRouteOutcomes);

        Assert.Equal(ReportTableLayouts.DateRowspan, presentation.TableLayout);
        Assert.Equal(ReportPdfOrientations.Landscape, presentation.PdfOrientation);
        Assert.Equal(ReportDetailRowKinds.ArrivedCompleted, presentation.DetailRowKind);
    }

    [Fact]
    public void GetPresentationDefaults_appointmentDuration_uses_portrait_pdf()
    {
        var presentation = ReportCatalogDefaults.GetPresentationDefaults(ReportGeneratorKind.AppointmentDuration);

        Assert.Equal(ReportPdfOrientations.Portrait, presentation.PdfOrientation);
        Assert.Equal(ReportDetailRowKinds.AppointmentDuration, presentation.DetailRowKind);
    }

    [Fact]
    public void ReportsCatalog_maps_item_without_technical_fields_in_options()
    {
        var options = Options.Create(new ReportsOptions
        {
            Catalog =
            [
                new ReportCatalogItemOptions
                {
                    Id = ReportIds.LoadAndDowntime,
                    Category = "resource-load",
                    Title = "Загрузка и простои",
                    Description = "Test"
                }
            ]
        });

        var catalog = new ReportsCatalog(options);
        Assert.True(catalog.TryGetItem(ReportIds.LoadAndDowntime, out var item));
        Assert.NotNull(item);
        Assert.Equal(ReportGeneratorKind.LoadAndDowntime, item.GeneratorKind);
        Assert.Equal(ReportTableLayouts.DateRowspan, item.TableLayout);
        Assert.Equal(ReportPdfOrientations.Landscape, item.PdfOrientation);
        Assert.Equal(ReportDetailRowKinds.LoadDowntime, item.DetailRowKind);
    }

    [Fact]
    public void ParseRequiredKind_unknown_id_throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ReportCatalogDefaults.ParseRequiredKind("unknown-report"));
        Assert.Contains("unknown-report", ex.Message, StringComparison.Ordinal);
    }
}
