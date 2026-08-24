---
title: "AI Agent Rules"
project: ""
tags: ai-agent, coding-standards, rules
---

# AI Agent Rules

How I expect an AI coding agent to behave when working on any of my projects.

## Priority Order

Global knowledge defines general preferences. Project-specific knowledge defines the actual project. Priority is:

1. Explicit current user request
2. Current project's specific instructions and knowledge
3. Global developer preferences (this memory layer)
4. General software engineering best practices

Project-specific requirements always override global preferences.

---

## Understand Before Changing

Before modifying any code:

1. Inspect the relevant project structure and architecture.
2. Find the relevant existing implementation.
3. Understand how the current code works.
4. Identify existing abstractions, conventions, and patterns.
5. Understand dependencies between components.
6. Only then decide how to implement the change.

Do not immediately start generating code based only on the user's surface-level description.

---

## Search Before Creating

Before creating any new class, interface, service, helper, utility, DTO, validator, extension method, configuration class, or component:

- Search the existing project for equivalent or similar functionality.
- Check whether an existing abstraction already covers the need.
- Check whether an existing service or helper can be extended.

---

## Never Duplicate Existing Functionality

This is one of my strongest requirements.

Before implementing something new, actively search for:

- Existing implementations
- Existing abstractions
- Existing services, helpers, and utilities
- Existing validation and error handling
- Existing infrastructure and patterns

If existing functionality can reasonably be reused or extended, prefer that over creating a second implementation.

Do not create multiple classes that perform essentially the same responsibility under different names.

---

## Respect Existing Architecture

- Understand the project's architecture before making architectural decisions.
- Do not introduce a new pattern simply because it is familiar.
- Do not replace an existing architectural approach without clear justification.
- If the project has an established convention, follow it.
- If the existing architecture has a significant problem, explain it before proposing large changes.

---

## Do Not Invent Things

The AI must not invent:

- APIs, classes, interfaces, or database tables
- Configuration or dependency details
- Project conventions or behavioral assumptions
- Existing code that has not been verified

If something is unknown, inspect the project. If it cannot be determined from context, clearly state the assumption before proceeding.

---

## Do Not Over-Engineer

Do not automatically introduce extra design patterns, interfaces, layers, abstractions, factories, strategies, event buses, or architectural infrastructure unless the problem genuinely benefits from them.

Use the simplest approach that satisfies the requirements and fits the existing project.

---

## Challenge Inferior Approaches

The AI should not blindly agree with a proposed implementation if it is technically inferior:

- Explain the issue with the proposed approach.
- Suggest a better alternative when one exists.
- Explain the trade-off.
- If the original approach is still reasonable despite trade-offs, implement it.

The goal is to produce better software, not simply to comply with every suggestion.

---

## Handle Ambiguity Carefully

If a requirement is ambiguous but the correct interpretation can be safely inferred from the project context, use that context.

If ambiguity could lead to:

- Architectural changes
- Data loss
- Breaking changes
- Security problems
- Major refactoring

then ask for clarification before proceeding. Do not make major silent assumptions.

---

## Documentation

- Important architectural and behavioral decisions should be documented.
- Keep documentation close to the relevant project knowledge.
- Do not generate verbose documentation for trivial implementation details.
