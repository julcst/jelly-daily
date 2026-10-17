using MediaBrowser.Controller.Library;
using Jellyfin.Data;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace JellyDaily;

[ApiController]
[Route("DailyEpisodes")]
[Authorize(Policy = "RequiresElevation")]
public sealed class AdminController(ReleaseService releases, IUserManager users, ILibraryManager library) : ControllerBase
{
    [HttpGet("Status")]
    public async Task<object> Status() => new {
        Progress = await releases.SyncAsync(),
        Users = users.GetUsers().Where(u => !u.HasPermission(PermissionKind.IsAdministrator)).Select(u => new { u.Id, u.Username }),
        Series = library.GetItemList(new InternalItemsQuery { IncludeItemTypes = [BaseItemKind.Series], Recursive = true }).Select(s => new { s.Id, s.Name })
    };
    public sealed record BonusRequest(Guid UserId, Guid SeriesId, int Count, bool BypassWatch);
    [HttpPost("Bonus")]
    public async Task<IActionResult> Bonus(BonusRequest request)
    {
        if (request.Count is < 1 or > 100 || !Plugin.Instance.Configuration.Rules.Any(r => r.UserId == request.UserId && r.SeriesId == request.SeriesId))
            return BadRequest("Select a configured child/series and 1–100 episodes.");
        await releases.SyncAsync(request.UserId, request.SeriesId, request.Count, request.BypassWatch);
        return NoContent();
    }
    [HttpPost("Reset")]
    public async Task<IActionResult> Reset(BonusRequest request)
    {
        if (!Plugin.Instance.Configuration.Rules.Any(r => r.UserId == request.UserId && r.SeriesId == request.SeriesId)) return BadRequest();
        await releases.SyncAsync(request.UserId, request.SeriesId, reset: true);
        return NoContent();
    }
}
