namespace WhackLash
{
	/// <summary>
	/// The bodies of the patches on <c>ProcessDamageResponseLocal</c>, the method where a hit that
	/// has already been decided is played on the enemy: the flinch, the knockdown, the pain meter,
	/// the health loss. Two things happen here.
	///
	/// Before the body runs, the vanilla pain meter is held down - but only once the enemy's focus
	/// meter, read as it stood before this hit, has reached the break point. Below that the
	/// vanilla meter runs untouched: the enemy earns its attack-through on the second hit as the
	/// game intends, and the hits that build the meter are landed at risk. Holding it down on every
	/// hit made a bare fist a stun-lock, which is what the break point is for. The body adds the
	/// enemy's per-hit amount and then picks the flinch from the result, so the ceiling has to
	/// leave room for that addition: <c>0.99 - perHit</c>, so the meter lands just under 1 and the
	/// enemy gets the full flinch, stays slowed, and cannot attack through. A class whose per-hit
	/// amount is itself 0.99 or more (Chuck, the bear) cannot be kept under by a prefix alone, so
	/// the postfix clamps once more as a backstop, under the same gate.
	///
	/// After the body runs, the hit is scored: the enemy's meter is read as it stood before this
	/// hit, the knockdown and ragdoll bonuses are applied off that, and only then does the hit add
	/// its weight. So the first hit of a chain earns nothing, and every hit after it earns off the
	/// hits before.
	///
	/// Two sites share these bodies, because under Undead Legacy a zombie never enters the patched
	/// <c>EntityAlive</c> method at all - see <see cref="Patches"/>. Without Undead Legacy the
	/// <c>EntityHuman</c> override calls base, so both sites fire on one hit, and the
	/// <c>EntityAlive</c> site stands down for humans to avoid counting twice.
	/// </summary>
	internal static class ResponsePatches
	{
		private const float Ceiling = 0.99f;

		/// <summary>
		/// Runs at <c>Priority.First</c> so it sits ahead of Undead Legacy's prefixes, which skip
		/// everything of lower priority. Never skips the original itself.
		/// </summary>
		internal static void Prefix(EntityAlive __instance, DamageResponse _dmResponse)
		{
			if (!Settings.Enabled || !Settings.NeutralizePainMeter || !HitClassifier.Qualifies(__instance)
				|| !IsBroken(__instance))
			{
				return;
			}

			EntityClass entityClass = EntityClass.list[__instance.entityClass];
			float perHit = entityClass.PainResistPerHit;
			if (entityClass.PainResistPerHitLowHealth > perHit)
			{
				perHit = entityClass.PainResistPerHitLowHealth;
			}
			if (perHit < 0f)
			{
				// -1 is the game's own "feels no pain"; there is nothing to hold down.
				return;
			}

			float ceiling = Ceiling - perHit;
			if (ceiling < 0f)
			{
				ceiling = 0f;
			}
			if (__instance.painResistPercent > ceiling)
			{
				__instance.painResistPercent = ceiling;
				Counters.PainClamped++;
			}
		}

		/// <summary>Runs at <c>Priority.Last</c>, after Undead Legacy's postfix has played the hit.</summary>
		internal static void Postfix(EntityAlive __instance, DamageResponse _dmResponse, bool _fromHumanSite)
		{
			if (!Settings.Enabled)
			{
				return;
			}
			if (!_fromHumanSite && __instance is EntityHuman)
			{
				return;
			}
			if (!HitClassifier.Qualifies(__instance))
			{
				if (__instance != null && __instance.IsDead())
				{
					FocusMeter.Remove(__instance.entityId);
				}
				return;
			}

			// Read before Add below, so the gate agrees with the prefix's view of this hit.
			if (Settings.NeutralizePainMeter && __instance.painResistPercent > Ceiling && IsBroken(__instance))
			{
				__instance.painResistPercent = Ceiling;
			}

			if (_dmResponse.Fatal)
			{
				FocusMeter.Remove(__instance.entityId);
				return;
			}

			DamageSource source = _dmResponse.Source;
			if (source == null || source.BuffClass != null)
			{
				return;
			}
			if (HitClassifier.Attacker(__instance, source) == null)
			{
				return;
			}

			float weight = HitClassifier.Weight(source);
			if (weight <= 0f)
			{
				Counters.HitsIgnored++;
				return;
			}

			float meter = FocusMeter.Get(__instance.entityId);
			if (meter > 0f)
			{
				AddKnockdown(__instance, _dmResponse, meter);
				TryRagdoll(__instance, _dmResponse, meter);
			}

			FocusMeter.Add(__instance.entityId, weight);
			Counters.HitsCounted++;
		}

		/// <summary>Whether the enemy's focus meter, as it stands before this hit, has reached the break point.</summary>
		private static bool IsBroken(EntityAlive _entity)
		{
			return FocusMeter.Get(_entity.entityId) >= Settings.BreakPoints;
		}

		/// <summary>
		/// The game keeps two knockdown accumulators, one for the body and one for the legs, fed by
		/// every stunning hit's damage and compared to a share of max health on the next hit. This
		/// adds a slice more, split by body part exactly as the game splits it. A cop's Special part
		/// is neither, and gets nothing, as in vanilla. Sleepers and the already-stunned get nothing
		/// either: the game computes no knockdown for them.
		/// </summary>
		private static void AddKnockdown(EntityAlive _entity, DamageResponse _dmResponse, float _meter)
		{
			if (Settings.StunPercent <= 0f || _entity.bodyDamage.CurrentStun != EnumEntityStunType.None
				|| !_dmResponse.Source.CanStun || _entity.sleepingOrWakingUp)
			{
				return;
			}

			int extra = HitClassifier.RoundHalfUp(_dmResponse.Strength * _meter * Settings.StunPercent / 100f);
			if (extra <= 0)
			{
				return;
			}

			EnumBodyPartHit part = _dmResponse.HitBodyPart;
			if ((part & (EnumBodyPartHit.Arms | EnumBodyPartHit.Torso | EnumBodyPartHit.Head)) > EnumBodyPartHit.None)
			{
				_entity.bodyDamage.StunProne += extra;
			}
			else if (part.IsLeg())
			{
				_entity.bodyDamage.StunKnee += extra;
			}
			else
			{
				return;
			}
			Counters.StunAdded += extra;
		}

		/// <summary>
		/// A knockdown the game played as the animated fall can become the physics ragdoll instead.
		/// The game itself ragdolls an enemy that is already in a stun animation, so switching
		/// mid-fall is nothing it does not do. Skipped when the game already ragdolled this hit.
		/// </summary>
		private static void TryRagdoll(EntityAlive _entity, DamageResponse _dmResponse, float _meter)
		{
			if (Settings.RagdollPercent <= 0f || _dmResponse.Stun != EnumEntityStunType.Prone
				|| _entity.emodel == null || _entity.emodel.IsRagdollActive)
			{
				return;
			}
			if (_entity.rand.RandomFloat >= _meter * Settings.RagdollPercent / 100f)
			{
				return;
			}

			_entity.DoRagdoll(_dmResponse);
			Counters.RagdollsForced++;
		}
	}
}
