using WebApplication.Models.ElectronicQueueProf;

namespace WebApplication.Services.Reports.Catalog;

internal static class ServiceDelaysQueries
{
    private const double MaxStageMinutes = 10080;

    internal readonly record struct StageObservation(
        int IdListItem,
        int IdAppointment,
        DateOnly DateArrival,
        int? IdDoctor,
        int? IdCabinet,
        TimeOnly? TimeCall,
        TimeOnly? TimeStartServicing,
        TimeOnly? TimeEndServicing,
        int TimeServicing,
        string? SpecialtyDefinition);

    internal readonly record struct EntityMetrics(
        int EntityId,
        string EntityName,
        string SpecialtyLabels,
        double TotalDelayMin,
        double? AvgDelayMin,
        double MinDelayMin,
        double MaxDelayMin,
        int OverNormCount,
        double TotalDelayMinExact,
        double? AvgDelayMinExact,
        double MinDelayMinExact,
        double MaxDelayMinExact);

    internal static List<EntityMetrics> BuildEntityMetrics(
        IReadOnlyList<StageObservation> stages,
        IReadOnlyDictionary<int, string> entityLabels)
    {
        var incidentByEntity = AggregateOverNorm(stages);
        var specialtyByEntity = BuildSpecialtyLabelsByEntity(stages);

        var entityIds = incidentByEntity
            .Where(kv => kv.Value.OverNormCount > 0)
            .Select(kv => kv.Key)
            .ToHashSet();

        if (entityIds.Count == 0)
            return [];

        var metrics = new List<EntityMetrics>();
        foreach (var entityId in entityIds)
        {
            var agg = incidentByEntity[entityId];
            var name = entityLabels.TryGetValue(entityId, out var label) ? label : "—";
            var specialties = specialtyByEntity.TryGetValue(entityId, out var spec)
                ? spec
                : "—";
            var avg = agg.OverNormCount > 0 ? agg.TotalDelayMin / agg.OverNormCount : (double?)null;
            var minDelay = agg.OverNormCount > 0 && agg.MinDelayMin < double.MaxValue
                ? agg.MinDelayMin
                : 0;
            var avgExact = agg.OverNormCount > 0
                ? agg.TotalDelayMinExact / agg.OverNormCount
                : (double?)null;
            var minDelayExact = agg.OverNormCount > 0 && agg.MinDelayMinExact < double.MaxValue
                ? agg.MinDelayMinExact
                : 0;

            metrics.Add(new EntityMetrics(
                entityId,
                name,
                specialties,
                agg.TotalDelayMin,
                avg,
                minDelay,
                agg.MaxDelayMin,
                agg.OverNormCount,
                agg.TotalDelayMinExact,
                avgExact,
                minDelayExact,
                agg.MaxDelayMinExact));
        }

        return metrics;
    }

    internal static double ComputeStageDelayMinutes(StageObservation stage) =>
        TryComputeOverNormMinutes(stage) ?? 0;

    internal static bool IsOverNorm(StageObservation stage) =>
        TryComputeOverNormMinutes(stage) is not null;

    internal static double? TryComputeOverNormMinutes(StageObservation stage)
    {
        if (!stage.TimeStartServicing.HasValue
            || !stage.TimeEndServicing.HasValue
            || stage.TimeServicing <= 0)
            return null;

        var startDt = EqDateTimeExtensions.CombineOnArrivalDate(
            stage.DateArrival,
            stage.TimeStartServicing.Value);
        var endDt = EqDateTimeExtensions.CombineOnArrivalDate(
            stage.DateArrival,
            stage.TimeEndServicing.Value);
        var serviceSeconds = CatalogReportShared.ComputeDurationSeconds(startDt, endDt);
        if (serviceSeconds is null)
            return null;

        var serviceMinutesRounded = CatalogReportShared.RoundDurationMinutes(
            CatalogReportShared.MinutesFromSeconds(serviceSeconds.Value));
        var overNormMinutes = serviceMinutesRounded - stage.TimeServicing;
        if (overNormMinutes <= 0 || overNormMinutes >= MaxStageMinutes)
            return null;

        return overNormMinutes;
    }

    internal static double? TryComputeOverNormMinutesExact(StageObservation stage)
    {
        if (!stage.TimeStartServicing.HasValue
            || !stage.TimeEndServicing.HasValue
            || stage.TimeServicing <= 0)
            return null;

        var startDt = EqDateTimeExtensions.CombineOnArrivalDate(
            stage.DateArrival,
            stage.TimeStartServicing.Value);
        var endDt = EqDateTimeExtensions.CombineOnArrivalDate(
            stage.DateArrival,
            stage.TimeEndServicing.Value);
        var serviceMinutesExact = CatalogReportShared.ComputeDurationMinutesExact(startDt, endDt);
        if (serviceMinutesExact is null)
            return null;

        var overNormMinutes = serviceMinutesExact.Value - stage.TimeServicing;
        if (overNormMinutes <= 0 || overNormMinutes >= MaxStageMinutes)
            return null;

        return overNormMinutes;
    }

    private static int ResolveDoctorId(StageObservation stage) =>
        stage.IdDoctor is > 0 ? stage.IdDoctor.Value : 0;

    private sealed class OverNormAggregate
    {
        public double TotalDelayMin;
        public double MinDelayMin = double.MaxValue;
        public double MaxDelayMin;
        public int OverNormCount;
        public double TotalDelayMinExact;
        public double MinDelayMinExact = double.MaxValue;
        public double MaxDelayMinExact;
    }

    private static Dictionary<int, OverNormAggregate> AggregateOverNorm(
        IReadOnlyList<StageObservation> stages)
    {
        var map = new Dictionary<int, OverNormAggregate>();

        foreach (var stage in stages)
        {
            var entityId = ResolveDoctorId(stage);
            if (entityId == 0)
                continue;

            var overNorm = TryComputeOverNormMinutes(stage);
            var overNormExact = TryComputeOverNormMinutesExact(stage);
            if (overNorm is null or <= 0 || overNormExact is null or <= 0)
                continue;

            if (!map.TryGetValue(entityId, out var agg))
            {
                agg = new OverNormAggregate();
                map[entityId] = agg;
            }

            agg.OverNormCount++;
            agg.TotalDelayMin += overNorm.Value;
            if (overNorm.Value < agg.MinDelayMin)
                agg.MinDelayMin = overNorm.Value;
            if (overNorm.Value > agg.MaxDelayMin)
                agg.MaxDelayMin = overNorm.Value;
            agg.TotalDelayMinExact += overNormExact.Value;
            if (overNormExact.Value < agg.MinDelayMinExact)
                agg.MinDelayMinExact = overNormExact.Value;
            if (overNormExact.Value > agg.MaxDelayMinExact)
                agg.MaxDelayMinExact = overNormExact.Value;
        }

        return map;
    }

    private static Dictionary<int, string> BuildSpecialtyLabelsByEntity(
        IReadOnlyList<StageObservation> stages)
    {
        var map = new Dictionary<int, HashSet<string>>();

        foreach (var stage in stages)
        {
            var entityId = ResolveDoctorId(stage);
            if (entityId == 0)
                continue;

            var def = stage.SpecialtyDefinition?.Trim();
            if (string.IsNullOrEmpty(def))
                continue;

            if (!map.TryGetValue(entityId, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                map[entityId] = set;
            }

            set.Add(def);
        }

        return map.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Count == 0
                ? "—"
                : string.Join("; ", kv.Value.OrderBy(s => s, StringComparer.OrdinalIgnoreCase)));
    }
}
