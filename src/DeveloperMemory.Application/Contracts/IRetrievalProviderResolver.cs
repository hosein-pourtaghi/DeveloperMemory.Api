using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;

namespace DeveloperMemory.Application.Contracts;

public interface IRetrievalProviderResolver
{
    IMemoryRetrievalProvider Resolve(RetrievalMode mode);
}
