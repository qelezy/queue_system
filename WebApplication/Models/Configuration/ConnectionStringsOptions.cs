namespace WebApplication.Models.Configuration;

public sealed class ConnectionStringsOptions
{
    public const string SectionName = "ConnectionStrings";

    public string UserDatabase { get; set; } = string.Empty;

    public string ElectronicQueue { get; set; } = string.Empty;
}
