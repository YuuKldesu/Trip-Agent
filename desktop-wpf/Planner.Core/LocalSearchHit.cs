using System;

namespace Planner.Core;

public sealed record LocalSearchHit(string Kind, string Id, string TableId, string Title, string Detail, DateOnly? Day);
