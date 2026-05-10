using System;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Monitorian.Core.Models
{
    [DataContract]
    public class BrightnessProfile
    {
        public BrightnessProfile() { }

        public BrightnessProfile(string name, IDictionary<string, int> values)
        {
            Name = name;
            Values = new Dictionary<string, int>(values ?? throw new ArgumentNullException(nameof(values)));
        }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public Dictionary<string, int> Values { get; set; } = new();

        [DataMember]
        public Dictionary<string, bool> SleepModeEnabledStates { get; set; } = new();
    }
}
