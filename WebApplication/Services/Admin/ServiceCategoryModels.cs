namespace WebApplication.Services.Admin;

public sealed class ServiceCategoryRecord
{
    public int IdCategory { get; init; }
    public string Name { get; init; } = "";
    public string Letter { get; init; } = "";
    public int Priority { get; init; }
    public bool IsArchived { get; init; }
    public int IdSetting { get; init; }
    public string? SettingName { get; init; }
    public int? StartSpecialtyId { get; init; }
    public int? EndSpecialtyId { get; init; }
    public string? StartSpecialtyName { get; init; }
    public string? EndSpecialtyName { get; init; }
    public int TimePause { get; init; }
    public int CriticalNumPause { get; init; }
    public int SharedCategoryCount { get; init; }
}

public sealed class ServiceCategorySaveRequest
{
    public string Name { get; set; } = "";
    public string Letter { get; set; } = "";
    public int Priority { get; set; }
    public int? IdSetting { get; set; }
    public int? StartSpecialtyId { get; set; }
    public int? EndSpecialtyId { get; set; }
    public int TimePause { get; set; }
    public int CriticalNumPause { get; set; }
    public string? SettingName { get; set; }
    public bool ConfirmSharedSettingUpdate { get; set; }
}

public sealed class SettingOption
{
    public int Id { get; init; }
    public string Label { get; init; } = "";
    public int ActiveCategoryCount { get; init; }
    public string? SettingName { get; init; }
    public string? StartSpecialtyName { get; init; }
    public string? EndSpecialtyName { get; init; }
    public int TimePause { get; init; }
    public int CriticalNumPause { get; init; }
}

public sealed class SpecialtyOption
{
    public int Id { get; init; }
    public string Label { get; init; } = "";
}

public sealed class ServiceCategoryAdminResult
{
    public bool Succeeded { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public ServiceCategoryRecord? Data { get; init; }
    public int SharedCategoryCount { get; init; }
    public IReadOnlyList<string> SharedCategoryNames { get; init; } = [];

    public static ServiceCategoryAdminResult Ok(ServiceCategoryRecord data) =>
        new() { Succeeded = true, Data = data };

    public static ServiceCategoryAdminResult Fail(string message, IReadOnlyList<string>? errors = null, string? errorCode = null) =>
        new()
        {
            Succeeded = false,
            Message = message,
            Errors = errors ?? [],
            ErrorCode = errorCode
        };

    public static ServiceCategoryAdminResult SharedSettingConfirmationRequired(
        int count,
        IReadOnlyList<string> names) =>
        new()
        {
            Succeeded = false,
            ErrorCode = "sharedSettingConfirmationRequired",
            Message = "Изменения настроек затронут другие категории обслуживания.",
            SharedCategoryCount = count,
            SharedCategoryNames = names
        };
}

public sealed class ServiceCategorySettingSnapshot
{
    public int IdSetting { get; init; }
    public string? SettingName { get; init; }
    public int? StartSpecialtyId { get; init; }
    public int? EndSpecialtyId { get; init; }
    public int TimePause { get; init; }
    public int CriticalNumPause { get; init; }
}
