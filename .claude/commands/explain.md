---
description: Explain code line by line, including why the syntax is used
---

Explain the code below thoroughly. Assume I know general programming but not the
obscure corners of C# and Unity. Answer in English.

Cover all four of these:

1. **Purpose** — a short paragraph on what this code is for and where it fits,
   before going line by line.

2. **Line by line** — walk through it in order. For each meaningful line: what it
   does and what it operates on. Group trivial consecutive lines rather than
   padding, but do not skip anything that carries logic.

3. **Syntax and language choices** — this is the important part. For every C#
   construct used, explain *why it is written that way*:
   - what the keyword, operator or symbol actually means
   - why this construct was chosen over the alternatives
   - what would break or change if it were written differently

   Give particular attention to anything cryptic or easy to misread: `?.`, `??`,
   `=>`, `var`, `readonly`, `static`, `ref` / `out` / `in`, generics and their
   constraints, `yield return`, `async` / `await`, LINQ chains, delegates and
   events, nullable types, pattern matching, `struct` vs `class` semantics,
   extension methods, attributes in square brackets, and operator overloads.

   For Unity specifically, explain lifecycle methods, `[SerializeField]`,
   coroutines, `Object` lifetime and its fake-null behaviour, and anything else
   that only makes sense in Unity's context.

4. **Traps** — anything surprising, risky, or a common source of bugs here.

Be precise. If something is genuinely unclear or looks wrong, say so rather than
inventing a justification for it.

$ARGUMENTS
