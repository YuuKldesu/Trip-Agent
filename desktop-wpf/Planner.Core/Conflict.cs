namespace Planner.Core;

public sealed record Conflict(string Kind, string FirstKey, string? SecondKey, string Message);
