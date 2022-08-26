using System;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace TopMostFriend.Languages {
    [XmlRoot(@"Language")]
    public class Language {
        [XmlElement(@"Info")]
        public LanguageInfo Info { get; set; }

        [XmlArray(@"Strings")]
        [XmlArrayItem(@"String", Type = typeof(LanguageString))]
        public LanguageString[] Strings { get; set; }

        public LanguageString GetString(string name) {
            if(name == null)
                throw new ArgumentNullException(nameof(name));
            return Strings.FirstOrDefault(s => name.Equals(s.Name));
        }
    }
}
