using WebApplication.Services.Admin;
using Xunit;

namespace WebApplication.Tests.Admin;

public sealed class ServiceCategoryAdminServiceTests
{
    [Fact]
    public async Task CreateAsync_ReturnsValidationError_WhenLetterDuplicate()
    {
        var repo = new FakeElectronicQueueAdminRepository
        {
            ActiveLetters = { ["A"] = true }
        };
        var service = new ServiceCategoryAdminService(repo);

        var result = await service.CreateAsync(new ServiceCategorySaveRequest
        {
            Name = "Test",
            Letter = "A",
            Priority = 1,
            TimePause = 5,
            CriticalNumPause = 3
        });

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("Буква талона"));
    }

    [Fact]
    public async Task CreateAsync_LinksExistingSetting_WhenIdSettingProvided()
    {
        var repo = new FakeElectronicQueueAdminRepository
        {
            ExistingSettings = { [10] = true }
        };
        var service = new ServiceCategoryAdminService(repo);

        var result = await service.CreateAsync(new ServiceCategorySaveRequest
        {
            Name = "Variant",
            Letter = "Z",
            Priority = 2,
            IdSetting = 10
        });

        Assert.True(result.Succeeded);
        Assert.Equal(10, repo.LastCreateIdSetting);
        Assert.False(repo.LastCreateWithNewSetting);
    }

    [Fact]
    public async Task CreateAsync_CreatesNewSetting_WhenIdSettingNotProvided()
    {
        var repo = new FakeElectronicQueueAdminRepository();
        var service = new ServiceCategoryAdminService(repo);

        var result = await service.CreateAsync(new ServiceCategorySaveRequest
        {
            Name = "New route",
            Letter = "Z",
            Priority = 2,
            TimePause = 5,
            CriticalNumPause = 3,
            EndSpecialtyId = 2
        });

        Assert.True(result.Succeeded);
        Assert.Null(repo.LastCreateIdSetting);
        Assert.True(repo.LastCreateWithNewSetting);
    }

    [Fact]
    public async Task CreateAsync_Fails_WhenLinkedSettingNotFound()
    {
        var repo = new FakeElectronicQueueAdminRepository();
        var service = new ServiceCategoryAdminService(repo);

        var result = await service.CreateAsync(new ServiceCategorySaveRequest
        {
            Name = "Variant",
            Letter = "Z",
            Priority = 2,
            IdSetting = 99
        });

        Assert.False(result.Succeeded);
        Assert.Contains("не найдена", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsSharedSettingConfirmation_WhenSettingChangedAndShared()
    {
        var repo = new FakeElectronicQueueAdminRepository
        {
            Categories =
            [
                new ServiceCategoryRecord
                {
                    IdCategory = 1,
                    IdSetting = 10,
                    Name = "A",
                    Letter = "A",
                    Priority = 1,
                    TimePause = 5,
                    CriticalNumPause = 3,
                    SharedCategoryCount = 2
                }
            ],
            SharedCountBySetting = { [10] = 2 },
            SharedNamesBySetting = { [10] = ["A", "B"] }
        };
        var service = new ServiceCategoryAdminService(repo);

        var result = await service.UpdateAsync(1, new ServiceCategorySaveRequest
        {
            Name = "A",
            Letter = "A",
            Priority = 1,
            TimePause = 7,
            CriticalNumPause = 3,
            ConfirmSharedSettingUpdate = false
        });

        Assert.False(result.Succeeded);
        Assert.Equal("sharedSettingConfirmationRequired", result.ErrorCode);
        Assert.Equal(2, result.SharedCategoryCount);
        Assert.Equal(["A", "B"], result.SharedCategoryNames);
        Assert.False(repo.SettingUpdated);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesSharedSetting_WhenConfirmed()
    {
        var repo = new FakeElectronicQueueAdminRepository
        {
            Categories =
            [
                new ServiceCategoryRecord
                {
                    IdCategory = 1,
                    IdSetting = 10,
                    Name = "A",
                    Letter = "A",
                    Priority = 1,
                    TimePause = 5,
                    CriticalNumPause = 3,
                    SharedCategoryCount = 2
                }
            ],
            SharedCountBySetting = { [10] = 2 }
        };
        var service = new ServiceCategoryAdminService(repo);

        var result = await service.UpdateAsync(1, new ServiceCategorySaveRequest
        {
            Name = "A",
            Letter = "A",
            Priority = 1,
            TimePause = 7,
            CriticalNumPause = 3,
            ConfirmSharedSettingUpdate = true
        });

        Assert.True(result.Succeeded);
        Assert.True(repo.SettingUpdated);
        Assert.True(repo.CategoryUpdated);
    }

    [Fact]
    public async Task ArchiveAsync_SetsArchivedResult()
    {
        var repo = new FakeElectronicQueueAdminRepository
        {
            Categories =
            [
                new ServiceCategoryRecord
                {
                    IdCategory = 5,
                    IdSetting = 1,
                    Name = "Cat",
                    Letter = "C",
                    Priority = 1,
                    TimePause = 5,
                    CriticalNumPause = 3,
                    SharedCategoryCount = 1
                }
            ]
        };
        var service = new ServiceCategoryAdminService(repo);

        var result = await service.ArchiveAsync(5);

        Assert.True(result.Succeeded);
        Assert.Contains(5, repo.ArchivedCategoryIds);
        Assert.True(result.Data?.IsArchived);
    }

    [Fact]
    public async Task ArchiveAsync_Fails_WhenOpenAppointmentsToday()
    {
        var repo = new FakeElectronicQueueAdminRepository
        {
            Categories =
            [
                new ServiceCategoryRecord
                {
                    IdCategory = 5,
                    IdSetting = 1,
                    Name = "Cat",
                    Letter = "C",
                    Priority = 1,
                    TimePause = 5,
                    CriticalNumPause = 3,
                    SharedCategoryCount = 1
                }
            ],
            OpenAppointmentsToday = { [5] = true }
        };
        var service = new ServiceCategoryAdminService(repo);

        var result = await service.ArchiveAsync(5);

        Assert.False(result.Succeeded);
        Assert.Contains("открытые талоны", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RestoreAsync_RestoresArchivedCategory()
    {
        var repo = new FakeElectronicQueueAdminRepository
        {
            Categories =
            [
                new ServiceCategoryRecord
                {
                    IdCategory = 5,
                    IdSetting = 1,
                    Name = "Cat",
                    Letter = "C",
                    Priority = 1,
                    TimePause = 5,
                    CriticalNumPause = 3,
                    SharedCategoryCount = 1,
                    IsArchived = true
                }
            ]
        };
        var service = new ServiceCategoryAdminService(repo);

        var result = await service.RestoreAsync(5);

        Assert.True(result.Succeeded);
        Assert.Contains(5, repo.RestoredCategoryIds);
        Assert.False(result.Data?.IsArchived);
    }

    [Fact]
    public async Task RestoreAsync_Fails_WhenAlreadyActive()
    {
        var repo = new FakeElectronicQueueAdminRepository
        {
            Categories =
            [
                new ServiceCategoryRecord
                {
                    IdCategory = 5,
                    IdSetting = 1,
                    Name = "Cat",
                    Letter = "C",
                    Priority = 1,
                    TimePause = 5,
                    CriticalNumPause = 3,
                    SharedCategoryCount = 1,
                    IsArchived = false
                }
            ]
        };
        var service = new ServiceCategoryAdminService(repo);

        var result = await service.RestoreAsync(5);

        Assert.False(result.Succeeded);
        Assert.Contains("уже активна", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RestoreAsync_Fails_WhenLetterDuplicate()
    {
        var repo = new FakeElectronicQueueAdminRepository
        {
            Categories =
            [
                new ServiceCategoryRecord
                {
                    IdCategory = 5,
                    IdSetting = 1,
                    Name = "Cat",
                    Letter = "C",
                    Priority = 1,
                    TimePause = 5,
                    CriticalNumPause = 3,
                    SharedCategoryCount = 1,
                    IsArchived = true
                }
            ],
            ActiveLetters = { ["C"] = true }
        };
        var service = new ServiceCategoryAdminService(repo);

        var result = await service.RestoreAsync(5);

        Assert.False(result.Succeeded);
        Assert.Contains("буква талона", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeElectronicQueueAdminRepository : IElectronicQueueAdminRepository
    {
        public List<ServiceCategoryRecord> Categories { get; init; } = [];
        public Dictionary<string, bool> ActiveLetters { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<int, int> SharedCountBySetting { get; init; } = [];
        public Dictionary<int, List<string>> SharedNamesBySetting { get; init; } = [];
        public Dictionary<int, bool> OpenAppointmentsToday { get; init; } = [];
        public Dictionary<int, bool> ExistingSettings { get; init; } = [];
        public bool SettingUpdated { get; private set; }
        public bool CategoryUpdated { get; private set; }
        public int? LastCreateIdSetting { get; private set; }
        public bool LastCreateWithNewSetting { get; private set; }
        public HashSet<int> ArchivedCategoryIds { get; } = [];
        public HashSet<int> RestoredCategoryIds { get; } = [];

        public Task ArchiveAsync(int idCategory, CancellationToken cancellationToken = default)
        {
            ArchivedCategoryIds.Add(idCategory);
            var cat = Categories.First(c => c.IdCategory == idCategory);
            Categories.Remove(cat);
            Categories.Add(new ServiceCategoryRecord
            {
                IdCategory = cat.IdCategory,
                IdSetting = cat.IdSetting,
                Name = cat.Name,
                Letter = cat.Letter,
                Priority = cat.Priority,
                TimePause = cat.TimePause,
                CriticalNumPause = cat.CriticalNumPause,
                SharedCategoryCount = cat.SharedCategoryCount,
                IsArchived = true
            });
            return Task.CompletedTask;
        }

        public Task RestoreAsync(int idCategory, CancellationToken cancellationToken = default)
        {
            RestoredCategoryIds.Add(idCategory);
            var cat = Categories.First(c => c.IdCategory == idCategory);
            Categories.Remove(cat);
            Categories.Add(new ServiceCategoryRecord
            {
                IdCategory = cat.IdCategory,
                IdSetting = cat.IdSetting,
                Name = cat.Name,
                Letter = cat.Letter,
                Priority = cat.Priority,
                TimePause = cat.TimePause,
                CriticalNumPause = cat.CriticalNumPause,
                SharedCategoryCount = cat.SharedCategoryCount,
                IsArchived = false
            });
            return Task.CompletedTask;
        }

        public Task<int> CountActiveCategoriesBySettingAsync(int idSetting, CancellationToken cancellationToken = default) =>
            Task.FromResult(SharedCountBySetting.TryGetValue(idSetting, out var count) ? count : 1);

        public Task<int> CreateAsync(ServiceCategorySaveRequest request, CancellationToken cancellationToken = default)
        {
            if (request.IdSetting is int idSetting && idSetting > 0)
            {
                LastCreateIdSetting = idSetting;
                LastCreateWithNewSetting = false;
            }
            else
            {
                LastCreateIdSetting = null;
                LastCreateWithNewSetting = true;
            }

            var created = new ServiceCategoryRecord
            {
                IdCategory = 100,
                IdSetting = request.IdSetting ?? 200,
                Name = request.Name,
                Letter = request.Letter,
                Priority = request.Priority,
                TimePause = request.TimePause,
                CriticalNumPause = request.CriticalNumPause,
                SharedCategoryCount = 1
            };
            Categories.Add(created);

            return Task.FromResult(100);
        }

        public Task<bool> ExistsActiveLetterAsync(string letter, int? excludeCategoryId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ActiveLetters.ContainsKey(letter.Trim()));

        public Task<ServiceCategoryRecord?> GetCategoryAsync(int idCategory, CancellationToken cancellationToken = default) =>
            Task.FromResult(Categories.FirstOrDefault(c => c.IdCategory == idCategory));

        public Task<IReadOnlyList<string>> GetActiveCategoryNamesBySettingAsync(int idSetting, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(SharedNamesBySetting.TryGetValue(idSetting, out var names) ? names : []);

        public Task<ServiceCategorySettingSnapshot?> GetSettingSnapshotAsync(int idSetting, CancellationToken cancellationToken = default) =>
            Task.FromResult<ServiceCategorySettingSnapshot?>(null);

        public Task<bool> HasOpenAppointmentsTodayAsync(int idCategory, CancellationToken cancellationToken = default) =>
            Task.FromResult(OpenAppointmentsToday.TryGetValue(idCategory, out var open) && open);

        public Task<IReadOnlyList<ServiceCategoryRecord>> ListCategoriesAsync(bool includeArchived, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ServiceCategoryRecord>>(Categories);

        public Task<IReadOnlyList<SettingOption>> ListSettingsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SettingOption>>([]);

        public Task<IReadOnlyList<SpecialtyOption>> ListSpecialtiesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SpecialtyOption>>([]);

        public Task<bool> SettingExistsAsync(int idSetting, CancellationToken cancellationToken = default) =>
            Task.FromResult(ExistingSettings.ContainsKey(idSetting));

        public Task<bool> SpecialtyExistsAsync(int idSpecialty, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task UpdateCategoryAsync(int idCategory, ServiceCategorySaveRequest request, CancellationToken cancellationToken = default)
        {
            CategoryUpdated = true;
            return Task.CompletedTask;
        }

        public Task UpdateSettingAsync(int idSetting, ServiceCategorySaveRequest request, CancellationToken cancellationToken = default)
        {
            SettingUpdated = true;
            return Task.CompletedTask;
        }
    }
}
