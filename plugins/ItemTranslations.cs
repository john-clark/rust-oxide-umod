using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Item Translations", "Ryan", "1.0.0")]
    [Description("Provides a data file of item names for translation")]
    class ItemTranslations : RustPlugin
    {
        private StoredData sData;

        private class StoredData
        {
            public Dictionary<string, string> ItemTranslations = new Dictionary<string, string>();
        }

        private void OnServerInitialized()
        {
            var count = 0;
            foreach (var item in ItemManager.itemList)
            {
                if (!sData.ItemTranslations.ContainsKey(item.displayName.english))
                {
                    sData.ItemTranslations.Add(item.displayName.english, item.displayName.english);
                }
                else if (item.displayName.english != sData.ItemTranslations[item.displayName.english])
                {
                    item.displayName.english = sData.ItemTranslations[item.displayName.english];
                    count++;
                }
            }
            if(count != 0)
                PrintWarning($"Updated {count} item translations");
            Interface.Oxide.DataFileSystem.WriteObject(Name, sData);
        }

        private void Init()
        {
            sData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
        }
    }
}
