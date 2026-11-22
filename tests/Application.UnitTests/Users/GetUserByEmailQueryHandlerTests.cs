using Application.Abstractions.Authentication;
using Application.Users.GetByEmail;
using Application.UnitTests.Abstractions;
using Domain.Users;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class GetUserByEmailQueryHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnUser_WhenEmailBelongsToCurrentUser()
    {
        await using TestDbContext context = CreateDbContext();
        User user = CreateUser("owner@example.com");
        context.Users.Add(user);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(user.Id);
        var handler = new GetUserByEmailQueryHandler(context, userContext);

        Result<UserResponse> result = await handler.Handle(
            new GetUserByEmailQuery(user.Email), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(user.Id);
        result.Value.Email.ShouldBe(user.Email);
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenEmailBelongsToAnotherUser()
    {
        await using TestDbContext context = CreateDbContext();
        User otherUser = CreateUser("other@example.com");
        context.Users.Add(otherUser);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        var handler = new GetUserByEmailQueryHandler(context, userContext);

        Result<UserResponse> result = await handler.Handle(
            new GetUserByEmailQuery(otherUser.Email), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Users.Unauthorized");
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenEmailDoesNotExist()
    {
        await using TestDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        var handler = new GetUserByEmailQueryHandler(context, userContext);

        Result<UserResponse> result = await handler.Handle(
            new GetUserByEmailQuery("missing@example.com"), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Users.NotFoundByEmail");
    }

    private static User CreateUser(string email) => new()
    {
        Id = Guid.NewGuid(),
        Email = email,
        FirstName = "Test",
        LastName = "User",
        PasswordHash = "unused-in-this-test"
    };
}