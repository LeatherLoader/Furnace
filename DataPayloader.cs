using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityAssetsLib;
using UnityAssetsLib.FileTypes;
using UnityAssetsLib.ObjTypes;

namespace Furnace
{
    public class DataPayloader
    {
        private string mScriptAssemblyName;
        private string mScriptNamespace;
        private string mScriptClassName;

        public DataPayloader(string assemblyName, string scriptNamespace, string scriptClassName)
        {
            mScriptAssemblyName = assemblyName;
            mScriptNamespace = scriptNamespace;
            mScriptClassName = scriptClassName;
        }

        public bool Inject(string injectionFile)
        {
            //Build assembly path to use
            string assemblyPath = "";
            if (Path.IsPathRooted(injectionFile))
                assemblyPath = Path.Combine(Path.GetDirectoryName(injectionFile), mScriptAssemblyName);
            else
                assemblyPath = Path.Combine(Path.Combine("..", Path.GetDirectoryName(injectionFile)), mScriptAssemblyName);

            //Try to open & parse file
            if (!File.Exists(injectionFile))
            {
                Console.WriteLine("Could not locate mainData file.");
                return false;
            }

            SwappableEndianBinaryReader reader = new SwappableEndianBinaryReader(File.OpenRead(injectionFile));
            AssetsFile file = new AssetsFile();
            file.Read(reader);
            reader.Close();

            //Remove injected leather/neolith monobehaviours & monoscripts
            List<uint> scriptLocalIndexes = new List<uint>();
            RemoveObjects<MonoScript>(file, obj => {
                    if (obj.Assembly.IndexOf("Leather", StringComparison.OrdinalIgnoreCase) >= 0 || obj.Assembly.IndexOf("Neolith", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        scriptLocalIndexes.Add(obj.Info.Index);
                        return true;
                    } else
                        return false;
            });

            RemoveObjects<MonoBehaviour>(file, obj => {
                return obj.ScriptFileIndex == 0 && scriptLocalIndexes.Contains(obj.ScriptLocalIndex);
            });

            WipeAndReAddToMonoManager(file, assemblyPath);

            MonoScript newScript = new MonoScript(mScriptClassName, 0, 0, mScriptClassName, mScriptNamespace, assemblyPath, 0);
            file.Objects.Add(newScript);

            file.Header.ReIndex(file.Objects);

            GameObject injectionObj = GetInjectionObj(file);
            file.Objects.Add(new MonoBehaviour(0, injectionObj.Info.Index, 0, newScript.Info.Index, ""));

            SwappableEndianBinaryWriter writer = new SwappableEndianBinaryWriter(File.Open(injectionFile, FileMode.Create));
            file.Write(writer);
            writer.Close();

            return true;
        }

        private GameObject GetInjectionObj(AssetsFile file)
        {
            foreach (GameObject obj in file.Objects.OfType<GameObject>())
            {
                if (string.Equals(obj.Name, "UI Root", StringComparison.OrdinalIgnoreCase) || string.Equals(obj.Name, "RustServer", StringComparison.OrdinalIgnoreCase))
                    return obj;
            }

            return null;
        }

        private void RemoveObjects<T>(AssetsFile file, Func<T, bool> objRemovalTest) where T : UnityType
        {
            for (int i = file.Objects.Count - 1; i >= 0; i--)
            {
                if (file.Objects[i] is T && objRemovalTest((T)file.Objects[i]))
                    file.Objects.RemoveAt(i);
            }
        }

        private void WipeAndReAddToMonoManager(AssetsFile file, string assemblyPath)
        {
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

                    manager.Assemblies.Add(assemblyPath);
                }
            }
        }
    }
}
