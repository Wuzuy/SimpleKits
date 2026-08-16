using System;
using System.Collections.Generic;
using System.Linq;
using Rocket.API;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;

namespace SimpleKits
{
	public static class KitAdminActions
	{
		public static bool IsAdmin(IRocketPlayer caller, SimpleKitsPlugin plugin)
		{
			return caller is not UnturnedPlayer || plugin.PlayerHasBypass(((UnturnedPlayer)caller).Player);
		}

		public static void ExecuteAdmin(IRocketPlayer caller, SimpleKitsPlugin plugin, string[] args)
		{
			if (!IsAdmin(caller, plugin))
			{
				UnturnedChat.Say(caller, plugin.Translate("kits_admin_sem_perm", plugin.Configuration.Instance.BypassPermission));
				return;
			}

			if (args.Length == 0)
			{
				ShowAdminHelp(caller);
				return;
			}

			switch (args[0].ToLower())
			{
				case "add":
					AddKit(caller, plugin, args);
					break;
				case "remove":
					RemoveKit(caller, plugin, args);
					break;
				case "additem":
					AddItem(caller, plugin, args);
					break;
				case "removeitem":
					RemoveItem(caller, plugin, args);
					break;
				case "set":
					SetField(caller, plugin, args);
					break;
				case "list":
					ListKits(caller, plugin);
					break;
				default:
					UnturnedChat.Say(caller, plugin.Translate("kits_admin_acao_invalida", args[0]));
					ShowAdminHelp(caller);
					break;
			}
		}

		public static void ShowAdminHelp(IRocketPlayer caller)
		{
			UnturnedChat.Say(caller, "Uso: /kits admin <acao> (ou /kitsadmin <acao>)");
			UnturnedChat.Say(caller, "  add <nome> [cooldown] [prioridade] [permissao]");
			UnturnedChat.Say(caller, "  remove <nome>");
			UnturnedChat.Say(caller, "  additem <nome> <itemID> [quantidade]");
			UnturnedChat.Say(caller, "  removeitem <nome> <itemID>");
			UnturnedChat.Say(caller, "  set <nome> <cooldown|prioridade|permissao|nome|icon> <valor>");
			UnturnedChat.Say(caller, "  list");
		}

		private static void AddKit(IRocketPlayer caller, SimpleKitsPlugin plugin, string[] args)
		{
			if (args.Length < 2)
			{
				UnturnedChat.Say(caller, plugin.Translate("kits_admin_add_uso"));
				return;
			}

			string name = args[1];
			if (plugin.FindKit(name) != null)
			{
				UnturnedChat.Say(caller, plugin.Translate("kit_add_duplicado", name));
				return;
			}

			Kit kit = new Kit { Name = name };
			if (args.Length > 2 && int.TryParse(args[2], out int cooldown) && cooldown >= 0)
			{
				kit.CooldownSeconds = cooldown;
			}
			if (args.Length > 3 && uint.TryParse(args[3], out uint priority))
			{
				kit.Priority = priority;
			}
			if (args.Length > 4 && !string.IsNullOrEmpty(args[4]))
			{
				kit.Permission = args[4];
			}

			plugin.Configuration.Instance.Kits.Add(kit);
			plugin.Configuration.Save();
			UnturnedChat.Say(caller, plugin.Translate("kit_add_ok", name));
			plugin.RefreshAllSessions();
		}

		private static void RemoveKit(IRocketPlayer caller, SimpleKitsPlugin plugin, string[] args)
		{
			if (args.Length < 2)
			{
				UnturnedChat.Say(caller, plugin.Translate("kits_admin_remove_uso"));
				return;
			}

			Kit kit = plugin.FindKit(args[1]);
			if (kit == null)
			{
				UnturnedChat.Say(caller, plugin.Translate("kit_nao_existe", args[1]));
				return;
			}

			plugin.Configuration.Instance.Kits.Remove(kit);
			plugin.Configuration.Save();
			UnturnedChat.Say(caller, plugin.Translate("kit_remove_ok", kit.Name));
			plugin.RefreshAllSessions();
		}

		private static void AddItem(IRocketPlayer caller, SimpleKitsPlugin plugin, string[] args)
		{
			if (args.Length < 3)
			{
				UnturnedChat.Say(caller, plugin.Translate("kits_admin_additem_uso"));
				return;
			}

			Kit kit = plugin.FindKit(args[1]);
			if (kit == null)
			{
				UnturnedChat.Say(caller, plugin.Translate("kit_nao_existe", args[1]));
				return;
			}

			if (!ushort.TryParse(args[2], out ushort itemId))
			{
				UnturnedChat.Say(caller, plugin.Translate("kits_admin_id_invalido", args[2]));
				return;
			}

			if (Assets.find(EAssetType.ITEM, itemId) == null)
			{
				UnturnedChat.Say(caller, plugin.Translate("kit_additem_invalido", kit.Name, itemId));
				return;
			}

			byte amount = 1;
			if (args.Length > 3 && (!byte.TryParse(args[3], out amount) || amount == 0))
			{
				amount = 1;
			}

			kit.Items.Add(new KitItem { ItemID = itemId, Amount = amount });
			plugin.Configuration.Save();
			UnturnedChat.Say(caller, plugin.Translate("kit_additem_ok", kit.Name, itemId, amount));
			plugin.RefreshAllSessions();
		}

		private static void RemoveItem(IRocketPlayer caller, SimpleKitsPlugin plugin, string[] args)
		{
			if (args.Length < 3)
			{
				UnturnedChat.Say(caller, plugin.Translate("kits_admin_removeitem_uso"));
				return;
			}

			Kit kit = plugin.FindKit(args[1]);
			if (kit == null)
			{
				UnturnedChat.Say(caller, plugin.Translate("kit_nao_existe", args[1]));
				return;
			}

			if (!ushort.TryParse(args[2], out ushort itemId))
			{
				UnturnedChat.Say(caller, plugin.Translate("kits_admin_id_invalido", args[2]));
				return;
			}

			int before = kit.Items.Count;
			kit.Items.RemoveAll(i => i.ItemID == itemId);
			if (kit.Items.Count == before)
			{
				UnturnedChat.Say(caller, plugin.Translate("kit_removeitem_ausente", kit.Name, itemId));
				return;
			}

			plugin.Configuration.Save();
			UnturnedChat.Say(caller, plugin.Translate("kit_removeitem_ok", kit.Name, itemId));
			plugin.RefreshAllSessions();
		}

		private static void SetField(IRocketPlayer caller, SimpleKitsPlugin plugin, string[] args)
		{
			if (args.Length < 4)
			{
				UnturnedChat.Say(caller, plugin.Translate("kits_admin_set_uso"));
				return;
			}

			Kit kit = plugin.FindKit(args[1]);
			if (kit == null)
			{
				UnturnedChat.Say(caller, plugin.Translate("kit_nao_existe", args[1]));
				return;
			}

			string field = args[2].ToLower();
			string value = args[3];
			switch (field)
			{
				case "cooldown":
					if (!int.TryParse(value, out int cd) || cd < 0)
					{
						UnturnedChat.Say(caller, plugin.Translate("kits_admin_valor_invalido", field));
						return;
					}
					kit.CooldownSeconds = cd;
					break;
				case "prioridade":
				case "priority":
					if (!uint.TryParse(value, out uint prio))
					{
						UnturnedChat.Say(caller, plugin.Translate("kits_admin_valor_invalido", field));
						return;
					}
					kit.Priority = prio;
					break;
				case "permissao":
				case "permission":
					kit.Permission = value == "-" || value.Length == 0 ? null : value;
					break;
				case "icon":
				case "icone":
					kit.IconUrl = value == "-" || value.Length == 0 ? null : value;
					break;
				case "nome":
				case "name":
					if (plugin.FindKit(value) != null)
					{
						UnturnedChat.Say(caller, plugin.Translate("kit_add_duplicado", value));
						return;
					}
					kit.Name = value;
					break;
				default:
					UnturnedChat.Say(caller, plugin.Translate("kits_admin_campo_invalido", field));
					return;
			}

			plugin.Configuration.Save();
			UnturnedChat.Say(caller, plugin.Translate("kit_set_ok", kit.Name, field, value));
			plugin.RefreshAllSessions();
		}

		private static void ListKits(IRocketPlayer caller, SimpleKitsPlugin plugin)
		{
			List<Kit> kits = plugin.Configuration.Instance.Kits;
			if (kits.Count == 0)
			{
				UnturnedChat.Say(caller, plugin.Translate("kits_vazio"));
				return;
			}

			foreach (Kit kit in kits)
			{
				List<string> parts = new List<string>();
				foreach (KitItem item in kit.Items)
				{
					parts.Add(item.ItemID + " x" + item.Amount);
				}
				string items = parts.Count > 0 ? string.Join(", ", parts) : "(sem itens)";
				string perm = string.IsNullOrEmpty(kit.Permission) ? "-" : kit.Permission;
				UnturnedChat.Say(caller, kit.Name + " | cooldown " + kit.CooldownSeconds + "s | prioridade " + kit.Priority + " | permissao " + perm + " | itens: " + items);
			}
		}
	}
}