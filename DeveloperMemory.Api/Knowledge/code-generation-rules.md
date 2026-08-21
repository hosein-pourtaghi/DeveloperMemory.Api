---
name: Code Generation Rules
scope: global
---

# Code Generation Rules

Standards for how AI-generated or AI-modified code should look and behave.

## Production Quality

Generated code should be production-quality, not pseudo-implementation. Do not leave unnecessary TODOs, placeholders, or commented-out dead code unless explicitly requested.

Code should be:

- Readable and self-documenting
- Consistent with the existing project's style
- Properly structured with appropriate separation of concerns
- Appropriately validated at boundaries
- Properly error-handled for failure scenarios
- Secure where relevant to the context
- Performance-conscious where relevant to the context

---

## Minimal and Focused Changes

When implementing a task:

- Change only what is necessary.
- Do not refactor unrelated code.
- Do not rename unrelated classes.
- Do not reorganize unrelated folders.
- Do not rewrite working code without a reason.
- Do not introduce unnecessary abstractions alongside the change.

Prefer small, focused changes over large refactors that happen to include the requested change.

---

## Preserve Existing Behavior

When modifying existing functionality:

- Preserve existing behavior unless the task explicitly requires changing it.
- Avoid accidental breaking changes.
- Consider backward compatibility for public interfaces.
- Check callers of modified methods.
- Check related tests and configuration.

If behavior intentionally changes, make that change explicit and deliberate.

---

## Error Handling

- Use the project's existing error-handling approach.
- Do not introduce a completely different error-handling strategy for a single feature.
- Do not swallow exceptions silently.
- Do not write empty catch blocks.
- Do not return meaningless error messages.
- Do not expose sensitive internal details in error responses.
- Log useful diagnostic information where the project's logging architecture supports it.

---

## Performance

Performance should be considered but not prematurely optimized. Pay particular attention to:

- Database queries, especially N+1 query patterns
- Unnecessary object allocations in hot paths
- Excessive or redundant network calls
- Blocking asynchronous code
- Unbounded collections loaded into memory
- Repeated expensive operations without caching

Prefer reasoning about actual bottlenecks over blindly optimizing everything.

---

## Security

AI-generated code should consider common security concerns where relevant:

- Input validation and sanitization
- Secrets and credentials handling
- Injection prevention
- Safe deserialization
- Sensitive information in logs
- File access boundaries
- Authentication and authorization boundaries

Never hard-code secrets, API keys, passwords, or credentials in source code.

---

## Testing

When modifying behavior:

- Inspect existing tests before changing behavior.
- Follow existing testing conventions and frameworks.
- Add or update tests for meaningful behavioral changes.
- Do not create meaningless tests purely to increase coverage numbers.
- Consider testability when designing new code.

---

## Refactoring

Refactoring is allowed when it genuinely improves the solution, but it must be intentional and relevant.

- Do not perform unrelated cleanup during feature tasks.
- If a refactor is large or architectural, explain why it is needed.
- Keep refactoring changes controlled and reviewable.
- Do not mix unrelated concerns in a single change.

---

## Dependency Rules

Before adding a new package:

1. Check whether the project already has something that solves the problem.
2. Check whether the language or framework provides the functionality natively.
3. Check whether an existing dependency can be reused.
4. Only then consider adding a new dependency.

If a new dependency is necessary, explain why it is justified. Avoid dependency proliferation.
