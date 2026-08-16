using System.Collections.Generic;
using Rocket.API;
using Rocket.Unturned.Player;

namespace SimpleKits
{
	public class CommandKitAdmin : IRocketCommand
	{
		public AllowedCaller AllowedCaller => AllowedCaller.Both;

		public string Name => "kitsadmin";

		public string Help => "Manage kits from chat (same as /kits admin)";

		public string Syntax => "<acao> <argumentos...>";

		public List<string> Aliases => new List<string>();

		public List<string> Permissions => new List<string> { "kits.admin" };

		public void Execute(IRocketPlayer caller, string[] command)
		{
			SimpleKitsPlugin plugin = SimpleKitsPlugin.Instance;
			if (plugin == null)
			{
				return;
			}

			KitAdminActions.ExecuteAdmin(caller, plugin, command);
		}
	}
}