namespace WhackLash
{
	/// <summary>
	/// The two questions every patch asks: does this enemy take part, and how much does this hit
	/// count for.
	///
	/// What landed a hit is read off the damage source's item, which is the held weapon for a
	/// melee swing or a gun, the bow or crossbow for an arrow or bolt, the weapon itself for
	/// something thrown, and the ammo for a turret. Traps, vehicles and blocks carry an empty item
	/// and a bleed or a burn carries a buff instead, so all of those weigh nothing. A thrown spear
	/// carries the same item as a held one, so it counts as melee.
	/// </summary>
	internal static class HitClassifier
	{
		/// <summary>Checked first: the junk turret is tagged ranged as well, and drones likewise.</summary>
		private static readonly FastTags<TagGroup.Global> tagsExcluded =
			FastTags<TagGroup.Global>.Parse("turretRanged,turretMelee,drone");

		private static readonly FastTags<TagGroup.Global> tagsMelee =
			FastTags<TagGroup.Global>.Parse("melee");

		/// <summary>The vanilla crossbow is tagged crossbow without archery; grenades and molotovs are thrown.</summary>
		private static readonly FastTags<TagGroup.Global> tagsArchery =
			FastTags<TagGroup.Global>.Parse("archery,bow,crossbow,throwable,thrownWeapon,grenade,molotov");

		private static readonly FastTags<TagGroup.Global> tagsGun =
			FastTags<TagGroup.Global>.Parse("gun,launcher,explosive,explosivesSkill");

		/// <summary>Meter points this hit is worth, zero when it is not one that counts.</summary>
		internal static float Weight(DamageSource _source)
		{
			if (_source == null || _source.BuffClass != null)
			{
				return 0f;
			}

			ItemValue item = _source.AttackingItem;
			ItemClass itemClass = item != null ? item.ItemClass : null;
			if (itemClass == null)
			{
				return 0f;
			}

			FastTags<TagGroup.Global> tags = itemClass.ItemTags;
			if (tags.Test_AnySet(tagsExcluded))
			{
				return 0f;
			}
			if (tags.Test_AnySet(tagsMelee))
			{
				return Settings.WeightMelee;
			}
			if (tags.Test_AnySet(tagsArchery))
			{
				return Settings.WeightArchery;
			}
			if (tags.Test_AnySet(tagsGun))
			{
				return Settings.WeightGun;
			}
			return 0f;
		}

		/// <summary>
		/// Whether this enemy takes part at all, by its own switch. The entityFlags bits come from
		/// entityclasses.xml, so unlike <c>is EntityZombie</c> they cover zombie dogs and vultures
		/// (flagged animal and zombie both) and Undead Legacy's own zombies. Bandits ride with
		/// zombies. A plain animal counts only when it is a hostile one.
		/// </summary>
		internal static bool Qualifies(EntityAlive _entity)
		{
			if (_entity == null || _entity is EntityPlayer || _entity.IsDead())
			{
				return false;
			}

			EntityFlags flags = _entity.entityFlags;
			if ((flags & (EntityFlags.Zombie | EntityFlags.Bandit)) != EntityFlags.None)
			{
				return Settings.Zombies;
			}
			if ((flags & EntityFlags.Animal) != EntityFlags.None)
			{
				return Settings.Animals && _entity is EntityEnemyAnimal;
			}
			return false;
		}

		/// <summary>The attacking player, or null when the hit was not a player's.</summary>
		internal static EntityPlayer Attacker(EntityAlive _target, DamageSource _source)
		{
			if (_target == null || _target.world == null || _source == null)
			{
				return null;
			}
			return _target.world.GetEntity(_source.getEntityId()) as EntityPlayer;
		}

		/// <summary>
		/// Half rounds up. <c>Mathf.RoundToInt</c> rounds half to even, which would swallow small
		/// bonuses on alternate hits.
		/// </summary>
		internal static int RoundHalfUp(float _value)
		{
			return (int)(_value + 0.5f);
		}
	}
}
