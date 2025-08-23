using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BIDE
{
    public static class BIDEUtils
    {
        public static HashSet<int> IntersectTidsets(HashSet<int> a1, HashSet<int> a2)
        {
            if (a1 == null || a2 == null) return new HashSet<int>();
            HashSet<int> result = new HashSet<int>(a1);
            result.IntersectWith(a2);
            return result;
        }

        public static Dictionary<int, List<int>> IntersectTidpossets(Dictionary<int, List<int>> b1, Dictionary<int, List<int>> b2, List<int> a, int gap)
        {
            Dictionary<int, List<int>> b = new Dictionary<int, List<int>>();
            if (b1 == null || b2 == null || a == null || a.Count == 0) return b;
            foreach (int tid in a)
            {
                if (!b1.ContainsKey(tid) || !b2.ContainsKey(tid)) continue;
                List<int> b1_positions = b1[tid];
                List<int> b2_positions = b2[tid];
                List<int> b_positions = new List<int>();
                foreach (int b2_pos in b2_positions)
                {
                    if (b1_positions.Any(p => p < b2_pos && (gap == 0 || b2_pos <= p + gap)))
                    {
                        b_positions.Add(b2_pos);
                    }
                }
                if (b_positions.Count > 0)
                {
                    b.Add(tid, b_positions);
                }
            }
            return b;
        }

        public static Dictionary<int, List<int>> IntersectTidpossetsBack(Dictionary<int, List<int>> b1, Dictionary<int, List<int>> b2, List<int> a, int gap)
        {
            Dictionary<int, List<int>> b = new Dictionary<int, List<int>>();
            if (b1 == null || b2 == null || a == null || a.Count == 0) return b;
            foreach (int tid in a)
            {
                if (!b1.ContainsKey(tid) || !b2.ContainsKey(tid)) continue;
                List<int> b1_positions = b1[tid];
                List<int> b2_positions = b2[tid];
                List<int> b_positions = new List<int>();
                foreach (int b2_pos in b2_positions)
                {
                    if (b1_positions.Any(p => p > b2_pos && (gap == 0 || p <= b2_pos + gap)))
                    {
                        b_positions.Add(b2_pos);
                    }
                }
                if (b_positions.Count > 0)
                {
                    b.Add(tid, b_positions);
                }
            }
            return b;
        }

        public static List<string> UnionItemsets(List<string> c1, List<string> c2)
        {
            List<string> c = new List<string>(c1);
            c.Add(c2[c2.Count - 1]);
            return c;
        }

        public static Dictionary<string, BIDENode> FindSingletonCandidates(BIDEDataset dt_data)
        {
            Dictionary<string, BIDENode> SC = new Dictionary<string, BIDENode>();
            for (int tid = 0; tid < dt_data.n_row; tid++)
            {
                int label = dt_data.labels[tid];
                for (int i = 0; i < dt_data.data[tid].Count; i++)
                {
                    string it = dt_data.data[tid][i];
                    if (!SC.ContainsKey(it))
                    {
                        BIDENode node = new BIDENode(dt_data.n_label);
                        node.itemset.Add(it);
                        node.tidposset[label].Add(tid, new List<int> { i });
                        SC.Add(it, node);
                    }
                    else
                    {
                        if (!SC[it].tidposset[label].ContainsKey(tid))
                        {
                            SC[it].tidposset[label].Add(tid, new List<int>());
                        }
                        SC[it].tidposset[label][tid].Add(i);
                    }
                }
            }
            return SC;
        }

        public static List<BIDENode> FindSequential1Items(List<BIDENode> SC, int n_label, double minSup)
        {
            List<BIDENode> SPs = new List<BIDENode>();
            foreach (BIDENode node in SC)
            {
                int sup = 0;
                for (int label = 0; label < n_label; label++)
                {
                    sup += node.tidposset[label].Count;
                }
                if (sup >= minSup)
                {
                    node.id = Program.GetNextNodeId();
                    node.sup = sup;
                    SPs.Add(node);
                }
            }
            return SPs;
        }

        public static List<string> GetForwardExtensions(BIDENode node, BIDEDataset dt_data, double minSup, int gap)
        {
            var supCount = new Dictionary<string, int>();
            var relevantTids = node.tidposset.SelectMany(t => t.Keys).Distinct().ToList();
            foreach (var tid in relevantTids)
            {
                int label = dt_data.labels[tid];
                if (!node.tidposset[label].ContainsKey(tid)) continue;
                var positions = node.tidposset[label][tid];
                var itemPositions = dt_data.tidItemPositions.ContainsKey(tid) ? dt_data.tidItemPositions[tid] : new Dictionary<string, List<int>>();
                foreach (var item in dt_data.itemTids.Keys.Where(i => dt_data.itemTids[i].Contains(tid)))
                {
                    if (!itemPositions.ContainsKey(item)) continue;
                    var itemPos = itemPositions[item];
                    bool valid = gap > 0
                        ? itemPos.Any(p => positions.Any(q => p > q && p <= q + gap))
                        : itemPos.Any(p => positions.Any(q => p > q));
                    if (valid)
                    {
                        if (!supCount.ContainsKey(item))
                            supCount[item] = 0;
                        supCount[item]++;
                    }
                }
            }
            return supCount.Where(kv => kv.Value >= minSup).Select(kv => kv.Key).ToList();
        }

        public static List<string> GetBackwardExtensions(BIDENode node, BIDEDataset dt_data, double minSup, int gap)
        {
            var supCount = new Dictionary<string, int>();
            var relevantTids = node.tidposset.SelectMany(t => t.Keys).Distinct().ToList();
            foreach (var tid in relevantTids)
            {
                int label = dt_data.labels[tid];
                if (!node.tidposset[label].ContainsKey(tid)) continue;
                var positions = node.tidposset[label][tid];
                var itemPositions = dt_data.tidItemPositions.ContainsKey(tid) ? dt_data.tidItemPositions[tid] : new Dictionary<string, List<int>>();
                foreach (var item in dt_data.itemTids.Keys.Where(i => dt_data.itemTids[i].Contains(tid)))
                {
                    if (!itemPositions.ContainsKey(item)) continue;
                    var itemPos = itemPositions[item];
                    bool valid = gap > 0
                        ? itemPos.Any(p => positions.Any(q => p < q && q <= p + gap))
                        : itemPos.Any(p => positions.Any(q => p < q));
                    if (valid)
                    {
                        if (!supCount.ContainsKey(item))
                            supCount[item] = 0;
                        supCount[item]++;
                    }
                }
            }
            return supCount.Where(kv => kv.Value >= minSup).Select(kv => kv.Key).ToList();
        }

        public static void WriteSPToFile(string file_itemset, BIDENode node, int n_row)
        {
            if (string.IsNullOrEmpty(file_itemset)) return;
            using (StreamWriter sw = new StreamWriter(file_itemset, true))
            {
                string itemset = string.Join(" ", node.itemset);
                int size = node.itemset.Count;
                double sup = Math.Round((double)node.sup / n_row, 4);
                sw.WriteLine($"{node.id},{itemset},{size},{sup}");
            }
        }

        public static void WriteSPs(string file_itemset, List<BIDENode> SPs, int n_row)
        {
            if (string.IsNullOrEmpty(file_itemset)) return;
            using (StreamWriter sw = new StreamWriter(file_itemset))
            {
                sw.WriteLine("id,itemset,size,sup");
                foreach (BIDENode node in SPs.OrderBy(n => n.id))
                {
                    string itemset = string.Join(" ", node.itemset);
                    int size = node.itemset.Count;
                    double sup = Math.Round((double)node.sup / n_row, 4);
                    sw.WriteLine($"{node.id},{itemset},{size},{sup}");
                }
            }
        }

        public static void WriteTrainSPs(string file_train, List<BIDENode> SPs, BIDEDataset dt_train)
        {
            if (string.IsNullOrEmpty(file_train)) return;
            using (StreamWriter sw = new StreamWriter(file_train))
            {
                HashSet<int> processedTids = new HashSet<int>();
                int lineCount = 0;
                for (int i = 0; i < dt_train.n_row; i++)
                {
                    if (processedTids.Contains(i)) continue;
                    int label = dt_train.labels[i];
                    List<string> itemset = new List<string>();
                    foreach (BIDENode node in SPs)
                    {
                        if (node.tidposset[label].ContainsKey(i))
                        {
                            itemset.Add(node.id.ToString());
                        }
                    }
                    string s_label = dt_train.dict_label.FirstOrDefault(x => x.Value == label).Key ?? $"label_{label}";
                    sw.WriteLine($"{s_label}\t{string.Join(" ", itemset)}");
                    processedTids.Add(i);
                    lineCount++;
                }
                Console.WriteLine($"Wrote {lineCount} lines to {file_train}");
                if (lineCount != dt_train.n_row)
                {
                    Console.WriteLine($"Warning: Expected {dt_train.n_row} lines, but wrote {lineCount} lines to {file_train}");
                }
            }
        }

        public static void WriteTrainItemsSPs(string file_train, List<BIDENode> SPs, BIDEDataset dt_train)
        {
            if (string.IsNullOrEmpty(file_train)) return;
            using (StreamWriter sw = new StreamWriter(file_train))
            {
                HashSet<int> processedTids = new HashSet<int>();
                int lineCount = 0;
                for (int i = 0; i < dt_train.n_row; i++)
                {
                    if (processedTids.Contains(i)) continue;
                    int label = dt_train.labels[i];
                    List<string> itemset = new List<string>(dt_train.data[i]);
                    foreach (BIDENode node in SPs)
                    {
                        if (node.IsClosed && node.tidposset[label].ContainsKey(i))
                        {
                            itemset.Add(node.id.ToString());
                        }
                    }
                    string s_label = dt_train.dict_label.FirstOrDefault(x => x.Value == label).Key ?? $"label_{label}";
                    if (itemset.Count > 0)
                    {
                        sw.WriteLine($"{s_label}\t{string.Join(" ", itemset)}");
                        processedTids.Add(i);
                        lineCount++;
                    }
                }
                Console.WriteLine($"Wrote {lineCount} lines to {file_train}");
                if (lineCount != dt_train.n_row)
                {
                    Console.WriteLine($"Warning: Expected {dt_train.n_row} lines, but wrote {lineCount} lines to {file_train}");
                }
            }
        }
    }
}