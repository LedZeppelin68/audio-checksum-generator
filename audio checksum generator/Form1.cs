using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using HashLib;

namespace audio_checksum_generator
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void textBox1_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Copy;
        }

        private void textBox1_DragDrop(object sender, DragEventArgs e)
        {
            textBox1.Text = ((string[])e.Data.GetData(DataFormats.FileDrop))[0];
        }

        private void button1_Click(object sender, EventArgs e)
        {
            backgroundWorker1.RunWorkerAsync();
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            string working_dir = textBox1.Text;

            string[] flacs = Directory.GetFiles(working_dir, "*.flac", SearchOption.AllDirectories);

            if (flacs.Length != 0)
            {
                for (int i = 0; i < flacs.Length; i++)
                {
                    string[] cuesheet = File.ReadAllLines(flacs[i].Replace(".flac", ".cue")).Where(x => x.Contains("INDEX 01")).ToArray();
                    List<int> cue_indexes = cuesheet.Select(x => MFS2Samples(x.Replace("INDEX 01", "").Trim())).ToList();

                    int sample_total = ShowSamples(flacs[i]);
                    cue_indexes.Add(sample_total);

                    IHash crc32_full = HashFactory.Checksum.CreateCRC32a();
                    IHash crc32_full_wonull = HashFactory.Checksum.CreateCRC32a();
                    IHash md5_full = HashFactory.Crypto.CreateMD5();
                    crc32_full.Initialize();
                    crc32_full_wonull.Initialize();
                    md5_full.Initialize();

                    for (int j = 0; j < cue_indexes.Count - 1; j++)
                    {
                        IHash crc32_single = HashFactory.Checksum.CreateCRC32a();
                        IHash crc32_single_wonull = HashFactory.Checksum.CreateCRC32a();
                        IHash md5_single = HashFactory.Crypto.CreateMD5();
                        crc32_single.Initialize();
                        crc32_single_wonull.Initialize();
                        md5_single.Initialize();

                        int sample_start = cue_indexes[j];
                        int sample_count = cue_indexes[j + 1] - cue_indexes[j];

                        UInt32 arv1_sum = 0;
                        UInt32 sample_counter = 1;

                        UInt32 arv2_sum = 0;
                        UInt32 sample_counter_v2 = 1;

                        using (Process flacexe = new Process())
                        {
                            flacexe.StartInfo.FileName = "flac.exe";
                            flacexe.StartInfo.Arguments = string.Format("--decode --stdout --skip={0} --until=+{1} \"{2}\"", sample_start, sample_count, flacs[i]);
                            flacexe.StartInfo.UseShellExecute = false;
                            flacexe.StartInfo.RedirectStandardOutput = true;
                            flacexe.StartInfo.CreateNoWindow = true;

                            flacexe.Start();

                            using (BinaryReader memr = new BinaryReader(flacexe.StandardOutput.BaseStream))
                            {
                                memr.ReadBytes(44);

                                byte[] buffer;
                                do
                                {
                                    buffer = memr.ReadBytes(2352 * 2);

                                    crc32_full.TransformBytes(buffer);
                                    md5_full.TransformBytes(buffer);

                                    crc32_single.TransformBytes(buffer);
                                    md5_single.TransformBytes(buffer);

                                    using (BinaryReader bufferr = new BinaryReader(new MemoryStream(buffer)))
                                    {
                                        while (bufferr.BaseStream.Position != bufferr.BaseStream.Length)
                                        {
                                            Int16 sample = bufferr.ReadInt16();
                                            if (sample != 0)
                                            {
                                                crc32_full_wonull.TransformShort(sample);
                                                crc32_single_wonull.TransformShort(sample);
                                            }
                                        }
                                    }

                                    using (BinaryReader bufferr = new BinaryReader(new MemoryStream(buffer)))
                                    {
                                        while (bufferr.BaseStream.Position != bufferr.BaseStream.Length)
                                        {
                                            UInt32 sample = bufferr.ReadUInt32();
                                            arv1_sum += sample * sample_counter++;
                                        }
                                    }

                                    using (BinaryReader bufferr = new BinaryReader(new MemoryStream(buffer)))
                                    {
                                        while (bufferr.BaseStream.Position != bufferr.BaseStream.Length)
                                        {
                                            UInt64 sample = bufferr.ReadUInt32();

                                            UInt64 _tempcrc = sample * sample_counter_v2++;
                                            arv2_sum += (UInt32)(_tempcrc / (UInt64)0x100000000);
                                            arv2_sum += (UInt32)(_tempcrc & (UInt64)0xffffffff);
                                        }
                                    }
                                }
                                while (buffer.Length == 2352 * 2);
                            }
                            flacexe.WaitForExit();
                        }

                        string crc32_single_result = crc32_single.TransformFinal().GetUInt().ToString("x8");
                        string crc32_single_wonull_result = crc32_single_wonull.TransformFinal().GetUInt().ToString("x8");
                        string md5_single_result = BitConverter.ToString(md5_single.TransformFinal().GetBytes()).Replace("-", "").ToLower();

                        string arv1 = arv1_sum.ToString("x8");
                        string arv2 = arv2_sum.ToString("x8");

                        Invoke((MethodInvoker)delegate
                        {
                            listBox1.Items.Add(string.Format("{0:D2} {1,9} {2} {3} {4} {5} {6}", j + 1, sample_count, crc32_single_result, crc32_single_wonull_result, md5_single_result, arv1, arv2));
                        });
                    }

                    string crc32_full_result = crc32_full.TransformFinal().GetUInt().ToString("x8");
                    string crc32_full_wonull_result = crc32_full_wonull.TransformFinal().GetUInt().ToString("x8");
                    string md5_full_result = BitConverter.ToString(md5_full.TransformFinal().GetBytes()).Replace("-", "").ToLower();

                    Invoke((MethodInvoker)delegate
                    {
                        listBox1.Items.Add(string.Empty);
                        listBox1.Items.Add(string.Format("   {0} {1} {2} {3}", sample_total, crc32_full_result, crc32_full_wonull_result, md5_full_result));
                    });
                }
            }
        }

        private int ShowSamples(string flac)
        {
            int samples = 0;
            using (Process metaflacexe = new Process())
            {
                metaflacexe.StartInfo.FileName = "metaflac.exe";
                metaflacexe.StartInfo.Arguments = string.Format("--show-total-samples \"{0}\"", flac);
                metaflacexe.StartInfo.UseShellExecute = false;
                metaflacexe.StartInfo.RedirectStandardOutput = true;
                metaflacexe.StartInfo.CreateNoWindow = true;

                metaflacexe.Start();
                samples = Convert.ToInt32(metaflacexe.StandardOutput.ReadLine());
                metaflacexe.WaitForExit();
            }

            return samples;
        }

        private int MFS2Samples(string v)
        {
            string[] MSF = v.Split(new char[] { ':' });
            int samples = 0;

            samples += Convert.ToInt32(MSF[2]) * 588;
            samples += Convert.ToInt32(MSF[1]) * 588 * 75;
            samples += Convert.ToInt32(MSF[0]) * 588 * 75 * 60;

            return samples;
        }
    }
}
