namespace BeltRunner.Core.Phase;

/// <summary>
/// Represents a phase factory that also acts as the typed metadata surface for the phases it creates.
/// </summary>
/// <typeparam name="TFactory">
/// The concrete factory type exposed through <see cref="IPhaseContext{TFactory}.Factory"/>.
/// </typeparam>
/// <remarks>
/// <para>
/// This interface is the recommended authoring surface for new phases that want to keep artifact key
/// declarations in the factory while exposing those keys to the phase through the typed runtime context.
/// </para>
/// <para>
/// The factory still participates in the regular <see cref="IPhaseFactory"/> plan-time contract and can
/// be consumed anywhere a non-generic phase factory is expected.
/// </para>
/// </remarks>
public interface IPhaseFactory<out TFactory> : IPhaseFactory where TFactory : IPhaseFactory {
}
