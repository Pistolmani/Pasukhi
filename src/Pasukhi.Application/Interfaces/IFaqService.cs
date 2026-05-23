using Pasukhi.Application.DTOs.Faqs;

namespace Pasukhi.Application.Interfaces;

public interface IFaqService : ICrudService<FaqItemDto, CreateFaqItemRequest, UpdateFaqItemRequest>
{
}
