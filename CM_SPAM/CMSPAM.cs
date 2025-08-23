using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CM_SPAM
{
    public class CMSPAM
    {
        private List<CMSPAMNode> SPs = new List<CMSPAMNode>();
        private Dictionary<string, BitArray> itemBitmaps = new Dictionary<string, BitArray>();
        private ConcurrentDictionary<string, int> patternSupport = new ConcurrentDictionary<string, int>();
        private ConcurrentDictionary<string, Dictionary<int, HashSet<int>>> coOccurrenceMap = new ConcurrentDictionary<string, Dictionary<int, HashSet<int>>>();
        public int NumberOfSPs => SPs.Count; // Thuộc tính để truy cập số lượng SPs

        private void BuildCoOccurrenceMap(List<CMSPAMNode> initialSPs, int n_row, int n_label, CMSPAMDataset dt_data, double minSup, int gap)
        {
            Parallel.ForEach(initialSPs, li =>
            {
                foreach (var lj in initialSPs)
                {
                    string key = $"{string.Join(" ", li.itemset)} -> {string.Join(" ", lj.itemset)}";
                    var tidPositions = new Dictionary<int, HashSet<int>>();
                    int supportCount = 0;

                    for (int tid = 0; tid < n_row; tid++)
                    {
                        int label = dt_data.labels[tid];
                        if (li.tidposset[label].ContainsKey(tid) && lj.tidposset[label].ContainsKey(tid))
                        {
                            var li_pos = li.tidposset[label][tid].OrderBy(p => p).ToList();
                            var lj_pos = lj.tidposset[label][tid].OrderBy(p => p).ToList();
                            HashSet<int> validPositions = new HashSet<int>();
                            int i = 0, j = 0;

                            while (i < lj_pos.Count && j < li_pos.Count)
                            {
                                if (lj_pos[i] > li_pos[j] && (gap == 0 || lj_pos[i] <= li_pos[j] + gap))
                                {
                                    validPositions.Add(lj_pos[i]);
                                    i++;
                                }
                                else if (lj_pos[i] <= li_pos[j])
                                {
                                    i++;
                                }
                                else
                                {
                                    j++;
                                }
                            }

                            if (validPositions.Any())
                            {
                                tidPositions[tid] = validPositions;
                                supportCount++;
                            }
                        }
                    }

                    if (supportCount >= minSup)
                    {
                        coOccurrenceMap.TryAdd(key, tidPositions);
                    }
                }
            });
        }

        public void MineClosedPatterns(CMSPAMDataset dt_data, double minSup, int gap, string out_sp, string out_seq_sp, string out_seq_sym_sp)
        {
            double absMinSup = minSup * dt_data.n_row;
            CMSPAMUtils.BuildItemBitmaps(dt_data, itemBitmaps);
            var initialSPs = CMSPAMUtils.FindSequential1Items(itemBitmaps, absMinSup, dt_data.n_label, dt_data);
            SPs.AddRange(initialSPs);
            foreach (var node in initialSPs)
            {
                patternSupport.TryAdd(string.Join(" ", node.itemset), node.sup);
            }

            BuildCoOccurrenceMap(initialSPs, dt_data.n_row, dt_data.n_label, dt_data, absMinSup, gap);
            FindClosedSequentialKItems(initialSPs, absMinSup, gap, dt_data.n_row, dt_data.n_label, dt_data);

            if (!string.IsNullOrEmpty(out_sp))
            {
                CMSPAMUtils.WriteSPs(out_sp, SPs, dt_data.n_row);
            }
            if (!string.IsNullOrEmpty(out_seq_sp))
            {
                CMSPAMUtils.WriteTrainSPs(out_seq_sp, SPs, dt_data);
            }
            if (!string.IsNullOrEmpty(out_seq_sym_sp))
            {
                CMSPAMUtils.WriteTrainItemsSPs(out_seq_sym_sp, SPs, dt_data);
            }
        }

        private void FindClosedSequentialKItems(List<CMSPAMNode> Lr, double minSup, int gap, int n_row, int n_label, CMSPAMDataset dt_data)
        {
            var newPatterns = new ConcurrentBag<CMSPAMNode>();
            Parallel.ForEach(Lr, li =>
            {
                var P_i = new List<CMSPAMNode>();
                var localPatterns = new ConcurrentBag<CMSPAMNode>();

                foreach (var lj in Lr)
                {
                    string key = $"{string.Join(" ", li.itemset)} -> {string.Join(" ", lj.itemset)}";
                    if (!coOccurrenceMap.ContainsKey(key))
                    {
                        continue;
                    }

                    var O = new CMSPAMNode(n_label);
                    var newBitmap = new BitArray(n_row);
                    var tidPositions = coOccurrenceMap[key];

                    foreach (var tid in tidPositions.Keys)
                    {
                        int label = dt_data.labels[tid];
                        newBitmap[tid] = true;
                        O.tidposset[label][tid] = tidPositions[tid].ToList();
                    }

                    O.sup = newBitmap.Cast<bool>().Count(b => b);
                    if (O.sup >= minSup)
                    {
                        O.itemset = CMSPAMUtils.UnionItemsets(li.itemset, lj.itemset);
                        if (CMSPAMUtils.IsClosedPattern(O, patternSupport))
                        {
                            O.id = Program.GetNextNodeId();
                            localPatterns.Add(O);
                            P_i.Add(O);

                            // Cập nhật CM chỉ cho các cặp có triển vọng
                            foreach (var lk in Lr)
                            {
                                string newKey = $"{string.Join(" ", O.itemset)} -> {string.Join(" ", lk.itemset)}";
                                var newTidPositions = new Dictionary<int, HashSet<int>>();
                                int supportCount = 0;

                                for (int tid = 0; tid < n_row; tid++)
                                {
                                    int label = dt_data.labels[tid];
                                    if (O.tidposset[label].ContainsKey(tid) && lk.tidposset[label].ContainsKey(tid))
                                    {
                                        var o_pos = O.tidposset[label][tid].OrderBy(p => p).ToList();
                                        var lk_pos = lk.tidposset[label][tid].OrderBy(p => p).ToList();
                                        HashSet<int> validPositions = new HashSet<int>();
                                        int i = 0, j = 0;

                                        while (i < lk_pos.Count && j < o_pos.Count)
                                        {
                                            if (lk_pos[i] > o_pos[j] && (gap == 0 || lk_pos[i] <= o_pos[j] + gap))
                                            {
                                                validPositions.Add(lk_pos[i]);
                                                i++;
                                            }
                                            else if (lk_pos[i] <= o_pos[j])
                                            {
                                                i++;
                                            }
                                            else
                                            {
                                                j++;
                                            }
                                        }

                                        if (validPositions.Any())
                                        {
                                            newTidPositions[tid] = validPositions;
                                            supportCount++;
                                        }
                                    }
                                }

                                if (supportCount >= minSup)
                                {
                                    coOccurrenceMap.TryAdd(newKey, newTidPositions);
                                }
                            }
                        }
                    }
                }

                if (P_i.Count > 0)
                {
                    FindClosedSequentialKItems(P_i, minSup, gap, n_row, n_label, dt_data);
                }

                foreach (var pattern in localPatterns)
                {
                    newPatterns.Add(pattern);
                    patternSupport.TryAdd(string.Join(" ", pattern.itemset), pattern.sup);
                }
            });

            lock (SPs)
            {
                SPs.AddRange(newPatterns);
            }
        }
    }
}