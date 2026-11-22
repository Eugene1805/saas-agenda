namespace Saas.Agenda.Domain.ValueObjects;

public enum AgendaAccessOperation
{
    LIST_FILTER,
    READ_APPOINTMENT,
    MANAGE_APPOINTMENT,
}

public sealed record AgendaAccessDecision(
    bool Allowed,
    string? DenialCode = null,
    string? DenialMessage = null);
