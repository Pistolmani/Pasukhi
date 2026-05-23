using Microsoft.AspNetCore.Mvc;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.API.Controllers;

public abstract class CrudControllerBase<TDto, TCreate, TUpdate, TService> : ControllerBase
    where TService : ICrudService<TDto, TCreate, TUpdate>
{
    protected readonly TService Service;

    protected CrudControllerBase(TService service)
    {
        Service = service;
    }

    protected abstract Guid GetEntityId(TDto dto);

    [HttpGet]
    public virtual async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await Service.GetAllAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public virtual async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await Service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public virtual async Task<IActionResult> Create(
        [FromBody] TCreate request,
        CancellationToken cancellationToken)
    {
        var result = await Service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = GetEntityId(result) }, result);
    }

    [HttpPut("{id:guid}")]
    public virtual async Task<IActionResult> Update(
        Guid id,
        [FromBody] TUpdate request,
        CancellationToken cancellationToken)
    {
        var result = await Service.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public virtual async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
