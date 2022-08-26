using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;
using TopMostFriend.Languages;

namespace TopMostFriend {
    public static class Locale {
        public const string DEFAULT = @"en-GB";
        private static XmlSerializer Serializer { get; }
        private static Dictionary<string, Language> Languages { get; }
        private static Language ActiveLanguage { get; set; }

        static Locale() {
            Serializer = new XmlSerializer(typeof(Language));
            Languages = new Dictionary<string, Language>();
            Assembly currentAsm = Assembly.GetExecutingAssembly();
            string[] resources = currentAsm.GetManifestResourceNames();

            foreach(string resource in resources)
                if(resource.StartsWith(@"TopMostFriend.Languages.") && resource.EndsWith(@".xml"))
                    using(Stream resourceStream = currentAsm.GetManifestResourceStream(resource))
                        LoadLanguage(resourceStream);
        }

        public static string LoadLanguage(Stream stream) {
            Language lang = (Language)Serializer.Deserialize(stream);
            foreach(LanguageString ls in lang.Strings)
                ls.Value = ls.Value.Trim();

            Languages.Add(lang.Info.Id, lang);
            if(ActiveLanguage == null && DEFAULT.Equals(lang.Info.Id))
                ActiveLanguage = lang;

#if DEBUG
            Debug.WriteLine(@" ==========");
            Debug.WriteLine(lang.Info);
            foreach(LanguageString str in lang.Strings)
                Debug.WriteLine(str);
            Debug.WriteLine(string.Empty);
#endif

            return lang.Info.Id;
        }

        public static LanguageInfo GetCurrentLanguage() {
            return ActiveLanguage.Info;
        }

        public static LanguageInfo[] GetAvailableLanguages() {
            return Languages.Values.Select(l => l.Info).ToArray();
        }

        public static string GetPreferredLanguage() {
            return Settings.Has(Program.LANGUAGE)
                ? Settings.Get(Program.LANGUAGE, DEFAULT)
                : CultureInfo.InstalledUICulture.Name;
        }

        public static void SetLanguage(string langId) {
            if(!Languages.ContainsKey(langId))
                langId = DEFAULT;
            ActiveLanguage = Languages[langId];
        }

        public static void SetLanguage(LanguageInfo langInfo) {
            SetLanguage(langInfo.Id);
        }

        public static string String(string name, params object[] args) {
            LanguageString str = ActiveLanguage.GetString(name);
            if(str == null)
                return name;

            List<object> rargs = new List<object> { Program.TITLE };
            rargs.AddRange(args);

            return str.Format(rargs.ToArray());
        }

        public static void Meow() {
            Debug.WriteLine(@"meow");
        }
    }
}
