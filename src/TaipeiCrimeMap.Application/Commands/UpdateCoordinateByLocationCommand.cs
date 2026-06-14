namespace TaipeiCrimeMap.Application.Commands;

public record UpdateCoordinateByLocationCommand(string RawLocation, double Latitude, double Longitude);
