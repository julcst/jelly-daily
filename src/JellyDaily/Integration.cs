using System.Security.Claims;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JellyDaily;

public sealed class Services : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection services, IServerApplicationHost host)
    {
        services.AddSingleton<ReleaseService>();
        services.AddSingleton<IReleaseAccess>(s => s.GetRequiredService<ReleaseService>());
        services.AddScoped<AccessFilter>();
        services.Configure<MvcOptions>(o => o.Filters.AddService<AccessFilter>(int.MinValue));
    }
}

// Runs after Jellyfin authentication and model binding, before controller execution.
public sealed class AccessFilter(IReleaseAccess releases, ILogger<AccessFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var principal = context.HttpContext.User;
        var claim = principal.FindFirstValue("Jellyfin-UserId");
        if (!Guid.TryParse(claim, out var user) || !releases.IsChild(user)) { await next(); return; }
        try
        {
            var state = await releases.SyncAsync();
            // Cross-user query overrides must not escape the child's permissions.
            foreach (var pair in context.ActionArguments)
            {
                if (pair.Key.Equals("userId", StringComparison.OrdinalIgnoreCase) && pair.Value is Guid other && other != Guid.Empty && other != user)
                { context.Result = new ForbidResult(); return; }
                var ids = pair.Value switch { Guid id => new[] { id }, IEnumerable<Guid> many => many, string text when Guid.TryParse(text, out var id) => new[] { id }, _ => [] };
                if (ids.Any(id => releases.IsLocked(user, id, state))) { context.Result = new ForbidResult(); return; }
            }
        }
        catch (Exception error)
        {
            logger.LogError(error, "Daily Episodes access validation failed; denying child request");
            context.Result = new StatusCodeResult(503);
            return;
        }
        await next();
    }
}
public sealed class ReleaseTask(ReleaseService releases) : IScheduledTask
{
    public string Name => "Release daily episodes";
    public string Key => "JellyDailyRelease";
    public string Description => "Reconcile child access and grant eligible daily episodes.";
    public string Category => "Daily Episodes";
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); await releases.SyncAsync(); progress.Report(100); }
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => [
        new() { Type = TaskTriggerInfoType.StartupTrigger },
        new() { Type = TaskTriggerInfoType.IntervalTrigger, IntervalTicks = TimeSpan.FromMinutes(1).Ticks }
    ];
}
