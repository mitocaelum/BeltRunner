using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace BeltRunner.Analysis;

/// <summary>
/// Reports low-level BeltRunner authoring patterns when higher-level APIs exist.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TelemetryTrackingAnalyzer : DiagnosticAnalyzer {
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(DiagnosticDescriptors.BR0001, DiagnosticDescriptors.BR0002, DiagnosticDescriptors.BR0003);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(InitializeForCompilation);
    }

    private static void InitializeForCompilation(CompilationStartAnalysisContext context) {
        INamedTypeSymbol? telemetryType = context.Compilation.GetTypeByMetadataName("BeltRunner.Core.Phase.IPhaseTelemetry");
        INamedTypeSymbol? extensionsType = context.Compilation.GetTypeByMetadataName("BeltRunner.Core.Phase.PhaseTelemetryExtensions");
        INamedTypeSymbol? phaseType = context.Compilation.GetTypeByMetadataName("BeltRunner.Core.Phase.IPhase");
        INamedTypeSymbol? phaseBaseType = context.Compilation.GetTypeByMetadataName("BeltRunner.Core.Phase.PhaseBase`1");
        INamedTypeSymbol? unitStatusType = context.Compilation.GetTypeByMetadataName("BeltRunner.Core.Units.UnitStatus");

        if( telemetryType is null || extensionsType is null || phaseType is null || phaseBaseType is null || unitStatusType is null ) {
            return;
        }

        IMethodSymbol? setTotalUnits = telemetryType.GetMembers("SetTotalUnits").OfType<IMethodSymbol>().SingleOrDefault();
        IMethodSymbol? setUnitStatus = telemetryType.GetMembers("SetUnitStatus").OfType<IMethodSymbol>().SingleOrDefault();
        IMethodSymbol? startUnit = extensionsType.GetMembers("StartUnit").OfType<IMethodSymbol>().FirstOrDefault();
        IMethodSymbol? completeUnit = extensionsType.GetMembers("CompleteUnit").OfType<IMethodSymbol>().FirstOrDefault();
        IFieldSymbol? runningStatus = unitStatusType.GetMembers("Running").OfType<IFieldSymbol>().SingleOrDefault();

        if( setTotalUnits is null || setUnitStatus is null || startUnit is null || completeUnit is null || runningStatus is null ) {
            return;
        }

        context.RegisterOperationAction(
            operationContext => AnalyzeInvocation(operationContext, setTotalUnits, setUnitStatus, startUnit, completeUnit, runningStatus),
            OperationKind.Invocation);
        context.RegisterSymbolAction(
            symbolContext => AnalyzeNamedType(symbolContext, phaseType, phaseBaseType),
            SymbolKind.NamedType);
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        IMethodSymbol setTotalUnits,
        IMethodSymbol setUnitStatus,
        IMethodSymbol startUnit,
        IMethodSymbol completeUnit,
        IFieldSymbol runningStatus) {

        if( context.Operation is not IInvocationOperation invocation ) {
            return;
        }

        IMethodSymbol target = invocation.TargetMethod.OriginalDefinition;

        if( SymbolEqualityComparer.Default.Equals(target, setTotalUnits) ) {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.BR0001,
                invocation.Syntax.GetLocation(),
                invocation.TargetMethod.Name,
                "BeginPhaseProgressTracking(...)"));
            return;
        }

        if( SymbolEqualityComparer.Default.Equals(target, startUnit) || SymbolEqualityComparer.Default.Equals(target, completeUnit) ) {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.BR0002,
                invocation.Syntax.GetLocation(),
                invocation.TargetMethod.Name,
                "BeginPhaseProgressTracking(...).BeginUnit(...)"));
            return;
        }

        if( SymbolEqualityComparer.Default.Equals(target, setUnitStatus) &&
            invocation.Arguments.Length >= 2 &&
            invocation.Arguments[1].Value is IFieldReferenceOperation fieldReference &&
            SymbolEqualityComparer.Default.Equals(fieldReference.Field, runningStatus) ) {

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.BR0002,
                invocation.Syntax.GetLocation(),
                invocation.TargetMethod.Name,
                "BeginPhaseProgressTracking(...).BeginUnit(...)"));
        }
    }

    private static void AnalyzeNamedType(
        SymbolAnalysisContext context,
        INamedTypeSymbol phaseType,
        INamedTypeSymbol phaseBaseType) {

        if( context.Symbol is not INamedTypeSymbol typeSymbol || typeSymbol.TypeKind != TypeKind.Class ) {
            return;
        }

        if( !typeSymbol.AllInterfaces.Any(interfaceSymbol => SymbolEqualityComparer.Default.Equals(interfaceSymbol, phaseType)) ) {
            return;
        }

        if( DerivesFromPhaseBase(typeSymbol, phaseBaseType) ) {
            return;
        }

        Location? location = typeSymbol.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(context.CancellationToken))
            .OfType<TypeDeclarationSyntax>()
            .Select(typeDeclaration => typeDeclaration.Identifier.GetLocation())
            .FirstOrDefault();

        if( location is null ) {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.BR0003,
            location,
            "PhaseBase<TFactory>",
            "IPhase"));
    }

    private static bool DerivesFromPhaseBase(INamedTypeSymbol typeSymbol, INamedTypeSymbol phaseBaseType) {
        for( INamedTypeSymbol? current = typeSymbol.BaseType; current is not null; current = current.BaseType ) {
            if( current.IsGenericType &&
                SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, phaseBaseType) ) {

                return true;
            }
        }

        return false;
    }
}
