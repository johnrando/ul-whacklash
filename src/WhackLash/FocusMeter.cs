using System.Collections.Generic;
using UnityEngine;

namespace WhackLash
{
	/// <summary>
	/// One meter per enemy, keyed by entity id and shared by every player hitting it. A hit adds
	/// its weight, the meter drains at a fixed rate from the moment of the last hit, and it never
	/// holds more than the cap.
	///
	/// Decay is lazy: a meter stores its value and the time it was written, and every read
	/// subtracts what has drained since. No per-tick work, nothing to keep in step, and an enemy
	/// nobody is hitting costs nothing. Entries are pruned in passing once they have drained, and
	/// dropped outright when their enemy dies.
	///
	/// Every machine keeps its own copy. Hits reach clients through the same damage packets the
	/// server processes, so the copies agree to within network latency, which is all the payoffs
	/// need. Main thread only; no locking.
	/// </summary>
	internal static class FocusMeter
	{
		private struct State
		{
			internal float Value;

			/// <summary><see cref="Time.time"/> when Value was written.</summary>
			internal float Stamp;
		}

		private static readonly Dictionary<int, State> meters = new Dictionary<int, State>();

		/// <summary>Reused by <see cref="Prune"/> so a sweep allocates nothing.</summary>
		private static readonly List<int> drained = new List<int>();

		private static int writesSincePrune;

		/// <summary>The highest value any meter reached, for <c>wl info</c>. Zeroed by <c>wl reset</c>.</summary>
		internal static float Peak;

		/// <summary>Meters still on the books, drained or not; a sweep runs every so many writes.</summary>
		internal static int LiveCount
		{
			get { return meters.Count; }
		}

		/// <summary>The meter as it stands now, zero if the enemy has none or it has drained.</summary>
		internal static float Get(int _entityId)
		{
			if (!meters.TryGetValue(_entityId, out State state))
			{
				return 0f;
			}
			float value = state.Value - (Time.time - state.Stamp) * Settings.DecayPerSecond;
			return value > 0f ? value : 0f;
		}

		/// <summary>Adds one hit's weight, capped. A nonsense result (a NaN from a bad setting) resets to zero.</summary>
		internal static void Add(int _entityId, float _weight)
		{
			float value = Get(_entityId) + _weight;
			if (value > Settings.Cap)
			{
				value = Settings.Cap;
			}
			if (float.IsNaN(value) || value < 0f)
			{
				value = 0f;
			}

			meters[_entityId] = new State { Value = value, Stamp = Time.time };
			if (value > Peak)
			{
				Peak = value;
			}

			if (++writesSincePrune >= 64 || meters.Count > 256)
			{
				Prune();
			}
		}

		/// <summary>The enemy is dead; whatever it had built no longer matters.</summary>
		internal static void Remove(int _entityId)
		{
			meters.Remove(_entityId);
		}

		private static void Prune()
		{
			writesSincePrune = 0;
			drained.Clear();
			foreach (KeyValuePair<int, State> entry in meters)
			{
				if (Get(entry.Key) <= 0f)
				{
					drained.Add(entry.Key);
				}
			}
			for (int i = 0; i < drained.Count; i++)
			{
				meters.Remove(drained[i]);
			}
			drained.Clear();
		}
	}
}
