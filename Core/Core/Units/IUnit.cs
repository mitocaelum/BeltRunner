using System;
using System.Collections.Generic;
using BeltRunner.Core.Phase;

namespace BeltRunner.Core.Units;

/// <summary>
/// Represents a single processing target that flows through a plan.
/// </summary>
/// <remarks>
/// <para>
/// A unit is the smallest framework-tracked item that moves through the phases of a run.
/// Typical examples include a file, a record, a request, or any other independently handled target.
/// </para>
/// <para>
/// BeltRunner tracks the lifecycle of each unit through framework-level state such as
/// <see cref="Status"/> and <see cref="CurrentPhaseKey"/>.
/// Domain-specific state is intentionally not prescribed by this contract.
/// If an application needs additional status, progress, or metadata, it should extend this model
/// in its own implementation.
/// </para>
/// <para>
/// Implementations are expected to provide stable values for identity and metadata during a run.
/// In particular, <see cref="Id"/> should uniquely identify the unit instance within the scope of
/// a run, and <see cref="Name"/> should remain suitable for diagnostics and user-facing reporting.
/// </para>
/// </remarks>
public interface IUnit {
    /// <summary>
    /// Gets the unique identifier of the unit.
    /// </summary>
    /// <value>
    /// A non-empty identifier that uniquely distinguishes this unit from other units in the same run.
    /// </value>
    /// <remarks>
    /// <para>
    /// This identifier is used for framework-level tracking, correlation, and reporting.
    /// </para>
    /// <para>
    /// The identifier should be stable for the lifetime of the unit instance.
    /// </para>
    /// </remarks>
    Guid Id { get; }

    /// <summary>
    /// Gets the human-readable name of the unit.
    /// </summary>
    /// <value>
    /// A display-friendly name used for diagnostics, logs, and UI presentation.
    /// </value>
    /// <remarks>
    /// <para>
    /// This value does not need to be globally unique.
    /// </para>
    /// <para>
    /// Choose a name that helps operators and developers quickly understand what the unit represents,
    /// such as a file name, document title, or logical key.
    /// </para>
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Gets the current framework-managed status of the unit.
    /// </summary>
    /// <value>
    /// The current <see cref="UnitStatus"/> assigned by BeltRunner.
    /// </value>
    /// <remarks>
    /// <para>
    /// This status is intended only for framework-level lifecycle tracking.
    /// It describes where the unit is in the processing lifecycle from the perspective of BeltRunner.
    /// </para>
    /// <para>
    /// If the application requires richer or domain-specific states
    /// (for example, validation categories, business approval states, or custom progress markers),
    /// those states should be defined outside this interface by the application.
    /// </para>
    /// </remarks>
    UnitStatus Status { get; }

    /// <summary>
    /// Gets the key of the phase currently associated with the unit.
    /// </summary>
    /// <value>
    /// The key of the current phase, or <see langword="null"/> if the unit is not currently associated with any phase.
    /// </value>
    /// <remarks>
    /// <para>
    /// This property indicates which phase most recently claimed, processed, or otherwise associated itself with the unit.
    /// </para>
    /// <para>
    /// The value is typically updated by the framework as the unit progresses through the run.
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <see langword="null"/> if the unit has not yet entered a phase, or if it is intentionally not associated with one.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// The last associated key may remain available after processing completes, which allows diagnostic and reporting scenarios
    /// to identify the most recent phase that handled the unit.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// This property is informational. It should not be treated as a substitute for full event history.
    /// </para>
    /// </remarks>
    PhaseKey? CurrentPhaseKey { get; }

    /// <summary>
    /// Gets the tags assigned to the unit.
    /// </summary>
    /// <value>
    /// A read-only collection of zero or more <see cref="UnitTag"/> values associated with the unit.
    /// </value>
    /// <remarks>
    /// <para>
    /// Tags provide lightweight classification metadata for filtering, grouping, routing, or diagnostics.
    /// </para>
    /// <para>
    /// Examples include priority labels, category markers, source identifiers, or application-defined flags.
    /// </para>
    /// <para>
    /// The framework does not prescribe tag semantics. Their meaning is defined by the application.
    /// </para>
    /// </remarks>
    IReadOnlyCollection<UnitTag> Tags { get; }
}

/// <summary>
/// Represents a unit that carries a strongly typed payload.
/// </summary>
/// <typeparam name="T">
/// The type of payload carried by the unit.
/// </typeparam>
public interface IUnit<T> : IUnit {
    /// <summary>
    /// Gets the payload carried by the unit.
    /// </summary>
    /// <value>
    /// The application-defined payload associated with this unit.
    /// </value>
    /// <remarks>
    /// <para>
    /// This is the primary data object that phases consume, inspect, or transform while processing the unit.
    /// </para>
    /// <para>
    /// The framework does not impose mutability rules on the payload type.
    /// However, immutable payloads or carefully controlled mutation are generally easier to reason about
    /// in multi-phase processing pipelines.
    /// </para>
    /// </remarks>
    T Data { get; }
}
