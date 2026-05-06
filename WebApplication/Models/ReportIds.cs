namespace WebApplication.Models;

public static class ReportIds
{
    public const string QueueSummary = "queue-summary";
    public const string CabinetLoad = "cabinet-load";
    public const string DoctorCabinetLoadDowntime = "doctor-cabinet-load-downtime";
    public const string WaitTimeDistribution = "wait-time-distribution";
    public const string ServiceDurationDistribution = "service-duration-distribution";
    public const string FullCycleStageDelays = "full-cycle-stage-delays";
    public const string UnservedChainBreaks = "unserved-chain-breaks";
    public const string MultiStageService = "multi-stage-service";
    public const string FlowBalanceArrivedVsCompleted = "flow-balance-arrived-vs-completed";
    public const string ServiceCategoriesPerformance = "service-categories-performance";
    public const string BottlenecksLongQueuesRanking = "bottlenecks-long-queues-ranking";
}
