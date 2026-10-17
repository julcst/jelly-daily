using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
namespace JellyDaily.Tests;
public sealed class AccessTests
{
    private sealed class Access : IReleaseAccess
    {
        public Guid Child = Guid.NewGuid(); public Guid Locked = Guid.NewGuid(); public bool Broken;
        public bool IsChild(Guid user) => user == Child;
        public bool IsLocked(Guid user, Guid itemId, Dictionary<string, ReleaseState> state) => itemId == Locked;
        public Task<Dictionary<string, ReleaseState>> SyncAsync(Guid? bonusUser = null, Guid? bonusSeries = null, int count = 1, bool bypass = false, bool reset = false) =>
            Broken ? throw new IOException("Corrupt state") : Task.FromResult(new Dictionary<string, ReleaseState>());
    }
    private static async Task<(IActionResult? Result, bool Ran)> Execute(Access access, Guid user, Dictionary<string, object?> args)
    {
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("Jellyfin-UserId", user.ToString())], "Test")) };
        var context = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var executing = new ActionExecutingContext(context, [], args, new object()); bool ran = false;
        await new AccessFilter(access, NullLogger<AccessFilter>.Instance).OnActionExecutionAsync(executing, () => {
            ran = true; return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        });
        return (executing.Result, ran);
    }
    [Theory] [InlineData("itemId")] [InlineData("id")] [InlineData("mediaSourceId")]
    public async Task DirectItemAndStreamIdentifiersAreDenied(string argument)
    {
        var access = new Access(); var result = await Execute(access, access.Child, new() { [argument] = access.Locked });
        Assert.IsType<ForbidResult>(result.Result); Assert.False(result.Ran);
    }
    [Fact] public async Task AdultIsUnaffected()
    {
        var access = new Access { Broken = true }; var result = await Execute(access, Guid.NewGuid(), new() { ["itemId"] = access.Locked });
        Assert.True(result.Ran); Assert.Null(result.Result);
    }
    [Fact] public async Task OtherUserOverrideIsDenied()
    {
        var access = new Access(); var result = await Execute(access, access.Child, new() { ["userId"] = Guid.NewGuid() });
        Assert.IsType<ForbidResult>(result.Result); Assert.False(result.Ran);
    }
    [Fact] public async Task BrokenStateFailsClosed()
    {
        var access = new Access { Broken = true }; var result = await Execute(access, access.Child, []);
        Assert.Equal(503, Assert.IsType<StatusCodeResult>(result.Result).StatusCode); Assert.False(result.Ran);
    }
    [Fact] public async Task UnlockedRequestsContinue()
    {
        var access = new Access(); var result = await Execute(access, access.Child, new() { ["itemId"] = Guid.NewGuid() });
        Assert.True(result.Ran); Assert.Null(result.Result);
    }
}
