using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Todos.Get;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Todos;

internal sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("todos", async (
            Guid userId,
            string? search,
            string? sortColumn,
            string? sortOrder,
            int? page,
            int? pageSize,
            IQueryHandler<GetTodosQuery, PagedList<TodoResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            if (page is < 1 || pageSize is < 1 or > 100)
            {
                return Results.BadRequest("Page must be at least 1 and pageSize must be between 1 and 100.");
            }

            var query = new GetTodosQuery(
                userId,
                search,
                sortColumn,
                sortOrder,
                page ?? 1,
                pageSize ?? 10);

            Result<PagedList<TodoResponse>> result = await handler.Handle(query, cancellationToken);

            if (result.IsSuccess)
            {
                return Results.Ok(result.Value);
            }

            if (result.Error.Code == "Users.Unauthorized")
            {
                return Results.Forbid();
            }

            return CustomResults.Problem(result);
        })
        .WithTags(Tags.Todos)
        .RequireAuthorization();
    }
}
