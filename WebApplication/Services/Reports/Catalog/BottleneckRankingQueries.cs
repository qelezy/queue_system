using WebApplication.Models.ElectronicQueueProf;

namespace WebApplication.Services.Reports.Catalog;

internal static class BottleneckRankingQueries
{
    private const double MaxStageMinutes = 10080;

    internal readonly record struct StageObservation(
        int IdListItem,
        int IdAppointment,
        DateOnly DateArrival,
        int IdDoctor,
        int IdCabinet,
        TimeOnly? TimeCall,
        TimeOnly? TimeStartServicing,
        TimeOnly? TimeEndServicing,
        int TimeServicing,
        string? SpecialtyDefinition);

    internal readonly record struct EntityMetrics(
        int EntityId,
        string EntityName,
        string SpecialtyLabels,
        int QueueIncidents,
        double TotalDelayMin,
        double? AvgDelayMin,
        double MinDelayMin,
        double MaxDelayMin,
        int OverNormCount);

    internal static List<EntityMetrics> BuildEntityMetrics(
        IReadOnlyList<StageObservation> stages,
        IReadOnlyDictionary<int, string> entityLabels,
        string analysisMode)
    {
        var byCabinet = IsCabinetMode(analysisMode);
        var incidentByEntity = AggregateIncidents(stages, byCabinet);
        var specialtyByEntity = BuildSpecialtyLabelsByEntity(stages, byCabinet);

        var entityIds = incidentByEntity
            .Where(kv => kv.Value.Incidents > 0 || kv.Value.TotalDelayMin > 0)
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
            var avg = agg.Incidents > 0 ? agg.TotalDelayMin / agg.Incidents : (double?)null;
            var minDelay = agg.Incidents > 0 && agg.MinDelayMin < double.MaxValue
                ? agg.MinDelayMin
                : 0;

            metrics.Add(new EntityMetrics(
                entityId,
                name,
                specialties,
                agg.Incidents,
                agg.TotalDelayMin,
                avg,
                minDelay,
                agg.MaxDelayMin,
                agg.OverNormCount));
        }

        return metrics;
    }

    internal static double ComputeStageDelayMinutes(StageObservation stage) =>
        ComputeStageDelayBreakdown(stage).StageDelayMin;

    internal static bool IsOverNorm(StageObservation stage)
    {
        if (!stage.TimeStartServicing.HasValue
            || !stage.TimeEndServicing.HasValue
            || stage.TimeServicing <= 0)
            return false;

        var startDt = EqDateTimeExtensions.CombineOnArrivalDate(
            stage.DateArrival,
            stage.TimeStartServicing.Value);
        var endDt = EqDateTimeExtensions.CombineOnArrivalDate(
            stage.DateArrival,
            stage.TimeEndServicing.Value);
        var serviceMin = (endDt - startDt).TotalMinutes;
        return IsValidStageMinutes(serviceMin) && serviceMin > stage.TimeServicing;
    }

    private static StageDelayBreakdown ComputeStageDelayBreakdown(StageObservation stage)
    {
        var delayCallMin = 0.0;
        var overNormMin = 0.0;

        if (stage.TimeCall.HasValue && stage.TimeStartServicing.HasValue)
        {
            var callDt = EqDateTimeExtensions.CombineOnArrivalDate(stage.DateArrival, stage.TimeCall.Value);
            var startDt = EqDateTimeExtensions.CombineOnArrivalDate(
                stage.DateArrival,
                stage.TimeStartServicing.Value);
            var callDelay = (startDt - callDt).TotalMinutes;
            if (IsValidStageMinutes(callDelay) && callDelay > 0)
                delayCallMin = callDelay;
        }

        if (stage.TimeStartServicing.HasValue
            && stage.TimeEndServicing.HasValue
            && stage.TimeServicing > 0)
        {
            var startDt = EqDateTimeExtensions.CombineOnArrivalDate(
                stage.DateArrival,
                stage.TimeStartServicing.Value);
            var endDt = EqDateTimeExtensions.CombineOnArrivalDate(
                stage.DateArrival,
                stage.TimeEndServicing.Value);
            var serviceMin = (endDt - startDt).TotalMinutes;
            if (IsValidStageMinutes(serviceMin))
            {
                var overNorm = serviceMin - stage.TimeServicing;
                if (overNorm > 0 && IsValidStageMinutes(overNorm))
                    overNormMin = overNorm;
            }
        }

        return new StageDelayBreakdown(delayCallMin + overNormMin);
    }

    private readonly record struct StageDelayBreakdown(double StageDelayMin);

    private static bool IsValidStageMinutes(double minutes) =>
        minutes >= 0 && minutes < MaxStageMinutes;

    private static bool IsCabinetMode(string analysisMode) =>
        string.Equals(analysisMode, BottleneckRankingReportBuilder.ModeCabinet, StringComparison.OrdinalIgnoreCase);

    private static int ResolveEntityId(StageObservation stage, bool byCabinet) =>
        byCabinet ? stage.IdCabinet : stage.IdDoctor;

    private sealed class IncidentAggregate
    {
        public int Incidents;
        public double TotalDelayMin;
        public double MinDelayMin = double.MaxValue;
        public double MaxDelayMin;
        public int OverNormCount;
    }

    private static Dictionary<int, IncidentAggregate> AggregateIncidents(
        IReadOnlyList<StageObservation> stages,
        bool byCabinet)
    {
        var map = new Dictionary<int, IncidentAggregate>();

        foreach (var stage in stages)
        {
            var entityId = ResolveEntityId(stage, byCabinet);
            if (entityId == 0)
                continue;

            if (!map.TryGetValue(entityId, out var agg))
            {
                agg = new IncidentAggregate();
                map[entityId] = agg;
            }

            if (IsOverNorm(stage))
                agg.OverNormCount++;

            var delay = ComputeStageDelayBreakdown(stage).StageDelayMin;
            if (delay <= 0)
                continue;

            agg.Incidents++;
            agg.TotalDelayMin += delay;
            if (delay < agg.MinDelayMin)
                agg.MinDelayMin = delay;
            if (delay > agg.MaxDelayMin)
                agg.MaxDelayMin = delay;
        }

        return map;
    }

    private static Dictionary<int, string> BuildSpecialtyLabelsByEntity(
        IReadOnlyList<StageObservation> stages,
        bool byCabinet)
    {
        var map = new Dictionary<int, HashSet<string>>();

        foreach (var stage in stages)
        {
            var entityId = ResolveEntityId(stage, byCabinet);
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
