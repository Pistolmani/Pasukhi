using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Pasukhi.API.Controllers;
using Pasukhi.Application.DTOs.Channels;
using Pasukhi.Application.Interfaces;
using Pasukhi.Domain.Enums;

namespace Pasukhi.UnitTests.Controllers;

public class ChannelsControllerTests
{
    private static ChannelConnectionDto NewDto(Guid? id = null) => new(
        id ?? Guid.NewGuid(), Guid.NewGuid(), ChannelType.Instagram,
        "acct-1", null, "tok", "vtok", true, null,
        DateTime.UtcNow, DateTime.UtcNow);

    private static ChannelsController NewController(
        IChannelService? channels = null,
        IMessengerProfileService? messenger = null) =>
        new(
            channels ?? Substitute.For<IChannelService>(),
            messenger ?? Substitute.For<IMessengerProfileService>());

    [Fact]
    public async Task GetAll_returns_200_with_list()
    {
        var service = Substitute.For<IChannelService>();
        var items = new List<ChannelConnectionDto> { NewDto() };
        service.GetAllAsync(default).ReturnsForAnyArgs(items);
        var controller = new ChannelsController(service, Substitute.For<IMessengerProfileService>());

        var result = await controller.GetAll(default) as OkObjectResult;

        Assert.Equal(200, result?.StatusCode);
        Assert.Equal(items, result?.Value);
    }

    [Fact]
    public async Task GetById_returns_404_when_not_found()
    {
        var service = Substitute.For<IChannelService>();
        service.GetByIdAsync(Arg.Any<Guid>(), default).ReturnsForAnyArgs((ChannelConnectionDto?)null);
        var controller = NewController(service);

        var result = await controller.GetById(Guid.NewGuid(), default);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Create_returns_201_with_correct_location()
    {
        var service = Substitute.For<IChannelService>();
        var dto = NewDto();
        service.CreateAsync(Arg.Any<CreateChannelConnectionRequest>(), default).ReturnsForAnyArgs(dto);
        var controller = NewController(service);

        var result = await controller.Create(
            new CreateChannelConnectionRequest(ChannelType.Instagram, "acct-1", null, "tok", "vtok", true),
            default) as CreatedAtActionResult;

        Assert.Equal(201, result?.StatusCode);
        Assert.Equal(dto.Id, ((dynamic)result!.RouteValues!["id"]!));
    }

    [Fact]
    public async Task SyncMessengerProfile_returns_200_with_result()
    {
        var messenger = Substitute.For<IMessengerProfileService>();
        var syncResult = new SyncMessengerProfileResult(true, 3, true);
        messenger.SyncAsync(Arg.Any<SyncMessengerProfileRequest>(), default).ReturnsForAnyArgs(syncResult);
        var controller = NewController(messenger: messenger);

        var result = await controller.SyncMessengerProfile(
            new SyncMessengerProfileRequest("Hello!", 3),
            default) as OkObjectResult;

        Assert.Equal(200, result?.StatusCode);
        Assert.Equal(syncResult, result?.Value);
    }

    [Fact]
    public async Task GetMessengerGreeting_returns_200_with_text()
    {
        var messenger = Substitute.For<IMessengerProfileService>();
        messenger.GetStoredGreetingTextAsync(default).ReturnsForAnyArgs("Hi there!");
        var controller = NewController(messenger: messenger);

        var result = await controller.GetMessengerGreeting(default) as OkObjectResult;

        Assert.Equal(200, result?.StatusCode);
        var json = JsonSerializer.Serialize(result!.Value);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Hi there!", doc.RootElement.GetProperty("greetingText").GetString());
    }

    [Fact]
    public async Task Delete_returns_204()
    {
        var controller = NewController();

        var result = await controller.Delete(Guid.NewGuid(), default);

        Assert.IsType<NoContentResult>(result);
    }
}
