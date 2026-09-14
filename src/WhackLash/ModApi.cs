namespace WhackLash
{
	public class ModApi : IModApi
	{
		public void InitMod(Mod _modInstance)
		{
			// Settings first: the patches log which way their switches are set, and that should be
			// the player's setting rather than the built-in default.
			Config.Load();
			Patches.Apply();
		}
	}
}
