using BeltRunner.Core.Units;

namespace BeltRunner.SampleWebApp.ScrapingDemo;

/// <summary>
/// Wraps demo data in a BeltRunner unit so each sample phase can expose work items to the UI.
/// </summary>
/// <typeparam name="T">The type of payload carried by the unit.</typeparam>
/// <remarks>
/// The sample application uses this type to attach friendly names to sample work items without introducing
/// additional phase-specific unit implementations.
/// </remarks>
internal sealed class DemoUnit<T> : Unit<T> {
    /// <summary>
    /// Initializes a new instance of the <see cref="DemoUnit{T}"/> class.
    /// </summary>
    /// <param name="data">The payload that represents the unit input.</param>
    /// <param name="name">The name shown for the unit in progress displays.</param>
    public DemoUnit(T data, string name) : base(data, name) {
    }
}
