namespace PriorityQueueTest
{
	// Simple replica of ProcessCore steps.  They must return true to advance to the next step
	public record Step(string Description, Func<bool> Func);
}
