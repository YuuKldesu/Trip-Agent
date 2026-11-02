using System;
using System.Collections.Generic;

namespace Planner.Core;

public sealed record BoardDay(DateOnly Date, IReadOnlyList<BoardEntry> Entries);
