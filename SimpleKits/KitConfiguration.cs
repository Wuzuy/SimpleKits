using System.Collections.Generic;
using System.Xml.Serialization;
using Rocket.API;

namespace SimpleKits
{
	public class KitConfiguration : IRocketPluginConfiguration
	{
		public string BypassPermission { get; set; }

		public ushort EffectId { get; set; }

		public string ServerIconURL { get; set; }

		public string ItemIconUrlTemplate { get; set; } = "https://cdn.jsdelivr.net/gh/Akulation/vanilla-icons@main/icons/{0}.png";

		public bool VirtualVaultOnly { get; set; }

		[XmlArray("Kits")]
		[XmlArrayItem("Kit")]
		public List<Kit> Kits { get; set; } = new List<Kit>();

		public void LoadDefaults()
		{
			BypassPermission = "kits.admin";
			EffectId = 47501;
			ServerIconURL = "";
			Kits = new List<Kit>
			{
				new Kit
				{
					Name = "start",
					CooldownSeconds = 30,
					Permission = null,
					Priority = 0,
					Items = new List<KitItem>
					{
						new KitItem { ItemID = 95, Amount = 2 },
						new KitItem { ItemID = 393, Amount = 3 },
						new KitItem { ItemID = 394, Amount = 1 }
					}
				},
				new Kit
				{
					Name = "medic",
					CooldownSeconds = 60,
					Permission = "kits.medic",
					Priority = 1,
					Items = new List<KitItem>
					{
						new KitItem { ItemID = 95, Amount = 4 },
						new KitItem { ItemID = 394, Amount = 2 }
					}
				}
			};
		}
	}
}
