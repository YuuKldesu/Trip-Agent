using System;

namespace Planner.Core;

public sealed record ScopeItem(string ActivityId, DateOnly? OriginDate, string TableId, string Title, string Place, DateTime StartAt, DateTime EndAt, string ConstraintType);
