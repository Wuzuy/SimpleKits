using Newtonsoft.Json;
using Rocket.API;
using Rocket.API.Collections;
using Rocket.Core.Logging;
using Rocket.Core.Plugins;
using Rocket.Unturned;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Events;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;

namespace SimpleKits
{
	public class SimpleKitsPlugin : RocketPlugin<KitConfiguration>
	{
		public static SimpleKitsPlugin Instance;

		public List<KitUiSession> Sessions { get; } = new List<KitUiSession>();

		private readonly Dictionary<ulong, Dictionary<string, DateTime>> cooldowns =
			new Dictionary<ulong, Dictionary<string, DateTime>>();

		private readonly Dictionary<ulong, bool> autoEquipSettings = new Dictionary<ulong, bool>();
		private readonly Dictionary<ulong, bool> allowOverflowSettings = new Dictionary<ulong, bool>();

		protected override void Load()
		{
			Instance = this;
			LoadCooldowns();
			LoadPlayerSettings();
			StartCoroutine(VaultMonitor());
			EffectManager.onEffectButtonClicked += OnEffectButtonClicked;
			EffectManager.onEffectTextCommitted += OnEffectTextCommitted;
			U.Events.OnPlayerDisconnected += Events_OnPlayerDisconnected;
			Logger.Log("SimpleKits loaded! Kits: " + Configuration.Instance.Kits.Count);

			if (Level.isLoaded)
			{
				foreach (SteamPlayer client in Provider.clients)
				{
					Events_OnPlayerConnected(UnturnedPlayer.FromSteamPlayer(client));
				}
			}
		}

		protected override void Unload()
		{
			while (Sessions.Count > 0)
			{
				Sessions[0].Terminate();
			}

			SaveCooldowns();
			SavePlayerSettings();
			StopAllCoroutines();
			EffectManager.onEffectButtonClicked -= OnEffectButtonClicked;
			EffectManager.onEffectTextCommitted -= OnEffectTextCommitted;
			U.Events.OnPlayerDisconnected -= Events_OnPlayerDisconnected;
			Instance = null;
		}

		public override TranslationList DefaultTranslations => new TranslationList
		{
			{ "ui_title", "Simple Kits" },
			{ "ui_ready", "READY" },
			{ "kit_uso", "Usage: /kit <name>" },
			{ "kit_nao_existe", "The kit \"{0}\" does not exist. Use /kits to see the available kits." },
			{ "kit_entregue", "Kit \"{0}\" delivered!" },
			{ "kit_erro", "Could not deliver kit \"{0}\" (invalid item)." },
			{ "kit_cooldown", "Kit \"{0}\" is on cooldown. {1} second(s) remaining." },
			{ "kit_sem_permissao", "You do not have permission to claim kit \"{0}\"." },
			{ "kits_lista", "Available kits: {0}" },
			{ "kits_vazio", "No kits configured." },
			{ "kits_admin_sem_perm", "Voce precisa da permissao \"{0}\" para gerenciar kits." },
			{ "kits_admin_acao_invalida", "Acao desconhecida: {0}." },
			{ "kits_admin_add_uso", "Uso: /kits admin add <nome> [cooldown] [prioridade] [permissao]" },
			{ "kits_admin_remove_uso", "Uso: /kits admin remove <nome>" },
			{ "kits_admin_additem_uso", "Uso: /kits admin additem <nome> <itemID> [quantidade]" },
			{ "kits_admin_removeitem_uso", "Uso: /kits admin removeitem <nome> <itemID>" },
			{ "kits_admin_set_uso", "Uso: /kits admin set <nome> <cooldown|prioridade|permissao|nome> <valor>" },
			{ "kits_admin_id_invalido", "Item ID invalido: {0}." },
			{ "kits_admin_valor_invalido", "Valor invalido para \"{0}\"." },
			{ "kits_admin_campo_invalido", "Campo invalido: {0}. Use cooldown, prioridade, permissao ou nome." },
			{ "kit_add_ok", "Kit \"{0}\" criado! Use /kits admin additem para adicionar itens." },
			{ "kit_add_duplicado", "Ja existe um kit chamado \"{0}\"." },
			{ "kit_remove_ok", "Kit \"{0}\" removido." },
			{ "kit_additem_ok", "Item {1} x{2} adicionado ao kit \"{0}\"." },
			{ "kit_additem_invalido", "Item ID {1} nao existe no jogo (nao adicionado ao kit \"{0}\")." },
			{ "kit_removeitem_ok", "Item {1} removido do kit \"{0}\"." },
			{ "kit_removeitem_ausente", "O item {1} nao esta no kit \"{0}\"." },
			{ "kit_set_ok", "Kit \"{0}\" atualizado: {1} = {2}." },
			{ "kit_ui_salvo", "Kit \"{0}\" salvo com sucesso!" },
			{ "kit_ui_criado", "Kit \"{0}\" criado com sucesso!" },
			{ "kit_ui_removido", "Kit \"{0}\" removido." },
			{ "kit_ui_nome_vazio", "O nome do kit nao pode ser vazio." },
			{ "kit_ui_item_invalido", "Item invalido: {0}. Formato: IDxQtd (ex.: 95x2,393x3)." },
			{ "kit_ui_cooldown_invalido", "Cooldown invalido." },
			{ "kit_ui_prioridade_invalida", "Prioridade invalida." },
			{ "kit_ui_titulo_criar", "Criando novo kit" },
			{ "kit_ui_titulo_editar", "Editando: {0}" },
			{ "kits_admin_alias", "Dica: voce tambem pode usar /kitsadmin (mesma coisa que /kits admin)." },
			{ "ui_auto_on", "AUTO-EQUIP: ON" },
			{ "ui_auto_off", "AUTO-EQUIP: OFF" },
			{ "ui_overflow_on", "PEGAR SEM ESPACO: ON" },
			{ "ui_overflow_off", "PEGAR SEM ESPACO: OFF" },
			{ "kit_ui_mao_vazia", "Voce nao esta segurando nenhum item." },
			{ "kit_ui_mao_ok", "Item {0} adicionado ao kit (1x)." },
			{ "kit_vault_aberto", "Bau aberto! Arraste itens do seu inventario para o bau e feche (F ou ESC) para gravar no kit. Os itens do bau SUBSTITUEM os itens atuais do kit." },
			{ "kit_vault_fechado", "Bau fechado! {0} item(ns) salvo(s) no kit \"{1}\"." },
			{ "kit_vault_vazio", "Bau fechado sem itens. O kit \"{0}\" ficou sem itens." },
			{ "kit_vault_erro", "Nao foi possivel criar o bau." },
			{ "kit_vault_virtual", "Bau do jogo indisponivel nesta versao; usando bau virtual. Clique nos itens do inventario para depositar no kit." },
			{ "kit_vault_depositado", "Depositado no kit: {0} (ID {1}) x{2}." },
			{ "kit_vault_devolvido", "Devolvido ao inventario: {0} (ID {1}) x{2}." },
			{ "kit_vault_cheio", "Inventario cheio! O item nao foi devolvido." },
			{ "kit_sem_espaco", "Kit \"{0}\" bloqueado: nao ha espaco suficiente no inventario. O cooldown nao foi consumido." },
			{ "kit_itens_dropados", "Kit \"{0}\" entregue. {1} item(ns) cairam no chao por falta de espaco." }
		};

		public Kit FindKit(string name)
		{
			foreach (Kit kit in Configuration.Instance.Kits)
			{
				if (kit.Name != null && kit.Name.ToLower() == name.ToLower())
				{
					return kit;
				}
			}
			return null;
		}

		public bool PlayerHasPermission(Player player, string permission)
		{
			UnturnedPlayer uplayer = UnturnedPlayer.FromSteamPlayer(player.channel.owner);
			return uplayer.HasPermission(permission);
		}

		public bool PlayerHasBypass(Player player)
		{
			return PlayerHasPermission(player, Configuration.Instance.BypassPermission);
		}

		public EffectAsset FindUiEffectAsset()
		{
			EffectAsset asset = Assets.find(EAssetType.EFFECT, Configuration.Instance.EffectId) as EffectAsset;
			if (asset == null)
			{
				Logger.LogError("UI effect asset with ID " + Configuration.Instance.EffectId + " not found! Server needs the Kits UI asset in Servers/<id>/Workshop/Content/.");
			}
			return asset;
		}

		public string GetKitContentsText(Kit kit)
		{
			List<string> parts = new List<string>();

			foreach (KitItem kitItem in kit.Items)
			{
				ItemAsset itemAsset = Assets.find(EAssetType.ITEM, kitItem.ItemID) as ItemAsset;
				string name = itemAsset != null ? itemAsset.itemName : ("#" + kitItem.ItemID);
				string suffix = "";
				byte[] state = kitItem.StateBytes;
				if (itemAsset != null && itemAsset.type == EItemType.GUN && state != null && state.Length >= 10)
				{
					int count = 0;
					for (int i = 0; i < 5; i++)
					{
						if (BitConverter.ToUInt16(state, i * 2) != 0)
						{
							count++;
						}
					}
					if (count > 0)
					{
						suffix = "  <color=#38BDF8FF>+" + count + " ac.</color>";
					}
				}
				parts.Add(name + " x" + kitItem.Amount + suffix);
			}

			return string.Join("\n", parts);
		}

		public bool TryClaimKit(Player player, Kit kit, bool bypassCooldown = false)
		{
			UnturnedPlayer uplayer = UnturnedPlayer.FromSteamPlayer(player.channel.owner);
			Logger.Log("[SimpleKits] TryClaimKit: " + kit.Name + " (steamID " + player.channel.owner.playerID.steamID + ")");

			if (!string.IsNullOrEmpty(kit.Permission) && !PlayerHasBypass(player) && !PlayerHasPermission(player, kit.Permission))
			{
				UnturnedChat.Say(uplayer, Translate("kit_sem_permissao", kit.Name));
				Logger.Log("[SimpleKits] Claim negado (permissao)");
				return false;
			}

			if (!bypassCooldown)
			{
				double remaining = GetCooldownRemaining(player, kit);
				if (remaining > 0)
				{
					int seconds = (int)Math.Ceiling(remaining);
					UnturnedChat.Say(uplayer, Translate("kit_cooldown", kit.Name, seconds));
					Logger.Log("[SimpleKits] Claim negado (cooldown " + seconds + "s)");
					return false;
				}
			}

			Dictionary<ushort, HashSet<int>> before = SnapshotItemPositions(player, kit);
			HashSet<int> beforeSlots = SnapshotInventorySlots(player);
			List<Item> pendingItems = new List<Item>();
			foreach (KitItem kitItem in kit.Items)
			{
				ItemAsset asset = Assets.find(EAssetType.ITEM, kitItem.ItemID) as ItemAsset;
				if (asset == null)
				{
					Logger.LogError("Invalid item in kit \"" + kit.Name + "\": " + kitItem.ItemID);
					UnturnedChat.Say(uplayer, Translate("kit_erro", kit.Name));
					return false;
				}

				int remaining = Math.Max(1, (int)kitItem.Amount);
				byte[] state = kitItem.StateBytes;
				int maxAmount = Math.Max(1, asset.MaxAmount);
				while (remaining > 0)
				{
					int amount = state != null && state.Length > 0 || maxAmount <= 1
						? 1
						: Math.Min(remaining, maxAmount);
					pendingItems.Add(new Item(kitItem.ItemID, (byte)amount, 100,
						state == null ? null : (byte[])state.Clone()));
					remaining -= amount;
				}
			}

			bool allowOverflow = GetAllowOverflow(player);
			int dropped = 0;
			foreach (Item item in pendingItems)
			{
				if (player.inventory.tryAddItem(item, false))
				{
					continue;
				}

				if (!allowOverflow)
				{
					RollbackAddedInventoryItems(player, beforeSlots);
					UnturnedChat.Say(uplayer, Translate("kit_sem_espaco", kit.Name));
					Logger.Log("[SimpleKits] Claim bloqueado por falta de espaco: " + kit.Name);
					return false;
				}

				ItemManager.dropItem(item, player.transform.position, false, true, true);
				dropped++;
			}

			if (GetAutoEquip(player))
			{
				TryAutoEquip(player, kit, before);
			}

			if (!bypassCooldown)
			{
				SetCooldown(player, kit);
			}
			UnturnedChat.Say(uplayer, Translate("kit_entregue", kit.Name));
			if (dropped > 0)
			{
				UnturnedChat.Say(uplayer, Translate("kit_itens_dropados", kit.Name, dropped));
			}
			Logger.Log("[SimpleKits] Claim OK: " + kit.Name);
			return true;
		}

		public void RefreshAllSessions()
		{
			foreach (KitUiSession session in Sessions.ToList())
			{
				session.UpdatePage();
			}
		}

		public ItemBarricadeAsset FindChestAsset()
		{
			ushort[] ids = { 328, 1280, 37955, 37956 };
			foreach (ushort id in ids)
			{
				ItemBarricadeAsset chest = Assets.find(EAssetType.ITEM, id) as ItemBarricadeAsset;
				if (chest != null)
				{
					Logger.Log("[SimpleKits] Bau do jogo encontrado: item " + id + " (" + chest.itemName + ")");
					return chest;
				}
			}
			Logger.Log("[SimpleKits] Nenhum bau do jogo encontrado (328, 1280, 37955, 37956)");
			return null;
		}

		private IEnumerator VaultMonitor()
		{
			while (true)
			{
				yield return new WaitForSeconds(0.75f);
				foreach (KitUiSession session in Sessions.ToList())
				{
					session.CheckVaultClosed();
				}
			}
		}

		public double GetCooldownRemaining(Player player, Kit kit)
		{
			if (kit.CooldownSeconds <= 0)
			{
				return 0;
			}

			if (!cooldowns.TryGetValue(player.channel.owner.playerID.steamID.m_SteamID, out Dictionary<string, DateTime> byKit))
			{
				return 0;
			}

			if (!byKit.TryGetValue(kit.Name, out DateTime until))
			{
				return 0;
			}

			return (until - DateTime.UtcNow).TotalSeconds;
		}

		public void SetCooldown(Player player, Kit kit)
		{
			if (kit.CooldownSeconds <= 0)
			{
				return;
			}

			ulong steamId = player.channel.owner.playerID.steamID.m_SteamID;
			if (!cooldowns.TryGetValue(steamId, out Dictionary<string, DateTime> byKit))
			{
				byKit = new Dictionary<string, DateTime>();
				cooldowns[steamId] = byKit;
			}

			byKit[kit.Name] = DateTime.UtcNow.AddSeconds(kit.CooldownSeconds);
		}

		public bool GetAutoEquip(Player player)
		{
			return !autoEquipSettings.TryGetValue(player.channel.owner.playerID.steamID.m_SteamID, out bool value) || value;
		}

		public void SetAutoEquip(Player player, bool enabled)
		{
			autoEquipSettings[player.channel.owner.playerID.steamID.m_SteamID] = enabled;
			SavePlayerSettings();
		}

		public bool GetAllowOverflow(Player player)
		{
			return allowOverflowSettings.TryGetValue(player.channel.owner.playerID.steamID.m_SteamID, out bool value) && value;
		}

		public void SetAllowOverflow(Player player, bool enabled)
		{
			allowOverflowSettings[player.channel.owner.playerID.steamID.m_SteamID] = enabled;
			SavePlayerSettings();
		}

		private static int InventorySlotKey(byte page, int index)
		{
			return (page << 8) | index;
		}

		private static HashSet<int> SnapshotInventorySlots(Player player)
		{
			HashSet<int> slots = new HashSet<int>();
			for (byte page = PlayerInventory.SLOTS; page < PlayerInventory.PAGES - 2; page++)
			{
				byte count = player.inventory.getItemCount(page);
				for (byte index = 0; index < count; index++)
				{
					if (player.inventory.getItem(page, index) != null)
					{
						slots.Add(InventorySlotKey(page, index));
					}
				}
			}
			return slots;
		}

		private static void RollbackAddedInventoryItems(Player player, HashSet<int> before)
		{
			for (int page = PlayerInventory.PAGES - 3; page >= PlayerInventory.SLOTS; page--)
			{
			byte count = player.inventory.getItemCount((byte)page);
				for (int index = count - 1; index >= 0; index--)
				{
					if (!before.Contains(InventorySlotKey((byte)page, index)))
					{
						player.inventory.removeItem((byte)page, (byte)index);
					}
				}
			}
		}

		private Dictionary<ushort, HashSet<int>> SnapshotItemPositions(Player player, Kit kit)
		{
			Dictionary<ushort, HashSet<int>> snapshot = new Dictionary<ushort, HashSet<int>>();
			if (kit.Items == null)
			{
				return snapshot;
			}

			foreach (KitItem kitItem in kit.Items)
			{
				HashSet<int> positions = new HashSet<int>();
				for (byte page = 0; page < PlayerInventory.PAGES - 1; page++)
				{
					byte width = player.inventory.getWidth(page);
					byte height = player.inventory.getHeight(page);
					for (byte x = 0; x < width; x++)
					{
						for (byte y = 0; y < height; y++)
						{
							byte index = player.inventory.getIndex(page, x, y);
							if (index != byte.MaxValue)
							{
								ItemJar jar = player.inventory.getItem(page, index);
								if (jar != null && jar.item != null && jar.item.id == kitItem.ItemID)
								{
									positions.Add(page * 100 + x * 10 + y);
								}
							}
						}
					}
				}
				snapshot[kitItem.ItemID] = positions;
			}
			return snapshot;
		}

		private bool FindNewItemPosition(Player player, ushort itemId, Dictionary<ushort, HashSet<int>> before, out byte page, out byte x, out byte y, out byte index)
		{
			page = byte.MaxValue;
			x = byte.MaxValue;
			y = byte.MaxValue;
			index = byte.MaxValue;

			if (!before.TryGetValue(itemId, out HashSet<int> existing))
			{
				existing = new HashSet<int>();
			}

			for (byte p = 0; p < PlayerInventory.PAGES - 1; p++)
			{
				byte width = player.inventory.getWidth(p);
				byte height = player.inventory.getHeight(p);
				for (byte px = 0; px < width; px++)
				{
					for (byte py = 0; py < height; py++)
					{
						byte i = player.inventory.getIndex(p, px, py);
						if (i == byte.MaxValue)
						{
							continue;
						}

						if (player.inventory.getItem(p, i) != null && player.inventory.getItem(p, i).item != null
						&& player.inventory.getItem(p, i).item.id == itemId && !existing.Contains(p * 100 + px * 10 + py))
						{
							page = p;
							x = px;
							y = py;
							index = i;
							return true;
						}
					}
				}
			}
			return false;
		}

		private void TryAutoEquip(Player player, Kit kit, Dictionary<ushort, HashSet<int>> before)
		{
			foreach (KitItem kitItem in kit.Items)
			{
				if (!FindNewItemPosition(player, kitItem.ItemID, before, out byte page, out byte x, out byte y, out byte index))
				{
					continue;
				}

				ItemAsset asset = Assets.find(EAssetType.ITEM, kitItem.ItemID) as ItemAsset;
				if (asset == null)
				{
					continue;
				}

				Item item = player.inventory.getItem(page, index).item;

				if (asset is ItemShirtAsset || asset is ItemPantsAsset || asset is ItemHatAsset || asset is ItemMaskAsset || asset is ItemGlassesAsset || asset is ItemVestAsset || asset is ItemBackpackAsset)
				{
					ushort worn = asset switch
					{
						ItemShirtAsset => player.clothing.shirt,
						ItemPantsAsset => player.clothing.pants,
						ItemHatAsset => player.clothing.hat,
						ItemMaskAsset => player.clothing.mask,
						ItemGlassesAsset => player.clothing.glasses,
						ItemVestAsset => player.clothing.vest,
						_ => player.clothing.backpack
					};
					if (worn != 0)
					{
						continue;
					}

					player.inventory.removeItem(page, index);
					switch (asset)
					{
						case ItemShirtAsset:
							player.clothing.askWearShirt(item.id, item.quality, item.state, true);
							break;
						case ItemPantsAsset:
							player.clothing.askWearPants(item.id, item.quality, item.state, true);
							break;
						case ItemHatAsset:
							player.clothing.askWearHat(item.id, item.quality, item.state, true);
							break;
						case ItemMaskAsset:
							player.clothing.askWearMask(item.id, item.quality, item.state, true);
							break;
						case ItemGlassesAsset:
							player.clothing.askWearGlasses(item.id, item.quality, item.state, true);
							break;
						case ItemVestAsset:
							player.clothing.askWearVest(item.id, item.quality, item.state, true);
							break;
						case ItemBackpackAsset:
							player.clothing.askWearBackpack(item.id, item.quality, item.state, true);
							break;
					}
					Logger.Log("[SimpleKits] Auto-equip (corpo): " + item.id);
					continue;
				}

				if (player.equipment.equippedPage == page)
				{
					continue;
				}

				if (asset.canPlayerEquip)
				{
					player.equipment.ServerEquip(page, x, y);
					Logger.Log("[SimpleKits] Auto-equip (mao): " + item.id + " (page " + page + ")");
				}
			}
		}

		private void LoadPlayerSettings()
		{
			string path = Path.Combine(Directory, "PlayerSettings.json");
			if (!File.Exists(path))
			{
				return;
			}

			try
			{
				List<PlayerSettingEntry> entries = JsonConvert.DeserializeObject<List<PlayerSettingEntry>>(File.ReadAllText(path));
				if (entries != null)
				{
					foreach (PlayerSettingEntry entry in entries)
					{
						autoEquipSettings[entry.PlayerID] = entry.AutoEquip;
						allowOverflowSettings[entry.PlayerID] = entry.AllowOverflow;
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "Failed to load player settings");
			}
		}

		private void SavePlayerSettings()
		{
			string path = Path.Combine(Directory, "PlayerSettings.json");
			try
			{
				List<PlayerSettingEntry> entries = new List<PlayerSettingEntry>();
				HashSet<ulong> playerIds = new HashSet<ulong>(autoEquipSettings.Keys);
				playerIds.UnionWith(allowOverflowSettings.Keys);
				foreach (ulong playerId in playerIds)
				{
					entries.Add(new PlayerSettingEntry
					{
						PlayerID = playerId,
						AutoEquip = !autoEquipSettings.TryGetValue(playerId, out bool autoEquip) || autoEquip,
						AllowOverflow = allowOverflowSettings.TryGetValue(playerId, out bool allowOverflow) && allowOverflow
					});
				}
				File.WriteAllText(path, JsonConvert.SerializeObject(entries, Formatting.Indented));
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "Failed to save player settings");
			}
		}

		private void OnEffectButtonClicked(Player player, string buttonName)
		{
			Logger.Log("[SimpleKits] Click: " + buttonName + " (steamID " + player.channel.owner.playerID.steamID + ")");
			KitUiSession session = Sessions.FirstOrDefault(s => s.Owner == player);
			session?.OnButtonClicked(buttonName);
		}

		private void OnEffectTextCommitted(Player player, string inputName, string text)
		{
			Logger.Log("[SimpleKits] Text: " + inputName + " = \"" + text + "\" (steamID " + player.channel.owner.playerID.steamID + ")");
			KitUiSession session = Sessions.FirstOrDefault(s => s.Owner == player);
			session?.OnTextCommitted(inputName, text);
		}

		private void Events_OnPlayerConnected(UnturnedPlayer player)
		{
			// Sessions are created on demand when the player runs /kits.
		}

		private void Events_OnPlayerDisconnected(UnturnedPlayer player)
		{
			KitUiSession session = Sessions.FirstOrDefault(s => s.Owner == player.Player);
			session?.Terminate();
		}

		private void LoadCooldowns()
		{
			string path = Path.Combine(Directory, "Cooldowns.json");
			if (!File.Exists(path))
			{
				return;
			}

			try
			{
				List<CooldownEntry> entries = JsonConvert.DeserializeObject<List<CooldownEntry>>(File.ReadAllText(path));
				if (entries != null)
				{
					DateTime now = DateTime.UtcNow;
					foreach (CooldownEntry entry in entries)
					{
						foreach (KitCooldownEntry kitEntry in entry.Timers)
						{
							DateTime until = kitEntry.LastTime + TimeSpan.FromSeconds(FindKit(kitEntry.KitName)?.CooldownSeconds ?? 0);
							if (until <= now)
							{
								continue;
							}

							if (!cooldowns.TryGetValue(entry.PlayerID, out Dictionary<string, DateTime> byKit))
							{
								byKit = new Dictionary<string, DateTime>();
								cooldowns[entry.PlayerID] = byKit;
							}

							byKit[kitEntry.KitName] = until;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "Failed to load cooldowns");
			}
		}

		private void SaveCooldowns()
		{
			string path = Path.Combine(Directory, "Cooldowns.json");
			try
			{
				List<CooldownEntry> entries = new List<CooldownEntry>();
				DateTime now = DateTime.UtcNow;
				foreach (KeyValuePair<ulong, Dictionary<string, DateTime>> pair in cooldowns)
				{
					CooldownEntry entry = new CooldownEntry { PlayerID = pair.Key, Timers = new List<KitCooldownEntry>() };
					foreach (KeyValuePair<string, DateTime> kitPair in pair.Value)
					{
						if (kitPair.Value > now)
						{
							Kit kit = FindKit(kitPair.Key);
							if (kit != null)
							{
								entry.Timers.Add(new KitCooldownEntry
								{
									KitName = kitPair.Key,
									LastTime = kitPair.Value - TimeSpan.FromSeconds(kit.CooldownSeconds)
								});
							}
						}
					}
					if (entry.Timers.Count > 0)
					{
						entries.Add(entry);
					}
				}
				File.WriteAllText(path, JsonConvert.SerializeObject(entries, Formatting.Indented));
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "Failed to save cooldowns");
			}
		}

		private class CooldownEntry
		{
			public ulong PlayerID { get; set; }
			public List<KitCooldownEntry> Timers { get; set; }
		}

		private class KitCooldownEntry
		{
			public string KitName { get; set; }
			public DateTime LastTime { get; set; }
		}

		private class PlayerSettingEntry
		{
			public ulong PlayerID { get; set; }
			public bool AutoEquip { get; set; }
			public bool AllowOverflow { get; set; }
		}
	}
}
