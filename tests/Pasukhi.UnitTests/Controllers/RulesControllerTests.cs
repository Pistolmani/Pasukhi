using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Pasukhi.API.Controllers;
using Pasukhi.Application.DTOs.Rules;
using Pasukhi.Application.Interfaces;
using Pasukhi.Domain.Enums;

namespace Pasukhi.UnitTests.Controllers;

public class RulesControllerTests
{
    private static AutomationRuleDto NewDto(Guid? id = null) => new(
        id ?? Guid.NewGuid(), Guid.NewGuid(), "Rule", 1,
        TriggerType.Keyword, "hello", ActionType.SendReply, "Hi!",
        true, 0, DateTime.UtcNow, DateTime.UtcNow);

    [Fact]
    public async Task GetAll_returns_200_with_list()
    {
        var service = Substitute.For<IRuleService>();
        var items = new List<AutomationRuleDto> { NewDto() };
        service.GetAllAsync(default).ReturnsForAnyArgs(items);
        var controller = new RulesController(service);

        var result = await controller.GetAll(default) as OkObjectResult;

        Assert.Equal(200, result?.StatusCode);
        Assert.Equal(items, result?.Value);
    }

    [Fact]
    public async Task GetById_returns_404_when_not_found()
    {
        var service = Substitute.For<IRuleService>();
        service.GetByIdAsync(Arg.Any<Guid>(), default).ReturnsForAnyArgs((AutomationRuleDto?)null);
        var controller = new RulesController(service);

        var result = await controller.GetById(Guid.NewGuid(), default);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Create_returns_201_with_correct_location()
    {
        var service = Substitute.For<IRuleService>();
        var dto = NewDto();
        service.CreateAsync(Arg.Any<CreateAutomationRuleRequest>(), default).ReturnsForAnyArgs(dto);
        var controller = new RulesController(service);

        var result = await controller.Create(
            new CreateAutomationRuleRequest("Rule", 1, TriggerType.Keyword, "hello", ActionType.SendReply, "Hi!", true),
            default) as CreatedAtActionResult;

        Assert.Equal(201, result?.StatusCode);
        Assert.Equal(dto.Id, ((dynamic)result!.RouteValues!["id"]!));
    }

    [Fact]
    public async Task UpdatePriorities_returns_204()
    {
        var service = Substitute.For<IRuleService>();
        var controller = new RulesController(service);

        var result = await controller.UpdatePriorities(
            new UpdateRulePrioritiesRequest(Array.Empty<RulePriorityItem>()),
            default);

        Assert.IsType<NoContentResult>(result);
        await service.Received(1).UpdatePrioritiesAsync(Arg.Any<UpdateRulePrioritiesRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_returns_204()
    {
        var service = Substitute.For<IRuleService>();
        var controller = new RulesController(service);

        var result = await controller.Delete(Guid.NewGuid(), default);

        Assert.IsType<NoContentResult>(result);
    }
}
