using SharpFocus.Core.Tests.TestHelpers;
using System.Diagnostics;

namespace SharpFocus.Core.Tests.Diagnostics;

/// <summary>
/// Diagnostic tests to understand CFG structure.
/// These tests are for development/debugging purposes.
/// </summary>
public class CfgDiagnosticTests
{
    private void WriteLine(string message)
    {
        Console.WriteLine(message);
        Debug.WriteLine(message);
    }

    [Fact]
    public void DumpCfg_SimpleAssignment()
    {
        // Arrange
        var cfg = CompilationHelper.CreateControlFlowGraph(@"
            class TestClass
            {
                void TestMethod()
                {
                    int x = 0;
                    x = 5;
                }
            }");

        // Act & Assert - dump the CFG
        WriteLine($"CFG has {cfg.Blocks.Length} blocks");

        for (int i = 0; i < cfg.Blocks.Length; i++)
        {
            var block = cfg.Blocks[i];
            WriteLine($"\nBlock {block.Ordinal} ({block.Kind}):");
            WriteLine($"  Operations: {block.Operations.Length}");

            for (int j = 0; j < block.Operations.Length; j++)
            {
                var op = block.Operations[j];
                WriteLine($"    [{j}] {op.Kind}: {op.GetType().Name}");
                WriteLine($"        Syntax: {op.Syntax?.ToString().Replace("\n", " ").Replace("\r", "")}");
            }
        }
    }

    [Fact]
    public void DumpCfg_ParameterAssignment()
    {
        // Arrange
        var cfg = CompilationHelper.CreateControlFlowGraph(@"
            class TestClass
            {
                void TestMethod(int param)
                {
                    param = 42;
                }
            }");

        // Act & Assert - dump the CFG
        WriteLine($"CFG has {cfg.Blocks.Length} blocks");

        foreach (var block in cfg.Blocks)
        {
            WriteLine($"\nBlock {block.Ordinal} ({block.Kind}):");
            WriteLine($"  Operations: {block.Operations.Length}");

            for (int j = 0; j < block.Operations.Length; j++)
            {
                var op = block.Operations[j];
                WriteLine($"    [{j}] {op.Kind}: {op.GetType().Name}");

                if (op is Microsoft.CodeAnalysis.Operations.ISimpleAssignmentOperation assign)
                {
                    WriteLine($"        Target: {assign.Target?.Kind} - {assign.Target?.GetType().Name}");
                    if (assign.Target is Microsoft.CodeAnalysis.Operations.IParameterReferenceOperation paramRef)
                    {
                        WriteLine($"        Parameter Name: {paramRef.Parameter.Name}");
                    }
                }
            }
        }

        // Verify we have at least some operations
        var totalOps = cfg.Blocks.Sum(b => b.Operations.Length);
        Assert.True(totalOps > 0, "CFG should have at least one operation");
    }

    [Fact]
    public void DumpCfg_RefArgument()
    {
        // Arrange
        var cfg = CompilationHelper.CreateControlFlowGraph(@"
            class TestClass
            {
                void TestMethod()
                {
                    int x = 0;
                    ModifyByRef(ref x);
                }

                void ModifyByRef(ref int value) { value = 10; }
            }");

        // Act & Assert - dump the CFG
        WriteLine($"CFG has {cfg.Blocks.Length} blocks");

        for (int i = 0; i < cfg.Blocks.Length; i++)
        {
            var block = cfg.Blocks[i];
            WriteLine($"\nBlock {block.Ordinal} ({block.Kind}):");
            WriteLine($"  Operations: {block.Operations.Length}");

            for (int j = 0; j < block.Operations.Length; j++)
            {
                var op = block.Operations[j];
                WriteLine($"    [{j}] {op.Kind}: {op.GetType().Name}");
                WriteLine($"        Syntax: {op.Syntax?.ToString().Replace("\n", " ").Replace("\r", "")}");
            }
        }
    }
}
