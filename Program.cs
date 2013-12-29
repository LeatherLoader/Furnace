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

            //Read from file into memory stream
            byte[] mainDataBytes = File.ReadAllBytes("mainData");

            MemoryStream inStream = new MemoryStream(mainDataBytes);
            SwappableEndianBinaryReader reader = new SwappableEndianBinaryReader(inStream);

            //Parse header section
            AssetHeader header = new AssetHeader();
            header.Read(reader);

            //Locate & mark the UI Root or RustServer game object
            bool foundConnector = false;
            uint connectorIndex = 0;

            foreach (ObjectInfo obj in header.FileData)
            {
                if (obj.ClassId == 1)
                {
                    inStream.Position = header.OldDataStart + obj.OldOffset;
                    uint componentCount = reader.ReadUInt32();
                    //Skip components
                    inStream.Position += (12 * componentCount);
                    //Layer
                    reader.ReadUInt32();
                    string name = reader.ReadUnityString();


                    if (!string.IsNullOrEmpty(name) && (name.Equals("UI Root") || name.Equals("RustServer")))
                    {
                        foundConnector = true;
                        connectorIndex = obj.Index;
                        break;
                    }
                }
            }

            if (!foundConnector)
            {
                Console.WriteLine("Could not locate GameObject to attach bootstrapper to.");
                return;
            }

            //Make changes
            MonoBehaviour behavior = new MonoBehaviour(0, connectorIndex, 0, (uint)(header.FileData.Count() + 2), "");
            MonoScript script = new MonoScript("ModBootstrapper", 1199, 0, "ModBootstrapper", "LeatherLoader", System.IO.Path.Combine("..","LeatherLoader"), 0);

            //Locate & mark the first MonoManager in the file
            bool foundManager = false;
            uint managerIndex = 0;
            uint managerAdditionSize = 4 + UnityHelper.ByteAlign((uint)script.Assembly.Length, 4);

            foreach (ObjectInfo obj in header.FileData)
            {
                if (obj.ClassId == 116)
                {
                    foundManager = true;
                    managerIndex = obj.Index;
                    break;
                }
            }

            if (!foundManager)
            {
                Console.WriteLine("Could not locate MonoManager.");
                return;
            }

            header.AdjustObjectSize((int)managerIndex, managerAdditionSize);

            header.AddObject(behavior);
            header.AddObject(script);

            //Write the header out to a new memory stream with a new external file and the size of __EDITOR_CONNECTOR increased
            MemoryStream outStream = new MemoryStream();
            SwappableEndianBinaryWriter writer = new SwappableEndianBinaryWriter(outStream);

            header.Write(writer);

            List<ObjectInfo> sortedInfos = header.FileData.ToList();
            for (int i = 0; i < sortedInfos.Count-2; i++)
            {
                uint endPosition = header.FileSize;

                if ((i+1) < sortedInfos.Count)
                {
                    endPosition = header.NewDataStart + sortedInfos[(i + 1)].NewOffset;
                }

                if (sortedInfos[i].Index == managerIndex)
                {
                    //Write out modified manager object

                    //Set the inptu stream to the manager's position
                    inStream.Position = header.OldDataStart + sortedInfos[i].OldOffset;

                    //Read old script count, write new script count
                    uint scriptLength = reader.ReadUInt32();
                    writer.Write((uint)scriptLength);

                    //Write script refs
                    byte[] oldScriptRefs = reader.ReadBytes((int)(scriptLength * 8));
                    writer.Write(oldScriptRefs);

                    //Read old assembly count, write new assembly count
                    uint assemblyLength = reader.ReadUInt32();
                    writer.Write((uint)assemblyLength + 1);

                    //Write old assembly names
                    for(uint j = 0; j < assemblyLength; j++)
                    {
                        string name = reader.ReadUnityString();
                        writer.WriteUnityString(name);
                    }

                    writer.WriteUnityString(script.Assembly);
                }
                else
                {
                    //Write the bytes out in a straightforward manner
                    writer.Write(mainDataBytes, (int)(header.OldDataStart + sortedInfos[i].OldOffset), (int)sortedInfos[i].OldSize);
                }

                //Write post-object padding
                while (outStream.Position < endPosition) { writer.Write((byte)0); }
            }

            behavior.Write(writer);
            script.Write(writer);

            byte[] outData = outStream.GetBuffer();

            FileStream stream = File.OpenWrite("mainData");
            stream.Write(outData, 0, (int)outStream.Length);
            Console.WriteLine("mainData modified!");
            return;
        }
    }
}
