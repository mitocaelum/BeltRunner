namespace BeltRunner.Core.TEST.Testing;

internal static class TestNarrative {
    public static void Observe(string observation) {
        HumanReadableTestLog.Observe(TestContext.CurrentContext.Test.ID, observation);
    }

    public static void ObserveMany(params string[] observations) {
        foreach( string observation in observations ) {
            Observe(observation);
        }
    }
}
