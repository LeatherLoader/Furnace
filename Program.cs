using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityAssetsLib;
using UnityAssetsLib.FileTypes;
using UnityAssetsLib.ObjTypes;

namespace Furnace
{
    class Program
    {
        static void Main(string[] args)
        {
            DoIt();
            Console.WriteLine("Press any key to end...");
            Console.ReadKey();
        }

        static void DoIt()
        {
            if (!File.Exists("mainData"))
            {
                Console.WriteLine("Could not locate mainData file.");
                return;
            }
            SwappableEndianBinaryReader reader = new SwappableEndianBinaryReader(File.OpenRead("mainData"));
            AssetsFile file = new AssetsFile();
            file.Read(reader);
            reader.Close();

            List<MonoScript> scriptsToRemove = new List<MonoScript>();
            foreach (UnityType obj in file.Objects)
            {
                if (obj is MonoManager)
                {
                    MonoManager manager = (MonoManager)obj;

                    for (int assIndex = manager.Assemblies.Count - 1; assIndex >= 0; assIndex--)
                    {
                        if (manager.Assemblies[assIndex].IndexOf("Leather", StringComparison.OrdinalIgnoreCase) >= 0 || manager.Assemblies[assIndex].IndexOf("Neolith", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            manager.Assemblies.RemoveAt(assIndex);
                        }
                    }

                    manager.Assemblies.Add("NeolithLoader.dll");
                }
                else if (obj is MonoScript)
                {
                    MonoScript script = (MonoScript)obj;

                    if (script.Assembly.IndexOf("Leather", StringComparison.OrdinalIgnoreCase) >= 0 || script.Assembly.IndexOf("Neolith", StringComparison.OrdinalIgnoreCase) >= 0)
                        scriptsToRemove.Add(script);
                }
            }

            List<uint> scriptLocalIndexes = new List<uint>();
            foreach (MonoScript script in scriptsToRemove)
            {
                scriptLocalIndexes.Add(script.Info.Index);
                file.Objects.Remove(script);
            }

            List<MonoBehaviour> behavioursToRemove = new List<MonoBehaviour>();
            foreach (UnityType obj in file.Objects)
            {
                if (obj is MonoBehaviour)
                {
                    MonoBehaviour behaviour = (MonoBehaviour)obj;
                    if (behaviour.ScriptFileIndex == 0 && scriptLocalIndexes.Contains(behaviour.ScriptLocalIndex))
                        behavioursToRemove.Add(behaviour);
                }
            }

            foreach (MonoBehaviour behaviour in behavioursToRemove)
                file.Objects.Remove(behaviour);

            // TODO
            //file.ReIndex();

            SwappableEndianBinaryWriter writer = new SwappableEndianBinaryWriter(File.Open("mainData-2", FileMode.Create));
            file.Write(writer);
            writer.Close();
        }
    }
}
