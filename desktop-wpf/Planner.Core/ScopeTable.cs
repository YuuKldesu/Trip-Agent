using System;

namespace Planner.Core;

public sealed record ScopeTable(string Id, string Name, bool Enabled, DateOnly? ValidFrom, DateOnly? ValidUntil);
