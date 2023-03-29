/*
RelativeEmbed - Tools to embed and extract data into and from the relative pixel differences between images
Written in 2023 by VDZ
To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.
*/
//TL;DR for above notice: You can do whatever you want with this including commercial use without any restrictions or requirements.

using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;

namespace RelativeExtract
{
    class ArgumentParser
    {
        string[] args = null;
        int pos = 0;
        public ArgumentParser(string[] args) { this.args = args; }
        public bool HasNext() { return pos < args.Length; }
        public string GetNext() { return ((args != null && HasNext()) ? args[pos++] : null); }
    }

    public class EmbeddedData
    {
        public enum ContentType
        {
            Invalid = 0x00,
            Data = 0x01,
            File = 0x02,
            Text = 0x03
        }

        public ContentType contentType = ContentType.Invalid;
        public bool hashMatches = false;
        public byte[] data = null;
    }

    public class RelativeExtractor
    {
        public const byte VERSION_FORMAT = 0x01;
        public const string KEY_DEFAULT = "SecureBeneathTheWatchfulEyes";
        public static byte[] salt =
        {
            0x41, 0x72, 0x67, 0x75, 0x69, 0x6e, 0x67, 0x20, 0x74, 0x68, 0x61, 0x74, 0x20, 0x79, 0x6f, 0x75,
            0x20, 0x64, 0x6f, 0x6e, 0x27, 0x74, 0x20, 0x63, 0x61, 0x72, 0x65, 0x20, 0x61, 0x62, 0x6f, 0x75,
            0x74, 0x20, 0x74, 0x68, 0x65, 0x20, 0x72, 0x69, 0x67, 0x68, 0x74, 0x20, 0x74, 0x6f, 0x20, 0x70,
            0x72, 0x69, 0x76, 0x61, 0x63, 0x79, 0x20, 0x62, 0x65, 0x63, 0x61, 0x75, 0x73, 0x65, 0x20, 0x79,
            0x6f, 0x75, 0x20, 0x68, 0x61, 0x76, 0x65, 0x20, 0x6e, 0x6f, 0x74, 0x68, 0x69, 0x6e, 0x67, 0x20,
            0x74, 0x6f, 0x20, 0x68, 0x69, 0x64, 0x65, 0x20, 0x69, 0x73, 0x20, 0x6e, 0x6f, 0x20, 0x64, 0x69,
            0x66, 0x66, 0x65, 0x72, 0x65, 0x6e, 0x74, 0x20, 0x74, 0x68, 0x61, 0x6e, 0x20, 0x73, 0x61, 0x79,
            0x69, 0x6e, 0x67, 0x20, 0x79, 0x6f, 0x75, 0x20, 0x64, 0x6f, 0x6e, 0x27, 0x74, 0x20, 0x63, 0x61,
            0x72, 0x65, 0x20, 0x61, 0x62, 0x6f, 0x75, 0x74, 0x20, 0x66, 0x72, 0x65, 0x65, 0x20, 0x73, 0x70,
            0x65, 0x65, 0x63, 0x68, 0x20, 0x62, 0x65, 0x63, 0x61, 0x75, 0x73, 0x65, 0x20, 0x79, 0x6f, 0x75,
            0x20, 0x68, 0x61, 0x76, 0x65, 0x20, 0x6e, 0x6f, 0x74, 0x68, 0x69, 0x6e, 0x67, 0x20, 0x74, 0x6f,
            0x20, 0x73, 0x61, 0x79, 0x2e
        };
        private static bool silent = true;
        private static bool verbose = false;

        public static void Main(string[] args)
        {
            silent = false;

            //Read arguments
            ArgumentParser ap = new ArgumentParser(args);
            string key = KEY_DEFAULT;
            bool doPrint = false;
            string inputfile = null;
            string referenceFile = null;
            string outputfile = null;
            List<string> appendFiles = new List<string>();
            while (ap.HasNext())
            {
                string arg = ap.GetNext();
                if (arg.StartsWith("-"))
                {
                    switch (arg)
                    {
                        case "-k":
                        case "--key":
                            if (!ap.HasNext())
                            {
                                PrintAndExit("No argument provided for -k or --key!", true, true);
                                return;
                            }
                            key = ap.GetNext();
                            break;
                        case "-a":
                        case "--append":
                            if (!ap.HasNext())
                            {
                                PrintAndExit("No argument provided for -a or --appendfile!", true, true);
                                return;
                            }
                            appendFiles.Add(ap.GetNext());
                            break;
                        case "-p":
                        case "--print":
                            doPrint = true;
                            break;
                        case "-v":
                        case "--verbose":
                            verbose = true;
                            break;
                        case "-s":
                        case "--silent":
                            silent = true;
                            break;
                        default:
                            PrintAndExit("Unrecognized option: " + arg, true, true);
                            return;
                    }
                }
                else
                {
                    if (inputfile == null) { inputfile = arg; }
                    else if (referenceFile == null) { referenceFile = arg; }
                    else if (outputfile == null) { outputfile = arg; }
                    else PrintAndExit("Too many arguments specified.", true, true);
                }
            }

            //Check required arguments
            if (inputfile == null) PrintAndExit("No input file specified!", true, true);
            if (referenceFile == null) PrintAndExit("No reference file specified!", true, true);
            if (outputfile == null && !doPrint) PrintAndExit("No output file specified!", true, true);

            //Extract data
            EmbeddedData data = ExtractData(inputfile, referenceFile, key);
            if (data == null) PrintAndExit("Could not extract data!");
            EmbeddedData[] appended = new EmbeddedData[appendFiles.Count];
            for (int i = 0; i < appended.Length; i++)
            {
                appended[i] = ExtractData(appendFiles[i], referenceFile, key);
                if (appended[i] == null) PrintAndExit("Could not extract data from appended file #" + (i + 1) + "!");
            }

            //Handle data
            if (!data.hashMatches) Print("WARNING: Hash does not match data!");
            for (int i = 0; i < appended.Length; i++) { if (!appended[i].hashMatches) Print("WARNING: Hash does not match data for appended file #" + (i + 1) + "!"); }
            if (doPrint)
            {
                UTF8Encoding utf8 = new UTF8Encoding(false);
                Console.Write(utf8.GetString(data.data));
                for (int i = 0; i < appended.Length; i++) { Console.Write(utf8.GetString(appended[i].data)); }
                Console.WriteLine();
            }
            else
            {
                //Write to file
                FileStream fs = new FileStream(outputfile, FileMode.Create, FileAccess.Write);
                fs.Write(data.data, 0, data.data.Length);
                for (int i = 0; i < appended.Length; i++) { fs.Write(appended[i].data, 0, appended[i].data.Length); }
                fs.Close();
                Print("Embedded data extracted to '" + outputfile + "'.");
            }
        }

        private static void PrintAndExit(string msg, bool evenIfSilent = false, bool printUsage = false, int exitCode = 1)
        {
            if (evenIfSilent) Console.WriteLine(msg);
            else Print(msg);
            if (printUsage) PrintUsage();
            Environment.Exit(exitCode);
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: RelativeExtract.exe [options] <inputFile> <referenceFile> [outputFile]");
            Console.WriteLine("-k [key], --key [key]: Use the specified encryption key");
            Console.WriteLine("-a [inputFile], --append [inputFile]: Also extract another file and append it to the result (can be repeated)");
            Console.WriteLine("-p, --print: Display the embedded text instead of writing to a file");
            Console.WriteLine("-v, --verbose: Print verbose output");
            Console.WriteLine("-s, --silent: Do not print any output other than --print output (will still print output if invalid arguments are provided, but not on error)");
            Console.WriteLine("If no key is specified, the key '" + KEY_DEFAULT + "' will be used by default. Explicitly specify an empty "
                + "key (\"\") if you want to extract unencrypted data.");
            return;
        }

        public static EmbeddedData ExtractData(string inputFilePath, string referenceFilePath, string key)
        {
            Bitmap input = new Bitmap(inputFilePath);
            Bitmap reference = new Bitmap(referenceFilePath);
            return ExtractData(input, reference, key);
        }

        public static EmbeddedData ExtractData(Bitmap input, Bitmap reference, string key)
        {
            return ParseData(ExtractRawData(input, reference), key);
        }

        public static EmbeddedData ParseData(byte[] rawData, string key)
        {
            Stream inputStream = null;
            bool useEncryption = (key != null && key.Length > 0);
            if (useEncryption)
            {
                MemoryStream encryptedStream = new MemoryStream(rawData);
                //Read IV
                VerbosePrint("Reading IV...");
                byte[] iv = new byte[16];
                encryptedStream.Read(iv, 0, 16);
                //Decrypt
                VerbosePrint("Beginning decryption...");
                AesManaged aes = new AesManaged();
                Rfc2898DeriveBytes rfc2898 = new Rfc2898DeriveBytes(key, salt);
                rfc2898.IterationCount = 4854;
                inputStream = new CryptoStream(encryptedStream, aes.CreateDecryptor(rfc2898.GetBytes(32), iv), CryptoStreamMode.Read);
            }
            else
            {
                VerbosePrint("Empty key specified, not using encryption.");
                inputStream = new MemoryStream(rawData);
            }

            //Check format version
            int version = inputStream.ReadByte();
            VerbosePrint("Format version: " + version);
            if (version > VERSION_FORMAT)
            {
                Print("Data is invalid or uses newer version (" + version + ", only format version " + VERSION_FORMAT + " and below is supported)!");
                return null;
            }
            //Read other metadata
            EmbeddedData data = new EmbeddedData();
            data.contentType = (EmbeddedData.ContentType)inputStream.ReadByte();
            VerbosePrint("Content type: " + data.contentType.ToString());
            byte[] buffer = new byte[16];
            inputStream.Read(buffer, 0, 4); //Read compressed length
            int length = BitConverter.ToInt32(buffer, 0);
            VerbosePrint("Compressed data length: " + length);
            inputStream.Read(buffer, 0, 16); //Read MD5 hash
            VerbosePrint("Read hash...");
            //Read compressed data
            byte[] compressedData = new byte[length];
            inputStream.Read(compressedData, 0, length);
            VerbosePrint("Read compressed data...");
            //inputStream.Close(); //Causes 'padding is invalid' exception for CryptoStream as it attempts to FlushFinalBlock()
            //Check hash
            MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
            byte[] hash = md5.ComputeHash(compressedData);
            data.hashMatches = true;
            VerbosePrint("Checking if hash matches...");
            for (int i = 0; i < 16; i++)
            {
                if (buffer[i] != hash[i]) { data.hashMatches = false; }
            }
            if (!data.hashMatches) Print("Warning: Hash mismatch in data!");
            else VerbosePrint("Hash matches data.");
            //Extract
            VerbosePrint("Extracting...");
            MemoryStream compressedStream = new MemoryStream(compressedData);
            DeflateStream deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress, false);
            MemoryStream dataStream = new MemoryStream();
            deflateStream.CopyTo(dataStream);
            data.data = dataStream.ToArray();
            dataStream.Close();
            deflateStream.Close();
            compressedStream.Close();
            VerbosePrint("Successfully extracted.");

            return data;
        }

        public static byte[] ExtractRawData(Bitmap input, Bitmap reference)
        {
            VerbosePrint("Reading data from images...");
            MemoryStream ms = new MemoryStream();
            byte b = 0;
            byte bitPos = 0;

            for (int iy = 0; iy < input.Height; iy++)
            {
                if (iy >= reference.Height) continue;
                for (int ix = 0; ix < input.Width; ix++)
                {
                    if (ix >= reference.Width) continue;
                    Color cInput = input.GetPixel(ix, iy);
                    Color cReference = reference.GetPixel(ix, iy);
                    if (cInput.A == 0) continue; //Skip transparent pixels
                    if (reference.GetPixel(ix, iy).A == 0) continue; //Also ignore if reference pixel is transparent, even if input isn't
                    for (int iColors = 0; iColors < 3; iColors++)
                    {
                        int bInput = (iColors == 0 ? cInput.R : (iColors == 1 ? cInput.G : cInput.B));
                        int bReference = (iColors == 0 ? cReference.R : (iColors == 1 ? cReference.G : cReference.B));
                        int diff = Math.Abs(bReference - bInput);
                        if (diff < 1 || diff > 2) continue;
                        if (diff == 2) b |= (byte)(0x1 << bitPos); //Set bit if +2 or -2
                        bitPos++;
                        if (bitPos == 8) //Byte finished, commit and move on
                        {
                            ms.WriteByte(b);
                            b = 0;
                            bitPos = 0;
                        }
                    }
                }
            }
            VerbosePrint("Read " + ms.Length + " bytes from image.");
            return ms.ToArray();
        }

        private static void Print(string msg)
        {
            if (!silent) Console.WriteLine(msg);
        }

        private static void VerbosePrint(string msg)
        {
            if (verbose && !silent) Console.WriteLine(msg);
        }
    }
}