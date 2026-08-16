using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class KitUiBuilder
{
	private const string PrefabPath = "Assets/Effects/KitsUI/Effect.prefab";
	private const string BundleDir = "Build/Bundles";
	private const string StagingDir = "Build/Staging/Effects/KitsUI";
	private const string BundleName = "KitsUI.unity3d";
	private const string DatGuid = "49b531cd73b94bde82c54ff0d22d0ebc";
	private const ushort EffectId = 47501;

	private static Font font;
	private static Sprite uiSprite;

	[MenuItem("KitsUI/Build Effect Asset (server + workshop)")]
	public static void BuildMenu()
	{
		Build();
		Debug.Log("[KitsUI] Build concluido.");
	}

	public static void BuildAndStage()
	{
		Build();
		EditorApplication.Exit(0);
	}

	private static void Build()
	{
		EnsureDefaults();

		if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
		{
			AssetDatabase.DeleteAsset(PrefabPath);
		}

		GameObject root = new GameObject("Effect");
		Canvas canvas = root.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		CanvasScaler scaler = root.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.matchWidthOrHeight = 0.5f;
		root.AddComponent<GraphicRaycaster>();

		BuildHierarchy(root.transform);

		Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(PrefabPath)));
		PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
		Object.DestroyImmediate(root);

		string absBundleDir = Path.GetFullPath(BundleDir);
		Directory.CreateDirectory(absBundleDir);
		AssetBundleBuild[] builds =
		{
			new AssetBundleBuild
			{
				assetBundleName = BundleName,
				assetNames = new[] { PrefabPath }
			}
		};
		BuildPipeline.BuildAssetBundles(absBundleDir, builds, BuildAssetBundleOptions.DeterministicAssetBundle, BuildTarget.StandaloneWindows64);

		Stage();
		Debug.Log("[KitsUI] Asset gerado: " + Path.GetFullPath(StagingDir));
	}

	private static void Stage()
	{
		string staging = Path.GetFullPath(StagingDir);
		Directory.CreateDirectory(staging);
		File.Copy(Path.Combine(Path.GetFullPath(BundleDir), BundleName), Path.Combine(staging, BundleName), true);

		string dat =
			"GUID " + DatGuid + "\n" +
			"Type Effect\n" +
			"ID " + EffectId + "\n" +
			"\n" +
			"Lifetime 9999999999999\n" +
			"Exclude_From_Master_Bundle\n";
		File.WriteAllText(Path.Combine(staging, "KitsUI.dat"), dat, new System.Text.UTF8Encoding(false));
	}

	private static void EnsureDefaults()
	{
		font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		if (font == null)
		{
			font = Resources.GetBuiltinResource<Font>("Arial.ttf");
		}
		uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
	}

	private static void BuildHierarchy(Transform root)
	{
		RectTransform closeArea = CreateImage(root, "backgroundfalseexit", Hex("000000A0"), true,
			Anchors.StretchAll, Vector2.zero, Vector2.zero, true);
		closeArea.gameObject.AddComponent<Button>().targetGraphic = closeArea.GetComponent<Image>();

		RectTransform panel = CreateImage(root, "KitsUIPanel", Hex("0E1524F0"), true,
			new Anchors(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)), new Vector2(0f, 20f), new Vector2(1560f, 900f), false);

		CreateText(panel, "Title", "SIMPLE KITS", 50, Color.white, TextAnchor.MiddleCenter,
			Anchors.TopStretch, new Vector2(0f, -42f), new Vector2(0f, 60f));

		CreateImage(panel, "HeaderAccent", Hex("38BDF8FF"), false,
			Anchors.TopStretch, new Vector2(0f, -108f), new Vector2(0f, 4f), false);

		CreateImage(panel, "Icon", Hex("38BDF8FF"), false,
			Anchors.TopLeft, new Vector2(44f, -44f), new Vector2(80f, 80f), false);

		CreateImage(panel, "Icona", Hex("38BDF8FF"), false,
			Anchors.TopLeft, new Vector2(44f, -44f), new Vector2(80f, 80f), false).gameObject.SetActive(false);

		CreateButton(panel, "CKit", "+ NEW", Hex("0EA5E9FF"), 20,
			Anchors.TopRight, new Vector2(-226f, -44f), new Vector2(150f, 60f)).gameObject.SetActive(false);

		CreateGearButton(panel, "Settings", new Vector2(-360f, -44f), 60f);

		CreateButton(panel, "BExit", "CLOSE", Hex("EF4444FF"), 22,
			Anchors.TopRight, new Vector2(-48f, -44f), new Vector2(150f, 60f));

		CreateButton(panel, "Exit", "CLOSE", Hex("EF4444FF"), 22,
			Anchors.TopRight, new Vector2(-48f, -44f), new Vector2(150f, 60f)).gameObject.SetActive(false);

		CreateText(panel, "Index", "01", 26, Hex("94A3B8FF"), TextAnchor.MiddleCenter,
			new Anchors(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f)), new Vector2(0f, 48f), new Vector2(140f, 40f));

		CreateButton(panel, "PPage", "<", Hex("334155FF"), 26,
			Anchors.BottomLeft, new Vector2(48f, 28f), new Vector2(90f, 62f));

		CreateButton(panel, "NPage", ">", Hex("334155FF"), 26,
			Anchors.BottomRight, new Vector2(-48f, 28f), new Vector2(90f, 62f));

		float slotW = 320f;
		float slotH = 280f;
		float[] cols = { -500f, -166.7f, 166.7f, 500f };
		float[] rows = { 190f, -190f };

		BuildEditorPanel(root);
		BuildVaultPanel(root);
		BuildSettingsPanel(root);
		BuildPreviewPanel(root);

		for (int i = 1; i <= 8; i++)
		{
			RectTransform slot = CreateImage(panel, "Kit" + i, Hex("17223AFF"), true,
				new Anchors(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)),
				new Vector2(cols[(i - 1) % 4], rows[(i - 1) / 4]),
				new Vector2(slotW, slotH), false);

			CreateImage(slot, "Kit" + i + "_Cover", new Color(0f, 0f, 0f, 0.6f), true,
				Anchors.StretchAll, Vector2.zero, Vector2.zero, false);

			CreateText(slot, "Kit" + i + "_Title", "Kit " + i, 26, Color.white, TextAnchor.MiddleCenter,
				Anchors.TopStretch, new Vector2(0f, -10f), new Vector2(0f, 36f));

			CreateImage(slot, "Kit" + i + "_TitleAccent", Hex("38BDF8FF"), false,
				Anchors.TopStretch, new Vector2(0f, -50f), new Vector2(0f, 3f), false);

			CreateImage(slot, "Kit" + i + "_Icon", Color.white, false,
				Anchors.TopLeft, new Vector2(16f, -58f), new Vector2(88f, 88f), false);

			CreateText(slot, "Kit" + i + "_Price", "Contents...", 16, Hex("A8BFD9FF"), TextAnchor.UpperLeft,
				Anchors.TopLeft, new Vector2(116f, -58f), new Vector2(188f, 100f));

			CreateText(slot, "Kit" + i + "_Cooldown", "READY", 13, Hex("FBBF24FF"), TextAnchor.MiddleCenter,
				Anchors.BottomStretch, new Vector2(0f, 74f), new Vector2(0f, 24f));

			CreateImage(slot, "Kit" + i + "_CooldownIcon (1)", Hex("FBBF24FF"), false,
				Anchors.BottomCenter, new Vector2(140f, 74f), new Vector2(22f, 22f), false).gameObject.SetActive(false);

			RectTransform playerExt = CreateImage(slot, "Kit" + i + "_Extension [PLAYER]", Color.clear, false,
				Anchors.StretchAll, Vector2.zero, Vector2.zero, true);

			CreateButton(playerExt, "Player_Kit" + i + "_ClaimP", "CLAIM", Hex("059669FF"), 20,
				Anchors.BottomCenter, new Vector2(-85f, 10f), new Vector2(150f, 58f));

			RectTransform claimP = playerExt.Find("Player_Kit" + i + "_ClaimP") as RectTransform;
			Transform claimPLabel = claimP.Find("Label");
			if (claimPLabel != null)
			{
				claimPLabel.name = "Player_Kit" + i + "_ClaimP Label";
			}

			CreateButton(playerExt, "Player_Kit" + i + "_ClaimP [DISABLED]", "CLAIM", Hex("475569FF"), 20,
				Anchors.BottomCenter, new Vector2(-85f, 10f), new Vector2(150f, 58f));

			RectTransform claimD = playerExt.Find("Player_Kit" + i + "_ClaimP [DISABLED]") as RectTransform;
			Transform claimDLabel = claimD.Find("Label");
			if (claimDLabel != null)
			{
				claimDLabel.name = "Player_Kit" + i + "_ClaimP [DISABLED] Label";
			}
			claimD.gameObject.SetActive(false);

			CreateButton(playerExt, "Player_Kit" + i + "_Preview", "VISUALIZAR", Hex("334155FF"), 16,
				Anchors.BottomCenter, new Vector2(85f, 10f), new Vector2(150f, 58f));

			RectTransform adminExt = CreateImage(slot, "Kit" + i + "_Extension", Color.clear, false,
				Anchors.StretchAll, Vector2.zero, Vector2.zero, true);
			adminExt.gameObject.SetActive(false);

			CreateButton(adminExt, "ADM_Kit" + i + "_Claim", "CLAIM", Hex("16A34AFF"), 13,
				Anchors.BottomCenter, new Vector2(-110f, 10f), new Vector2(70f, 54f)).gameObject.SetActive(false);
			CreateButton(adminExt, "ADM_Kit" + i + "_Edit", "EDIT", Hex("0EA5E9FF"), 13,
				Anchors.BottomCenter, new Vector2(-37f, 10f), new Vector2(70f, 54f)).gameObject.SetActive(false);
			CreateButton(adminExt, "ADM_Kit" + i + "_Delete", "DELETE", Hex("EF4444FF"), 12,
				Anchors.BottomCenter, new Vector2(37f, 10f), new Vector2(70f, 54f)).gameObject.SetActive(false);
			CreateButton(adminExt, "ADM_Kit" + i + "_Preview", "VER", Hex("334155FF"), 13,
				Anchors.BottomCenter, new Vector2(110f, 10f), new Vector2(70f, 54f)).gameObject.SetActive(false);
		}
	}

	private static RectTransform CreateImage(Transform parent, string name, Color color, bool raycastTarget, Anchors anchors, Vector2 pos, Vector2 size, bool stretchMode)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.SetParent(parent, false);
		ApplyAnchors(rt, anchors, pos, size, stretchMode);
		Image img = go.GetComponent<Image>();
		img.sprite = uiSprite;
		img.color = color;
		img.raycastTarget = raycastTarget;
		img.type = Image.Type.Sliced;
		return rt;
	}

	private static void BuildEditorPanel(Transform root)
	{
		RectTransform editor = CreateImage(root, "Create/Edit (Kit)", Hex("0E1524F0"), true,
			new Anchors(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)), new Vector2(0f, 20f), new Vector2(1560f, 900f), false);
		editor.gameObject.SetActive(false);

		CreateText(editor, "KitEditorTitle", "Criando novo kit", 30, Color.white, TextAnchor.MiddleCenter,
			Anchors.TopStretch, new Vector2(0f, -26f), new Vector2(0f, 48f));

		CreateImage(editor, "EditorAccent", Hex("38BDF8FF"), false,
			Anchors.TopStretch, new Vector2(0f, -78f), new Vector2(0f, 3f), false);

		CreateField(editor, "Nome:", -118f);
		CreateInput(editor, "KitName", "Ex.: pvp", 22, new Vector2(0f, -118f), new Vector2(760f, 54f));

		CreateField(editor, "Itens (formato: IDxQtd separado por vírgula - Ex.: 95x2,393x3,394x1):", -212f);
		CreateInput(editor, "KitItems", "95x2,393x3,394x1", 22, new Vector2(0f, -212f), new Vector2(760f, 54f));

		CreateField(editor, "Cooldown em segundos (0 = sem cooldown):", -306f);
		CreateInput(editor, "KitCooldown", "60", 22, new Vector2(0f, -306f), new Vector2(760f, 54f));

		CreateField(editor, "Prioridade (maior aparece primeiro):", -400f);
		CreateInput(editor, "KitPriority", "1", 22, new Vector2(0f, -400f), new Vector2(760f, 54f));

		CreateField(editor, "Permissão necessária (deixe vazio = todos podem):", -494f);
		CreateInput(editor, "KitPerm", "", 22, new Vector2(0f, -494f), new Vector2(760f, 54f));

		CreateField(editor, "Ícone do kit (URL; vazio = ícone do 1º item):", -588f);
		CreateInput(editor, "KitIcon", "https://... (opcional)", 22, new Vector2(0f, -588f), new Vector2(760f, 54f));

		CreateButton(editor, "OpenVault", "ABRIR BAÚ", Hex("0EA5E9FF"), 20,
			Anchors.TopCenter, new Vector2(0f, -664f), new Vector2(760f, 54f));

		CreateButton(editor, "Save", "SALVAR", Hex("059669FF"), 22,
			Anchors.BottomCenter, new Vector2(-190f, 96f), new Vector2(360f, 60f));

		CreateButton(editor, "Cancel", "CANCELAR", Hex("EF4444FF"), 22,
			Anchors.BottomCenter, new Vector2(190f, 96f), new Vector2(360f, 60f));
	}

	private static void BuildVaultPanel(Transform root)
	{
		RectTransform vault = CreateImage(root, "Vault", Hex("0E1524F0"), true,
			new Anchors(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)), new Vector2(0f, 20f), new Vector2(1360f, 820f), false);
		vault.gameObject.SetActive(false);

		CreateText(vault, "VaultTitle", "BAÚ VIRTUAL - CLIQUE NOS ITENS DO INVENTÁRIO", 28, Color.white, TextAnchor.MiddleCenter,
			Anchors.TopStretch, new Vector2(0f, -28f), new Vector2(0f, 46f));

		CreateText(vault, "VaultCount", "Depositados no kit: 0", 18, Hex("94A3B8FF"), TextAnchor.MiddleCenter,
			Anchors.TopStretch, new Vector2(0f, -80f), new Vector2(0f, 28f));

		CreateButton(vault, "VaultTabInv", "INVENTÁRIO", Hex("0EA5E9FF"), 18,
			Anchors.TopCenter, new Vector2(-130f, -120f), new Vector2(240f, 50f));

		CreateButton(vault, "VaultTabKit", "NO KIT", Hex("334155FF"), 18,
			Anchors.TopCenter, new Vector2(130f, -120f), new Vector2(240f, 50f));

		float slotW = 200f;
		float slotH = 96f;
		float gap = 10f;
		float startX = -525f;
		float startY = -203f;
		for (int i = 0; i < 30; i++)
		{
			float col = i % 6;
			float row = i / 6;
			RectTransform slotBtn = CreateButton(vault, "VaultSlot" + i, "", Hex("1E2B47FF"), 14,
				Anchors.TopCenter, new Vector2(startX + col * (slotW + gap), startY - row * (slotH + gap)), new Vector2(slotW, slotH));

			CreateImage(slotBtn, "VaultSlot" + i + "_Icon", Color.white, false,
				Anchors.MiddleLeft, new Vector2(10f, 0f), new Vector2(64f, 64f), false);

			RectTransform labelRt = slotBtn.Find("Label") as RectTransform;
			labelRt.name = "VaultSlot" + i + "_Label";
			labelRt.anchorMin = new Vector2(0f, 0f);
			labelRt.anchorMax = new Vector2(1f, 1f);
			labelRt.pivot = new Vector2(0.5f, 0.5f);
			labelRt.offsetMin = new Vector2(86f, 8f);
			labelRt.offsetMax = new Vector2(-12f, -8f);
			Text slotLabel = labelRt.GetComponent<Text>();
			slotLabel.fontSize = 14;
			slotLabel.alignment = TextAnchor.MiddleLeft;
		}

		RectTransform vaultFooter = CreateImage(vault, "VaultFooter", Hex("0A101EF0"), true,
			Anchors.BottomStretch, Vector2.zero, new Vector2(0f, 110f), false);
		vaultFooter.anchorMin = new Vector2(0f, 0f);
		vaultFooter.anchorMax = new Vector2(1f, 0f);
		vaultFooter.pivot = new Vector2(0.5f, 0f);
		vaultFooter.anchoredPosition = Vector2.zero;
		vaultFooter.sizeDelta = new Vector2(0f, 110f);

		CreateImage(vaultFooter, "VaultFooterAccent", Hex("38BDF8FF"), false,
			Anchors.TopStretch, new Vector2(0f, 0f), new Vector2(0f, 2f), false);

		CreateButton(vaultFooter, "VaultPrev", "<", Hex("334155FF"), 26,
			Anchors.BottomCenter, new Vector2(-440f, 34f), new Vector2(150f, 54f));

		CreateButton(vaultFooter, "VaultNext", ">", Hex("334155FF"), 26,
			Anchors.BottomCenter, new Vector2(440f, 34f), new Vector2(150f, 54f));

		CreateButton(vaultFooter, "VaultClose", "FECHAR BAU E VOLTAR AO EDITOR", Hex("059669FF"), 19,
			Anchors.BottomCenter, new Vector2(0f, 34f), new Vector2(820f, 54f));
	}

	private static void BuildSettingsPanel(Transform root)
	{
		RectTransform settings = CreateImage(root, "SettingsPanel", Hex("0E1524F0"), true,
			new Anchors(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)), new Vector2(0f, 20f), new Vector2(1560f, 900f), false);
		settings.gameObject.SetActive(false);

		CreateText(settings, "SettingsTitle", "CONFIGURAÇÕES", 50, Color.white, TextAnchor.MiddleCenter,
			Anchors.TopStretch, new Vector2(0f, -42f), new Vector2(0f, 60f));

		CreateImage(settings, "SettingsAccent", Hex("38BDF8FF"), false,
			Anchors.TopStretch, new Vector2(0f, -108f), new Vector2(0f, 4f), false);

		CreateButton(settings, "SettingsClose", "FECHAR", Hex("EF4444FF"), 22,
			Anchors.TopRight, new Vector2(-48f, -44f), new Vector2(150f, 60f));

		CreateText(settings, "SettingsLabelAuto", "AUTO-EQUIP", 26, Color.white, TextAnchor.MiddleLeft,
			Anchors.TopLeft, new Vector2(180f, -240f), new Vector2(360f, 64f));

		RectTransform setAuto = CreateButton(settings, "SetAutoEquip", "ON", Hex("0EA5E9FF"), 24,
			Anchors.TopCenter, new Vector2(-200f, -240f), new Vector2(200f, 64f));
		setAuto.Find("Label").name = "SetAutoEquipLabel";

		CreateButton(settings, "InfoAutoEquip", "?", Hex("334155FF"), 28,
			Anchors.TopCenter, new Vector2(40f, -240f), new Vector2(64f, 64f));

		CreateText(settings, "SettingsLabelOverflow", "PEGAR SEM ESPAÇO", 26, Color.white, TextAnchor.MiddleLeft,
			Anchors.TopLeft, new Vector2(180f, -340f), new Vector2(360f, 64f));

		RectTransform setOverflow = CreateButton(settings, "SetOverflow", "OFF", Hex("475569FF"), 24,
			Anchors.TopCenter, new Vector2(-200f, -340f), new Vector2(200f, 64f));
		setOverflow.Find("Label").name = "SetOverflowLabel";

		CreateButton(settings, "InfoOverflow", "?", Hex("334155FF"), 28,
			Anchors.TopCenter, new Vector2(40f, -340f), new Vector2(64f, 64f));

		RectTransform tipInfo = CreateImage(settings, "TipInfo", Hex("101A30FF"), false,
			Anchors.BottomCenter, new Vector2(0f, 64f), new Vector2(720f, 170f), false);
		tipInfo.gameObject.SetActive(false);

		CreateImage(tipInfo, "TipInfoAccent", Hex("38BDF8FF"), false,
			Anchors.TopStretch, new Vector2(0f, 0f), new Vector2(0f, 3f), false);

		RectTransform tipText = CreateText(tipInfo, "TipInfoText", "", 17, Color.white, TextAnchor.MiddleLeft,
			Anchors.StretchAll, new Vector2(20f, -10f), new Vector2(-40f, -20f));
		tipText.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Wrap;
		tipText.GetComponent<Text>().verticalOverflow = VerticalWrapMode.Overflow;
	}

	private static void BuildPreviewPanel(Transform root)
	{
		RectTransform preview = CreateImage(root, "PreviewPanel", Hex("0E1524F0"), true,
			new Anchors(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)), new Vector2(0f, 20f), new Vector2(1360f, 820f), false);
		preview.gameObject.SetActive(false);

		CreateText(preview, "PreviewTitle", "VISUALIZAR", 30, Color.white, TextAnchor.MiddleCenter,
			Anchors.TopStretch, new Vector2(0f, -28f), new Vector2(0f, 46f));

		CreateText(preview, "PreviewHint", "Clique com o botão DIREITO em VISUALIZAR para ver os acessórios das armas (mira, pente, cano, etc.).", 16, Hex("94A3B8FF"), TextAnchor.MiddleCenter,
			Anchors.TopStretch, new Vector2(0f, -82f), new Vector2(0f, 26f));

		CreateButton(preview, "PreviewClose", "FECHAR", Hex("EF4444FF"), 20,
			Anchors.TopRight, new Vector2(-48f, -28f), new Vector2(150f, 54f));

		float slotW = 200f;
		float slotH = 96f;
		float gap = 10f;
		float startX = -525f;
		float startY = -160f;
		for (int i = 0; i < 30; i++)
		{
			float col = i % 6;
			float row = i / 6;
			RectTransform slotBtn = CreateButton(preview, "PreviewSlot" + i, "", Hex("1E2B47FF"), 14,
				Anchors.TopCenter, new Vector2(startX + col * (slotW + gap), startY - row * (slotH + gap)), new Vector2(slotW, slotH));

			CreateImage(slotBtn, "PreviewSlot" + i + "_Icon", Color.white, false,
				Anchors.MiddleLeft, new Vector2(10f, 0f), new Vector2(64f, 64f), false);

			RectTransform labelRt = slotBtn.Find("Label") as RectTransform;
			labelRt.name = "PreviewSlot" + i + "_Label";
			labelRt.anchorMin = new Vector2(0f, 0f);
			labelRt.anchorMax = new Vector2(1f, 1f);
			labelRt.pivot = new Vector2(0.5f, 0.5f);
			labelRt.offsetMin = new Vector2(86f, 8f);
			labelRt.offsetMax = new Vector2(-12f, -8f);
			Text slotLabel = labelRt.GetComponent<Text>();
			slotLabel.fontSize = 14;
			slotLabel.alignment = TextAnchor.MiddleLeft;
		}

		RectTransform details = CreateImage(preview, "PreviewDetailsPanel", Hex("0A101EF0"), true,
			Anchors.BottomStretch, Vector2.zero, new Vector2(0f, 140f), false);
		details.anchorMin = new Vector2(0f, 0f);
		details.anchorMax = new Vector2(1f, 0f);
		details.pivot = new Vector2(0.5f, 0f);
		details.anchoredPosition = Vector2.zero;
		details.sizeDelta = new Vector2(0f, 140f);
		details.gameObject.SetActive(false);

		CreateImage(details, "PreviewDetailsAccent", Hex("38BDF8FF"), false,
			Anchors.TopStretch, new Vector2(0f, 0f), new Vector2(0f, 2f), false);

		CreateText(details, "PreviewDetails", "Detalhes...", 15, Color.white, TextAnchor.MiddleLeft,
			Anchors.StretchAll, new Vector2(20f, -10f), new Vector2(-20f, -10f)).GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Wrap;
	}

	private static void CreateGearButton(Transform parent, string name, Vector2 pos, float size)
	{
		RectTransform rt = CreateImage(parent, name, Color.white, true,
			Anchors.TopRight, pos, new Vector2(size, size), false);

		Sprite gear = CreateGearSprite(64, Hex("D1D5DBFF"));
		Image img = rt.GetComponent<Image>();
		img.sprite = gear;
		img.type = Image.Type.Simple;
		img.preserveAspect = true;

		Button button = rt.gameObject.AddComponent<Button>();
		button.targetGraphic = img;
		ColorBlock colors = button.colors;
		colors.highlightedColor = Color.white;
		colors.pressedColor = Hex("9CA3AFFF");
		button.colors = colors;
	}

	private static Sprite CreateGearSprite(int size, Color color)
	{
		Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
		Color[] pixels = new Color[size * size];
		Color transparent = new Color(0f, 0f, 0f, 0f);
		float c = (size - 1) * 0.5f;
		float outer = c * 0.78f;
		float body = c * 0.60f;
		float hole = c * 0.20f;
		float toothH = c * 0.18f;
		float toothW = 0.30f;

		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float dx = x - c;
				float dy = y - c;
				float d = Mathf.Sqrt(dx * dx + dy * dy);
				bool solid = false;
				if (d <= outer && d >= hole)
				{
					if (d <= body)
					{
						solid = true;
					}
					else
					{
						float angle = Mathf.Atan2(dy, dx);
						float lobe = Mathf.Abs(Mathf.Cos(angle * 8f));
						solid = d >= body - toothH * 0.4f && lobe > toothW;
					}
				}
				pixels[y * size + x] = solid ? color : transparent;
			}
		}

		tex.SetPixels(pixels);
		tex.Apply();
		Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
		sprite.name = "GearIcon";
		return sprite;
	}

	private static void CreateField(Transform parent, string text, float yPos)
	{
		CreateText(parent, "KitEditorLabel", text, 20, Hex("94A3B8FF"), TextAnchor.MiddleRight,
			Anchors.TopLeft, new Vector2(20f, yPos), new Vector2(380f, 60f));
	}

	private static RectTransform CreateInput(Transform parent, string name, string placeholder, int fontSize, Vector2 pos, Vector2 size)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.SetParent(parent, false);
		ApplyAnchors(rt, Anchors.TopCenter, pos, size, true);

		Image bg = go.GetComponent<Image>();
		bg.sprite = uiSprite;
		bg.color = Hex("101A30FF");
		bg.type = Image.Type.Sliced;

		Text text = CreateFieldText(rt, "Text", fontSize, Color.white);
		InputField input = go.AddComponent<InputField>();
		input.textComponent = text;
		input.lineType = InputField.LineType.SingleLine;

		Text placeholderText = CreateFieldText(rt, "Placeholder", fontSize, Hex("5B6B85FF"));
		placeholderText.text = placeholder;
		input.placeholder = placeholderText;
		return rt;
	}

	private static Text CreateFieldText(Transform parent, string name, int fontSize, Color color)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.SetParent(parent, false);
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = new Vector2(12f, 4f);
		rt.offsetMax = new Vector2(-12f, -4f);
		Text t = go.GetComponent<Text>();
		t.font = font;
		t.fontSize = fontSize;
		t.fontStyle = FontStyle.Bold;
		t.alignment = TextAnchor.MiddleLeft;
		t.color = color;
		t.raycastTarget = false;
		t.horizontalOverflow = HorizontalWrapMode.Overflow;
		t.verticalOverflow = VerticalWrapMode.Overflow;
		return t;
	}

	private static RectTransform CreateText(Transform parent, string name, string value, int size, Color color, TextAnchor alignment, Anchors anchors, Vector2 pos, Vector2 sizeDelta)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.SetParent(parent, false);
		ApplyAnchors(rt, anchors, pos, sizeDelta, true);
		Text text = go.GetComponent<Text>();
		text.font = font;
		text.fontSize = size;
		text.fontStyle = FontStyle.Bold;
		text.alignment = alignment;
		text.color = color;
		text.text = value;
		text.raycastTarget = false;
		text.supportRichText = true;
		return rt;
	}

	private static RectTransform CreateButton(Transform parent, string name, string label, Color color, int size, Anchors anchors, Vector2 pos, Vector2 sizeDelta)
	{
		RectTransform bg = CreateImage(parent, name, color, true, anchors, pos, sizeDelta, true);
		Button button = bg.gameObject.AddComponent<Button>();
		button.targetGraphic = bg.GetComponent<Image>();
		ColorBlock colors = button.colors;
		colors.highlightedColor = new Color(color.r * 1.15f, color.g * 1.15f, color.b * 1.15f, color.a);
		colors.pressedColor = new Color(color.r * 0.85f, color.g * 0.85f, color.b * 0.85f, color.a);
		button.colors = colors;

		GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
		RectTransform labelRt = labelGo.GetComponent<RectTransform>();
		labelRt.SetParent(bg, false);
		labelRt.anchorMin = Vector2.zero;
		labelRt.anchorMax = Vector2.one;
		labelRt.offsetMin = Vector2.zero;
		labelRt.offsetMax = Vector2.zero;
		Text text = labelGo.GetComponent<Text>();
		text.font = font;
		text.fontSize = size;
		text.fontStyle = FontStyle.Bold;
		text.alignment = TextAnchor.MiddleCenter;
		text.color = Color.white;
		text.text = label;
		text.raycastTarget = false;
		text.horizontalOverflow = HorizontalWrapMode.Overflow;
		text.verticalOverflow = VerticalWrapMode.Overflow;
		return bg;
	}

	private static void ApplyAnchors(RectTransform rt, Anchors anchors, Vector2 pos, Vector2 size, bool stretchMode)
	{
		rt.anchorMin = anchors.min;
		rt.anchorMax = anchors.max;
		rt.pivot = anchors.pivot;
		if (stretchMode)
		{
			rt.sizeDelta = size;
			rt.anchoredPosition = pos;
		}
		else
		{
			rt.sizeDelta = size;
			rt.anchoredPosition = pos;
		}
	}

	private static Color Hex(string hex)
	{
		byte r = System.Convert.ToByte(hex.Substring(0, 2), 16);
		byte g = System.Convert.ToByte(hex.Substring(2, 2), 16);
		byte b = System.Convert.ToByte(hex.Substring(4, 2), 16);
		byte a = hex.Length >= 8 ? System.Convert.ToByte(hex.Substring(6, 2), 16) : (byte)255;
		return new Color32(r, g, b, a);
	}

	private struct Anchors
	{
		public Vector2 min;
		public Vector2 max;
		public Vector2 pivot;

		public Anchors(Vector2 min, Vector2 max)
		{
			this.min = min;
			this.max = max;
			pivot = (min + max) * 0.5f;
		}

		public static Anchors TopLeft => new Anchors(new Vector2(0f, 1f), new Vector2(0f, 1f));
		public static Anchors TopRight => new Anchors(new Vector2(1f, 1f), new Vector2(1f, 1f));
		public static Anchors TopCenter => new Anchors(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
		public static Anchors TopStretch => new Anchors(new Vector2(0f, 1f), new Vector2(1f, 1f));
		public static Anchors MiddleCenter => new Anchors(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
		public static Anchors MiddleLeft => new Anchors(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
		public static Anchors BottomLeft => new Anchors(new Vector2(0f, 0f), new Vector2(0f, 0f));
		public static Anchors BottomRight => new Anchors(new Vector2(1f, 0f), new Vector2(1f, 0f));
		public static Anchors BottomCenter => new Anchors(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
		public static Anchors BottomStretch => new Anchors(new Vector2(0f, 0f), new Vector2(1f, 0f));
		public static Anchors StretchAll => new Anchors(Vector2.zero, Vector2.one);
	}
}
