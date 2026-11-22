namespace Saas.Agenda.Application.Ports;

public interface IClockPort
{
    DateTimeOffset UtcNow { get; }
}
