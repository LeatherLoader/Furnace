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
            bool successfulPayloadInjection = false;
            try
            {
                DataPayloader payloader = new DataPayloader(@"LeatherLoader", "LeatherLoader", "ModBootstrapper");
                successfulPayloadInjection = payloader.Inject("mainData");
            } catch (Exception e)
            {
                Console.WriteLine("Error while injecting payload: " + e.ToString());
            }

            if (!successfulPayloadInjection)
            {
                Console.WriteLine("Couldn't inject payload to assets file.");
                return;
            }
            else
                Console.WriteLine("Assets file data injection successful.");
        }
    }
}
