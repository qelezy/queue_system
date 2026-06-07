using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using WebApplication.Models.Configuration;

namespace WebApplication.Services.Admin;

public sealed class ElectronicQueueAdminRepository : IElectronicQueueAdminRepository
{
    private readonly string _connectionString;

    public ElectronicQueueAdminRepository(IOptions<ConnectionStringsOptions> options)
    {
        _connectionString = options.Value.ElectronicQueue
            ?? throw new InvalidOperationException("Строка подключения ElectronicQueue не задана.");
    }

    public async Task<IReadOnlyList<ServiceCategoryRecord>> ListCategoriesAsync(
        bool includeArchived,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                c.id_category,
                c.name,
                c.letter,
                c.priority,
                c.old,
                c.id_setting,
                sq.name AS setting_name,
                sq.start_id_specialty,
                sq.end_id_specialty,
                sq.time_pause,
                sq.critical_num_pause,
                ss.definition AS start_specialty_name,
                es.definition AS end_specialty_name,
                shared.cnt AS shared_count
            FROM Category c
            INNER JOIN Setting_queue sq ON sq.id_setting = c.id_setting
            LEFT JOIN Specialty ss ON ss.id_specialty = sq.start_id_specialty
            LEFT JOIN Specialty es ON es.id_specialty = sq.end_id_specialty
            CROSS APPLY (
                SELECT COUNT(*) AS cnt
                FROM Category c2
                WHERE c2.id_setting = c.id_setting AND c2.old = 0
            ) shared
            WHERE (@includeArchived = 1 OR c.old = 0)
            ORDER BY c.priority DESC, c.name
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@includeArchived", includeArchived ? 1 : 0);

        var rows = new List<ServiceCategoryRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(ReadCategoryRecord(reader));

        return rows;
    }

    public async Task<ServiceCategoryRecord?> GetCategoryAsync(int idCategory, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                c.id_category,
                c.name,
                c.letter,
                c.priority,
                c.old,
                c.id_setting,
                sq.name AS setting_name,
                sq.start_id_specialty,
                sq.end_id_specialty,
                sq.time_pause,
                sq.critical_num_pause,
                ss.definition AS start_specialty_name,
                es.definition AS end_specialty_name,
                shared.cnt AS shared_count
            FROM Category c
            INNER JOIN Setting_queue sq ON sq.id_setting = c.id_setting
            LEFT JOIN Specialty ss ON ss.id_specialty = sq.start_id_specialty
            LEFT JOIN Specialty es ON es.id_specialty = sq.end_id_specialty
            CROSS APPLY (
                SELECT COUNT(*) AS cnt
                FROM Category c2
                WHERE c2.id_setting = c.id_setting AND c2.old = 0
            ) shared
            WHERE c.id_category = @idCategory
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@idCategory", idCategory);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return ReadCategoryRecord(reader);
    }

    public async Task<ServiceCategorySettingSnapshot?> GetSettingSnapshotAsync(
        int idSetting,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id_setting, name, start_id_specialty, end_id_specialty, time_pause, critical_num_pause
            FROM Setting_queue
            WHERE id_setting = @idSetting
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@idSetting", idSetting);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return new ServiceCategorySettingSnapshot
        {
            IdSetting = reader.GetInt32(0),
            SettingName = reader.IsDBNull(1) ? null : reader.GetString(1),
            StartSpecialtyId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
            EndSpecialtyId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
            TimePause = reader.GetInt32(4),
            CriticalNumPause = reader.GetInt32(5)
        };
    }

    public async Task<int> CountActiveCategoriesBySettingAsync(int idSetting, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COUNT(*) FROM Category WHERE id_setting = @idSetting AND old = 0";
        return await ExecuteScalarIntAsync(sql, cancellationToken, ("@idSetting", idSetting)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetActiveCategoryNamesBySettingAsync(
        int idSetting,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT name
            FROM Category
            WHERE id_setting = @idSetting AND old = 0
            ORDER BY name
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@idSetting", idSetting);

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            names.Add(reader.GetString(0).Trim());

        return names;
    }

    public async Task<int> CreateAsync(ServiceCategorySaveRequest request, CancellationToken cancellationToken = default)
    {
        if (request.IdSetting is int existingIdSetting && existingIdSetting > 0)
            return await CreateCategoryLinkedAsync(request, existingIdSetting, cancellationToken).ConfigureAwait(false);

        return await CreateCategoryWithNewSettingAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> CreateCategoryLinkedAsync(
        ServiceCategorySaveRequest request,
        int idSetting,
        CancellationToken cancellationToken)
    {
        const string insertCategorySql = """
            INSERT INTO Category (id_setting, name, priority, letter, old)
            OUTPUT INSERTED.id_category
            VALUES (@idSetting, @name, @priority, @letter, 0)
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(insertCategorySql, connection);
        command.Parameters.AddWithValue("@idSetting", idSetting);
        command.Parameters.AddWithValue("@name", request.Name.Trim());
        command.Parameters.AddWithValue("@priority", request.Priority);
        command.Parameters.AddWithValue("@letter", request.Letter.Trim());

        var idCategoryObj = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(idCategoryObj);
    }

    private async Task<int> CreateCategoryWithNewSettingAsync(
        ServiceCategorySaveRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            const string insertSettingSql = """
                INSERT INTO Setting_queue (start_id_specialty, end_id_specialty, time_pause, critical_num_pause, name)
                OUTPUT INSERTED.id_setting
                VALUES (@startId, @endId, @timePause, @criticalNumPause, @settingName)
                """;

            await using var insertSetting = new SqlCommand(insertSettingSql, connection, transaction);
            insertSetting.Parameters.AddWithValue("@startId", (object?)request.StartSpecialtyId ?? DBNull.Value);
            insertSetting.Parameters.AddWithValue("@endId", (object?)request.EndSpecialtyId ?? DBNull.Value);
            insertSetting.Parameters.AddWithValue("@timePause", request.TimePause);
            insertSetting.Parameters.AddWithValue("@criticalNumPause", request.CriticalNumPause);
            var settingName = string.IsNullOrWhiteSpace(request.SettingName)
                ? request.Name.Trim()
                : request.SettingName.Trim();
            insertSetting.Parameters.AddWithValue("@settingName", settingName);

            var idSettingObj = await insertSetting.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var idSetting = Convert.ToInt32(idSettingObj);

            const string insertCategorySql = """
                INSERT INTO Category (id_setting, name, priority, letter, old)
                OUTPUT INSERTED.id_category
                VALUES (@idSetting, @name, @priority, @letter, 0)
                """;

            await using var insertCategory = new SqlCommand(insertCategorySql, connection, transaction);
            insertCategory.Parameters.AddWithValue("@idSetting", idSetting);
            insertCategory.Parameters.AddWithValue("@name", request.Name.Trim());
            insertCategory.Parameters.AddWithValue("@priority", request.Priority);
            insertCategory.Parameters.AddWithValue("@letter", request.Letter.Trim());
            var idCategoryObj = await insertCategory.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var idCategory = Convert.ToInt32(idCategoryObj);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return idCategory;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task UpdateCategoryAsync(
        int idCategory,
        ServiceCategorySaveRequest request,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE Category
            SET name = @name, letter = @letter, priority = @priority
            WHERE id_category = @idCategory
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@idCategory", idCategory);
        command.Parameters.AddWithValue("@name", request.Name.Trim());
        command.Parameters.AddWithValue("@priority", request.Priority);
        command.Parameters.AddWithValue("@letter", request.Letter.Trim());

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateSettingAsync(
        int idSetting,
        ServiceCategorySaveRequest request,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE Setting_queue
            SET name = @settingName,
                start_id_specialty = @startId,
                end_id_specialty = @endId,
                time_pause = @timePause,
                critical_num_pause = @criticalNumPause
            WHERE id_setting = @idSetting
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@idSetting", idSetting);
        var settingName = string.IsNullOrWhiteSpace(request.SettingName)
            ? request.Name.Trim()
            : request.SettingName.Trim();
        command.Parameters.AddWithValue("@settingName", settingName);
        command.Parameters.AddWithValue("@startId", (object?)request.StartSpecialtyId ?? DBNull.Value);
        command.Parameters.AddWithValue("@endId", (object?)request.EndSpecialtyId ?? DBNull.Value);
        command.Parameters.AddWithValue("@timePause", request.TimePause);
        command.Parameters.AddWithValue("@criticalNumPause", request.CriticalNumPause);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RestoreAsync(int idCategory, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Category SET old = 0 WHERE id_category = @idCategory";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@idCategory", idCategory);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> SettingExistsAsync(int idSetting, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT CASE WHEN EXISTS (SELECT 1 FROM Setting_queue WHERE id_setting = @id) THEN 1 ELSE 0 END";
        var value = await ExecuteScalarIntAsync(sql, cancellationToken, ("@id", idSetting)).ConfigureAwait(false);
        return value == 1;
    }

    public async Task<IReadOnlyList<SettingOption>> ListSettingsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                sq.id_setting,
                sq.name,
                sq.end_id_specialty,
                sq.time_pause,
                sq.critical_num_pause,
                ss.definition AS start_specialty_name,
                es.definition AS end_specialty_name,
                active.cnt AS active_count
            FROM Setting_queue sq
            LEFT JOIN Specialty ss ON ss.id_specialty = sq.start_id_specialty
            LEFT JOIN Specialty es ON es.id_specialty = sq.end_id_specialty
            CROSS APPLY (
                SELECT COUNT(*) AS cnt
                FROM Category c
                WHERE c.id_setting = sq.id_setting AND c.old = 0
            ) active
            ORDER BY sq.name, sq.id_setting
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);

        var items = new List<(int Id, string DisplayName, string Disambiguator, string? SettingName, string? StartSpecialtyName, string? EndSpecialtyName, int TimePause, int CriticalNumPause, int ActiveCount)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var id = reader.GetInt32(0);
            var settingName = reader.IsDBNull(1) ? null : reader.GetString(1).Trim();
            var startSpecialtyName = reader.IsDBNull(5) ? null : reader.GetString(5).Trim();
            var endSpecialtyName = reader.IsDBNull(6) ? null : reader.GetString(6).Trim();
            var timePause = reader.GetInt32(3);
            var criticalNumPause = reader.GetInt32(4);
            var activeCount = reader.GetInt32(7);
            var displayName = string.IsNullOrWhiteSpace(settingName) ? $"Настройка #{id}" : settingName;
            var disambiguator = string.IsNullOrWhiteSpace(endSpecialtyName) ? "Один приём" : endSpecialtyName;

            items.Add((id, displayName, disambiguator, settingName, startSpecialtyName, endSpecialtyName, timePause, criticalNumPause, activeCount));
        }

        var duplicateNames = items
            .GroupBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rows = new List<SettingOption>();
        foreach (var item in items)
        {
            var label = duplicateNames.Contains(item.DisplayName)
                ? $"{item.DisplayName} — {item.Disambiguator}"
                : item.DisplayName;

            rows.Add(new SettingOption
            {
                Id = item.Id,
                SettingName = item.SettingName,
                StartSpecialtyName = item.StartSpecialtyName,
                EndSpecialtyName = item.EndSpecialtyName,
                TimePause = item.TimePause,
                CriticalNumPause = item.CriticalNumPause,
                ActiveCategoryCount = item.ActiveCount,
                Label = label
            });
        }

        return rows;
    }

    public async Task ArchiveAsync(int idCategory, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Category SET old = 1 WHERE id_category = @idCategory";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@idCategory", idCategory);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsActiveLetterAsync(
        string letter,
        int? excludeCategoryId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM Category
                WHERE letter = @letter AND old = 0 AND (@excludeCategoryId IS NULL OR id_category <> @excludeCategoryId)
            ) THEN 1 ELSE 0 END
            """;

        var value = await ExecuteScalarIntAsync(
            sql,
            cancellationToken,
            ("@letter", letter.Trim()),
            ("@excludeCategoryId", (object?)excludeCategoryId ?? DBNull.Value)).ConfigureAwait(false);

        return value == 1;
    }

    public async Task<bool> SpecialtyExistsAsync(int idSpecialty, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT CASE WHEN EXISTS (SELECT 1 FROM Specialty WHERE id_specialty = @id) THEN 1 ELSE 0 END";
        var value = await ExecuteScalarIntAsync(sql, cancellationToken, ("@id", idSpecialty)).ConfigureAwait(false);
        return value == 1;
    }

    public async Task<bool> HasOpenAppointmentsTodayAsync(int idCategory, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM Appointment
                WHERE id_category = @idCategory
                  AND time_complete IS NULL
                  AND date_arrival = CAST(SYSDATETIME() AS date)
            ) THEN 1 ELSE 0 END
            """;

        var value = await ExecuteScalarIntAsync(sql, cancellationToken, ("@idCategory", idCategory)).ConfigureAwait(false);
        return value == 1;
    }

    public async Task<IReadOnlyList<SpecialtyOption>> ListSpecialtiesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id_specialty, definition
            FROM Specialty
            ORDER BY definition
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);

        var rows = new List<SpecialtyOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new SpecialtyOption
            {
                Id = reader.GetInt32(0),
                Label = reader.GetString(1).Trim()
            });
        }

        return rows;
    }

    private static ServiceCategoryRecord ReadCategoryRecord(SqlDataReader reader) =>
        new()
        {
            IdCategory = reader.GetInt32(0),
            Name = reader.GetString(1).Trim(),
            Letter = reader.GetString(2).Trim(),
            Priority = reader.GetInt32(3),
            IsArchived = reader.GetBoolean(4),
            IdSetting = reader.GetInt32(5),
            SettingName = reader.IsDBNull(6) ? null : reader.GetString(6).Trim(),
            StartSpecialtyId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
            EndSpecialtyId = reader.IsDBNull(8) ? null : reader.GetInt32(8),
            TimePause = reader.GetInt32(9),
            CriticalNumPause = reader.GetInt32(10),
            StartSpecialtyName = reader.IsDBNull(11) ? null : reader.GetString(11).Trim(),
            EndSpecialtyName = reader.IsDBNull(12) ? null : reader.GetString(12).Trim(),
            SharedCategoryCount = reader.GetInt32(13)
        };

    private async Task<int> ExecuteScalarIntAsync(
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result);
    }
}
