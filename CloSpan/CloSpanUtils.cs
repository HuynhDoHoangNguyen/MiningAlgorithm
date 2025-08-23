using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
namespace CloSpan
{
    public static class CloSpanUtils
    {
        public static List<int> IntersectTidsets(List<int> a1, List<int> a2)
        {
            List<int> a = new List<int>();
            if (a1.Count == 0 || a2.Count == 0)
            {
                return a;
            }

            int i = 0, j = 0;
            a2.Add(a1[a1.Count - 1] + 1);
            while (i < a1.Count)
            {
                if (a1[i] < a2[j])
                {
                    i++;
                }
                else if (a1[i] > a2[j])
                {
                    j++;
                }
                else
                {
                    a.Add(a1[i++]);
                    j++;
                }
            }
            a2.RemoveAt(a2.Count - 1);
            return a;
        }

        public static Dictionary<int, List<int>> IntersectTidpossets(Dictionary<int, List<int>> b1, Dictionary<int, List<int>> b2, List<int> a, int gap)
        {
            Dictionary<int, List<int>> b = new Dictionary<int, List<int>>();
            foreach (int tid in a)
            {
                List<int> b1_positions = b1[tid];
                List<int> b2_positions = b2[tid];
                List<int> b_positions = new List<int>();
                if (gap > 0)
                {
                    foreach (int b2_pos in b2_positions)
                    {
                        foreach (int b1_pos in b1_positions)
                        {
                            if (b2_pos > b1_pos && b2_pos <= b1_pos + gap)
                            {
                                b_positions.Add(b2_pos);
                                break;
                            }
                        }
                    }
                }
                else
                {
                    foreach (int b2_pos in b2_positions)
                    {
                        foreach (int b1_pos in b1_positions)
                        {
                            if (b2_pos > b1_pos)
                            {
                                b_positions.Add(b2_pos);
                                break;
                            }
                        }
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

        public static Dictionary<string, CloSpanNode> FindSingletonCandidates(CloSpanDataset dt_data)
        {
            Dictionary<string, CloSpanNode> SC = new Dictionary<string, CloSpanNode>();
            for (int tid = 0; tid < dt_data.n_row; tid++)
            {
                int label = dt_data.labels[tid];
                for (int i = 0; i < dt_data.data[tid].Count; i++)
                {
                    string it = dt_data.data[tid][i];
                    if (!SC.ContainsKey(it))
                    {
                        CloSpanNode node = new CloSpanNode(dt_data.n_label);
                        node.itemset.Add(it);
                        node.tidposset[label].Add(tid, new List<int>());
                        node.tidposset[label][tid].Add(i);
                        SC.Add(it, node);
                    }
                    else
                    {
                        if (!SC[it].tidposset[label].ContainsKey(tid))
                        {
                            SC[it].tidposset[label].Add(tid, new List<int>());
                            SC[it].tidposset[label][tid].Add(i);
                        }
                        else
                        {
                            SC[it].tidposset[label][tid].Add(i);
                        }
                    }
                }
            }
            return SC;
        }

        public static List<CloSpanNode> FindSequential1Items(List<CloSpanNode> SC, int n_label, double minSup, ref int nNode)
        {
            List<CloSpanNode> SPs = new List<CloSpanNode>();
            foreach (CloSpanNode node in SC)
            {
                for (int label = 0; label < n_label; label++)
                {
                    node.sup += node.tidposset[label].Count;
                }
                if (node.sup >= minSup)
                {
                    node.id = nNode++;
                    SPs.Add(node);
                }
            }
            return SPs;
        }

        public static void WriteSPs(string file_itemset, List<CloSpanNode> SPs, int n_row)
        {
            using (StreamWriter sw = new StreamWriter(file_itemset))
            {
                sw.WriteLine("id,itemset,size,sup");
                foreach (CloSpanNode node in SPs)
                {
                    string itemset = string.Join(" ", node.itemset);
                    int size = node.itemset.Count;
                    double sup = Math.Round((double)node.sup / n_row, 4);
                    sw.WriteLine(node.id + "," + itemset + "," + size + "," + sup);
                }
            }
        }

        public static void WriteTrainSPs(string file_train, List<CloSpanNode> SPs, CloSpanDataset dt_train)
        {
            using (StreamWriter sw = new StreamWriter(file_train))
            {
                for (int i = 0; i < dt_train.n_row; i++)
                {
                    int label = dt_train.labels[i];
                    List<string> itemset = new List<string>();
                    foreach (CloSpanNode node in SPs)
                    {
                        if (node.tidposset[label].ContainsKey(i))
                        {
                            itemset.Add(node.id.ToString());
                        }
                    }
                    string s_label = dt_train.dict_label.FirstOrDefault(x => x.Value == label).Key;
                    sw.WriteLine(s_label + "\t" + string.Join(" ", itemset));
                }
            }
        }

        public static void WriteTrainItemsSPs(string file_train, List<CloSpanNode> SPs, CloSpanDataset dt_train)
        {
            using (StreamWriter sw = new StreamWriter(file_train))
            {
                for (int i = 0; i < dt_train.n_row; i++)
                {
                    int label = dt_train.labels[i];
                    List<string> itemset = new List<string>();
                    itemset.AddRange(dt_train.data[i]);
                    foreach (CloSpanNode node in SPs)
                    {
                        if (node.tidposset[label].ContainsKey(i))
                        {
                            itemset.Add(node.id.ToString());
                        }
                    }
                    string s_label = dt_train.dict_label.FirstOrDefault(x => x.Value == label).Key;
                    sw.WriteLine(s_label + "\t" + string.Join(" ", itemset));
                }
            }
        }

        public static bool IsClosedPattern(CloSpanNode node, ConcurrentDictionary<string, int> patternSupport)
        {
            string nodePattern = string.Join(" ", node.itemset);
            foreach (var pattern in patternSupport)
            {
                if (pattern.Value >= node.sup && pattern.Key.Contains(nodePattern) && pattern.Key != nodePattern)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
