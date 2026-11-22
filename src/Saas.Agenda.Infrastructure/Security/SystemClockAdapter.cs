using Saas.Agenda.Application.Ports;

namespace Saas.Agenda.Infrastructure.Security;

internal sealed class SystemClockAdapter : IClockPort
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
