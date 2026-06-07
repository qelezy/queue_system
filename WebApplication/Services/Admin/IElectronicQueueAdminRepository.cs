namespace WebApplication.Services.Admin;

public interface IElectronicQueueAdminRepository
{
    Task<IReadOnlyList<ServiceCategoryRecord>> ListCategoriesAsync(bool includeArchived, CancellationToken cancellationToken = default);

    Task<ServiceCategoryRecord?> GetCategoryAsync(int idCategory, CancellationToken cancellationToken = default);

    Task<ServiceCategorySettingSnapshot?> GetSettingSnapshotAsync(int idSetting, CancellationToken cancellationToken = default);

    Task<int> CountActiveCategoriesBySettingAsync(int idSetting, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetActiveCategoryNamesBySettingAsync(int idSetting, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(ServiceCategorySaveRequest request, CancellationToken cancellationToken = default);

    Task UpdateCategoryAsync(int idCategory, ServiceCategorySaveRequest request, CancellationToken cancellationToken = default);

    Task UpdateSettingAsync(int idSetting, ServiceCategorySaveRequest request, CancellationToken cancellationToken = default);

    Task ArchiveAsync(int idCategory, CancellationToken cancellationToken = default);

    Task RestoreAsync(int idCategory, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SettingOption>> ListSettingsAsync(CancellationToken cancellationToken = default);

    Task<bool> SettingExistsAsync(int idSetting, CancellationToken cancellationToken = default);

    Task<bool> ExistsActiveLetterAsync(string letter, int? excludeCategoryId, CancellationToken cancellationToken = default);

    Task<bool> SpecialtyExistsAsync(int idSpecialty, CancellationToken cancellationToken = default);

    Task<bool> HasOpenAppointmentsTodayAsync(int idCategory, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SpecialtyOption>> ListSpecialtiesAsync(CancellationToken cancellationToken = default);
}
