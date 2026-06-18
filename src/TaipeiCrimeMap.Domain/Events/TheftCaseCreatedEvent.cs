using TaipeiCrimeMap.Domain.Common;

namespace TaipeiCrimeMap.Domain.Events;

public sealed record TheftCaseCreatedEvent(Guid TheftCaseId, int CaseNumber) : IDomainEvent;
