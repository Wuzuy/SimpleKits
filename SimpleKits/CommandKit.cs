using System.Collections.Generic;
using Rocket.API;
using Rocket.Core.Plugins;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace SimpleKits
{
	public class CommandKit : IRocketCommand
	{
		public AllowedCaller AllowedCaller => AllowedCaller.Player;

		public string Name => "kit";

		public string Help => "Claims a configured kit";

		public string Syntax => "<name>";

		public List<string> Aliases => new List<string>();

		public List<string> Permissions => new List<string> { "kits.usar" };

		public void Execute(IRocketPlayer caller, string[] command)
		{
			SimpleKitsPlugin plugin = SimpleKitsPlugin.Instance;
			if (plugin == null)
			{
				return;
			}

			UnturnedPlayer uplayer = (UnturnedPlayer)caller;

			if (command.Length == 0)
			{
				UnturnedChat.Say(caller, plugin.Translate("kit_uso"));
				return;
			}

			Kit kit = plugin.FindKit(command[0]);
			if (kit == null)
			{
				UnturnedChat.Say(caller, plugin.Translate("kit_nao_existe", command[0]));
				return;
			}

			plugin.TryClaimKit(uplayer.Player, kit);
		}
	}
}
