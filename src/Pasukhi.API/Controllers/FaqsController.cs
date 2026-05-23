using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pasukhi.Application.DTOs.Faqs;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.API.Controllers;

[ApiController]
[Route("api/faqs")]
[Authorize]
public class FaqsController : CrudControllerBase<FaqItemDto, CreateFaqItemRequest, UpdateFaqItemRequest, IFaqService>
{
    public FaqsController(IFaqService faqs) : base(faqs) { }

    protected override Guid GetEntityId(FaqItemDto dto) => dto.Id;
}
