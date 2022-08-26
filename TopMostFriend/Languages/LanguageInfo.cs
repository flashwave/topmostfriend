using System.Xml.Serialization;

namespace TopMostFriend.Languages {
    public class LanguageInfo {
        [XmlElement(@"Id")]
        public string Id { get; set; }

        [XmlElement(@"NameNative")]
        public string NameNative { get; set; }

        [XmlElement(@"NameEnglish")]
        public string NameEnglish { get; set; }

        [XmlElement(@"TargetVersion")]
        public string TargetVersion { get; set; }

        public override string ToString() {
            return $@"{NameNative} / {NameEnglish} ({Id})";
        }
    }
}
