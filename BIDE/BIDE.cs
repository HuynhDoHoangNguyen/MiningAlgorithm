using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BIDE
{
    public class BIDE
    {
        private ConcurrentBag<BIDENode> closedSPs = new ConcurrentBag<BIDENode>();
        private ConcurrentBag<string> spBuffer = new ConcurrentBag<string>();
        private int patternCount = 0;
        private static readonly ConcurrentBag<string> logBuffer = new ConcurrentBag<string>();

        public void MineClosedPatterns(BIDEDataset dt_data, double minSup, int gap, int maxLen, int logFreq, string out_sp, string out_seq_sp, string out_seq_sym_sp)
        {
            double absMinSup = minSup * dt_data.n_row;
            using (StreamWriter logWriter = new StreamWriter("bide_log.txt", true))
            {
                dt_data.FilterFrequentItems(minSup); // Lọc mục thường xuyên trước khi khai phá
                var SC = BIDEUtils.FindSingletonCandidates(dt_data);
                var initialSPs = BIDEUtils.FindSequential1Items(SC.Values.ToList(), dt_data.n_label, absMinSup);

                foreach (var node in initialSPs)
                {
                    node.ForwardExtensions = BIDEUtils.GetForwardExtensions(node, dt_data, absMinSup, gap);
                    node.BackwardExtensions = BIDEUtils.GetBackwardExtensions(node, dt_data, absMinSup, gap);
                    if (node.ForwardExtensions.Count == 0 && node.BackwardExtensions.Count == 0)
                    {
                        node.id = Program.GetNextNodeId();
                        node.IsClosed = true;
                        closedSPs.Add(node);
                        Interlocked.Increment(ref patternCount);
                        if (!string.IsNullOrEmpty(out_sp))
                            BufferSP(out_sp, node, dt_data.n_row, logFreq);
                    }
                }

                var sortedSPs = initialSPs.OrderByDescending(node => node.sup).ToList();
                var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
                Parallel.ForEach(sortedSPs, options, node =>
                    FindClosedSequentialKItems(node, absMinSup, gap, dt_data, maxLen, logWriter, logFreq, out_sp));

                if (!string.IsNullOrEmpty(out_sp))
                {
                    using (StreamWriter sw = new StreamWriter(out_sp, true))
                    {
                        sw.WriteLine("id,itemset,size,sup");
                        while (spBuffer.TryTake(out string line))
                        {
                            sw.WriteLine(line);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(out_seq_sp))
                {
                    BIDEUtils.WriteTrainSPs(out_seq_sp, closedSPs.ToList(), dt_data);
                }
                if (!string.IsNullOrEmpty(out_seq_sym_sp))
                {
                    BIDEUtils.WriteTrainItemsSPs(out_seq_sym_sp, closedSPs.ToList(), dt_data);
                }
                Console.WriteLine($"minSup: {minSup}, SPs: {patternCount}");
            }
        }

        private void FindClosedSequentialKItems(BIDENode li, double minSup, int gap, BIDEDataset dt_data, int maxLen, StreamWriter logWriter, int logFreq, string out_sp)
        {
            if (li.itemset.Count >= maxLen)
            {
                return;
            }

            // BackScan Pruning (BSC)
            bool canBeClosed = true;
            foreach (var item in li.BackwardExtensions)
            {
                var O = ObjectPool<BIDENode>.Get();
                try
                {
                    O.Clear(dt_data.n_label);
                    int sup = 0;
                    for (int label = 0; label < dt_data.n_label; label++)
                    {
                        var itemTidset = dt_data.itemTids.ContainsKey(item) ? dt_data.itemTids[item] : new HashSet<int>();
                        var O_tidset = BIDEUtils.IntersectTidsets(li.tidposset[label].Keys.ToHashSet(), itemTidset);
                        if (O_tidset.Count < minSup)
                            continue;
                        var itemPositions = dt_data.tidItemPositions
                            .Where(d => d.Value.ContainsKey(item))
                            .ToDictionary(d => d.Key, d => d.Value[item]);
                        O.tidposset[label] = BIDEUtils.IntersectTidpossetsBack(li.tidposset[label], itemPositions, O_tidset.ToList(), gap);
                        sup += O.tidposset[label].Count;
                    }
                    if (sup >= minSup && sup == li.sup)
                    {
                        canBeClosed = false;
                        ObjectPool<BIDENode>.Return(O);
                        break;
                    }
                    ObjectPool<BIDENode>.Return(O);
                }
                catch
                {
                    ObjectPool<BIDENode>.Return(O);
                    throw;
                }
            }

            if (!canBeClosed)
                return;

            // Forward-Closure Checking
            var P_i = new ConcurrentBag<BIDENode>();
            foreach (var item in li.ForwardExtensions)
            {
                int itemSup = dt_data.itemTids.ContainsKey(item) ? dt_data.itemTids[item].Count : 0;
                if (itemSup < minSup)
                    continue;

                var O = ObjectPool<BIDENode>.Get();
                try
                {
                    O.Clear(dt_data.n_label);
                    int sup = 0;
                    bool earlyPrune = false;
                    for (int label = 0; label < dt_data.n_label; label++)
                    {
                        var itemTidset = dt_data.itemTids.ContainsKey(item) ? dt_data.itemTids[item] : new HashSet<int>();
                        if (itemTidset.Count < minSup)
                        {
                            earlyPrune = true;
                            break;
                        }
                        var O_tidset = BIDEUtils.IntersectTidsets(li.tidposset[label].Keys.ToHashSet(), itemTidset);
                        if (O_tidset.Count < minSup)
                        {
                            earlyPrune = true;
                            break;
                        }
                        var itemPositions = dt_data.tidItemPositions
                            .Where(d => d.Value.ContainsKey(item))
                            .ToDictionary(d => d.Key, d => d.Value[item]);
                        O.tidposset[label] = BIDEUtils.IntersectTidpossets(li.tidposset[label], itemPositions, O_tidset.ToList(), gap);
                        sup += O.tidposset[label].Count;
                    }

                    if (earlyPrune || sup < minSup)
                    {
                        ObjectPool<BIDENode>.Return(O);
                        continue;
                    }

                    O.sup = sup;
                    O.itemset = BIDEUtils.UnionItemsets(li.itemset, new List<string> { item });
                    O.ForwardExtensions = BIDEUtils.GetForwardExtensions(O, dt_data, minSup, gap);
                    O.BackwardExtensions = BIDEUtils.GetBackwardExtensions(O, dt_data, minSup, gap);
                    P_i.Add(O);
                }
                catch
                {
                    ObjectPool<BIDENode>.Return(O);
                    throw;
                }
            }

            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
            Parallel.ForEach(P_i, options, node =>
                FindClosedSequentialKItems(node, minSup, gap, dt_data, maxLen, logWriter, logFreq, out_sp));

            // Nếu không có mở rộng tiến hợp lệ, đánh dấu là mẫu đóng
            if (P_i.IsEmpty)
            {
                li.id = Program.GetNextNodeId();
                li.IsClosed = true;
                closedSPs.Add(li);
                Interlocked.Increment(ref patternCount);
                if (!string.IsNullOrEmpty(out_sp))
                    BufferSP(out_sp, li, dt_data.n_row, logFreq);
            }
        }

        private void BufferSP(string out_sp, BIDENode node, int n_row, int logFreq)
        {
            if (string.IsNullOrEmpty(out_sp)) return;
            string itemset = string.Join(" ", node.itemset);
            int size = node.itemset.Count;
            double sup = Math.Round((double)node.sup / n_row, 4);
            spBuffer.Add($"{node.id},{itemset},{size},{sup}");
            if (spBuffer.Count >= logFreq * 10)
            {
                FlushSPBuffer(out_sp, logFreq);
            }
        }

        private void FlushSPBuffer(string out_sp, int logFreq)
        {
            if (string.IsNullOrEmpty(out_sp) || spBuffer.IsEmpty) return;
            using (StreamWriter sw = new StreamWriter(out_sp, true))
            {
                while (spBuffer.TryTake(out string line))
                {
                    sw.WriteLine(line);
                }
            }
        }

        private static void BufferLog(StreamWriter logWriter, string message, int logFreq)
        {
            logBuffer.Add(message);
            if (logBuffer.Count >= logFreq)
            {
                FlushLogBuffer(logWriter);
            }
        }

        private static void FlushLogBuffer(StreamWriter logWriter)
        {
            while (logBuffer.TryTake(out string message))
            {
                logWriter.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: {message}");
            }
        }
    }

    public static class ObjectPool<T> where T : new()
    {
        private static readonly ConcurrentBag<T> _pool = new ConcurrentBag<T>();
        public static T Get() => _pool.TryTake(out T item) ? item : new T();
        public static void Return(T item) => _pool.Add(item);
    }
}