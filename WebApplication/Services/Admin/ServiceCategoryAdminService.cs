namespace WebApplication.Services.Admin;

public sealed class ServiceCategoryAdminService : IServiceCategoryAdminService
{
    private readonly IElectronicQueueAdminRepository _repository;

    public ServiceCategoryAdminService(IElectronicQueueAdminRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<ServiceCategoryRecord>> ListAsync(
        bool includeArchived,
        CancellationToken cancellationToken = default) =>
        _repository.ListCategoriesAsync(includeArchived, cancellationToken);

    public Task<ServiceCategoryRecord?> GetAsync(int idCategory, CancellationToken cancellationToken = default) =>
        _repository.GetCategoryAsync(idCategory, cancellationToken);

    public Task<IReadOnlyList<SpecialtyOption>> GetSpecialtyOptionsAsync(CancellationToken cancellationToken = default) =>
        _repository.ListSpecialtiesAsync(cancellationToken);

    public Task<IReadOnlyList<SettingOption>> GetSettingOptionsAsync(CancellationToken cancellationToken = default) =>
        _repository.ListSettingsAsync(cancellationToken);

    public async Task<ServiceCategoryAdminResult> CreateAsync(
        ServiceCategorySaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var linkExisting = request.IdSetting is int idSetting && idSetting > 0;

        if (linkExisting)
        {
            if (!await _repository.SettingExistsAsync(request.IdSetting!.Value, cancellationToken).ConfigureAwait(false))
                return ServiceCategoryAdminResult.Fail("Настройка обслуживания не найдена.");
        }

        var validation = await ValidateAsync(
            request,
            excludeCategoryId: null,
            validateSettingFields: !linkExisting,
            cancellationToken).ConfigureAwait(false);
        if (validation is not null)
            return validation;

        var idCategory = await _repository.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        var created = await _repository.GetCategoryAsync(idCategory, cancellationToken).ConfigureAwait(false);
        return created is null
            ? ServiceCategoryAdminResult.Fail("Категория создана, но не удалось загрузить результат.")
            : ServiceCategoryAdminResult.Ok(created);
    }

    public async Task<ServiceCategoryAdminResult> UpdateAsync(
        int idCategory,
        ServiceCategorySaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetCategoryAsync(idCategory, cancellationToken).ConfigureAwait(false);
        if (existing is null)
            return ServiceCategoryAdminResult.Fail("Категория не найдена.");

        if (existing.IsArchived)
            return ServiceCategoryAdminResult.Fail("Архивную категорию нельзя редактировать.");

        var validation = await ValidateAsync(
            request,
            idCategory,
            validateSettingFields: true,
            cancellationToken).ConfigureAwait(false);
        if (validation is not null)
            return validation;

        var settingChanged = SettingChanged(existing, request);
        if (settingChanged)
        {
            var sharedCount = await _repository.CountActiveCategoriesBySettingAsync(existing.IdSetting, cancellationToken)
                .ConfigureAwait(false);
            if (sharedCount > 1 && !request.ConfirmSharedSettingUpdate)
            {
                var names = await _repository.GetActiveCategoryNamesBySettingAsync(existing.IdSetting, cancellationToken)
                    .ConfigureAwait(false);
                return ServiceCategoryAdminResult.SharedSettingConfirmationRequired(sharedCount, names);
            }

            await _repository.UpdateSettingAsync(existing.IdSetting, request, cancellationToken).ConfigureAwait(false);
        }

        await _repository.UpdateCategoryAsync(idCategory, request, cancellationToken).ConfigureAwait(false);

        var updated = await _repository.GetCategoryAsync(idCategory, cancellationToken).ConfigureAwait(false);
        return updated is null
            ? ServiceCategoryAdminResult.Fail("Категория обновлена, но не удалось загрузить результат.")
            : ServiceCategoryAdminResult.Ok(updated);
    }

    public async Task<ServiceCategoryAdminResult> ArchiveAsync(int idCategory, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetCategoryAsync(idCategory, cancellationToken).ConfigureAwait(false);
        if (existing is null)
            return ServiceCategoryAdminResult.Fail("Категория не найдена.");

        if (existing.IsArchived)
            return ServiceCategoryAdminResult.Fail("Категория уже в архиве.");

        if (await _repository.HasOpenAppointmentsTodayAsync(idCategory, cancellationToken).ConfigureAwait(false))
        {
            return ServiceCategoryAdminResult.Fail(
                "Нельзя архивировать категорию: есть открытые талоны за сегодня.");
        }

        await _repository.ArchiveAsync(idCategory, cancellationToken).ConfigureAwait(false);
        var archived = await _repository.GetCategoryAsync(idCategory, cancellationToken).ConfigureAwait(false);
        return archived is null
            ? ServiceCategoryAdminResult.Fail("Категория архивирована, но не удалось загрузить результат.")
            : ServiceCategoryAdminResult.Ok(archived);
    }

    public async Task<ServiceCategoryAdminResult> RestoreAsync(int idCategory, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetCategoryAsync(idCategory, cancellationToken).ConfigureAwait(false);
        if (existing is null)
            return ServiceCategoryAdminResult.Fail("Категория не найдена.");

        if (!existing.IsArchived)
            return ServiceCategoryAdminResult.Fail("Категория уже активна.");

        if (await _repository.ExistsActiveLetterAsync(existing.Letter, excludeCategoryId: null, cancellationToken)
                .ConfigureAwait(false))
        {
            return ServiceCategoryAdminResult.Fail(
                "Нельзя восстановить: буква талона уже используется другой активной категорией.");
        }

        await _repository.RestoreAsync(idCategory, cancellationToken).ConfigureAwait(false);
        var restored = await _repository.GetCategoryAsync(idCategory, cancellationToken).ConfigureAwait(false);
        return restored is null
            ? ServiceCategoryAdminResult.Fail("Категория восстановлена, но не удалось загрузить результат.")
            : ServiceCategoryAdminResult.Ok(restored);
    }

    private async Task<ServiceCategoryAdminResult?> ValidateAsync(
        ServiceCategorySaveRequest request,
        int? excludeCategoryId,
        bool validateSettingFields,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Name))
            errors.Add("Укажите название категории.");

        if (string.IsNullOrWhiteSpace(request.Letter))
            errors.Add("Укажите букву талона.");
        else if (request.Letter.Trim().Length != 1)
            errors.Add("Буква талона должна быть одним символом.");

        if (request.Priority < 0)
            errors.Add("Приоритет не может быть отрицательным.");

        if (validateSettingFields)
        {
            if (request.TimePause < 0)
                errors.Add("Время паузы не может быть отрицательным.");

            if (request.CriticalNumPause < 0)
                errors.Add("Критическое количество пауз не может быть отрицательным.");

            if (request.EndSpecialtyId is int endId
                && !await _repository.SpecialtyExistsAsync(endId, cancellationToken).ConfigureAwait(false))
            {
                errors.Add("Конечная специальность не найдена.");
            }

            if (request.StartSpecialtyId is int startId
                && !await _repository.SpecialtyExistsAsync(startId, cancellationToken).ConfigureAwait(false))
            {
                errors.Add("Начальная специальность не найдена.");
            }

            if (!string.IsNullOrWhiteSpace(request.SettingName) && request.SettingName.Trim().Length > 64)
                errors.Add("Название настройки не должно превышать 64 символа.");
        }

        if (!string.IsNullOrWhiteSpace(request.Letter)
            && request.Letter.Trim().Length == 1
            && await _repository.ExistsActiveLetterAsync(request.Letter, excludeCategoryId, cancellationToken).ConfigureAwait(false))
        {
            errors.Add("Буква талона уже используется другой активной категорией.");
        }

        return errors.Count == 0
            ? null
            : ServiceCategoryAdminResult.Fail("Проверьте введённые данные.", errors);
    }

    private static bool SettingChanged(ServiceCategoryRecord existing, ServiceCategorySaveRequest request)
    {
        var newSettingName = ResolveSettingName(request);
        var existingSettingName = existing.SettingName?.Trim() ?? "";

        return existing.StartSpecialtyId != request.StartSpecialtyId
            || existing.EndSpecialtyId != request.EndSpecialtyId
            || existing.TimePause != request.TimePause
            || existing.CriticalNumPause != request.CriticalNumPause
            || !string.Equals(existingSettingName, newSettingName, StringComparison.Ordinal);
    }

    private static string ResolveSettingName(ServiceCategorySaveRequest request) =>
        string.IsNullOrWhiteSpace(request.SettingName) ? request.Name.Trim() : request.SettingName.Trim();
}
