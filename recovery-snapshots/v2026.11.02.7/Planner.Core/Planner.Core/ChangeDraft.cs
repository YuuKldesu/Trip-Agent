namespace Planner.Core;

public sealed record ChangeDraft(long Revision, ScopeContext Context, AiReply Reply);
