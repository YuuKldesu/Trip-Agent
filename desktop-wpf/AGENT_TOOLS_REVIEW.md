# Agent tools and memory final review

Review base: df5192c; reviewed implementation: 8e6096e. One fresh-context read-only reviewer (gpt-6-astra) inspected the whole feature. No second review was commissioned.

## Important findings — all fixed
1. Genuine evidence substring could accompany false confirmed value. Added deterministic evidence matching: verbatim text, numeric minute conversion, source-message-date relative dates, HH:mm normalization. Other inference stays a suggestion. Regression rejected 120 citing 30 minutes and wrong year citing tomorrow; valid normalized date accepted.
2. Ready checked only event start day. Now checks every occupied day through end-exclusive last instant, including overnight activity end date.
3. Automatic start accepted route queried at any time on same day. Ready now runs readonly RepositoryPlanner using only exact departure/mode/endpoint cached routes; missing routes produce precise query parameters, never approximate same-day acceptance. Summary includes host-calculated tentative times.
4. Oversized failed tool result left successful memory and re-injected rejected payload. Each tool now works on a clone, validates result and memory projection, and atomically commits only on success. Model projection excludes route geometry and is capped at64000 characters; request history is capped before network send.

All four have reproducing RED evidence in tmp/agent-review-red.log and tmp/agent-auto-red.log, followed by GREEN149/149 in tmp/agent-reviewed-final-core.log. Final build:0 warnings/errors. Focused native flow15/15 plus existing route HTTP6/6 and constraints5/5. Logs are under workspace tmp/.

## Rulings and evidence limits
- Work stayed in the existing feature branch rather than another worktree, consistent with project workflow; task planning approval was already given. Cost: shared checkout must not be concurrently switched.
- Use structured JSON tool_requests through existing compatible API rather than provider-native function calling. Cost: model must follow the declared JSON protocol; invalid responses remain visible and do not write data.
- Stored endpoints in relevant saved occurrences may be used as host-trusted references; source status is retained. Cost: previously stored coordinates can be outdated and remain subject to review.
- Partial followups retain the prior draft marked STALE, not an active plan. Cost: model can mention old suggestions, but only current validated Ready replies can be saved after user review.
- Unknown model-inferred confirmation values are rejected or remain suggestions; this deliberately favors an additional confirmation over silently inventing normalized preferences.
- No real paid map/model services were called. AmapClient and model HTTP roundtrips were executed with fixtures; Windows native control flows tested against isolated data. Real hardware touch, dragging, multi-monitor/all DPI and live provider behavior remain unverified.
- Existing full-native baseline fails after98 passes on calendar card corner-radius assertion, before this feature; not changed. This report does not claim the entire native suite passes. Old guided fixture cases that omit required place confirmations need protocol-aware fixtures before a future full-guided suite gate.

Reviewer declined to judge real paid online behavior, known prior calendar assertion and stale stored coordinates. We retain fixture-only verification, preserve calendar scope, and disclose stored-coordinate freshness. No deferred minor findings.
