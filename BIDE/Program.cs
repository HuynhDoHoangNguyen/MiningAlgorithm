using System;
using System.Diagnostics;
using System.Threading;

namespace BIDE
{
    class Program
    {
        private static int nNode = 1;
        public static int GetNextNodeId() => Interlocked.Increment(ref nNode);
        static int maxLen = int.MaxValue; // Giảm maxLen
        static int logFreq = 1000;

        private static int ArgPos(string str, string[] args)
        {
            for (int a = 0; a < args.Length; a++)
            {
                if (str.Equals(args[a]))
                {
                    if (a == args.Length - 1)
                    {
                        throw new ArgumentException($"Argument missing for {str}");
                    }
                    return a;
                }
            }
            return -1;
        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("BIDE: mining closed sequential patterns...");
                Console.WriteLine("\t-dataset <file>");
                Console.WriteLine("\tuse sequences from <file> to mine SPs");
                Console.WriteLine("\t-minsup <float>");
                Console.WriteLine("\tset minimum support threshold in [0,1]; default is 0.1");
                Console.WriteLine("\t-gap <int>");
                Console.WriteLine("\tset gap constraint > 0; set 0 if don't use gap constraint");
                Console.WriteLine("\t-maxlen <int>");
                Console.WriteLine("\tset maximum pattern length; default is 3");
                Console.WriteLine("\t-logfreq <int>");
                Console.WriteLine("\tset logging frequency (patterns); default is 1000");
                Console.WriteLine("\t-sp <file>");
                Console.WriteLine("\tsave discovered SPs to <file> (optional)");
                Console.WriteLine("\t-seqsp <file>");
                Console.WriteLine("\tconvert each sequence to a set of SPs and save it to <file> (optional)");
                Console.WriteLine("\t-seqsymsp <file>");
                Console.WriteLine("\tconvert each sequence to a set of symbols and SPs and save it to <file> (optional)");
                return;
            }

            string in_seq = "";
            double r_minSup = 0.1; // Tăng minSup
            int gap = 1;
            string out_sp = "";
            string out_seq_sp = "";
            string out_seq_sym_sp = "";
            int para_id;

            if ((para_id = ArgPos("-dataset", args)) > -1)
            {
                in_seq = args[para_id + 1];
            }
            if ((para_id = ArgPos("-minsup", args)) > -1)
            {
                r_minSup = double.Parse(args[para_id + 1]);
            }
            if ((para_id = ArgPos("-gap", args)) > -1)
            {
                gap = int.Parse(args[para_id + 1]);
            }
            //if ((para_id = ArgPos("-maxlen", args)) > -1)
            //{
            //    maxLen = int.Parse(args[para_id + 1]);
            //}
            if ((para_id = ArgPos("-logfreq", args)) > -1)
            {
                logFreq = int.Parse(args[para_id + 1]);
            }
            if ((para_id = ArgPos("-sp", args)) > -1)
            {
                out_sp = args[para_id + 1];
            }
            if ((para_id = ArgPos("-seqsp", args)) > -1)
            {
                out_seq_sp = args[para_id + 1];
            }
            if ((para_id = ArgPos("-seqsymsp", args)) > -1)
            {
                out_seq_sym_sp = args[para_id + 1];
            }

            BIDEDataset dt_data = new BIDEDataset();
            dt_data.loadData(in_seq, ' ');

            double[] label_dist = new double[dt_data.n_label];
            for (int label = 0; label < dt_data.n_label; label++)
            {
                label_dist[label] = (double)dt_data.rows_labels[label] / dt_data.n_row;
                label_dist[label] = Math.Round(label_dist[label] * 100, 2);
            }
            Console.WriteLine($"{in_seq}: #sequences={dt_data.n_row}, #symbols={dt_data.items.Count}, avg length={dt_data.avg_len}, #labels={dt_data.n_label}, label dist.={string.Join("&", label_dist)}");

            Stopwatch sw = Stopwatch.StartNew();
            nNode = 1;
            BIDE miner = new BIDE();
            miner.MineClosedPatterns(dt_data, r_minSup, gap, maxLen, logFreq, out_sp, out_seq_sp, out_seq_sym_sp);
            sw.Stop();
            Console.WriteLine($"runtime: {sw.ElapsedMilliseconds / 1000.0} (s)");
        }
    }
}