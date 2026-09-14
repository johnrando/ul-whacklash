namespace WhackLash
{
	/// <summary>
	/// The one thing this mod exposes to another: DoorSlammer has just slammed a door on a zombie,
	/// so if the player has been working on that zombie, give the door a chance to floor it.
	///
	/// PUBLISHED CONTRACT. DoorSlammer binds <see cref="TryProc"/> and <see cref="SetFlavor"/> by
	/// reflection - it cannot reference this assembly, because either mod has to work with the other
	/// absent - so those two signatures are the whole interface. Changing one silently switches that
	/// half of the interaction off. Game types and strings only. The shape is identical to
	/// Stumblr's and FletchWounds' <c>FlavorInterop</c>, which is how DoorSlammer finds all three
	/// with one binder. The four readout methods at the bottom are a second, read-only contract
	/// for PainMeter's HUD.
	/// </summary>
	public static class FlavorInterop
	{
		/// <summary>
		/// A slam has caught a zombie. Read its meter as it stood before the slam, roll the door
		/// chance off that, then count the slam as one melee hit. The slam's own damage has already
		/// landed; it carries no weapon and no attacker, so the hit patches ignored it, and this is
		/// the only place a slam feeds the meter.
		/// </summary>
		/// <param name="_zombie">The zombie the slam caught. Alive, and known to be a zombie.</param>
		/// <param name="_attacker">The player who closed the door. Unused: a knockdown is credited to nobody.</param>
		/// <param name="_caller">The calling mod's label, e.g. "DoorSlammer". Gated on this side's
		/// switch for it; a label this build does not know is let through until switched off.</param>
		/// <returns>Whether the zombie went down.</returns>
		public static bool TryProc(EntityAlive _zombie, EntityAlive _attacker, string _caller)
		{
			if (!Settings.Enabled || !FlavorSwitches.IsOn(_caller))
			{
				return false;
			}
			if (!HitClassifier.Qualifies(_zombie) || _zombie.isEntityRemote)
			{
				return false;
			}

			float meter = FocusMeter.Get(_zombie.entityId);
			Counters.DoorProcs++;

			bool floored = false;
			if (Settings.DoorPercent > 0f && meter >= Settings.DoorMinPoints && meter > 0f
				&& DoorKnockdown.CanApply(_zombie)
				&& _zombie.rand.RandomFloat < meter * Settings.DoorPercent / 100f)
			{
				bool ragdoll = Settings.RagdollPercent > 0f
					&& _zombie.rand.RandomFloat < meter * Settings.RagdollPercent / 100f;
				DoorKnockdown.Apply(_zombie, ragdoll);
				Counters.DoorKnockdowns++;
				floored = true;
			}

			FocusMeter.Add(_zombie.entityId, Settings.WeightMelee);
			Counters.HitsCounted++;
			return floored;
		}

		/// <summary>
		/// PUBLISHED CONTRACT, like <see cref="TryProc"/>. A partner calls this when the player
		/// toggles their switch for this mod over there. Sets this side's switch for that partner
		/// and saves. Deliberately does not push back: whoever the player typed at owns the mirror,
		/// which is what stops two mods calling each other forever.
		/// </summary>
		/// <param name="_partner">The calling mod's label, e.g. "DoorSlammer".</param>
		public static void SetFlavor(string _partner, bool _on)
		{
			FlavorSwitches.Set(_partner, _on);
			Config.Save();
		}

		// ---- Readout, PUBLISHED CONTRACT like the two above. PainMeter binds these four by
		// reflection to draw the focus meter under its pain bar. Primitives only, so a
		// Delegate.CreateDelegate on the far side is all it takes. Each reads live: 'wl' changes
		// the break point and cap at runtime, and the caller draws every frame.

		/// <summary>The enemy's focus meter as it stands now, 0 when the mod is off or it has none.</summary>
		public static float FocusPoints(int _entityId)
		{
			return Settings.Enabled ? FocusMeter.Get(_entityId) : 0f;
		}

		/// <summary>Points at which the pain clamp engages; below zero when the clamp is switched off.</summary>
		public static float FocusBreak()
		{
			return Settings.Enabled && Settings.NeutralizePainMeter ? Settings.BreakPoints : -1f;
		}

		/// <summary>The most the meter holds.</summary>
		public static float FocusCap()
		{
			return Settings.Cap;
		}

		/// <summary>The damage bonus the next hit on this enemy gets, in percent.</summary>
		public static float DamageBonusPercent(int _entityId)
		{
			return Settings.Enabled ? FocusMeter.Get(_entityId) * Settings.DamagePercent : 0f;
		}

		/// <summary>The <c>wl door</c> menu line.</summary>
		internal static string DoorStatus()
		{
			if (Settings.DoorPercent <= 0f)
			{
				return "off - a slammed door does not knock down";
			}

			return "a slammed door floors a zombie " + Config.Number(Settings.DoorPercent)
				+ "% per point, from " + Config.Number(Settings.DoorMinPoints) + " point"
				+ (Settings.DoorMinPoints == 1f ? "" : "s") + " up"
				+ (FlavorPartners.DoorSlammer.Found ? string.Empty : " (needs DoorSlammer)");
		}
	}
}
