using System;
using System.Collections.Generic;
using System.IO;
using SDG.Framework.Modules;
using SDG.Unturned;
using Steamworks;
using UnityEngine;

namespace UnturnedItemIcons
{
	public class ItemIconExporterModule : IModuleNexus
	{
		public void initialize()
		{
			UnturnedLog.info("[UnturnedItemIcons] Modulo inicializado. Aguardando assets carregarem...");
			GameObject go = new GameObject("UnturnedItemIcons");
			UnityEngine.Object.DontDestroyOnLoad(go);
			go.AddComponent<IconCaptureDriver>();
		}

		public void shutdown()
		{
			GameObject go = GameObject.Find("UnturnedItemIcons");
			if (go != null)
			{
				UnityEngine.Object.Destroy(go);
			}
			UnturnedLog.info("[UnturnedItemIcons] Modulo descarregado.");
		}
	}

	public class IconCaptureDriver : MonoBehaviour
	{
		private readonly Queue<ItemAsset> pending = new Queue<ItemAsset>();
		private readonly Dictionary<int, ItemAsset> byHandle = new Dictionary<int, ItemAsset>();
		private readonly Dictionary<int, float> handleDeadline = new Dictionary<int, float>();
		private readonly HashSet<ushort> processed = new HashSet<ushort>();
		private readonly Dictionary<ushort, int> attempts = new Dictionary<ushort, int>();
		private ItemAsset currentAsset;
		private bool started;
		private float nextCheck = 1f;
		private int exported;
		private int failed;
		private int lastKnownTotal = -1;
		private bool finishedLogged;

		private void Start()
		{
			if (!Dedicator.isDedicated)
			{
				ChatManager.onChatted += OnChatted;
			}
		}

		private bool toolChecked;

		private void EnsureItemTool()
		{
			if (toolChecked)
			{
				return;
			}
			toolChecked = true;

			if (UnityEngine.Object.FindObjectOfType<ItemTool>() != null)
			{
				return;
			}

			UnturnedLog.info("[UnturnedItemIcons] ItemTool nao encontrado - criando ferramenta de captura.");
			GameObject toolGo = new GameObject("ItemToolForExporter");
			toolGo.AddComponent<Camera>();
			Light light = toolGo.AddComponent<Light>();
			light.type = LightType.Directional;
			toolGo.AddComponent<ItemTool>();
		}

		private void OnDestroy()
		{
			if (!Dedicator.isDedicated)
			{
				ChatManager.onChatted -= OnChatted;
			}
		}

		private void OnChatted(SteamPlayer player, EChatMode mode, ref Color chatted, ref bool isRich, string text, ref bool isVisible)
		{
			if (text == null)
			{
				return;
			}

			string trimmed = text.Trim();
			if (string.Equals(trimmed, "/icones", StringComparison.OrdinalIgnoreCase) || string.Equals(trimmed, "!icones", StringComparison.OrdinalIgnoreCase) || string.Equals(trimmed, "icones", StringComparison.OrdinalIgnoreCase))
			{
				ForceRecapture();
			}
			else if (string.Equals(trimmed, "/icones-status", StringComparison.OrdinalIgnoreCase) || string.Equals(trimmed, "!icones-status", StringComparison.OrdinalIgnoreCase) || string.Equals(trimmed, "icones-status", StringComparison.OrdinalIgnoreCase))
			{
				UnturnedLog.info("[UnturnedItemIcons] Status: exportados=" + exported + " falhas=" + failed + " pendentes=" + pending.Count + " processados=" + processed.Count);
			}
		}

		private void ForceRecapture()
		{
			processed.Clear();
			attempts.Clear();
			finishedLogged = false;
			lastKnownTotal = -1;
			nextCheck = 0f;
			UnturnedLog.info("[UnturnedItemIcons] Recaptura completa forcada.");
		}

		private static bool IsAlreadyExported(ushort id)
		{
			return File.Exists(Path.Combine(ReadWrite.PATH, "Extras", "Items", "Official", id + ".png"));
		}

		private void Update()
		{
			if (!Dedicator.isDedicated && Input.GetKeyDown(KeyCode.F8))
			{
				ForceRecapture();
			}

			if (!Dedicator.isDedicated && Input.GetKeyDown(KeyCode.Escape) && Player.LocalPlayer != null && Player.LocalPlayer.inPluginModal)
			{
				EffectManager.sendEffectClicked("BExit");
			}

			if (handleDeadline.Count > 0)
			{
				List<int> expired = null;
				foreach (KeyValuePair<int, float> kv in handleDeadline)
				{
					if (Time.time > kv.Value)
					{
						if (expired == null)
						{
							expired = new List<int>();
						}
						expired.Add(kv.Key);
					}
				}
				if (expired != null)
				{
					foreach (int handle in expired)
					{
						handleDeadline.Remove(handle);
						if (!byHandle.TryGetValue(handle, out ItemAsset asset))
						{
							continue;
						}
						byHandle.Remove(handle);
						int tries = attempts.TryGetValue(asset.id, out int value) ? value : 0;
						if (tries < 3)
						{
							attempts[asset.id] = tries + 1;
							pending.Enqueue(asset);
						}
						else
						{
							failed++;
							UnturnedLog.warn("[UnturnedItemIcons] Item " + asset.id + " sem resposta (apos 3 tentativas).");
						}
					}
				}
			}

			if (pending.Count > 0)
			{
				EnsureItemTool();
				finishedLogged = false;
				for (int i = 0; i < 2 && pending.Count > 0; i++)
				{
					currentAsset = pending.Dequeue();
					int handle = ItemTool.getIcon(currentAsset.id, 0, 100, new byte[0], currentAsset, null, null, null,
						currentAsset.size_x * 50, currentAsset.size_y * 50, false, true, OnIconReady);
					byHandle[handle] = currentAsset;
					handleDeadline[handle] = Time.time + 120f;
				}
				return;
			}

			nextCheck -= Time.deltaTime;
			if (nextCheck > 0f)
			{
				return;
			}
			nextCheck = 5f;

			if (Assets.isLoading)
			{
				return;
			}

			Asset[] assets = Assets.find(EAssetType.ITEM);
			if (assets == null)
			{
				return;
			}

			if (assets.Length != lastKnownTotal)
			{
				lastKnownTotal = assets.Length;
				UnturnedLog.info("[UnturnedItemIcons] Itens disponiveis agora: " + assets.Length + " (exportados: " + exported + ", falhas: " + failed + ")");
			}

			int added = 0;
			int skipped = 0;
			foreach (Asset asset in assets)
			{
				if (asset is ItemAsset itemAsset && !itemAsset.isPro && processed.Add(itemAsset.id))
				{
					if (IsAlreadyExported(itemAsset.id))
					{
						skipped++;
					}
					else
					{
						pending.Enqueue(itemAsset);
						added++;
					}
				}
			}

			if (added > 0)
			{
				UnturnedLog.info("[UnturnedItemIcons] Novos itens detectados: " + added + " -> capturando...");
			}
			else if (skipped > 0)
			{
				UnturnedLog.info("[UnturnedItemIcons] Todos os " + skipped + " itens ja estao exportados - nada a capturar.");
			}
			else if (pending.Count == 0 && processed.Count > 0 && !finishedLogged)
			{
				finishedLogged = true;
				UnturnedLog.info("[UnturnedItemIcons] CONCLUIDO! Exportados: " + exported + " | Falhas: " + failed + " | Pasta: Extras/Items/Official");
			}

			if (!started)
			{
				started = true;
				UnturnedLog.info("[UnturnedItemIcons] Saida: " + Path.Combine(ReadWrite.PATH, "Extras", "Items", "Official"));
			}
		}

		private void OnIconReady(int handle, Texture2D texture)
		{
			ItemAsset asset = null;
			if (handle >= 0)
			{
				if (byHandle.TryGetValue(handle, out ItemAsset known))
				{
					asset = known;
				}
				byHandle.Remove(handle);
				handleDeadline.Remove(handle);
			}

			if (asset == null)
			{
				if (texture != null)
				{
					UnityEngine.Object.Destroy(texture);
				}
				return;
			}

			if (texture == null)
			{
				int tries = attempts.TryGetValue(asset.id, out int value) ? value : 0;
				if (tries < 3)
				{
					attempts[asset.id] = tries + 1;
					pending.Enqueue(asset);
					return;
				}

				failed++;
				UnturnedLog.warn("[UnturnedItemIcons] Icone nulo para o item " + asset.id + " (apos 3 tentativas).");
				return;
			}

			try
			{
				string dir = Path.Combine(ReadWrite.PATH, "Extras", "Items", "Official");
				Directory.CreateDirectory(dir);
				File.WriteAllBytes(Path.Combine(dir, asset.id + ".png"), texture.EncodeToPNG());
				UnityEngine.Object.Destroy(texture);
				exported++;
				if (exported % 50 == 0)
				{
					UnturnedLog.info("[UnturnedItemIcons] Progresso: " + exported + " exportados...");
				}
			}
			catch (Exception ex)
			{
				failed++;
				UnturnedLog.error("[UnturnedItemIcons] Falha ao salvar o item " + asset.id + ": " + ex.Message);
			}
		}
	}
}