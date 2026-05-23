using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace WebApplication.Hubs;

[Authorize]
public sealed class DashboardHub : Hub
{
}
