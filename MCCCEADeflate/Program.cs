// MCCCEADeflate
//
// see "LICENSE.txt" for license details

using Ionic.Zlib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCCCEADeflate
{
    class Program
    {

        enum Mode
        {
            Decompress,
            Compress,
            UNK_MODE
        };

        static Mode mode;

        static void PrintHelp()
        {
            Console.WriteLine("MCCCEADeflate");
            Console.WriteLine("v1.0.0 - 2020");
            Console.WriteLine("");
            Console.WriteLine("https://github.com/Dragonflare921/MCCCEADeflate");
            Console.WriteLine("");
            Console.WriteLine("@Dragonflare921 - Decompress");
            Console.WriteLine("@zeddikins      - Compress (ported from ceapack)");
            Console.WriteLine("AMD             - Original ceapack (CEA 360)");
            Console.WriteLine("");
            Console.WriteLine("Compression utility for MCC PC CEA maps.");
            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("Usage:");
            Console.WriteLine("    If no OUT_FILE is provided, a default name will be used");
            Console.WriteLine("");
            Console.WriteLine("    decompress: mcc_cea_deflate.exe -d <IN_FILE> [OUT_FILE]");
            Console.WriteLine("    compress:   mcc_cea_deflate.exe -c <IN_FILE> [OUT_FILE]");
            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("Options:");
            Console.WriteLine("    -h                         Print this help text");
            Console.WriteLine("    -d <IN_FILE> [OUT_FILE]    Decompress a map");
            Console.WriteLine("    -c <IN_FILE> [OUT_FILE]    Compress a map");
            Console.WriteLine("");
            return;
        }

        static void Main(string[] args)
        {
            string in_file_path = "";
            string out_file_path = "";


            if (args.Length < 1)
            {
                Console.WriteLine("Not enough args! Heres some help:\n");
                PrintHelp();
                return;
            }

            // handle args
            if (args[0] == "-h")    // help
            {
                PrintHelp();
                return;
            }
            else if (args[0] == "-d") // decompress
            {
                if (args.Length < 2)
                {
                    Console.WriteLine("Not enough args! Heres some help:\n");
                    PrintHelp();
                    return;
                }

                mode = Mode.Decompress;

                in_file_path = args[1];

                if (args.Length > 2)
                {
                    out_file_path = args[2];
                }
                else // use a default out_file_path
                {
                    string default_file_name = Path.GetFileNameWithoutExtension(in_file_path) + "_decompressed" + Path.GetExtension(in_file_path);
                    out_file_path = Path.GetDirectoryName(in_file_path) + "\\" + default_file_name;
                }
                
            }
            else if (args[0] == "-c") // compress
            {
                if (args.Length < 2)
                {
                    Console.WriteLine("Not enough args! Heres some help:\n");
                    PrintHelp();
                    return;
                }

                mode = Mode.Compress;

                in_file_path = args[1];

                if (args.Length > 2)
                {
                    out_file_path = args[2];
                }
                else // use a default out_file_path
                {
                    string default_file_name = Path.GetFileNameWithoutExtension(in_file_path) + "_compressed" + Path.GetExtension(in_file_path);
                    out_file_path = Path.GetDirectoryName(in_file_path) + "\\" + default_file_name;
                }
            }

            // input file needs to exist
            if (!File.Exists(in_file_path))
            {
                Console.WriteLine("Input file does not exist!");
                return;
            }

            // check the mode, do the op
            if (mode == Mode.Decompress)
            {
                Decompress(in_file_path, out_file_path);
            }
            else if (mode == Mode.Compress)
            {
                Compress(in_file_path, out_file_path);
            }
            else
            {
                return;
            }
        }

        // decompress a file at in_file_path and output at out_file_path
        public static void Decompress(string in_file_path, string out_file_path)
        {
            FileStream in_file_stream = File.OpenRead(in_file_path);
            FileStream out_file_stream = File.OpenWrite(out_file_path);

            // read the offset table
            byte[] chunk_count_bytes = new byte[4];
            in_file_stream.Read(chunk_count_bytes, 0, 4);
            int chunk_count = BitConverter.ToInt32(chunk_count_bytes, 0);

            int[] chunk_offsets = new int[chunk_count];
            byte[] chunk_offsets_bytes = new byte[chunk_count * 4];
            in_file_stream.Seek(4, SeekOrigin.Begin);
            in_file_stream.Read(chunk_offsets_bytes, 0, chunk_count * 4);

            for (int i = 0; i < chunk_count; i++)
            {
                chunk_offsets[i] = BitConverter.ToInt32(chunk_offsets_bytes, i * 4) + 6;
            }

            Console.WriteLine("Decompressing " + chunk_count + " chunks... this may take some time.");

            // read the chunks
            for (int i = 0; i < chunk_count; i++)
            {
                int len = 0;
                if (i != chunk_count - 1)
                {
                    len = chunk_offsets[i + 1] - chunk_offsets[i];
                }
                else
                {
                    len = (int)in_file_stream.Length - chunk_offsets[i];
                }

                // decompress the chunk
                byte[] buff = new byte[len];
                in_file_stream.Seek(chunk_offsets[i], SeekOrigin.Begin);
                in_file_stream.Read(buff, 0, (int)len);

                Stream buff_stream = new MemoryStream(buff);

                DeflateStream tmp_deflate = new DeflateStream(buff_stream, CompressionMode.Decompress);

                tmp_deflate.CopyTo(out_file_stream);
            }

            in_file_stream.Close();
            out_file_stream.Close();
        }

        // actually FUNCTIONING Compress()
        // provided by zedd, ported and converted to zlib deflate from AMD's lzx ceapack
        static void Compress(string input, string output)
        {
            //Open the input file
            using (FileStream fsi = new FileStream(input, FileMode.Open))
            {
                List<int> offsets = new List<int>();

                //Create the output stream
                using (FileStream fso = new FileStream(output, FileMode.OpenOrCreate))
                {
                    using (BinaryWriter bwo = new BinaryWriter(fso))
                    {
                        // Calculate and write the chunk count
                        int chunkcount = (((int)fsi.Length + 0x1FFFF) & ~0x1FFFF) / 0x20000;

                        bwo.Write(chunkcount);

                        int datastart = 0x40000;
                        fso.Position = datastart;

                        Console.WriteLine("Compressing " + chunkcount + " chunks... this may take some time.");

                        for (int i = 0; i < chunkcount; i++)
                        {
                            offsets.Add((int)fso.Position);
                            int size = 0x20000;
                            if (i == chunkcount - 1)
                                size = (int)fsi.Length % 0x20000;

                            bwo.Write(size);

                            using (ZlibStream zs = new ZlibStream(fso, CompressionMode.Compress, CompressionLevel.BestSpeed, true))
                            {
                                byte[] decompdata = new byte[size];
                                fsi.Read(decompdata, 0, size);
                                zs.Write(decompdata, 0, decompdata.Length);
                            }
                        }

                        fso.Position = 4;
                        for (int i = 0; i < chunkcount; i++)
                            bwo.Write(offsets[i]);
                    }
                }
            }
        }

        /*
        // NOTE: im leaving this here because its what i originally wrote, and SHOULD work, but doesnt
        //       the stream doesnt increment properly on a few chunks toward the end and ive got no fucking clue why
        //       i tried it with both the MS .NET DeflateStream and the DotNetZip DeflateStream and got the same results
        //       if someone else wants to pick at it and figure out what the shit is happening, feel free
        //
        //       but to make the tool functional im just using the Compress() above

        public static void Compress(string in_file_path, string out_file_path)
        {
            FileStream in_file_stream = File.OpenRead(in_file_path);
            FileStream out_file_stream = File.OpenWrite(out_file_path);

            DeflateStream out_deflate_stream = new DeflateStream(out_file_stream, CompressionMode.Compress, CompressionLevel.BestCompression);

            // set up for chunk compression
            List<int> offsets_list = new List<int>();
            const int FIRST_CHUNK_OFFSET = 0x40000;
            const int MAX_CHUNK_SIZE = 0x20000;
            out_file_stream.Seek(FIRST_CHUNK_OFFSET, SeekOrigin.Begin);

            byte[] chunk_bytes = new byte[MAX_CHUNK_SIZE];
            int chunk_size = 0;

            while ((chunk_size = in_file_stream.Read(chunk_bytes, 0, MAX_CHUNK_SIZE)) != 0)
            {
                // add the offset to the table for later
                offsets_list.Add((int)out_file_stream.Position);

                // format chunk header
                byte[] chunk_head = new byte[4];
                byte[] size_bytes = BitConverter.GetBytes(chunk_size);
                byte[] idk_bytes = { 0x28, 0x15 };  // this is always the same?

                // write chunk header
                size_bytes.CopyTo(chunk_head, 0);
                idk_bytes.CopyTo(chunk_head, 4);
                out_file_stream.Write(chunk_head, 0, 6);
                //out_file_stream.Flush();

                // compress the chunk
                MemoryStream chunk_stream = new MemoryStream(chunk_bytes);
                chunk_stream.CopyTo(out_deflate_stream);
                //out_deflate_stream.Flush();
            }

            // write the offset table (count followed by the offsets)
            out_file_stream.Seek(0, SeekOrigin.Begin);
            out_file_stream.Write(BitConverter.GetBytes(offsets_list.Count), 0, 4);

            for (int i = 0; i < offsets_list.Count; i++)
            {
                out_file_stream.Write(BitConverter.GetBytes(offsets_list[i]), 0, 4);
            }

            out_deflate_stream.Close();
            in_file_stream.Close();
            out_file_stream.Close();
        }*/
    }
}
