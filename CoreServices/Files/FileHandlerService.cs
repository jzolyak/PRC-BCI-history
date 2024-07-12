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
        public static BinaryWriter? CreateAndOpenBinaryFile(string name,string subdirectory)
        {
            if (string.IsNullOrEmpty(name)) return null; 
            if(!string.IsNullOrEmpty(subdirectory))
            {
                name = Path.Combine(subdirectory, name);
            }
            EnsureFolder(name);
            BinaryWriter? binaryWriter = null;
            if(!currentWriterList.ContainsKey(name) || !currentWriterList.TryGetValue(name, out binaryWriter))
            {
               binaryWriter = new BinaryWriter(new FileStream(name, FileMode.OpenOrCreate, FileAccess.Write,FileShare.ReadWrite));
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
        public static bool DeleteBinaryFile(string name, string subdirectory)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (!string.IsNullOrEmpty(subdirectory))
            {
                name = Path.Combine(subdirectory, name);
            }
            BinaryWriter? binaryWriter = null;
            if (!currentWriterList.ContainsKey(name))
            {
                return false;
            }
            if (!currentWriterList.TryGetValue(name, out binaryWriter))
            {
                return false;
            }
            if(binaryWriter==null) return false;
            binaryWriter.Close();
            File.Delete(name);
            currentWriterList.TryRemove(name, out binaryWriter);
            return true;
        }

        public static bool DeleteBinaryFile(BinaryWriter binaryWriter)
        {
            if (binaryWriter == null) return false;
            var keyValuePair = currentWriterList.FirstOrDefault(x => x.Value.Equals(binaryWriter));
            var name=keyValuePair.Key;
            if(string.IsNullOrEmpty(name)) return false;    
            binaryWriter.Close();
            File.Delete(name);
            currentWriterList.TryRemove(name, out binaryWriter);
            return true;
        }

        public static bool CloseBinaryWriter(BinaryWriter binaryWriter)
        {
            if (binaryWriter == null) return false;
            var keyValuePair = currentWriterList.FirstOrDefault(x => x.Value.Equals(binaryWriter));
            var name = keyValuePair.Key;
            if (string.IsNullOrEmpty(name)) return false;
            binaryWriter.BaseStream.Close();
            binaryWriter.Close();
            binaryWriter.Dispose();
            currentWriterList.TryRemove(name, out binaryWriter);
            Logger.Info(name, "Closed binary writer for:" + name);
            return true;
        }

       public static void EnsureFolder(string path)
        {
            string directoryName = Path.GetDirectoryName(path);
            // If path is a file name only, directory name will be an empty string
            if (directoryName.Length > 0)
            {
                // Create all directories on the path that don't already exist
                Directory.CreateDirectory(directoryName);
            }
        }

        public static void MoveFiles(string filename,string subdirectory,string destinationDirectory)
        {
            if(string.IsNullOrEmpty(filename) || string.IsNullOrEmpty(subdirectory) || string.IsNullOrEmpty(destinationDirectory)) return;    
           var sourcefilename = Path.Combine(subdirectory, filename);
            string deleteFilepath = Path.Combine(subdirectory, destinationDirectory)+Path.DirectorySeparatorChar;
            EnsureFolder(deleteFilepath);
            if (!File.Exists(sourcefilename)) return;
            string destinationFilename = Path.Combine(deleteFilepath, filename);
           Task.Run(()=> Movefile(sourcefilename, destinationFilename));
        }

        private static async void Movefile(string filename,string destinationFilename)
        {
            int retry = 0;
            while (retry < 5)
            {
                try
                {
                    File.Move(filename, destinationFilename, true);
                    Logger.Info(filename, "moved file from:" + filename + " to " + destinationFilename);
                    retry = 5;
                }
                catch (Exception ex)
                {
                    await Task.Delay(10000);
                    retry = retry + 1;
                    Logger.Error(filename,"Unable to move file from:" + filename + " to " + destinationFilename + " attempt:" + retry);
                }
            }

        }

        public static void RenameFile(string currentFileName,string newFileName,string subDirectory)
        {
            if (string.IsNullOrEmpty(currentFileName) || string.IsNullOrEmpty(subDirectory) || string.IsNullOrEmpty(newFileName)) return;
            var sourcefilename = Path.Combine(subDirectory, currentFileName);
            if (!File.Exists(sourcefilename)) return;
            string destinationFilename = Path.Combine(subDirectory, newFileName);
            File.Move(sourcefilename, destinationFilename);
        }
    }
}
