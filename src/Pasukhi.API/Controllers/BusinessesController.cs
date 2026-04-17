using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pasukhi.Application.DTOs.Businesses;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.API.Controllers;

[ApiController]
[Route("api/businesses")]
[Authorize(Roles = "SuperAdmin")]
public class BusinessesController : ControllerBase
{
    private readonly IBusinessService _businesses;

    public BusinessesController(IBusinessService businesses)
    {
        _businesses = businesses;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _businesses.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _businesses.GetByIdAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBusinessRequest request)
    {
        var result = await _businesses.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBusinessRequest request)
    {
        var result = await _businesses.UpdateAsync(id, request);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _businesses.DeleteAsync(id);
        return NoContent();
    }
}
