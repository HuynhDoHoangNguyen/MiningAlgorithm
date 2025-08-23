using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPAM
{
    public class SPAM
    {
        private List<SPAMNode> SPs = new List<SPAMNode>();
        private Dictionary<string, BitArray> itemBitmaps = new Dictionary<string, BitArray>();
        public int NumberOfSPs => SPs.Count; // Thuộc tính để truy cập số lượng SPs

        public void MinePatterns(SPAMDataset dt_data, double minSup, int gap, string out_sp, string out_seq_sp, string out_seq_sym_sp)
        {
            double absMinSup = minSup * dt_data.n_row;
            SPAMUtils.BuildItemBitmaps(dt_data, itemBitmaps);
            var initialSPs = SPAMUtils.FindSequential1Items(itemBitmaps, absMinSup, dt_data.n_label, dt_data);
            SPs.AddRange(initialSPs);

            FindSequentialKItems(initialSPs, absMinSup, gap, dt_data.n_row, dt_data.n_label, dt_data);

            if (!string.IsNullOrEmpty(out_sp))
            {
                SPAMUtils.WriteSPs(out_sp, SPs, dt_data.n_row);
            }
            if (!string.IsNullOrEmpty(out_seq_sp))
            {
                SPAMUtils.WriteTrainSPs(out_seq_sp, SPs, dt_data);
            }
            if (!string.IsNullOrEmpty(out_seq_sym_sp))
            {
                SPAMUtils.WriteTrainItemsSPs(out_seq_sym_sp, SPs, dt_data);
            }
        }

        private void FindSequentialKItems(List<SPAMNode> Lr, double minSup, int gap, int n_row, int n_label, SPAMDataset dt_data)
        {
            var newPatterns = new List<SPAMNode>();
            Parallel.ForEach(Lr, li =>
            {
                var P_i = new List<SPAMNode>();
                var localPatterns = new List<SPAMNode>();
                foreach (var lj in Lr)
                {
                    var O = new SPAMNode(n_label);
                    var newBitmap = new BitArray(n_row);
                    Dictionary<int, List<int>> tidPositions = new Dictionary<int, List<int>>();
                    for (int tid = 0; tid < n_row; tid++)
                    {
                        int label = dt_data.labels[tid];
                        if (li.tidposset[label].ContainsKey(tid) && lj.tidposset[label].ContainsKey(tid))
                        {
                            var li_pos = li.tidposset[label][tid];
                            var lj_pos = lj.tidposset[label][tid];
                            List<int> validPositions = new List<int>();

                            li_pos.Sort();
                            lj_pos.Sort();
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
                                newBitmap[tid] = true;
                                tidPositions[tid] = validPositions;
                            }
                        }
                    }
                    O.sup = newBitmap.Cast<bool>().Count(b => b);
                    if (O.sup >= minSup)
                    {
                        O.itemset = SPAMUtils.UnionItemsets(li.itemset, lj.itemset);
                        O.id = Program.GetNextNodeId();
                        foreach (var tid in tidPositions.Keys)
                        {
                            int label = dt_data.labels[tid];
                            O.tidposset[label][tid] = tidPositions[tid];
                        }
                        localPatterns.Add(O);
                        P_i.Add(O);
                    }
                }
                if (P_i.Count > 0)
                {
                    FindSequentialKItems(P_i, minSup, gap, n_row, n_label, dt_data);
                }
                lock (newPatterns)
                {
                    newPatterns.AddRange(localPatterns);
                }
            });
            lock (SPs)
            {
                SPs.AddRange(newPatterns);
            }
        }
    }
}