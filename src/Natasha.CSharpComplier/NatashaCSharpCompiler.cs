﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Natasha.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Transactions;



public class NatashaCSharpCompiler : CompilerBase<CSharpCompilation, CSharpCompilationOptions>
{

    public readonly static ConcurrentDictionary<string, ReportDiagnostic> GlobalSuppressDiagnostics;

    public static void AddGlobalSupperess(string errorcode)
    {
        GlobalSuppressDiagnostics[errorcode] = ReportDiagnostic.Suppress;
    }


    public ConcurrentDictionary<string, ReportDiagnostic> SuppressDiagnostics;
    public static readonly Action<CSharpCompilationOptions, uint> SetTopLevelBinderFlagDelegate;
    public static readonly Action<CSharpCompilationOptions, bool> SetReferencesSupersedeLowerVersionsDelegate;


    public bool ReferencesSupersedeLowerVersions;
    public uint Flags;


    static NatashaCSharpCompiler()
    {

        GlobalSuppressDiagnostics = new ConcurrentDictionary<string, ReportDiagnostic>();
        AddGlobalSupperess("CS1701");
        AddGlobalSupperess("CS1702");
        AddGlobalSupperess("CS1705");
        AddGlobalSupperess("CS162");
        AddGlobalSupperess("CS219");
        AddGlobalSupperess("CS0414");
        AddGlobalSupperess("CS0616");
        AddGlobalSupperess("CS0649");
        AddGlobalSupperess("CS0693");
        AddGlobalSupperess("CS1591");
        AddGlobalSupperess("CS1998");


        // CS0162 - Unreachable code detected.
        // CS0219 - The variable 'V' is assigned but its value is never used.
        // CS0414 - The private field 'F' is assigned but its value is never used.
        // CS0616 - Member is obsolete.
        // CS0649 - Field 'F' is never assigned to, and will always have its default value.
        // CS0693 - Type parameter 'type parameter' has the same name as the type parameter from outer type 'T'
        // CS1591 - Missing XML comment for publicly visible type or member 'Type_or_Member'
        // CS1998 - This async method lacks 'await' operators and will run synchronously

        SetTopLevelBinderFlagDelegate = (Action<CSharpCompilationOptions, uint>)Delegate.CreateDelegate(
            typeof(Action<CSharpCompilationOptions, uint>), typeof(CSharpCompilationOptions)
            .GetProperty("TopLevelBinderFlags", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetMethod);

        SetReferencesSupersedeLowerVersionsDelegate = (Action<CompilationOptions, bool>)Delegate.CreateDelegate(
            typeof(Action<CompilationOptions, bool>), typeof(CompilationOptions)
            .GetProperty("ReferencesSupersedeLowerVersions", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetMethod);

    }

    public NatashaCSharpCompiler()
    {
        Flags = (uint)(CompilerBinderFlags.IgnoreAccessibility | CompilerBinderFlags.IgnoreCorLibraryDuplicatedTypes);
        ReferencesSupersedeLowerVersions = true;
        AllowUnsafe = true;
        Enum_OutputKind = OutputKind.DynamicallyLinkedLibrary;
        Enum_OptimizationLevel = OptimizationLevel.Release;
        AssemblyOutputKind = AssemblyBuildKind.Stream;
        SuppressDiagnostics = GlobalSuppressDiagnostics;
        Enum_Platform = Platform.AnyCpu;
        SuppressDiagnostics = new ConcurrentDictionary<string, ReportDiagnostic>();

    }


    public override CSharpCompilation GetCompilation()
    {
        var a = new DesktopStrongNameProvider(ImmutableArray.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "natasha.snk")));

        //CS0012  DesktopAssembly
        var compilationOptions = new CSharpCompilationOptions(
                              strongNameProvider: a,
                               concurrentBuild: true,
                               moduleName: Guid.NewGuid().ToString(),
                               reportSuppressedDiagnostics: false,
                               metadataImportOptions: MetadataImportOptions.All,
                               outputKind: Enum_OutputKind,
                               optimizationLevel: Enum_OptimizationLevel,
                               allowUnsafe: AllowUnsafe,
                               platform: Enum_Platform,
                               checkOverflow: false,
                               assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default,
                               specificDiagnosticOptions: SuppressDiagnostics);
        if (Flags != 0)
        {
            SetTopLevelBinderFlagDelegate(compilationOptions, Flags);
        }
        SetReferencesSupersedeLowerVersionsDelegate(compilationOptions, ReferencesSupersedeLowerVersions);
        OptionAction?.Invoke(compilationOptions);
        Compilation = CSharpCompilation.Create(AssemblyName, CompileTrees, Domain.GetCompileReferences(), compilationOptions);
        return Compilation;

    }

    public void SetBindingFlag(CompilerBinderFlags flags)
    {
        Flags = (uint)flags;
    }

    public override EmitResult EmitToFile(CSharpCompilation compilation)
    {

        EmitResult CompileResult;
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            CompileResult = compilation.Emit(DllPath, PdbPath);
        }
        else
        {
            CompileResult = compilation.UnixEmit(DllPath, PdbPath);
        }
        return CompileResult;

    }
    public override EmitResult EmitToStream(CSharpCompilation compilation, MemoryStream stream)
    {
        EmitResult CompileResult;
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            CompileResult = compilation.Emit(stream);
        }
        else
        {
            CompileResult = compilation.Emit(stream, options: new EmitOptions(false, DebugInformationFormat.PortablePdb));
        }
        return CompileResult;
    }
}



//
// 摘要:
//     A specific location for binding.
[Flags]
public enum CompilerBinderFlags : uint
{
    None = 0x0,
    SuppressConstraintChecks = 0x1,
    SuppressObsoleteChecks = 0x2,
    ConstructorInitializer = 0x4,
    FieldInitializer = 0x8,
    ObjectInitializerMember = 0x10,
    CollectionInitializerAddMethod = 0x20,
    AttributeArgument = 0x40,
    GenericConstraintsClause = 0x80,
    Cref = 0x100,
    CrefParameterOrReturnType = 0x200,
    //
    // 摘要:
    //     Indicates that the current context allows unsafe constructs.
    //
    // 言论：
    //     NOTE: Dev10 doesn't seem to treat attributes as being within the unsafe region.
    //     Fortunately, not following this behavior should not be a breaking change since
    //     attribute arguments have to be constants and there are no constants of unsafe
    //     types.
    UnsafeRegion = 0x400,
    //
    // 摘要:
    //     Indicates that the unsafe diagnostics are not reported in the current context,
    //     regardless of whether or not it is (part of) an unsafe region.
    SuppressUnsafeDiagnostics = 0x800,
    //
    // 摘要:
    //     Indicates that this binder is being used to answer SemanticModel questions (i.e.
    //     not for batch compilation).
    //
    // 言论：
    //     Imports touched by a binder with this flag set are not consider "used".
    SemanticModel = 0x1000,
    EarlyAttributeBinding = 0x2000,
    //
    // 摘要:
    //     Remarks, mutually exclusive with Microsoft.CodeAnalysis.CSharp.BinderFlags.UncheckedRegion.
    CheckedRegion = 0x4000,
    //
    // 摘要:
    //     Remarks, mutually exclusive with Microsoft.CodeAnalysis.CSharp.BinderFlags.CheckedRegion.
    UncheckedRegion = 0x8000,
    InLockBody = 0x10000,
    InCatchBlock = 0x20000,
    InFinallyBlock = 0x40000,
    InTryBlockOfTryCatch = 0x80000,
    InCatchFilter = 0x100000,
    InNestedFinallyBlock = 0x200000,
    IgnoreAccessibility = 0x400000,
    ParameterDefaultValue = 0x800000,
    //
    // 摘要:
    //     In the debugger, one can take the address of a managed object.
    AllowManagedAddressOf = 0x1000000,
    //
    // 摘要:
    //     In the debugger, the context is always unsafe, but one can still await.
    AllowAwaitInUnsafeContext = 0x2000000,
    //
    // 摘要:
    //     Ignore duplicate types from the cor library.
    IgnoreCorLibraryDuplicatedTypes = 0x4000000,
    //
    // 摘要:
    //     When binding imports in scripts/submissions, using aliases (other than from the
    //     current submission) are considered but other imports are not.
    InScriptUsing = 0x8000000,
    //
    // 摘要:
    //     In a file that has been included in the compilation via #load.
    InLoadedSyntaxTree = 0x10000000,
    //
    // 摘要:
    //     This is a Microsoft.CodeAnalysis.CSharp.ContextualAttributeBinder, or has Microsoft.CodeAnalysis.CSharp.ContextualAttributeBinder
    //     as its parent.
    InContextualAttributeBinder = 0x20000000,
    //
    // 摘要:
    //     Are we binding for the purpose of an Expression Evaluator
    InEEMethodBinder = 0x40000000,
    AllClearedAtExecutableCodeBoundary = 0x3F0000
}

