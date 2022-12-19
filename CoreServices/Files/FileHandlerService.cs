using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreServices.Files
{
    public class FileHandlerService
    {
        private static ConcurrentDictionary<string, BinaryWriter> currentWriterList = new ConcurrentDictionary<string, BinaryWriter>();
        //create and open and keep it open and return binary writer
        public static BinaryWriter? CreateAndOpenBinaryFile(string name)
        {
            if (string.IsNullOrEmpty(name)) return null; 
            BinaryWriter? binaryWriter = null;
            if(!currentWriterList.ContainsKey(name) || !currentWriterList.TryGetValue(name, out binaryWriter))
            {
               binaryWriter = new BinaryWriter(new FileStream(name, FileMode.Append, FileAccess.Write));
                if (!currentWriterList.ContainsKey(name))
                {
                    currentWriterList.TryAdd(name, binaryWriter);
                }
                else
                {
                    currentWriterList[name] = binaryWriter;
                }
            }

            return binaryWriter;
        }
        //save and close the binary file with bin extension

        //write binary method should have a param for payload(binary)

        //write text files

        //read binary

        //read text 

        //erase all the content of the opened file(this might not be case) or delete the complete file

    }
}
