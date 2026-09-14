namespace WhackLash
{
	/// <summary>
	/// What a slammed door does to a zombie the player has been working on: the game's own prone
	/// knockdown, or its break-through ragdoll when the ragdoll roll comes up. Nothing here is
	/// invented; both are reactions every zombie rig already plays, lifted from
	/// <c>EntityAlive.ProcessDamageResponse</c> and <c>EntityHuman.ExecuteDestroyBlockBehavior</c>
	/// the same way Stumblr lifts them.
	///
	/// The two Clear calls drop the move helper's idea of what it was doing, so the zombie
	/// re-plans after the fall instead of resuming its walk through the door. A knockdown deals no
	/// damage, grants no XP, sets no revenge target and triggers no rage.
	/// </summary>
	internal static class DoorKnockdown
	{
		/// <summary>Whether a knockdown can be played on this zombie right now.</summary>
		internal static bool CanApply(EntityAlive _zombie)
		{
			if (_zombie == null || _zombie.isEntityRemote || _zombie.IsDead())
			{
				return false;
			}
			if (_zombie.emodel == null || _zombie.emodel.avatarController == null || _zombie.moveHelper == null)
			{
				return false;
			}
			// A crawler (walkType 21) or a zombie already down has nothing to play.
			return _zombie.bodyDamage.CurrentStun == EnumEntityStunType.None && _zombie.walkType != 21;
		}

		internal static void Apply(EntityAlive _zombie, bool _ragdoll)
		{
			_zombie.moveHelper.ClearBlocked();
			_zombie.moveHelper.ClearTempMove();

			// Feeds the animator's random variation, so two zombies do not fall the same way.
			_zombie.emodel.avatarController.UpdateInt("RandomSelector", _zombie.rand.RandomRange(0, 64));

			if (_ragdoll)
			{
				_zombie.emodel.avatarController.BeginStun(EnumEntityStunType.StumbleBreakThroughRagdoll,
					EnumBodyPartHit.LeftUpperLeg, Utils.EnumHitDirection.None, _criticalHit: false, 1f);
				_zombie.SetStun(EnumEntityStunType.StumbleBreakThroughRagdoll);
				// No StunDuration, matching vanilla: the ragdoll ends when the body settles.
				return;
			}

			// The hit direction picks the fall: front, left or right, never back, since a fall from
			// a hit behind is the one that lands the zombie forward onto the player.
			Utils.EnumHitDirection direction;
			switch (_zombie.rand.RandomRange(0, 3))
			{
			case 0:
				direction = Utils.EnumHitDirection.Left;
				break;
			case 1:
				direction = Utils.EnumHitDirection.Right;
				break;
			default:
				direction = Utils.EnumHitDirection.Front;
				break;
			}

			_zombie.SetStun(EnumEntityStunType.Prone);
			_zombie.emodel.avatarController.BeginStun(EnumEntityStunType.Prone, EnumBodyPartHit.LeftUpperLeg,
				direction, _criticalHit: false, _zombie.rand.RandomFloat);
			_zombie.bodyDamage.StunDuration = ProneSeconds(_zombie);
		}

		/// <summary>
		/// The zombie's own knockdown range from entityclasses.xml, 0.5 to 1.8 seconds for the
		/// vanilla template, rolled the way the game rolls it. A class with no range set gets one second.
		/// </summary>
		private static float ProneSeconds(EntityAlive _zombie)
		{
			UnityEngine.Vector2 range = EntityClass.list[_zombie.entityClass].KnockdownProneStunDuration;
			if (range.y <= 0f)
			{
				return 1f;
			}
			return _zombie.rand.RandomRange(range.x, range.y);
		}
	}
}
