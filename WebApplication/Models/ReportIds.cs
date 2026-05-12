namespace WebApplication.Models;

public static class ReportIds
{
    public const string QueueSummary = "queue-summary";
    public const string CabinetLoad = "cabinet-load";
    public const string DoctorCabinetLoadDowntime = "load-and-downtime";
    public const string WaitTimeDistribution = "waiting-before-appointment";
    public const string ServiceDurationDistribution = "appointment-duration";
    public const string FullCycleStageDelays = "route-and-pauses";
    public const string UnservedChainBreaks = "unserved-and-chain-breaks";
    public const string MultiStageService = "multi-and-single-stage-routes";
    public const string FlowBalanceArrivedVsCompleted = "arrived-and-completed";
    public const string ServiceCategoriesPerformance = "service-categories-comparison";
    public const string BottlenecksLongQueuesRanking = "bottleneck-ranking";
}
