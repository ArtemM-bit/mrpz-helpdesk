using Support.Api.Models;

namespace Support.Api.Services;

public interface IStatusService
{
    ApiStatus GetStatus();
}
