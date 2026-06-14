using TaipeiCrimeMap.Application.Commands;
using TaipeiCrimeMap.Domain.Repositories;

namespace TaipeiCrimeMap.Application.Handlers;

public class UpdateCoordinateByLocationCommandHandler
{
    private readonly ICrimeRepository _crimeRepository;

    public UpdateCoordinateByLocationCommandHandler(ICrimeRepository crimeRepository)
    {
        _crimeRepository = crimeRepository;
    }

    public Task<int> HandleAsync(UpdateCoordinateByLocationCommand command, CancellationToken cancellationToken = default)
    {
        return _crimeRepository.UpdateCoordinateByLocationAsync(
            command.RawLocation, command.Latitude, command.Longitude, cancellationToken);
    }
}
