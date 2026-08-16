using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.NetTransport;
using SDG.Unturned;
using UnityEngine;

namespace SimpleKits
{
	public class KitUiSession
	{
		private const short UI_KEY = 4750;

		private readonly SimpleKitsPlugin plugin;
		private readonly bool hasBypass;
		private readonly Coroutine[] cooldownUpdaters = new Coroutine[8];
		private byte actualPage;

		private Kit editKit;
		private Kit editingSource;
		private string itemsText;
		private bool settingsPanelOpen;
		private bool previewPanelOpen;
		private bool previewDetailMode;

		private struct VaultEntry
		{
			public byte Page;
			public byte Index;
			public ItemJar Jar;
		}

		private readonly List<VaultEntry> vaultEntries = new List<VaultEntry>();
		private readonly HashSet<string> sentVaultIconUrls = new HashSet<string>();
		private readonly List<string> pendingIconKeys = new List<string>();
		private readonly List<string> pendingIconUrls = new List<string>();
		private Coroutine vaultIconSender;
		private int vaultPage;
		private int vaultTab;
		private bool vaultPanelOpen;

		private InteractableStorage vaultStorage;
		private BarricadeDrop vaultDrop;
		private Vector3 vaultPosition;

		public Player Owner { get; }

		public bool HasVaultOpen => vaultStorage != null;

		public KitUiSession(SimpleKitsPlugin plugin, Player owner, bool hasBypass)
		{
			this.plugin = plugin;
			Owner = owner;
			this.hasBypass = hasBypass;
			Instantiate();
		}

		private ITransportConnection TransportConnection => Owner.channel.GetOwnerTransportConnection();

		private bool IsEditing => editKit != null;

		private List<Kit> VisibleKits => plugin.Configuration.Instance.Kits
			.Where(k => hasBypass || string.IsNullOrEmpty(k.Permission) || plugin.PlayerHasPermission(Owner, k.Permission))
			.OrderByDescending(k => k.Priority)
			.ToList();

		private int PagesAmount()
		{
			int count = VisibleKits.Count;
			return count <= 0 ? 1 : 1 + (count - 1) / 8;
		}

		public void Instantiate()
		{
			EffectAsset asset = plugin.FindUiEffectAsset();
			if (asset == null)
			{
				return;
			}

			Owner.enablePluginWidgetFlag(EPluginWidgetFlags.Modal);
			Owner.disablePluginWidgetFlag(EPluginWidgetFlags.ShowLifeMeters);
			Owner.disablePluginWidgetFlag(EPluginWidgetFlags.ShowCenterDot);
			EffectManager.SendUIEffect(asset, UI_KEY, TransportConnection, true);
			EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "Title", plugin.Translate("ui_title"));

			bool hasIcon = !string.IsNullOrEmpty(plugin.Configuration.Instance.ServerIconURL);
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "Icon", hasIcon);
			if (hasIcon)
			{
				EffectManager.sendUIEffectImageURL(UI_KEY, TransportConnection, true, "Icon", plugin.Configuration.Instance.ServerIconURL, true, false);
			}

			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "CKit", hasBypass);
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "BExit", !hasBypass);
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "backgroundfalseexit", true);
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "Exit", hasBypass);
			EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "SetAutoEquipLabel",
				plugin.GetAutoEquip(Owner) ? plugin.Translate("ui_on") : plugin.Translate("ui_off"));
			EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "SetOverflowLabel",
				plugin.GetAllowOverflow(Owner) ? plugin.Translate("ui_on") : plugin.Translate("ui_off"));

			for (int i = 1; i <= 8; i++)
			{
				EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, $"Kit{i}_Extension [PLAYER]", !hasBypass);
				EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, $"Player_Kit{i}_ClaimP", !hasBypass);
				EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, $"Player_Kit{i}_ClaimP [DISABLED]", !hasBypass);
				EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, $"Player_Kit{i}_Preview", !hasBypass);
				EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, $"Kit{i}_Extension", hasBypass);
				EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, $"ADM_Kit{i}_Claim", hasBypass);
				EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, $"ADM_Kit{i}_Edit", hasBypass);
				EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, $"ADM_Kit{i}_Delete", hasBypass);
				EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, $"ADM_Kit{i}_Preview", hasBypass);
			}

			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "Create/Edit (Kit)", false);

			UpdatePage();
		}

		public void Terminate()
		{
			StopVaultIconSender();
			if (vaultStorage != null && editKit != null)
			{
				CaptureVaultItems();
				plugin.Configuration.Save();
			}
			CleanupVaultBarricade();

			foreach (Coroutine updater in cooldownUpdaters)
			{
				if (updater != null)
				{
					plugin.StopCoroutine(updater);
				}
			}

			Owner.disablePluginWidgetFlag(EPluginWidgetFlags.Modal);
			Owner.enablePluginWidgetFlag(EPluginWidgetFlags.ShowLifeMeters);
			Owner.enablePluginWidgetFlag(EPluginWidgetFlags.ShowCenterDot);
			EffectManager.askEffectClearByID(plugin.Configuration.Instance.EffectId, TransportConnection);
			plugin.Sessions.Remove(this);
		}

		public void OnButtonClicked(string buttonName)
		{
			if (buttonName.Contains("Claim"))
			{
				HandleClaim(buttonName);
				return;
			}

			if (buttonName.EndsWith("_Preview"))
			{
				HandlePreviewButton(buttonName, false);
				return;
			}

			if (buttonName.StartsWith("KitDetail"))
			{
				HandlePreviewButton(buttonName, true);
				return;
			}

			if (buttonName.StartsWith("VaultSlot"))
			{
				if (hasBypass && vaultPanelOpen)
				{
					DepositVaultItem(buttonName);
				}
				return;
			}

			switch (buttonName)
			{
				case "CKit":
					if (hasBypass)
					{
						OpenEditor(null);
					}
					return;
				case "Settings":
					if (settingsPanelOpen)
					{
						CloseSettings();
					}
					else
					{
						OpenSettings();
					}
					return;
				case "SettingsClose":
					if (settingsPanelOpen)
					{
						CloseSettings();
					}
					return;
				case "SetAutoEquip":
					bool enabled = !plugin.GetAutoEquip(Owner);
					plugin.SetAutoEquip(Owner, enabled);
					EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "SetAutoEquipLabel",
						enabled ? plugin.Translate("ui_on") : plugin.Translate("ui_off"));
					return;
				case "SetOverflow":
					bool allowOverflow = !plugin.GetAllowOverflow(Owner);
					plugin.SetAllowOverflow(Owner, allowOverflow);
					EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "SetOverflowLabel",
						allowOverflow ? plugin.Translate("ui_on") : plugin.Translate("ui_off"));
					return;
				case "InfoAutoEquip":
					ToggleTooltip(plugin.Translate("tip_auto_equip"));
					return;
				case "InfoOverflow":
					ToggleTooltip(plugin.Translate("tip_overflow"));
					return;
				case "PreviewClose":
					if (previewPanelOpen)
					{
						ClosePreview();
					}
					return;
				case "OpenVault":
					if (hasBypass && IsEditing && !vaultPanelOpen)
					{
						OpenVault();
					}
					return;
				case "VaultClose":
					if (hasBypass && vaultPanelOpen)
					{
						CloseVault();
					}
					return;
				case "VaultTabInv":
					if (hasBypass && vaultPanelOpen)
					{
						vaultTab = 0;
						vaultPage = 0;
						RenderVault();
					}
					return;
				case "VaultTabKit":
					if (hasBypass && vaultPanelOpen)
					{
						vaultTab = 1;
						vaultPage = 0;
						RenderVault();
					}
					return;
				case "VaultPrev":
					if (hasBypass && vaultPanelOpen)
					{
						vaultPage--;
						RenderVault();
					}
					return;
				case "VaultNext":
					if (hasBypass && vaultPanelOpen)
					{
						vaultPage++;
						RenderVault();
					}
					return;
				case "AddHand":
					if (hasBypass && IsEditing)
					{
						AddEquippedItemToKit();
					}
					return;
				case "Save":
					if (hasBypass)
					{
						SaveEditor();
					}
					return;
				case "Cancel":
					if (hasBypass)
					{
						Return();
					}
					return;
				case "BExit":
				case "Exit":
				case "backgroundfalseexit":
					if (HasVaultOpen)
					{
						return;
					}
					if (settingsPanelOpen)
					{
						CloseSettings();
					}
					else if (previewPanelOpen)
					{
						ClosePreview();
					}
					else if (vaultPanelOpen)
					{
						CloseVault();
					}
					else if (IsEditing)
					{
						Return();
					}
					else
					{
						Terminate();
					}
					return;
				case "PPage":
					if (actualPage > 0)
					{
						actualPage--;
						UpdatePage();
					}
					return;
				case "NPage":
					if (actualPage < PagesAmount() - 1)
					{
						actualPage++;
						UpdatePage();
					}
					return;
			}

			if (buttonName.StartsWith("ADM_Kit") && hasBypass)
			{
				if (buttonName.Contains("Edit") && TryGetKitFromSlotButton(buttonName, out Kit editTarget))
				{
					OpenEditor(editTarget);
					return;
				}

				if (buttonName.Contains("Delete") && TryGetKitFromSlotButton(buttonName, out Kit deleteTarget))
				{
					DeleteKit(deleteTarget);
				}
			}
		}

		public void OnTextCommitted(string inputName, string text)
		{
			if (editKit == null)
			{
				return;
			}

			switch (inputName)
			{
				case "KitName":
					editKit.Name = text.Trim();
					break;
				case "KitItems":
					itemsText = text.Trim();
					break;
				case "KitCooldown":
					if (int.TryParse(text.Trim(), out int cooldown) && cooldown >= 0)
					{
						editKit.CooldownSeconds = cooldown;
					}
					break;
				case "KitPriority":
					if (uint.TryParse(text.Trim(), out uint priority))
					{
						editKit.Priority = priority;
					}
					break;
				case "KitPerm":
					editKit.Permission = text.Trim() == "-" || text.Trim().Length == 0 ? null : text.Trim();
					break;
				case "KitIcon":
					string icon = text.Trim();
					editKit.IconUrl = icon == "-" || icon.Length == 0 ? null : icon;
					break;
			}
		}

		private void OpenSettings()
		{
			settingsPanelOpen = true;
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "KitsUIPanel", false);
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "SettingsPanel", true);
			EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "SetAutoEquipLabel",
				plugin.GetAutoEquip(Owner) ? plugin.Translate("ui_on") : plugin.Translate("ui_off"));
			EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "SetOverflowLabel",
				plugin.GetAllowOverflow(Owner) ? plugin.Translate("ui_on") : plugin.Translate("ui_off"));
		}

		private void CloseSettings()
		{
			settingsPanelOpen = false;
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "TipInfo", false);
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "SettingsPanel", false);
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "KitsUIPanel", true);
			UpdatePage();
		}

		private void ToggleTooltip(string text)
		{
			EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "TipInfoText", text);
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "TipInfo", true);
		}

		private void HandlePreviewButton(string buttonName, bool detail)
		{
			int kitPos = buttonName.IndexOf("Kit");
			if (kitPos < 0)
			{
				return;
			}
			int digitPos = kitPos + 3;
			if (digitPos >= buttonName.Length || !char.IsDigit(buttonName[digitPos]))
			{
				return;
			}

			int slotNumber = int.Parse(buttonName[digitPos].ToString());
			List<Kit> visible = VisibleKits;
			int pageCount = Math.Min(8, visible.Count - actualPage * 8);
			if (pageCount < 0)
			{
				pageCount = 0;
			}
			int kitOnPage = Array.IndexOf(MapVisibleSlots(pageCount), slotNumber);
			if (kitOnPage < 0 || actualPage * 8 + kitOnPage >= visible.Count)
			{
				return;
			}

			OpenPreview(visible[actualPage * 8 + kitOnPage], detail);
		}

		private void OpenPreview(Kit kit, bool detail)
		{
			previewPanelOpen = true;
			previewDetailMode = detail;
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "KitsUIPanel", false);
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "PreviewPanel", true);
			EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "PreviewTitle", kit.Name);
			RenderPreview(kit, detail);
		}

		private void RenderPreview(Kit kit, bool detail)
		{
			pendingIconKeys.Clear();
			pendingIconUrls.Clear();

			List<KitItem> kitItems = kit.Items ?? new List<KitItem>();
			for (int i = 0; i < 30; i++)
			{
				if (i < kitItems.Count)
				{
					KitItem kitItem = kitItems[i];
					ItemAsset asset = Assets.find(EAssetType.ITEM, kitItem.ItemID) as ItemAsset;
					string name = asset != null && !string.IsNullOrEmpty(asset.itemName) ? asset.itemName : "Item " + kitItem.ItemID;
					string text = ColorName(asset, name) + StateSuffix(asset, kitItem.StateBytes) + "\nID " + kitItem.ItemID + " x" + kitItem.Amount;
					EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "PreviewSlot" + i, true);
					EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "PreviewSlot" + i + "_Label", text);
					string iconUrl = ItemIconUrl(kitItem.ItemID);
					if (!string.IsNullOrEmpty(iconUrl) && !sentVaultIconUrls.Contains(iconUrl))
					{
						pendingIconKeys.Add("PreviewSlot" + i + "_Icon");
						pendingIconUrls.Add(iconUrl);
					}
				}
				else
				{
					EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "PreviewSlot" + i, true);
					EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "PreviewSlot" + i + "_Label", "<color=#64748BFF>Vazio</color>");
					EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "PreviewSlot" + i + "_Icon", false);
				}
			}

			QueueVaultIcons();

			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "PreviewDetailsPanel", detail);
			if (detail)
			{
				EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "PreviewDetails", plugin.GetKitDetailsText(kit));
			}
		}

		private void ClosePreview()
		{
			previewPanelOpen = false;
			previewDetailMode = false;
			StopVaultIconSender();
			pendingIconKeys.Clear();
			pendingIconUrls.Clear();
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "PreviewPanel", false);
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "PreviewDetailsPanel", false);
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "KitsUIPanel", true);
			UpdatePage();
		}

		private void HandleClaim(string buttonName)
		{
			int kitPos = buttonName.IndexOf("Kit");
			int digitPos = kitPos + 3;
			if (kitPos < 0 || digitPos >= buttonName.Length || !char.IsDigit(buttonName[digitPos]))
			{
				return;
			}

			int slotNumber = int.Parse(buttonName[digitPos].ToString());
			List<Kit> visible = VisibleKits;
			int pageCount = Math.Min(8, visible.Count - actualPage * 8);
			if (pageCount < 0)
			{
				pageCount = 0;
			}
			int kitOnPage = Array.IndexOf(MapVisibleSlots(pageCount), slotNumber);
			if (kitOnPage < 0 || actualPage * 8 + kitOnPage >= visible.Count)
			{
				return;
			}

			Kit kit = visible[actualPage * 8 + kitOnPage];

			if (kit.Items == null || kit.Items.Count == 0)
			{
				UnturnedChat.Say(UnturnedPlayer.FromSteamPlayer(Owner.channel.owner),
					plugin.Translate("kit_empty"));
				return;
			}

			bool adminClaim = buttonName.StartsWith("ADM");

			if (!adminClaim && buttonName.Contains("DISABLED"))
			{
				double remaining = plugin.GetCooldownRemaining(Owner, kit);
				if (remaining > 0)
				{
					UnturnedChat.Say(UnturnedPlayer.FromSteamPlayer(Owner.channel.owner),
						plugin.Translate("kit_cooldown", kit.Name, (int)Math.Ceiling(remaining)));
				}
				return;
			}

			plugin.TryClaimKit(Owner, kit, hasBypass && adminClaim);
			UpdatePage();
		}

		public void UpdatePage()
		{
			for (int i = 0; i < cooldownUpdaters.Length; i++)
			{
				if (cooldownUpdaters[i] != null)
				{
					plugin.StopCoroutine(cooldownUpdaters[i]);
					cooldownUpdaters[i] = null;
				}
			}

			List<Kit> visible = VisibleKits;
			int pageCount = Math.Min(8, visible.Count - actualPage * 8);
			if (pageCount < 0)
			{
				pageCount = 0;
			}
			int[] usedSlots = MapVisibleSlots(pageCount);

			EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "Index", (actualPage + 1).ToString("D2"));
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "PPage", actualPage != 0);
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "NPage", actualPage != PagesAmount() - 1);

			for (int j = 0; j < 8; j++)
			{
				int slotNumber = j + 1;
				int kitOnPage = Array.IndexOf(usedSlots, slotNumber);
				EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, $"Kit{slotNumber}", kitOnPage >= 0);
				if (kitOnPage < 0)
				{
					continue;
				}

				Kit kit = visible[actualPage * 8 + kitOnPage];

				EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, $"Kit{slotNumber}_Title", kit.Name);
				EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, $"Kit{slotNumber}_Price", plugin.GetKitContentsText(kit));

				ushort kitIconId = 0;
				string kitIconUrl = null;
				if (kit.IconUrl != null && (kit.IconUrl.StartsWith("http://") || kit.IconUrl.StartsWith("https://")))
				{
					kitIconUrl = kit.IconUrl;
				}
				else if (kit.Items != null && kit.Items.Count > 0)
				{
					kitIconId = kit.Items[0].ItemID;
					kitIconUrl = ItemIconUrl(kitIconId);
				}
				if (!string.IsNullOrEmpty(kitIconUrl))
				{
					Rocket.Core.Logging.Logger.Log($"[SimpleKits] IconURL Kit{slotNumber}_Icon -> " + kitIconUrl);
					EffectManager.sendUIEffectImageURL(UI_KEY, TransportConnection, true, $"Kit{slotNumber}_Icon", kitIconUrl);
					EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, $"Kit{slotNumber}_Icon", true);
				}
				else
				{
					EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, $"Kit{slotNumber}_Icon", false);
				}

				double remaining = plugin.GetCooldownRemaining(Owner, kit);
				if (remaining <= 0)
				{
					EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, $"Kit{slotNumber}_Cooldown", plugin.Translate("ui_ready"));
					EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, $"Kit{slotNumber}_CooldownIcon (1)", false);

					bool hasItems = kit.Items != null && kit.Items.Count > 0;
					EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, $"Player_Kit{slotNumber}_ClaimP", hasItems);
					EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, $"Player_Kit{slotNumber}_ClaimP [DISABLED]", !hasItems);
					if (!hasItems)
					{
						EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, $"Player_Kit{slotNumber}_ClaimP [DISABLED] Label", plugin.Translate("ui_empty"));
					}
				}
				else
				{
					EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, $"Kit{slotNumber}_CooldownIcon (1)", true);
					EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, $"Player_Kit{slotNumber}_ClaimP", false);
					EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, $"Player_Kit{slotNumber}_ClaimP [DISABLED]", true);
					cooldownUpdaters[slotNumber - 1] = plugin.StartCoroutine(CooldownUpdater(slotNumber - 1, remaining));
				}
			}
		}

		private bool TryGetKitFromSlotButton(string buttonName, out Kit kit)
		{
			kit = null;
			int kitPos = buttonName.IndexOf("Kit");
			int digitPos = kitPos + 3;
			if (kitPos < 0 || digitPos >= buttonName.Length || !char.IsDigit(buttonName[digitPos]))
			{
				return false;
			}

			int slotNumber = int.Parse(buttonName[digitPos].ToString());
			List<Kit> visible = VisibleKits;
			int pageCount = Math.Min(8, visible.Count - actualPage * 8);
			if (pageCount < 0)
			{
				pageCount = 0;
			}
			int kitOnPage = Array.IndexOf(MapVisibleSlots(pageCount), slotNumber);
			if (kitOnPage < 0 || actualPage * 8 + kitOnPage >= visible.Count)
			{
				return false;
			}

			kit = visible[actualPage * 8 + kitOnPage];
			return true;
		}

		private void OpenVault()
		{
			if (plugin.Configuration.Instance.VirtualVaultOnly)
			{
				Rocket.Core.Logging.Logger.Log("[SimpleKits] VirtualVaultOnly=true - abrindo painel virtual.");
			}
			else
			{
				ItemBarricadeAsset chest = plugin.FindChestAsset();
				if (chest != null && TryOpenRealVault(chest))
				{
					return;
				}
			}

			vaultPage = 0;
			vaultPanelOpen = true;
			sentVaultIconUrls.Clear();
			StopVaultIconSender();

			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "KitsUIPanel", false);
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "Create/Edit (Kit)", false);
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "Vault", true);

			RenderVault();
			UnturnedChat.Say(UnturnedPlayer.FromSteamPlayer(Owner.channel.owner), plugin.Translate("kit_vault_virtual"));
		}

		private bool TryOpenRealVault(ItemBarricadeAsset chest)
		{
			vaultPosition = Owner.transform.position + Owner.look.aim.forward * 2f + Vector3.up * 0.25f;
			try
			{
				Transform model = BarricadeManager.dropNonPlantedBarricade(new Barricade(chest), vaultPosition, Quaternion.identity,
					Owner.channel.owner.playerID.steamID.m_SteamID, 0);
				if (model == null)
				{
					Rocket.Core.Logging.Logger.LogError("[SimpleKits] dropNonPlantedBarricade retornou null");
					return false;
				}
				vaultDrop = BarricadeManager.FindBarricadeByRootTransform(model);
				vaultStorage = model.GetComponentInChildren<InteractableStorage>();
			}
			catch (Exception ex)
			{
				Rocket.Core.Logging.Logger.LogException(ex, "Failed to spawn vault chest");
				CleanupVaultBarricade();
				return false;
			}

			if (vaultStorage == null)
			{
				Rocket.Core.Logging.Logger.LogError("[SimpleKits] Locker sem InteractableStorage no prefab");
				CleanupVaultBarricade();
				return false;
			}

			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "backgroundfalseexit", false);
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "KitsUIPanel", false);
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "Create/Edit (Kit)", false);

			Owner.inventory.openStorage(vaultStorage);
			UnturnedChat.Say(UnturnedPlayer.FromSteamPlayer(Owner.channel.owner), plugin.Translate("kit_vault_aberto"));
			return true;
		}

		public void CheckVaultClosed()
		{
			if (vaultStorage == null)
			{
				return;
			}

			if (Owner.inventory.isStoring && Owner.inventory.storage == vaultStorage)
			{
				return;
			}

			CaptureVaultItems();
			CleanupVaultBarricade();
			vaultStorage = null;
			vaultDrop = null;
			ReopenEditor();
		}

		private void CaptureVaultItems()
		{
			UnturnedPlayer uplayer = UnturnedPlayer.FromSteamPlayer(Owner.channel.owner);
			List<KitItem> captured = new List<KitItem>();
			Items items = vaultStorage.items;
			if (items != null)
			{
				byte count = items.getItemCount();
				for (byte i = 0; i < count; i++)
				{
					ItemJar jar = items.getItem(i);
					if (jar != null && jar.item != null)
					{
						captured.Add(new KitItem
						{
							ItemID = jar.item.id,
							Amount = jar.item.amount,
							State = jar.item.state != null && jar.item.state.Length > 0
								? Convert.ToBase64String(jar.item.state)
								: null
						});
					}
				}
			}

			if (editKit != null)
			{
				editKit.Items = captured;
				itemsText = string.Join(",", captured.Select(c => c.ItemID + "x" + c.Amount));
			}

			string kitName = editKit?.Name ?? "";
			if (captured.Count == 0)
			{
				UnturnedChat.Say(uplayer, plugin.Translate("kit_vault_vazio", kitName));
			}
			else
			{
				UnturnedChat.Say(uplayer, plugin.Translate("kit_vault_fechado", captured.Count, kitName));
			}
		}

		private void CleanupVaultBarricade()
		{
			if (vaultDrop == null)
			{
				return;
			}

			try
			{
				if (Regions.tryGetCoordinate(vaultPosition, out byte regionX, out byte regionY))
				{
					BarricadeManager.destroyBarricade(vaultDrop, regionX, regionY, ushort.MaxValue);
				}
			}
			catch (Exception ex)
			{
				Rocket.Core.Logging.Logger.LogException(ex, "Failed to destroy vault chest");
			}
		}

		private void ReopenEditor()
		{
			if (editKit == null)
			{
				return;
			}

			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "backgroundfalseexit", true);
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "Create/Edit (Kit)", true);
			SendEditorFields();
		}

		private void CloseVault()
		{
			vaultPanelOpen = false;
			StopVaultIconSender();
			pendingIconKeys.Clear();
			pendingIconUrls.Clear();

			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "Vault", false);
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "backgroundfalseexit", true);
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "Create/Edit (Kit)", true);
			SendEditorFields();
		}

		private void RenderVault()
		{
			if (vaultTab == 0)
			{
				RenderVaultInventory();
			}
			else
			{
				RenderVaultKit();
			}
		}

		private string ItemIconUrl(ushort itemId)
		{
			string template = plugin.Configuration.Instance.ItemIconUrlTemplate;
			if (string.IsNullOrWhiteSpace(template))
			{
				return null;
			}
			return string.Format(template, itemId);
		}

		private void StopVaultIconSender()
		{
			if (vaultIconSender != null)
			{
				plugin.StopCoroutine(vaultIconSender);
				vaultIconSender = null;
			}
		}

		private void QueueVaultIcons()
		{
			StopVaultIconSender();
			if (pendingIconKeys.Count > 0)
			{
				vaultIconSender = plugin.StartCoroutine(SendVaultIcons());
			}
		}

		private IEnumerator SendVaultIcons()
		{
			for (int i = 0; i < pendingIconKeys.Count; i++)
			{
				string key = pendingIconKeys[i];
				string url = pendingIconUrls[i];
				if (sentVaultIconUrls.Add(url))
				{
					Rocket.Core.Logging.Logger.Log($"[SimpleKits] IconURL {key} -> {url}");
					EffectManager.sendUIEffectImageURL(UI_KEY, TransportConnection, true, key, url);
				}
				if (i % 5 == 4)
				{
					yield return new WaitForSeconds(0.25f);
				}
			}
			vaultIconSender = null;
		}

		private void RenderVaultInventory()
		{
			pendingIconKeys.Clear();
			pendingIconUrls.Clear();
			vaultEntries.Clear();
			byte[] pages = { 0, 1, 2, 3, 7 };
			foreach (byte page in pages)
			{
				Items items = Owner.inventory.items[page];
				if (items == null)
				{
					continue;
				}
				byte count = items.getItemCount();
				for (byte i = 0; i < count; i++)
				{
					ItemJar jar = items.getItem(i);
					if (jar != null && jar.item != null)
					{
						vaultEntries.Add(new VaultEntry { Page = page, Index = i, Jar = jar });
					}
				}
			}

			int total = vaultEntries.Count;
			int pageCount = Math.Max(1, (total + 29) / 30);
			vaultPage = Math.Max(0, Math.Min(vaultPage, pageCount - 1));

			for (int i = 0; i < 30; i++)
			{
				int global = vaultPage * 30 + i;
				if (global < total)
				{
					VaultEntry entry = vaultEntries[global];
					ItemAsset asset = Assets.find(EAssetType.ITEM, entry.Jar.item.id) as ItemAsset;
					string name = asset != null && !string.IsNullOrEmpty(asset.itemName) ? asset.itemName : "Item " + entry.Jar.item.id;
					string text = ColorName(asset, name) + StateSuffix(asset, entry.Jar.item.state) + "\nID " + entry.Jar.item.id + " x" + entry.Jar.item.amount;
					EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "VaultSlot" + i, true);
					EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "VaultSlot" + i + "_Label", text);
					string iconUrl = ItemIconUrl(entry.Jar.item.id);
					if (!string.IsNullOrEmpty(iconUrl) && !sentVaultIconUrls.Contains(iconUrl))
					{
						pendingIconKeys.Add("VaultSlot" + i + "_Icon");
						pendingIconUrls.Add(iconUrl);
					}
				}
				else
				{
					EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "VaultSlot" + i, true);
					EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "VaultSlot" + i + "_Label", "<color=#64748BFF>Vazio</color>");
					EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "VaultSlot" + i + "_Icon", false);
				}
			}

			QueueVaultIcons();

			int deposited = editKit?.Items?.Count ?? 0;
			EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "VaultCount",
				"INVENTÁRIO  |  Depositados no kit: " + deposited + "  |  Página " + (vaultPage + 1) + "/" + pageCount + "  (clique: 1 unidade para o kit)");
		}

		private void RenderVaultKit()
		{
			pendingIconKeys.Clear();
			pendingIconUrls.Clear();
			List<KitItem> kitItems = editKit?.Items ?? new List<KitItem>();
			int total = kitItems.Count;
			int pageCount = Math.Max(1, (total + 29) / 30);
			vaultPage = Math.Max(0, Math.Min(vaultPage, pageCount - 1));

			for (int i = 0; i < 30; i++)
			{
				int global = vaultPage * 30 + i;
				if (global < total)
				{
					KitItem kitItem = kitItems[global];
					ItemAsset asset = Assets.find(EAssetType.ITEM, kitItem.ItemID) as ItemAsset;
					string name = asset != null && !string.IsNullOrEmpty(asset.itemName) ? asset.itemName : "Item " + kitItem.ItemID;
					string text = ColorName(asset, name) + StateSuffix(asset, kitItem.StateBytes) + "\nID " + kitItem.ItemID + " x" + kitItem.Amount;
					EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "VaultSlot" + i, true);
					EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "VaultSlot" + i + "_Label", text);
					string iconUrl = ItemIconUrl(kitItem.ItemID);
					if (!string.IsNullOrEmpty(iconUrl) && !sentVaultIconUrls.Contains(iconUrl))
					{
						pendingIconKeys.Add("VaultSlot" + i + "_Icon");
						pendingIconUrls.Add(iconUrl);
					}
				}
				else
				{
					EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "VaultSlot" + i, true);
					EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "VaultSlot" + i + "_Label", "<color=#64748BFF>Vazio</color>");
					EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "VaultSlot" + i + "_Icon", false);
				}
			}

			QueueVaultIcons();

			EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "VaultCount",
				"NO KIT  |  " + total + " stack(s)  |  Página " + (vaultPage + 1) + "/" + pageCount);
		}

		private static string StateSuffix(ItemAsset asset, byte[] state)
		{
			if (state == null || state.Length < 10 || asset == null || asset.type != EItemType.GUN)
			{
				return "";
			}
			int count = 0;
			for (int i = 0; i < 5; i++)
			{
				if (BitConverter.ToUInt16(state, i * 2) != 0)
				{
					count++;
				}
			}
			return count > 0 ? "  <color=#38BDF8FF>+" + count + " ac.</color>" : "";
		}

		private static string ColorName(ItemAsset asset, string name)
		{
			if (asset == null)
			{
				return name;
			}
			return "<color=#" + ColorUtility.ToHtmlStringRGB(ItemTool.getRarityColorUI(asset.rarity)) + ">" + name + "</color>";
		}

		private void DepositVaultItem(string buttonName)
		{
			if (vaultTab == 1)
			{
				ReturnVaultItem(buttonName);
				return;
			}

			int slot = int.Parse(buttonName.Substring("VaultSlot".Length));
			int global = vaultPage * 30 + slot;
			if (global >= vaultEntries.Count)
			{
				return;
			}

			VaultEntry entry = vaultEntries[global];
			Item item = entry.Jar.item;
			ItemAsset asset = Assets.find(EAssetType.ITEM, item.id) as ItemAsset;

			byte[] state = item.state;
			bool wholeStack = asset != null && asset.type == EItemType.MAGAZINE;
			int depositAmount = wholeStack ? Math.Max(1, (int)item.amount) : 1;

			if (wholeStack)
			{
				if (Owner.equipment.equippedPage == entry.Page)
				{
					byte width = Owner.inventory.getWidth(entry.Page);
					byte x = (byte)(entry.Index % width);
					byte y = (byte)(entry.Index / width);
					if (Owner.equipment.equipped_x == x && Owner.equipment.equipped_y == y)
					{
						Owner.equipment.dequip();
					}
				}

				Owner.inventory.removeItem(entry.Page, entry.Index);
			}
			else if (item.amount > 1)
			{
				item.amount -= 1;
				Owner.inventory.updateAmount(entry.Page, entry.Index, item.amount);
			}
			else
			{
				if (Owner.equipment.equippedPage == entry.Page)
				{
					byte width = Owner.inventory.getWidth(entry.Page);
					byte x = (byte)(entry.Index % width);
					byte y = (byte)(entry.Index / width);
					if (Owner.equipment.equipped_x == x && Owner.equipment.equipped_y == y)
					{
						Owner.equipment.dequip();
					}
				}

				Owner.inventory.removeItem(entry.Page, entry.Index);
			}

			if (editKit.Items == null)
			{
				editKit.Items = new List<KitItem>();
			}
			string stateB64 = (state != null && state.Length > 0) ? Convert.ToBase64String(state) : null;
			KitItem existing = editKit.Items.FirstOrDefault(k => k.ItemID == item.id && string.Equals(k.State, stateB64));
			if (existing != null)
			{
				existing.Amount = (byte)Math.Min(255, existing.Amount + depositAmount);
			}
			else
			{
				editKit.Items.Add(new KitItem { ItemID = item.id, Amount = (byte)depositAmount, State = stateB64 });
			}
			itemsText = string.Join(",", editKit.Items.Select(k => k.ItemID + "x" + k.Amount));

			string name = asset != null && !string.IsNullOrEmpty(asset.itemName) ? asset.itemName : "Item " + item.id;
			UnturnedChat.Say(UnturnedPlayer.FromSteamPlayer(Owner.channel.owner), plugin.Translate("kit_vault_depositado", name, item.id, depositAmount));

			RenderVault();
		}

		private void ReturnVaultItem(string buttonName)
		{
			int slot = int.Parse(buttonName.Substring("VaultSlot".Length));
			int global = vaultPage * 30 + slot;
			List<KitItem> kitItems = editKit?.Items;
			if (kitItems == null || global >= kitItems.Count)
			{
				return;
			}

			KitItem kitItem = kitItems[global];
			ItemAsset asset = Assets.find(EAssetType.ITEM, kitItem.ItemID) as ItemAsset;
			if (asset == null)
			{
				return;
			}

			bool wholeStack = asset.type == EItemType.MAGAZINE;
			int returnAmount = wholeStack ? Math.Max(1, (int)kitItem.Amount) : 1;

			if (!Owner.inventory.tryAddItem(new Item(kitItem.ItemID, (byte)returnAmount, 100, kitItem.StateBytes), false))
			{
				UnturnedChat.Say(UnturnedPlayer.FromSteamPlayer(Owner.channel.owner), plugin.Translate("kit_vault_cheio"));
				return;
			}

			kitItem.Amount = (byte)Math.Max(0, kitItem.Amount - returnAmount);
			if (kitItem.Amount <= 0)
			{
				kitItems.Remove(kitItem);
			}
			itemsText = string.Join(",", kitItems.Select(k => k.ItemID + "x" + k.Amount));

			string name = !string.IsNullOrEmpty(asset.itemName) ? asset.itemName : "Item " + kitItem.ItemID;
			UnturnedChat.Say(UnturnedPlayer.FromSteamPlayer(Owner.channel.owner), plugin.Translate("kit_vault_devolvido", name, kitItem.ItemID, returnAmount));

			RenderVault();
		}

		private void AddEquippedItemToKit()
		{
			ItemAsset equipped = Owner.equipment.asset;
			if (equipped == null)
			{
				UnturnedChat.Say(UnturnedPlayer.FromSteamPlayer(Owner.channel.owner), plugin.Translate("kit_ui_mao_vazia"));
				return;
			}

			if (itemsText == null)
			{
				itemsText = "";
			}
			if (itemsText.Length > 0 && !itemsText.EndsWith(","))
			{
				itemsText += ",";
			}
			itemsText += equipped.id + "x1";
			EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "KitItems", itemsText);
			UnturnedChat.Say(UnturnedPlayer.FromSteamPlayer(Owner.channel.owner), plugin.Translate("kit_ui_mao_ok", equipped.id));
		}

		private void OpenEditor(Kit kit)
		{
			editingSource = kit;
			editKit = kit == null
				? new Kit { Name = "", CooldownSeconds = 0, Priority = 0, Items = new List<KitItem>() }
				: CloneKit(kit);
			itemsText = kit == null ? "" : string.Join(",", kit.Items.Select(i => i.ItemID + "x" + i.Amount));

			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "KitsUIPanel", false);
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "Create/Edit (Kit)", true);
			EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "KitEditorTitle",
				kit == null ? plugin.Translate("kit_ui_titulo_criar") : plugin.Translate("kit_ui_titulo_editar", kit.Name));
			SendEditorFields();
		}

		private void SendEditorFields()
		{
			EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "KitName", editKit.Name);
			EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "KitItems", itemsText);
			EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "KitCooldown", editKit.CooldownSeconds.ToString());
			EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "KitPriority", editKit.Priority.ToString());
			EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "KitPerm", editKit.Permission ?? "");
			EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, "KitIcon", editKit.IconUrl ?? "");
		}

		private void SaveEditor()
		{
			if (editKit == null)
			{
				return;
			}

			UnturnedPlayer uplayer = UnturnedPlayer.FromSteamPlayer(Owner.channel.owner);

			string name = editKit.Name?.Trim() ?? "";
			if (name.Length == 0)
			{
				UnturnedChat.Say(uplayer, plugin.Translate("kit_ui_nome_vazio"));
				return;
			}

			Kit existing = plugin.FindKit(name);
			if (existing != null && !ReferenceEquals(existing, editingSource))
			{
				UnturnedChat.Say(uplayer, plugin.Translate("kit_add_duplicado", name));
				return;
			}

			if (editKit.CooldownSeconds < 0)
			{
				UnturnedChat.Say(uplayer, plugin.Translate("kit_ui_cooldown_invalido"));
				return;
			}

			List<KitItem> parsedItems = ParseItemsText(itemsText, out string itemError);
			if (parsedItems == null)
			{
				UnturnedChat.Say(uplayer, plugin.Translate("kit_ui_item_invalido", itemError));
				return;
			}

			Dictionary<ushort, Queue<string>> statesById = new Dictionary<ushort, Queue<string>>();
			foreach (KitItem old in editKit.Items)
			{
				if (string.IsNullOrEmpty(old.State))
				{
					continue;
				}
				if (!statesById.TryGetValue(old.ItemID, out Queue<string> queue))
				{
					queue = new Queue<string>();
					statesById[old.ItemID] = queue;
				}
				queue.Enqueue(old.State);
			}
			foreach (KitItem parsed in parsedItems)
			{
				if (!string.IsNullOrEmpty(parsed.State))
				{
					continue;
				}
				if (statesById.TryGetValue(parsed.ItemID, out Queue<string> queue2) && queue2.Count > 0)
				{
					parsed.State = queue2.Dequeue();
				}
			}

			editKit.Name = name;
			editKit.Items = parsedItems;

			if (editingSource != null)
			{
				int index = plugin.Configuration.Instance.Kits.IndexOf(editingSource);
				if (index < 0)
				{
					editingSource = null;
					plugin.Configuration.Instance.Kits.Add(editKit);
				}
				else
				{
					plugin.Configuration.Instance.Kits[index] = editKit;
				}
			}
			else
			{
				plugin.Configuration.Instance.Kits.Add(editKit);
			}

			plugin.Configuration.Save();
			UnturnedChat.Say(uplayer, editingSource != null
				? plugin.Translate("kit_ui_salvo", editKit.Name)
				: plugin.Translate("kit_ui_criado", editKit.Name));
			Return();
			plugin.RefreshAllSessions();
		}

		private void DeleteKit(Kit kit)
		{
			plugin.Configuration.Instance.Kits.Remove(kit);
			plugin.Configuration.Save();
			UnturnedChat.Say(UnturnedPlayer.FromSteamPlayer(Owner.channel.owner), plugin.Translate("kit_ui_removido", kit.Name));
			UpdatePage();
			plugin.RefreshAllSessions();
		}

		private void Return()
		{
			editKit = null;
			editingSource = null;
			itemsText = null;
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "Create/Edit (Kit)", false);
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, "KitsUIPanel", true);
			UpdatePage();
		}

		private static Kit CloneKit(Kit source)
		{
			return new Kit
			{
				Name = source.Name,
				CooldownSeconds = source.CooldownSeconds,
				Priority = source.Priority,
				Permission = source.Permission,
				Items = source.Items.Select(i => new KitItem { ItemID = i.ItemID, Amount = i.Amount, State = i.State }).ToList()
			};
		}

		private static List<KitItem> ParseItemsText(string text, out string error)
		{
			error = null;
			List<KitItem> items = new List<KitItem>();
			if (string.IsNullOrWhiteSpace(text))
			{
				return items;
			}

			foreach (string part in text.Split(','))
			{
				string trimmed = part.Trim();
				if (trimmed.Length == 0)
				{
					continue;
				}

				string[] parts = trimmed.ToLower().Split('x');
				if (parts.Length < 1 || !ushort.TryParse(parts[0].Trim(), out ushort itemId) || Assets.find(EAssetType.ITEM, itemId) == null)
				{
					error = trimmed;
					return null;
				}

				byte amount = 1;
				if (parts.Length > 1 && (!byte.TryParse(parts[1].Trim(), out amount) || amount == 0))
				{
					error = trimmed;
					return null;
				}

				items.Add(new KitItem { ItemID = itemId, Amount = amount });
			}

			return items;
		}

		private static int[] MapVisibleSlots(int count)
		{
			if (count <= 0)
			{
				return Array.Empty<int>();
			}

			if (count <= 2)
			{
				int[] slots = new int[count];
				for (int i = 0; i < count; i++)
				{
					slots[i] = 2 + i;
				}
				return slots;
			}

			int[] result = new int[count];
			for (int i = 0; i < count; i++)
			{
				result[i] = i + 1;
			}
			return result;
		}

		private IEnumerator CooldownUpdater(int slotIndex, double remainingSeconds)
		{
			TimeSpan remaining = TimeSpan.FromSeconds(remainingSeconds);
			while (remaining.TotalSeconds > 0)
			{
				EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, $"Kit{slotIndex + 1}_Cooldown",
					remaining.ToString((remaining.Days > 0 ? "dd\\:" : "") + "hh\\:mm\\:ss"));
				yield return new WaitForSeconds(1f);
				remaining -= TimeSpan.FromSeconds(1);
			}

			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, $"Kit{slotIndex + 1}_CooldownIcon (1)", false);
			EffectManager.sendUIEffectText(UI_KEY, TransportConnection, true, $"Kit{slotIndex + 1}_Cooldown", plugin.Translate("ui_ready"));
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, $"Player_Kit{slotIndex + 1}_ClaimP", true);
			EffectManager.sendUIEffectVisibility(UI_KEY, TransportConnection, true, $"Player_Kit{slotIndex + 1}_ClaimP [DISABLED]", false);
			cooldownUpdaters[slotIndex] = null;
		}
	}
}
