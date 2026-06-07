namespace WebApplication.Services.Admin;

public interface IServiceCategoryAdminService
{
    Task<IReadOnlyList<ServiceCategoryRecord>> ListAsync(bool includeArchived, CancellationToken cancellationToken = default);

    Task<ServiceCategoryRecord?> GetAsync(int idCategory, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SpecialtyOption>> GetSpecialtyOptionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SettingOption>> GetSettingOptionsAsync(CancellationToken cancellationToken = default);

    Task<ServiceCategoryAdminResult> CreateAsync(ServiceCategorySaveRequest request, CancellationToken cancellationToken = default);

    Task<ServiceCategoryAdminResult> UpdateAsync(int idCategory, ServiceCategorySaveRequest request, CancellationToken cancellationToken = default);

    Task<ServiceCategoryAdminResult> ArchiveAsync(int idCategory, CancellationToken cancellationToken = default);

    Task<ServiceCategoryAdminResult> RestoreAsync(int idCategory, CancellationToken cancellationToken = default);
}
