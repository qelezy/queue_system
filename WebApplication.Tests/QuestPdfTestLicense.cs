using System.Runtime.CompilerServices;
using QuestPDF.Infrastructure;

namespace WebApplication.Tests;

internal static class QuestPdfTestLicense
{
    [ModuleInitializer]
    public static void Initialize() =>
        QuestPDF.Settings.License = LicenseType.Community;
}
