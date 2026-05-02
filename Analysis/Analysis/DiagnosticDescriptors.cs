using Microsoft.CodeAnalysis;

namespace BeltRunner.Analysis;

internal static class DiagnosticDescriptors {
    private const string CATEGORY = "Usage";

    internal static readonly DiagnosticDescriptor BR0001 = new(
        id: "BR0001",
        title: "Prefer aggregate phase progress tracking",
        messageFormat: "Use '{1}' instead of the low-level telemetry call '{0}' when aggregate completed-unit tracking is intended",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When a phase reports only completed-unit progress, the aggregate tracking helper is preferred over calling SetTotalUnits directly.");

    internal static readonly DiagnosticDescriptor BR0002 = new(
        id: "BR0002",
        title: "Prefer tracked unit scopes",
        messageFormat: "Use '{1}' instead of the low-level telemetry call '{0}' when tracked unit execution is intended",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When a unit is tracked through the phase progress helper, tracked unit scopes are preferred over manually starting or completing the unit.");

    internal static readonly DiagnosticDescriptor BR0003 = new(
        id: "BR0003",
        title: "Prefer PhaseBase<TFactory> for phase implementations",
        messageFormat: "Use '{0}' instead of directly implementing '{1}' for new phase implementations",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When authoring a new BeltRunner phase, the recommended high-level path is to inherit from PhaseBase<TFactory> instead of directly implementing IPhase.");
}
