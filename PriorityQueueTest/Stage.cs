using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Timer = System.Timers.Timer;

namespace PriorityQueueTest
{
	public class Stage : INotifyPropertyChanged
	{
		private const int MAX_LOGS = 30;

		#region Properties

		private string name;
		public string Name
		{
			get => name;
			set => Set(ref name, value);
		}

		private int blockedCount = 0;
		public int BlockedCount
		{
			get => blockedCount;
			set => Set(ref blockedCount, value);
		}

		private int currentBlockedCycles;
		public int CurrentBlockedCycles
		{
			get => currentBlockedCycles;
			set => Set(ref currentBlockedCycles, value);
		}

		private int maxBlockedCycles = 0;
		public int MaxBlockedCycles
		{
			get => maxBlockedCycles;
			set => Set(ref maxBlockedCycles, value);
		}

		private int moveCount = 0;
		public int MoveCount
		{
			get => moveCount;
			set => Set(ref moveCount, value);
		}

		private bool isMoving = false;
		public bool IsMoving
		{
			get => isMoving;
			set => Set(ref isMoving, value);
		}

		private bool isBlocked = false;
		public bool IsBlocked
		{
			get => isBlocked;
			set => Set(ref isBlocked, value);
		}

		public readonly ObservableCollection<string> Log = new();

		#endregion Properties

		#region Fields
		private readonly Random random = new((int)DateTime.Now.Ticks);
		private Timer? timer;
		private readonly List<Step> steps = new();
		private int stepIndex = 0;
		private bool waitStarted = false;
		private int waitCycleCount;
		#endregion

		public Action? CheckCollisions = null;

		public event PropertyChangedEventHandler? PropertyChanged;

		private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
		{
			if (EqualityComparer<T>.Default.Equals(field, value)) return;

			field = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		public Stage(string name)
		{
			Name = name;
			BuildSteps();
			CreateTimer();
		}

		private void BuildSteps()
		{
			steps.Add(new("Wait a random number of cycles", () => Wait(random.Next(5, 10))));
			steps.Add(new("Declare our intent to move", DeclareIntentToMove));
			steps.Add(new("Move the stage", StartMovement));
			steps.Add(new("Wait for movement to complete", () => Wait(4)));
			steps.Add(new("Indicate movement finished", StopMovement));

			AddLog("BuildSteps completed");
		}

		private void CreateTimer()
		{
			timer = new() { Interval = 250 };
			timer.Elapsed += OnTimerElapsed;
			timer.Start();

			AddLog("Timer created");
		}

		private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
		{
			if (CheckCollisions is not null) CheckCollisions();
			DoNextStep();
		}

		// Add a log message to the collection, but restrict the size to MAX_LOG entries
		private void AddLog(string msg)
		{
			Log.Add(msg);
			if (Log.Count > MAX_LOGS) Log.RemoveAt(0);
			// Without this the log view on screen only updates when something else changes
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Log)));
		}

		private void UpdateMetrics(bool isMoving, bool isBlocked, int moveCount, int currentBlockedCycles)
		{
			IsMoving = isMoving;
			IsBlocked = isBlocked;
			MoveCount = moveCount;
			CurrentBlockedCycles = currentBlockedCycles;
		}

		// Calls the current Func and moves to the next if the result is true
		private void DoNextStep()
		{
			bool result = steps[stepIndex].Func();
			AddLog($"{steps[stepIndex].Description} => Step succeeded? {result}");
			if (result)
			{
				stepIndex = ((stepIndex + 1) % steps.Count);
			}
		}

		// Waits the specified number of cycles before advancing to the next step
		private bool Wait(int cycleCount)
		{
			bool waitFinished = false;

			// If we haven't started this wait call, then initialize it
			if (!waitStarted)
			{
				waitStarted = true;
				waitCycleCount = cycleCount;
			}

			// Decrease the number of cycles left to wait
			waitCycleCount--;

			// If we've run out of cycles to wait, flag this command as complete
			if (waitCycleCount <= 0)
			{
				waitStarted = false;
				waitFinished = true;
			}

			return waitFinished;
		}

		#region Collision avoidance section

		// TODO: This is still not working.  Currently it's allowing multiple stages to run at once.  It's fine if both pickheads run at once, but the t-bar and a pickhead cannot run at the same time
		private bool DeclareIntentToMove()
		{
			// Declare this stage's intent to move on the next cycle
			PriorityQueue.Instance.DeclareIntentToMove(Name);

			// For the current move attempt, start at zero cycles for our time spent blocked by other threads
			CurrentBlockedCycles = 0;

			return true;
		}

		private bool StartMovement()
		{
			// If the TBar hasn't declared its intention to move OR the current stage IS the T-Bar, then continue with starting the movement if nothing is already moving
			if ((!PriorityQueue.Instance.TBarIntendsToMove() || Name.Equals("TBar")) && string.IsNullOrEmpty(Globals.CurrentMovingStage))
			{
				lock (Globals.MovementLockObject)
				{
					// Only assign this as the current moving stage if nothing else has begun movement
					Globals.CurrentMovingStage ??= Name;

					UpdateMetrics(true, false, MoveCount + 1, 0);
					return true;
				}
			}
			// Otherwise, if the T-Bar has specified that it intends to move or a stage is already moving, then this stage should wait and try again
			else
			{
				IsBlocked = true;
				if (CurrentBlockedCycles == 0)
				{
					BlockedCount++;
				}
				CurrentBlockedCycles++;

				if (CurrentBlockedCycles > MaxBlockedCycles)
					MaxBlockedCycles = CurrentBlockedCycles;

				return false;
			}
		}

		private bool StopMovement()
		{
			lock (Globals.MovementLockObject)
			{
				if (Name.Equals(Globals.CurrentMovingStage))
				{
					PriorityQueue.Instance.DeclareMovementFinishing(Name);
					Globals.CurrentMovingStage = null;
					UpdateMetrics(false, false, MoveCount, 0);
					return true;
				}
				else
					return false;
			}
		}

		// TODO: Add something that reports when collisions would likely occur.  Anytime the TBar is in motion and either Pickhead is in motion counts as a collision.

		#endregion Collision avoidance section
	}
}
