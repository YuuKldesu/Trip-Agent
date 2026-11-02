using System.Collections.Generic;

namespace Planner.Core;

public sealed record RepositoryPlan(long Revision, IReadOnlyList<PlannedTrip> Rows);
