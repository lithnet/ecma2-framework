# Lithnet.Ecma2Framework.SourceGeneration.Tests

Tests for the source generator: consumer discovery, the diagnostics it raises, and the sources it emits. The compile proof feeds the generated sources back through the C# compiler against the real runtime surface, so the generated `Main`, config providers, and MMS resolver are checked to compile. Targets net8.0.
