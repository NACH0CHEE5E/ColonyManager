namespace Nach0.ColonyManagement.localization
{
    public class LocalizationHelper
    {
        public string Prefix { get; set; }

        public LocalizationHelper(string prefix)
        {
            this.Prefix = prefix;
        }

        public string LocalizeOrDefault(string key, Players.Player p)
        {
            string localizationKey = this.GetLocalizationKey(key);
            string sentence = Localization.GetSentence(p.LastKnownLocale, localizationKey);
            if (sentence == localizationKey)
                return key;
            return sentence;
        }

        public string GetLocalizationKey(string key)
        {
            return "NACH0.ColonyManagement" + "." + this.Prefix + "." + key;
        }
    }
}
