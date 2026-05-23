using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Pasukhi.API.Controllers;
using Pasukhi.Application.DTOs.Faqs;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.UnitTests.Controllers;

public class FaqsControllerTests
{
    private static FaqItemDto NewDto(Guid? id = null) => new(
        id ?? Guid.NewGuid(), Guid.NewGuid(), "Q", "A", null, 0, true, 0,
        DateTime.UtcNow, DateTime.UtcNow);

    [Fact]
    public async Task GetAll_returns_200_with_list()
    {
        var service = Substitute.For<IFaqService>();
        var items = new List<FaqItemDto> { NewDto(), NewDto() };
        service.GetAllAsync(default).ReturnsForAnyArgs(items);
        var controller = new FaqsController(service);

        var result = await controller.GetAll(default) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result!.StatusCode);
        Assert.Equal(items, result.Value);
    }

    [Fact]
    public async Task GetById_returns_200_when_found()
    {
        var service = Substitute.For<IFaqService>();
        var dto = NewDto();
        service.GetByIdAsync(dto.Id, default).ReturnsForAnyArgs(dto);
        var controller = new FaqsController(service);

        var result = await controller.GetById(dto.Id, default) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result!.StatusCode);
        Assert.Equal(dto, result.Value);
    }

    [Fact]
    public async Task GetById_returns_404_when_not_found()
    {
        var service = Substitute.For<IFaqService>();
        service.GetByIdAsync(Arg.Any<Guid>(), default).ReturnsForAnyArgs((FaqItemDto?)null);
        var controller = new FaqsController(service);

        var result = await controller.GetById(Guid.NewGuid(), default);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Create_returns_201_with_location()
    {
        var service = Substitute.For<IFaqService>();
        var dto = NewDto();
        service.CreateAsync(Arg.Any<CreateFaqItemRequest>(), default).ReturnsForAnyArgs(dto);
        var controller = new FaqsController(service);

        var result = await controller.Create(new CreateFaqItemRequest("Q", "A", null, true, 0), default)
            as CreatedAtActionResult;

        Assert.NotNull(result);
        Assert.Equal(201, result!.StatusCode);
        Assert.Equal(nameof(FaqsController.GetById), result.ActionName);
        Assert.Equal(dto.Id, ((dynamic)result.RouteValues!["id"]!));
        Assert.Equal(dto, result.Value);
    }

    [Fact]
    public async Task Update_returns_200()
    {
        var service = Substitute.For<IFaqService>();
        var dto = NewDto();
        service.UpdateAsync(dto.Id, Arg.Any<UpdateFaqItemRequest>(), default).ReturnsForAnyArgs(dto);
        var controller = new FaqsController(service);

        var result = await controller.Update(dto.Id, new UpdateFaqItemRequest("Q", "A", null, true, 0), default)
            as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result!.StatusCode);
        Assert.Equal(dto, result.Value);
    }

    [Fact]
    public async Task Delete_returns_204()
    {
        var service = Substitute.For<IFaqService>();
        var controller = new FaqsController(service);

        var result = await controller.Delete(Guid.NewGuid(), default);

        Assert.IsType<NoContentResult>(result);
    }
}
