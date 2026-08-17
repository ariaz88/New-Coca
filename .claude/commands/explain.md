---
description: Explain code line by line, including why the syntax is used
---

Explain the code below thoroughly. Assume I know general programming but not the
obscure corners of C# and Unity. Answer in English.

Cover all five of these, in this order:

1. **The code, in full, first.** Before any explanation, reproduce the complete
   target verbatim in one fenced code block — signature, body, closing brace,
   and its existing comments. Do not paraphrase, truncate, or replace any part
   with `...`. I want to read the whole thing as a single piece before it is
   broken up.

   - If the target is a method, that means the entire method in one block.
   - If the target is a whole script, reproduce each method in full at the head
     of its own section, and lead with the class's fields and declarations.
   - State the file and line range above the block.

2. **Purpose** — a short paragraph on what this code is for and where it fits,
   before going line by line.

3. **Line by line** — walk through it in order. For each meaningful line: what it
   does and what it operates on. Group trivial consecutive lines rather than
   padding, but do not skip anything that carries logic.

4. **Syntax and language choices** — this is the important part. For every C#
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

5. **Traps** — anything surprising, risky, or a common source of bugs here.

Be precise. If something is genuinely unclear or looks wrong, say so rather than
inventing a justification for it.

$ARGUMENTS
