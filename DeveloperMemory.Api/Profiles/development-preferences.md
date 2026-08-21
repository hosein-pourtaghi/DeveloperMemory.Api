---
name: Development Preferences
scope: global
---

# Development Preferences

General preferences that should influence how code is written across all projects.

## Code Quality

- Prefer clean, readable, maintainable code.
- Prefer simple solutions over clever solutions.
- Avoid unnecessary complexity.
- Use meaningful names for types, methods, variables, and parameters.
- Keep classes and methods focused on a single responsibility.
- Prefer explicit code over implicit magic when it improves clarity.

## Design Principles

- Apply SOLID principles where appropriate, not dogmatically.
- Prefer composition over unnecessary inheritance.
- Use abstractions when they provide real value, not for the sake of layering.
- Avoid over-engineering and premature abstraction.
- Use the simplest architecture that satisfies the requirements.

## Dependencies

- Prefer existing project dependencies and platform functionality.
- Avoid adding new packages unless the benefit is clear and justified.
- Evaluate whether the language, framework, or BCL already solves the problem.
- Prefer well-maintained, widely-used, stable libraries when a dependency is needed.

## Maintainability

- Preserve existing conventions and patterns in a project.
- Avoid unnecessary refactoring when completing unrelated tasks.
- Keep changes focused on the stated goal.
- Consider the long-term maintenance cost of new abstractions.

## Engineering Quality

AI-generated code should consider:

- Performance where relevant, without premature optimization.
- Security, including input validation, secrets handling, and safe defaults.
- Reliability and graceful error handling.
- Input validation and boundary conditions.
- Testability through clean interfaces and separation of concerns.
- Observability through structured logging where the project supports it.

These are preferences, not absolute mandates. Project-specific requirements always take precedence.
