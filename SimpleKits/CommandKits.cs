using System;
using System.Collections.Generic;
using System.Linq;
using Rocket.API;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;

namespace SimpleKits
{
	public class CommandKits : IRocketCommand
	{
		public AllowedCaller AllowedCaller => AllowedCaller.Both;

		public string Name => "kits";

		public string Help => "Opens the kits UI";

		public string Syntax => "[help | admin <acao>]";

		public List<string> Aliases => new List<string>();

		public List<string> Permissions => new List<string>();

		public void Execute(IRocketPlayer caller, string[] command)
		{
			SimpleKitsPlugin plugin = SimpleKitsPlugin.Instance;
			if (plugin == null)
			{
				return;
			}

			if (command.Length > 0 && command[0].Equals("help", StringComparison.OrdinalIgnoreCase))
			{
				ShowHelp(caller, plugin);
				return;
			}

			if (command.Length > 0 && command[0].Equals("admin", StringComparison.OrdinalIgnoreCase))
			{
				KitAdminActions.ExecuteAdmin(caller, plugin, command.Skip(1).ToArray());
				return;
			}

			if (plugin.Configuration.Instance.Kits.Count == 0)
			{
				UnturnedChat.Say(caller, plugin.Translate("kits_vazio"));
				return;
			}

			if (caller is UnturnedPlayer uplayer)
			{
				Player player = uplayer.Player;
				if (plugin.Sessions.Any(s => s.Owner == player))
				{
					return;
				}

				plugin.Sessions.Add(new KitUiSession(plugin, player, plugin.PlayerHasBypass(player)));
			}
			else
			{
				List<string> names = new List<string>();
				foreach (Kit kit in plugin.Configuration.Instance.Kits)
				{
					if (!string.IsNullOrEmpty(kit.Name))
					{
						names.Add(kit.Name);
					}
				}
				UnturnedChat.Say(caller, plugin.Translate("kits_lista", string.Join(", ", names.ToArray())));
			}
		}

		private static void ShowHelp(IRocketPlayer caller, SimpleKitsPlugin plugin)
		{
			UnturnedChat.Say(caller, "/kits - abre a interface dos kits (admin ve botoes de EDITAR/APAGAR/+ NOVO)");
			if (caller is not UnturnedPlayer || caller.HasPermission("kits.usar"))
			{
				UnturnedChat.Say(caller, "/kit <nome> - recebe um kit");
			}
			if (KitAdminActions.IsAdmin(caller, plugin))
			{
				KitAdminActions.ShowAdminHelp(caller);
				UnturnedChat.Say(caller, plugin.Translate("kits_admin_alias"));
			}
		}
	}
}