using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CloSpan
{
    public class CloSpan
    {
        private List<CloSpanNode> closedSPs = new List<CloSpanNode>();
        private ConcurrentDictionary<string, int> patternSupport = new ConcurrentDictionary<string, int>();
        private int nNode = 1;

        public void MineClosedPatterns(CloSpanDataset dt_data, double minSup, int gap, string out_sp, string out_seq_sp, string out_seq_sym_sp)
        {
            double absMinSup = minSup * dt_data.n_row;
            var SC = CloSpanUtils.FindSingletonCandidates(dt_data);
            var initialSPs = CloSpanUtils.FindSequential1Items(SC.Values.ToList(), dt_data.n_label, absMinSup, ref nNode);
            lock (closedSPs) { closedSPs.AddRange(initialSPs); }

            foreach (var node in initialSPs)
            {
                patternSupport.TryAdd(string.Join(" ", node.itemset), node.sup);
            }

            FindClosedSequentialKItems(initialSPs, absMinSup, gap, dt_data.n_label, dt_data.rows_labels, 2);

            if (!string.IsNullOrEmpty(out_sp))
            {
                CloSpanUtils.WriteSPs(out_sp, closedSPs, dt_data.n_row);
            }
            if (!string.IsNullOrEmpty(out_seq_sp))
            {
                CloSpanUtils.WriteTrainSPs(out_seq_sp, closedSPs, dt_data);
            }
            if (!string.IsNullOrEmpty(out_seq_sym_sp))
            {
                CloSpanUtils.WriteTrainItemsSPs(out_seq_sym_sp, closedSPs, dt_data);
            }
        }

        private void FindClosedSequentialKItems(List<CloSpanNode> Lr, double minSup, int gap, int n_label, Dictionary<int, int> rows_labels, int level)
        {
            if (level > int.MaxValue) return;

            var newPatterns = new ConcurrentBag<CloSpanNode>();
            Parallel.ForEach(Lr, li =>
            {
                var P_i = new List<CloSpanNode>();
                foreach (var lj in Lr)
                {
                    var O = new CloSpanNode(n_label);
                    for (int label = 0; label < n_label; label++)
                    {
                        var O_tidset = CloSpanUtils.IntersectTidsets(li.tidposset[label].Keys.ToList(), lj.tidposset[label].Keys.ToList());
                        O.tidposset[label] = CloSpanUtils.IntersectTidpossets(li.tidposset[label], lj.tidposset[label], O_tidset, gap);
                        O.sup += O.tidposset[label].Count;
                    }

                    if (O.sup >= minSup)
                    {
                        O.itemset = CloSpanUtils.UnionItemsets(li.itemset, lj.itemset);
                        if (CloSpanUtils.IsClosedPattern(O, patternSupport))
                        {
                            O.id = nNode++;
                            P_i.Add(O);
                            newPatterns.Add(O);
                            patternSupport.TryAdd(string.Join(" ", O.itemset), O.sup);
                        }
                    }
                }
                if (P_i.Count > 0)
                {
                    FindClosedSequentialKItems(P_i, minSup, gap, n_label, rows_labels, level + 1);
                }
            });
            lock (closedSPs) { closedSPs.AddRange(newPatterns); }
        }
    }
}
