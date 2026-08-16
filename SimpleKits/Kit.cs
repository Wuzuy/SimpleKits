using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SimpleKits
{
	public class Kit
	{
		[XmlAttribute("Name")]
		public string Name { get; set; }

		[XmlAttribute("IconUrl")]
		public string IconUrl { get; set; }

		public int CooldownSeconds { get; set; }

		public string Permission { get; set; }

		public uint Priority { get; set; }

		[XmlArray("Items")]
		[XmlArrayItem("Item")]
		public List<KitItem> Items { get; set; } = new List<KitItem>();
	}

	public class KitItem
	{
		public ushort ItemID { get; set; }

		public byte Amount { get; set; } = 1;

		[XmlAttribute("State")]
		public string State { get; set; }

		[XmlIgnore]
		public byte[] StateBytes
		{
			get { return string.IsNullOrEmpty(State) ? null : Convert.FromBase64String(State); }
			set { State = (value == null || value.Length == 0) ? null : Convert.ToBase64String(value); }
		}
	}
}
