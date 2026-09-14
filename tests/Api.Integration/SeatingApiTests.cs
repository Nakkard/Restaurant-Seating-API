using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BusinessLogic.Models;
using Infrastructure.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.Integration;

// One class fixture keeps stateful scenarios sequential; individual scenarios issue parallel requests.
public sealed class SeatingApiTests(RestaurantFixture fixture) : IClassFixture<RestaurantFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task ArrivalReturnsCreatedAndRetrievableLocation()
    {
        await fixture.ResetAsync(2);
        using var response = await fixture.Client.PostAsJsonAsync("/api/groups", new { size = 2 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var group = await Read<GroupResult>(response);
        var retrieved = await fixture.Client.GetFromJsonAsync<GroupResult>(response.Headers.Location, Json);
        Assert.Equal(group, retrieved);
        Assert.Equal(GroupStatus.Seated, group.Status);
        Assert.True(group.ArrivalOrder > 0);
    }

    [Fact]
    public async Task DepartureSeatsQueueInArrivalOrderWithinTheSameRequest()
    {
        await fixture.ResetAsync(4);
        var seated = await Arrive(4);
        var earlier = await Arrive(3);
        var later = await Arrive(2);
        Assert.Equal(GroupStatus.Waiting, earlier.Status);
        Assert.Equal(GroupStatus.Waiting, later.Status);

        var completed = await Leave(seated.Id);

        Assert.Equal(GroupStatus.Completed, completed.Status);
        Assert.Equal(GroupStatus.Seated, (await Get(earlier.Id)).Status);
        Assert.Equal(GroupStatus.Waiting, (await Get(later.Id)).Status);
        var tables = await Tables();
        Assert.Equal(3, Assert.Single(tables).OccupiedSeats);
    }

    [Fact]
    public async Task SmallerGroupCanBypassAnUnseatableEarlierGroup()
    {
        await fixture.ResetAsync(4);
        var earlier = await Arrive(6);
        var later = await Arrive(2);
        Assert.Equal(GroupStatus.Waiting, (await Get(earlier.Id)).Status);
        Assert.Equal(GroupStatus.Seated, later.Status);
    }

    [Fact]
    public async Task EmptyTableTakesPriorityOverSharedExactFit()
    {
        await fixture.ResetAsync(4, 6);
        var first = await Arrive(2);
        var second = await Arrive(2);
        Assert.Equal(1, first.TableId);
        Assert.Equal(2, second.TableId);
    }

    [Fact]
    public async Task LeavingQueuePreservesHistoryAndDoesNotConsumeSeats()
    {
        await fixture.ResetAsync(2);
        var seated = await Arrive(2);
        var waiting = await Arrive(2);
        var left = await Leave(waiting.Id);
        Assert.Equal(GroupStatus.Left, left.Status);
        Assert.Null(left.TableId);
        await Leave(seated.Id);
        Assert.Equal(GroupStatus.Left, (await Get(waiting.Id)).Status);
        Assert.Equal(0, Assert.Single(await Tables()).OccupiedSeats);
    }

    [Fact]
    public async Task RepeatedDepartureIsIdempotent()
    {
        await fixture.ResetAsync(2);
        var first = await Arrive(2);
        var waiting = await Arrive(2);
        var initial = await Leave(first.Id);
        var repeated = await Leave(first.Id);
        Assert.Equal(initial, repeated);
        Assert.Equal(GroupStatus.Seated, (await Get(waiting.Id)).Status);
        Assert.Equal(2, Assert.Single(await Tables()).OccupiedSeats);
    }

    [Fact]
    public async Task ConcurrentArrivalsDoNotOverbookAndRespectRegisteredOrder()
    {
        await fixture.ResetAsync(6, 6);
        var groups = await Task.WhenAll(Enumerable.Range(0, 24).Select(_ => Arrive(1)));
        var ordered = groups.OrderBy(group => group.ArrivalOrder).ToArray();
        Assert.Equal(24, groups.Select(group => group.ArrivalOrder).Distinct().Count());
        Assert.All(ordered.Take(12), group => Assert.Equal(GroupStatus.Seated, group.Status));
        Assert.All(ordered.Skip(12), group => Assert.Equal(GroupStatus.Waiting, group.Status));
        Assert.All(await Tables(), table => Assert.Equal(6, table.OccupiedSeats));
    }

    [Fact]
    public async Task SeparateApplicationInstancesShareTheDatabaseLock()
    {
        await fixture.ResetAsync(6);
        await using var secondInstance = new RestaurantApiFactory(fixture.ConnectionString);
        using var secondClient = secondInstance.CreateClient();
        var groups = await Task.WhenAll(Enumerable.Range(0, 16)
            .Select(index => Arrive(1, index % 2 == 0 ? fixture.Client : secondClient)));
        var ordered = groups.OrderBy(group => group.ArrivalOrder).ToArray();
        Assert.All(ordered.Take(6), group => Assert.Equal(GroupStatus.Seated, group.Status));
        Assert.All(ordered.Skip(6), group => Assert.Equal(GroupStatus.Waiting, group.Status));
        Assert.Equal(6, Assert.Single(await Tables()).OccupiedSeats);
    }

    [Fact]
    public async Task ConcurrentDeparturesAndArrivalsCannotReleaseSeatsTwice()
    {
        await fixture.ResetAsync(6);
        var seated = await Arrive(6);
        var waiting = await Arrive(6);
        var departures = Enumerable.Range(0, 8).Select(_ => Leave(seated.Id)).ToArray();
        var arrivals = Enumerable.Range(0, 8).Select(_ => Arrive(1)).ToArray();
        await Task.WhenAll(departures);
        var newGroups = await Task.WhenAll(arrivals);
        Assert.Equal(GroupStatus.Seated, (await Get(waiting.Id)).Status);
        Assert.All(newGroups, group => Assert.Equal(GroupStatus.Waiting, group.Status));
        Assert.Equal(6, Assert.Single(await Tables()).OccupiedSeats);
    }

    [Fact]
    public async Task StateSurvivesANewApplicationInstance()
    {
        await fixture.ResetAsync(2);
        var group = await Arrive(2);
        await using var restarted = new RestaurantApiFactory(fixture.ConnectionString);
        using var client = restarted.CreateClient();
        Assert.Equal(group, await client.GetFromJsonAsync<GroupResult>($"/api/groups/{group.Id}", Json));
    }

    [Fact]
    public async Task ArrivalRollsBackItsInitialInsertIfSeatingSaveFails()
    {
        await fixture.ResetAsync(2);
        await using var failing = CreateFailingFactory();
        using var client = failing.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/groups", new { size = 2 });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var problem = await Read<ProblemDetails>(response);
        Assert.Null(problem.Detail);
        await using var database = fixture.CreateContext();
        Assert.Equal(0, await database.Groups.CountAsync());
        Assert.Equal(GroupStatus.Seated, (await Arrive(2)).Status); // The failed request released its lock.
    }

    [Fact]
    public async Task DepartureAndReassignmentBothRollBackIfSaveFails()
    {
        await fixture.ResetAsync(2);
        var seated = await Arrive(2);
        var waiting = await Arrive(2);
        await using var failing = CreateFailingFactory();
        using var client = failing.CreateClient();
        using var response = await client.PostAsync($"/api/groups/{seated.Id}/leave", null);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(GroupStatus.Seated, (await Get(seated.Id)).Status);
        Assert.Equal(GroupStatus.Waiting, (await Get(waiting.Id)).Status);
    }

    [Fact]
    public async Task LockTimeoutReturns503WithoutChangingState()
    {
        await fixture.ResetAsync(2);
        await using var database = fixture.CreateContext();
        await using var transaction = await database.Database.BeginTransactionAsync();
        await database.Database.ExecuteSqlRawAsync("""
            EXEC sys.sp_getapplock @Resource=N'RestaurantSeating:State',
                @LockMode=N'Exclusive', @LockOwner=N'Transaction';
            """);
        using var response = await fixture.Client.PostAsJsonAsync("/api/groups", new { size = 2 });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await transaction.RollbackAsync();
        Assert.Equal(0, await database.Groups.CountAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(7)]
    public async Task InvalidSizeReturnsProblemDetails(int size)
    {
        using var response = await fixture.Client.PostAsJsonAsync("/api/groups", new { size });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(400, (await Read<ProblemDetails>(response)).Status);
    }

    [Theory]
    [InlineData("/api/groups?status=invalid")]
    [InlineData("/api/groups?status=99")]
    [InlineData("/api/groups?offset=-1")]
    [InlineData("/api/groups?limit=101")]
    [InlineData("/api/groups?limit=0")]
    public async Task InvalidQueryReturns400(string url)
    {
        using var response = await fixture.Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(400, (await Read<ProblemDetails>(response)).Status);
    }

    [Fact]
    public async Task UnknownGroupReturns404ForBothReadAndLeave()
    {
        var id = Guid.NewGuid();
        using var get = await fixture.Client.GetAsync($"/api/groups/{id}");
        using var leave = await fixture.Client.PostAsync($"/api/groups/{id}/leave", null);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, leave.StatusCode);
        Assert.Equal(404, (await Read<ProblemDetails>(leave)).Status);
    }

    [Fact]
    public async Task QueueSupportsFilteringAndOrderedPagination()
    {
        await fixture.ResetAsync(2);
        await Arrive(2);
        var first = await Arrive(2);
        var second = await Arrive(2);
        var page = await fixture.Client.GetFromJsonAsync<GroupPage>("/api/groups?status=Waiting&offset=1&limit=1", Json);
        Assert.NotNull(page);
        Assert.Equal(second.Id, Assert.Single(page.Items).Id);
        Assert.True(first.ArrivalOrder < second.ArrivalOrder);
    }

    [Fact]
    public async Task DatabaseRejectsInvalidGroupSize()
    {
        await fixture.ResetAsync(2);
        var group = await Arrive(2);
        await using var database = fixture.CreateContext();
        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
            database.Database.ExecuteSqlInterpolatedAsync($"UPDATE GuestGroups SET Size=7 WHERE Id={group.Id}"));
        Assert.Equal(2, (await Get(group.Id)).Size);
    }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> CreateFailingFactory() =>
        fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<RestaurantDbContext>();
            services.AddScoped(_ => new RestaurantDbContext(new DbContextOptionsBuilder<RestaurantDbContext>()
                .UseSqlServer(fixture.ConnectionString)
                .AddInterceptors(new FailOnStateChange())
                .Options));
        }));

    private sealed class FailOnStateChange : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<GuestGroup>().Any(entry => entry.State == EntityState.Modified))
                throw new InvalidOperationException("Injected failure before saving seating changes.");
            return ValueTask.FromResult(result);
        }
    }

    private async Task<GroupResult> Arrive(int size, HttpClient? client = null)
    {
        using var response = await (client ?? fixture.Client).PostAsJsonAsync("/api/groups", new { size });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Read<GroupResult>(response);
    }

    private async Task<GroupResult> Leave(Guid id)
    {
        using var response = await fixture.Client.PostAsync($"/api/groups/{id}/leave", null);
        response.EnsureSuccessStatusCode();
        return await Read<GroupResult>(response);
    }

    private async Task<GroupResult> Get(Guid id) =>
        (await fixture.Client.GetFromJsonAsync<GroupResult>($"/api/groups/{id}", Json))!;

    private async Task<List<TableResult>> Tables() =>
        (await fixture.Client.GetFromJsonAsync<List<TableResult>>("/api/tables", Json))!;

    private static async Task<T> Read<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;
}
