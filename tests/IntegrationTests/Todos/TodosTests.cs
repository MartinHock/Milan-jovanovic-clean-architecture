using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Todos;

public sealed class TodosTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record TodoDto(Guid Id, Guid UserId, string Description, bool IsCompleted);

    [Fact]
    public async Task GetTodo_Should_ReturnUnauthorized_WhenTokenIsMissing()
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"todos/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateTodo_Should_PersistTodo_ThatCanBeRetrievedById()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        var createRequest = new
        {
            userId,
            description = "Integration test todo",
            labels = new[] { "integration" },
            priority = 2
        };

        // Act
        HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync("todos", createRequest, TestContext.Current.CancellationToken);

        // Assert
        createResponse.EnsureSuccessStatusCode();
        Guid todoId = await createResponse.Content.ReadFromJsonAsync<Guid>(TestContext.Current.CancellationToken);
        todoId.ShouldNotBe(Guid.Empty);

        HttpResponseMessage getResponse = await HttpClient.GetAsync($"todos/{todoId}", TestContext.Current.CancellationToken);
        getResponse.EnsureSuccessStatusCode();

        TodoDto? todo = await getResponse.Content.ReadFromJsonAsync<TodoDto>(TestContext.Current.CancellationToken);
        todo!.Id.ShouldBe(todoId);
        todo.UserId.ShouldBe(userId);
        todo.Description.ShouldBe("Integration test todo");
        todo.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task CompleteTodo_Should_MarkTodoAsCompleted()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        var createRequest = new
        {
            userId,
            description = "Todo to complete",
            labels = Array.Empty<string>(),
            priority = 1
        };
        HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync("todos", createRequest, TestContext.Current.CancellationToken);
        createResponse.EnsureSuccessStatusCode();
        Guid todoId = await createResponse.Content.ReadFromJsonAsync<Guid>(TestContext.Current.CancellationToken);

        // Act
        HttpResponseMessage completeResponse = await HttpClient.PutAsync($"todos/{todoId}/complete", null, TestContext.Current.CancellationToken);

        // Assert
        completeResponse.EnsureSuccessStatusCode();

        HttpResponseMessage getResponse = await HttpClient.GetAsync($"todos/{todoId}", TestContext.Current.CancellationToken);
        getResponse.EnsureSuccessStatusCode();
        TodoDto? todo = await getResponse.Content.ReadFromJsonAsync<TodoDto>(TestContext.Current.CancellationToken);
        todo!.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task GetTodos_Should_ReturnPagedResults()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        var createRequest = new
        {
            userId,
            description = "Paged todo",
            labels = Array.Empty<string>(),
            priority = 1
        };
        await PostTodoAsync(createRequest, Guid.NewGuid().ToString());
        await PostTodoAsync(createRequest, Guid.NewGuid().ToString());

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"todos?userId={userId}&page=1&pageSize=1", TestContext.Current.CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        PagedTodos? page = await response.Content.ReadFromJsonAsync<PagedTodos>(TestContext.Current.CancellationToken);
        page!.Items.Count.ShouldBe(1);
        page.TotalCount.ShouldBeGreaterThanOrEqualTo(2);
        page.HasNextPage.ShouldBeTrue();
    }

    [Fact]
    public async Task GetTodos_Should_ReturnEmptyPage_WhenOffsetExceedsIntMaxValue()
    {
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        HttpResponseMessage response = await HttpClient.GetAsync(
            $"todos?userId={userId}&page=2147483647&pageSize=100", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        PagedTodos? page = await response.Content.ReadFromJsonAsync<PagedTodos>(TestContext.Current.CancellationToken);
        page!.Items.ShouldBeEmpty();
        page.HasNextPage.ShouldBeFalse();
        page.HasPreviousPage.ShouldBeTrue();
    }

    [Fact]
    public async Task GetTodos_Should_UseStableOrdering_WhenSortValuesAreEqual()
    {
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        var createRequest = new
        {
            userId,
            description = "Same description for deterministic pagination",
            labels = Array.Empty<string>(),
            priority = 1
        };
        await PostTodoAsync(createRequest, Guid.NewGuid().ToString());
        await PostTodoAsync(createRequest, Guid.NewGuid().ToString());
        await PostTodoAsync(createRequest, Guid.NewGuid().ToString());

        var ids = new List<Guid>();
        for (int pageNumber = 1; pageNumber <= 3; pageNumber++)
        {
            HttpResponseMessage response = await HttpClient.GetAsync(
                $"todos?userId={userId}&sortColumn=description&page={pageNumber}&pageSize=1",
                TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
            PagedTodos? page = await response.Content.ReadFromJsonAsync<PagedTodos>(TestContext.Current.CancellationToken);
            ids.Add(page!.Items.Single().Id);
        }

        ids.Distinct().Count().ShouldBe(3);
        // PostgreSQL UUID ordering is not the same as .NET Guid comparison.
        // Re-reading the same pages must produce the same identifiers.
        for (int pageNumber = 1; pageNumber <= 3; pageNumber++)
        {
            HttpResponseMessage response = await HttpClient.GetAsync(
                $"todos?userId={userId}&sortColumn=description&page={pageNumber}&pageSize=1",
                TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
            PagedTodos? page = await response.Content.ReadFromJsonAsync<PagedTodos>(TestContext.Current.CancellationToken);
            page!.Items.Single().Id.ShouldBe(ids[pageNumber - 1]);
        }
    }
    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=-1")]
    [InlineData("pageSize=101")]
    [InlineData("pageSize=2147483647")]
    public async Task GetTodos_Should_RejectInvalidPagination(string query)
    {
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        HttpResponseMessage response = await HttpClient.GetAsync(
            $"todos?userId={userId}&{query}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetTodos_Should_RejectAccessToOtherUsersTodos()
    {
        (Guid ownerId, _) = await RegisterAndLoginAsync();
        (_, AccessTokens otherTokens) = await RegisterAndLoginAsync();
        Authenticate(otherTokens.AccessToken);

        HttpResponseMessage response = await HttpClient.GetAsync(
            $"todos?userId={ownerId}&page=1&pageSize=10", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
    private sealed record PagedTodos(
        List<TodoDto> Items,
        int Page,
        int PageSize,
        int TotalCount,
        bool HasNextPage,
        bool HasPreviousPage);

    private async Task<HttpResponseMessage> PostTodoAsync(object request, string idempotencyKey)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "todos")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("Idempotency-Key", idempotencyKey);

        return await HttpClient.SendAsync(message);
    }
}
