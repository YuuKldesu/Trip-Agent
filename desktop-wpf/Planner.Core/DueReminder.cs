using System;

namespace Planner.Core;

public sealed record DueReminder(string ActivityId, string Key, DateTime DueAt, string Title, string Label);
