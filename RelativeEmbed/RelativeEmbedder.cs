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

namespace RelativeEmbed
{
    class ArgumentParser
    {
        string[] args = null;
        int pos = 0;
        public ArgumentParser(string[] args) { this.args = args; }
        public bool HasNext() { return pos < args.Length; }
        public string GetNext() { return ((args != null && HasNext()) ? args[pos++] : null); }
    }

    class RelativeEmbedder
    {
        public enum ContentType
        {
            Invalid = 0x00,
            Data = 0x01,
            File = 0x02,
            Text = 0x03
        }

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
            string inputFile = null;
            string fileToEmbed = null;
            string outputFile = null;
            string key = KEY_DEFAULT;
            ContentType contentType = ContentType.File;
            long offset = 0;
            int bytesToRead = 0;
            while (ap.HasNext())
            {
                string arg = ap.GetNext();
                if (arg.StartsWith("-"))
                {
                    switch (arg)
                    {
                        case "-k":
                        case "--key":
                            if (!ap.HasNext()) PrintAndExit("No argument provided for -k or --key!", true, true);
                            key = ap.GetNext();
                            break;
                        case "-c":
                        case "--content":
                            if (!ap.HasNext()) PrintAndExit("No argument provided for -c or --content!", true, true);
                            string typestring = ap.GetNext();
                            switch (typestring.ToLower())
                            {
                                case "file": contentType = ContentType.File; break;
                                case "text": contentType = ContentType.Text; break;
                                case "data": contentType = ContentType.Data; break;
                                default:
                                    PrintAndExit("Unknown content type: " + typestring, true, true);
                                    return;
                            }
                            break;
                        case "-o":
                        case "--offset":
                            try
                            {
                                offset = Convert.ToInt64(ap.GetNext());
                                if (offset < 0) throw new Exception("Offset must be 0 or higher!");
                            }
                            catch (Exception ex)
                            {
                                PrintAndExit("Could not read value for offset: " + ex.ToString(), true, false);
                                return;
                            }
                            break;
                        case "-l":
                        case "--length":
                            try
                            {
                                bytesToRead = Convert.ToInt32(ap.GetNext());
                                if (bytesToRead < 0) throw new Exception("Length must be 0 or higher!");
                            }
                            catch (Exception ex)
                            {
                                PrintAndExit("Could not read value for length: " + ex.ToString(), true, false);
                                return;
                            }
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
                else if (inputFile == null) { inputFile = arg; }
                else if (fileToEmbed == null) { fileToEmbed = arg; }
                else if (outputFile == null) { outputFile = arg; }
                else PrintAndExit("Too many arguments specified.", true, true);
            }

            if (outputFile == null) PrintAndExit("Not enough arguments specified.", true, true); 

            if (key != KEY_DEFAULT) VerbosePrint("Using specified key: '" + key + "'");
            else VerbosePrint("No key specified, using default: '" + key + "'");
            if (EmbedFile(inputFile, fileToEmbed, outputFile, key, offset, bytesToRead, contentType))
            {
                Print("Successfully embedded file '" + fileToEmbed + "' into '" + inputFile + "' and exported the result as '" + outputFile + "'.");
            }
            else
            {
                Print("Failed to embed file!");
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
            Console.WriteLine("Usage: RelativeEmbed.exe [options] <inputFile> <fileToEmbed> <outputFile>");
            Console.WriteLine("-k [key], --key [key]: Use the specified encryption key");
            Console.WriteLine("-c [type], --content [type]: Specify content type: 'file', 'text' or 'data'");
            Console.WriteLine("-o [offset], --offset [offset]: Offset in fileToEmbed to start reading at (0 by default)");
            Console.WriteLine("-l [bytes], --length [bytes]: Number of bytes in fileToEmbed to read; use 0 to read everything (0 by default)");
            Console.WriteLine("-v, --verbose: Print verbose output");
            Console.WriteLine("-s, --silent: Do not print any output (will still print output if invalid arguments are provided, but not on error)");
            Console.WriteLine("If no key is specified, the key '" + KEY_DEFAULT + "' will be used by default. Explicitly specify an empty "
                + "key (\"\") if you want to embed the data unencrypted.");
            return;
        }

        public static bool EmbedFile(string inputFile, string fileToEmbed, string outputFile, string key, long startOffset = 0, int bytesToRead = 0, ContentType contentType = ContentType.File)
        {
            VerbosePrint("Reading data to embed from file '" + fileToEmbed + "'...");
            FileStream fs = new FileStream(fileToEmbed, FileMode.Open, FileAccess.Read);
            int toRead = bytesToRead;
            if (toRead == 0 || (toRead > (fs.Length - startOffset))) { toRead = (int)(fs.Length - startOffset); }
            VerbosePrint("Reading " + toRead + " bytes from offset " + startOffset + "...");
            if (startOffset > 0) fs.Seek(startOffset, SeekOrigin.Begin);
            byte[] dataToEmbed = new byte[toRead];
            fs.Read(dataToEmbed, 0, toRead);
            fs.Close();
            VerbosePrint("Embedding data into file '" + inputFile + "', to export as '" + outputFile + "'.");
            return EmbedData(inputFile, dataToEmbed, contentType, outputFile, key);
        }

        public static bool EmbedData(string inputFile, byte[] data, ContentType contentType, string outputFile, string key)
        {
            VerbosePrint("Loading input file...");
            Bitmap bmp = new Bitmap(inputFile);
            bmp = ConvertBitmap(bmp);
            bmp = EmbedData(bmp, data, contentType, key);
            if (bmp == null) return false;
            VerbosePrint("Exporting file...");
            bmp.Save(outputFile);
            VerbosePrint("Successfully exported file!");
            return true;
        }

        public static Bitmap EmbedData(Bitmap bmp, byte[] data, ContentType contentType, string key)
        {
            bool useEncryption = (key != null && key.Length > 0);

            //Compress
            VerbosePrint("Compressing...");
            MemoryStream msCompressed = new MemoryStream();
            DeflateStream ds = new DeflateStream(msCompressed, CompressionLevel.Optimal, true);
            ds.Write(data, 0, data.Length);
            ds.Close();
            int cSize = (int)msCompressed.Length;

            //Prefix data with IV, version, content type, length and MD5 hash
            MemoryStream msEmbedded = new MemoryStream(cSize + (useEncryption ? 16 : 0) + 1 + 1 + 4 + 16);
            AesManaged aes = null;
            if (useEncryption)
            {
                aes = new AesManaged();
                aes.GenerateIV();
                VerbosePrint("IV generated.");
                msEmbedded.Write(aes.IV, 0, 16);
            }
            VerbosePrint("Format version: " + VERSION_FORMAT);
            msEmbedded.WriteByte(VERSION_FORMAT);
            VerbosePrint("Content type: " + contentType + " (" + (byte)contentType + ")");
            msEmbedded.WriteByte((byte)contentType);
            VerbosePrint("Compressed data size: " + cSize);
            msEmbedded.Write(BitConverter.GetBytes(cSize), 0, 4);
            MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
            byte[] hash = md5.ComputeHash(msCompressed.ToArray(), 0, cSize); //Have to use array; stream gives wrong hash
            VerbosePrint("Hash generated.");
            msEmbedded.Write(hash, 0, 16);
            msCompressed.WriteTo(msEmbedded);
            VerbosePrint("Total size to embed: " + msEmbedded.Length);

            //Encrypt
            byte[] finalData = null;
            if (useEncryption)
            {
                VerbosePrint("Encrypting...");
                MemoryStream msEncrypted = null;
                msEncrypted = new MemoryStream((int)msEmbedded.Length);
                Rfc2898DeriveBytes rfc2898 = new Rfc2898DeriveBytes(key, salt);
                rfc2898.IterationCount = 4854;
                CryptoStream cs = new CryptoStream(msEncrypted, aes.CreateEncryptor(rfc2898.GetBytes(32), aes.IV), CryptoStreamMode.Write);
                msEmbedded.WriteTo(cs);
                cs.FlushFinalBlock();
                cs.Close();
                finalData = msEncrypted.ToArray();
            }
            else
            {
                VerbosePrint("Empty key specified, not applying encryption.");
                finalData = msEmbedded.ToArray();
            }
            msCompressed.Close();

            //Embed
            return EmbedRawData(bmp, finalData);
        }

        public static Bitmap EmbedRawData(Bitmap bmp, byte[] data)
        {
            Random random = new Random();
            //Count eligible pixels
            VerbosePrint("Counting eligible pixels...");
            int eligiblePixels = 0;
            for (int iy = 0; iy < bmp.Height; iy++)
            {
                for (int ix = 0; ix < bmp.Width; ix++)
                {
                    if (bmp.GetPixel(ix, iy).A > 0) eligiblePixels++;
                }
            }
            int eligibleBits = eligiblePixels * 3;
            int spaceInBytes = (int)Math.Floor((float)(eligibleBits) / 8f);
            VerbosePrint(eligiblePixels + " eligible pixels = " + spaceInBytes + " bytes");
            if (spaceInBytes < data.Length)
            {
                Print("Not enough space to embed data!");
                return null;
            }

            //Randomly determine locations to place data pixels
            VerbosePrint("Determining locations to embed data...");
            bool[] locations = new bool[eligibleBits]; //All false by default
            for (int i = 0; i < data.Length; i++)
            {
                for (int j = 0; j < 8; j++) //8 bits per byte
                {
                    int index = random.Next(eligibleBits);
                    while (locations[index] == true) //Pick the first nearby bit instead
                    {
                        if (i % 2 == 0) index++;
                        else index--;
                        if (index < 0) index = eligibleBits - 1;
                        if (index == eligibleBits) index = 0;
                    }
                    locations[index] = true;
                }
            }

            VerbosePrint("Embedding data into bitmap...");
            long dataPos = 0; //Position in data buffer
            byte bitPos = 0; //Bit being handled
            int locationsPos = 0; //Position in locations array

            for (int iy = 0; iy < bmp.Height; iy++)
            {
                for (int ix = 0; ix < bmp.Width; ix++)
                {
                    Color c = bmp.GetPixel(ix, iy);
                    byte b1 = c.R;
                    byte b2 = c.G;
                    byte b3 = c.B;
                    if (c.A > 0) //Skip fully transparent pixels
                    {
                        for (int iColors = 0; iColors < 3; iColors++)
                        {
                            if (!locations[locationsPos++]) continue; //Not one of the randomly selected pixel-bytes to write to
                            bool randomBool = (random.Next() % 2 == 1);
                            switch (iColors)
                            {
                                case 0: b1 = EmbedBitIntoByte(b1, data[dataPos], bitPos++, randomBool); break;
                                case 1: b2 = EmbedBitIntoByte(b2, data[dataPos], bitPos++, randomBool); break;
                                case 2: b3 = EmbedBitIntoByte(b3, data[dataPos], bitPos++, randomBool); break;
                            }
                            if (bitPos == 8)
                            {
                                bitPos = 0;
                                dataPos++;
                            }
                        }
                    }
                    bmp.SetPixel(ix, iy, Color.FromArgb(c.A, b1, b2, b3));
                }
            }

            if (dataPos < data.Length) //Should never happen due to eligible bits check earlier
            {
                Print("Failed to embed data! Not enough eligible pixels available to embed all data! (Only " + dataPos + " bytes could be written!)");
                return null;
            }
            VerbosePrint("Embedded data into bitmap.");
            return bmp;
        }

        private static byte EmbedBitIntoByte(byte input, byte toEmbed, int bitIndex, bool randomBool)
        {
            int multiplier = 1;
            if (input < 2) multiplier = 1; //No space for -2
            else if (input > 253) multiplier = -1; //No space for +2
            else if (randomBool) multiplier = -1; //Random + or -
            bool bitIsSet = ((toEmbed & (0x1 << bitIndex)) != 0);
            return (byte)((int)input + (multiplier * (bitIsSet ? 2 : 1))); //Add +1 or -1 on 0, add +2 or -2 on 1
        }

        private static Bitmap ConvertBitmap(Bitmap input)
        {
            if (input.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb) return input;
            Bitmap toReturn = new Bitmap(input.Width, input.Height);
            for (int iy = 0; iy < input.Height; iy++)
            {
                for (int ix = 0; ix < input.Width; ix++)
                {
                    toReturn.SetPixel(ix, iy, input.GetPixel(ix, iy));
                }
            }
            return toReturn;
        }

        private static byte PixelClamp(int num)
        {
            if (num < 0) return 0;
            if (num > 255) return 255;
            return (byte)num;
        }

        private static void Print(string msg)
        {
            if (!silent) Console.WriteLine(msg);
        }

        private static void VerbosePrint(string msg)
        {
            if (verbose) Console.WriteLine(msg);
        }
    }
}
