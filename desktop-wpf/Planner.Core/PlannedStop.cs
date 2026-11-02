using System;

namespace Planner.Core;

public sealed record PlannedStop(Occurrence Original, DateTime Start, DateTime End, RouteOption Route, DateTime Depart, DateTime Arrive, int BufferSec);
