using System.Collections.Concurrent;

namespace PriorityQueueTest
{
	public class PriorityQueue
	{
		public static readonly PriorityQueue instance = new();
		public static PriorityQueue Instance
		{
			get => instance;
		}

		// Doesn't work the way I need it to - removing from this draws something at random, I need the matching key removed
		//private ConcurrentBag<string> collection = new();

		private readonly ConcurrentDictionary<string, int> collection = new();

		private PriorityQueue() { }

		public void DeclareIntentToMove(string name) => collection[name] = 0;

		public string DeclareMovementFinishing(string name)
		{
			if (collection.ContainsKey(name))
			{
				collection.Remove(name, out int _);
				return name;
			}
			return "Key not found";
		}

		public bool TBarIntendsToMove() => collection.ContainsKey("TBar");

	}
}
