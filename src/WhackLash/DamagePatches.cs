namespace WhackLash
{
	/// <summary>
	/// The two patches that act before a hit is decided, on the machine deciding it. A client
	/// works out its own hits - damage, knockdown, dismember - and sends the result to the server,
	/// so these run wherever the swing happened; the meter they read is that machine's copy.
	/// </summary>
	internal static class DamagePatches
	{
		/// <summary>
		/// Prefix on <c>EntityAlive.DamageEntity</c>. The strength argument is the damage the hit is
		/// about to do, before armour; scaling it here scales everything downstream of it, the
		/// knockdown build-up and the dismember damage share included. The parameter name has to
		/// match the game's own, which is how Harmony finds it.
		/// </summary>
		internal static void DamagePrefix(EntityAlive __instance, DamageSource _damageSource, ref int _strength)
		{
			if (!Settings.Enabled || Settings.DamagePercent <= 0f || _strength <= 0)
			{
				return;
			}

			float meter = MeterFor(__instance, _damageSource);
			if (meter <= 0f)
			{
				return;
			}

			int boosted = HitClassifier.RoundHalfUp(_strength * (1f + meter * Settings.DamagePercent / 100f));
			if (boosted <= _strength)
			{
				return;
			}
			Counters.BonusDamage += boosted - _strength;
			_strength = boosted;
		}

		/// <summary>
		/// Prefix on <c>EntityAlive.CheckDismember</c>. The chance the game rolls is the weapon's
		/// dismember chance times the hit's share of max health times the body part's multiplier;
		/// this raises the first factor. The damage source is made fresh for every hit and the game
		/// rewrites this field on it itself for a killing blow, so changing it here reaches nothing
		/// else. Note a head dismember is a kill.
		/// </summary>
		internal static void DismemberPrefix(EntityAlive __instance, ref DamageResponse _dmResponse)
		{
			if (!Settings.Enabled || Settings.DismemberPercent <= 0f)
			{
				return;
			}

			DamageSource source = _dmResponse.Source;
			if (source == null || source.DismemberChance <= 0f)
			{
				return;
			}

			float meter = MeterFor(__instance, source);
			if (meter <= 0f)
			{
				return;
			}

			source.DismemberChance *= 1f + meter * Settings.DismemberPercent / 100f;
			Counters.DismemberBoosted++;
		}

		/// <summary>The target's meter, or zero when this hit is not one that earns anything.</summary>
		private static float MeterFor(EntityAlive _target, DamageSource _source)
		{
			if (!HitClassifier.Qualifies(_target) || _source == null || _source.BuffClass != null)
			{
				return 0f;
			}
			if (HitClassifier.Attacker(_target, _source) == null)
			{
				return 0f;
			}
			if (HitClassifier.Weight(_source) <= 0f)
			{
				return 0f;
			}
			return FocusMeter.Get(_target.entityId);
		}
	}
}
