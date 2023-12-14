using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using LabNation.DeviceInterface.Devices;
using ESuite.Drawables;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;

namespace ESuite
{
    internal partial class UIHandler
    {
        internal void SaveProbesToFile(List<Probe> probesToSave)
        {
            var serializer = new DataContractSerializer(typeof(List<Probe>));
            using (var sw = new System.IO.StreamWriter(ProbesFilename()))
            {
                using (var writer = new XmlTextWriter(sw))
                {
                    writer.Formatting = Formatting.Indented; // indent the Xml so it's human readable
                    serializer.WriteObject(writer, probesToSave);
                    writer.Flush();
                }
                sw.Close();
            }
        }

        internal List<Probe> LoadProbesFromFile()
        {
            var serializer = new DataContractSerializer(typeof(List<Probe>));
            try
            {
                using (var sw = new System.IO.StreamReader(ProbesFilename()))
                {
                    return (List<Probe>)(serializer.ReadObject(sw.BaseStream));
                }
            }
            catch (Exception)
            {
                LabNation.Common.Logger.Warn("Failed to load probes from file");
                return new List<Probe>();
            }
        }

        private static string ProbesFilename()
        {
            return Path.Combine(LabNation.Common.Utils.ApplicationDataPath, "probes.xml");
        }
    }        
}
