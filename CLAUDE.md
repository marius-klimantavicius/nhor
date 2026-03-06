## Unsafe Code

The C# project **is allowed** to use `unsafe` code blocks and pointers. This is
intentional to keep parity with the C++ implementation. Use `unsafe` wherever it
simplifies the port or maintains structural similarity with the original code.

## Third-Party Dependencies

The ported library code **must not** depend on third-party NuGet packages with
incompatible licenses (e.g. SixLabors.ImageSharp which uses a commercial license).
Embedded C/C++ libraries within ThorVG (LodePNG, jpgd, the internal WebP decoder)
**must be ported line-by-line to C#**, not replaced with external packages.

Allowed exceptions:
- **Jint** (MIT license) — replaces JerryScript for Lottie expression evaluation.
- **System.Text.Json** — part of the .NET runtime, replaces RapidJSON.
- **Test project only** — the test project (`ThorVG.Tests`) may use any NuGet
  package for test-data generation (e.g. ImageSharp for creating test images),
  but the main library (`ThorVG`) must not reference them.

## General Porting Guidelines

- Prefer idiomatic C# where it does not compromise structural fidelity to the
  original C++ code. The goal is a recognizable port, not a rewrite.
- Preserve original class/struct/enum names and member names where possible.
- Keep files in the same relative module structure as the C++ source.
- `#region` blocks may be used to mirror C++ `#ifdef` / conditional compilation
  sections.
- Use `Span<T>`, `stackalloc`, and raw pointers (in unsafe blocks) to match C++
  memory access patterns.

## Tooling and Scripting

- **All tooling scripts must be Python** (`.py` files) written using the `Write`
  tool. **STRICTLY FORBIDDEN: `python3 -c "..."` or `python -c "..."` via Bash.**
  Always create a `.py` file with the `Write` tool and then run it.
- Python scripts should use `subprocess` to invoke `dotnet`, `meson`, `ninja`,
  `g++`, and compiled binaries.
- Place all tool/debug/test scripts in the **`tmp/`** directory inside the project
  (not `/tmp/`).
