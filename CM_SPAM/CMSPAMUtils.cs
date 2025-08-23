using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CM_SPAM
{
    public static class CMSPAMUtils
    {
        public static void BuildItemBitmaps(CMSPAMDataset dt_data, Dictionary<string, BitArray> itemBitmaps)
        {
            foreach (var item in dt_data.items)
            {
                var bitmap = new BitArray(dt_data.n_row);
                for (int tid = 0; tid < dt_data.n_row; tid++)
                {
                    if (dt_data.data[tid].Contains(item))
                    {
                        bitmap[tid] = true;
                    }
                }
                itemBitmaps[item] = bitmap;
            }
        }

        public static List<CMSPAMNode> FindSequential1Items(Dictionary<string, BitArray> itemBitmaps, double minSup, int n_label, CMSPAMDataset dt_data)
        {
            var result = new List<CMSPAMNode>();
            foreach (var item in itemBitmaps)
            {
                int sup = item.Value.Cast<bool>().Count(b => b);
                if (sup >= minSup)
                {
                    var node = new CMSPAMNode(n_label) { itemset = new List<string> { item.Key }, sup = sup };
                    node.id = Program.GetNextNodeId();
                    // SỬA LỖI: Gán tidposset cho nhãn đúng
                    var tids = item.Value.Cast<bool>().Select((b, i) => b ? i : -1).Where(i => i != -1).ToList();
                    foreach (var tid in tids)
                    {
                        int label = dt_data.labels[tid];
                        var positions = new List<int>();
                        for (int pos = 0; pos < dt_data.data[tid].Count; pos++)
                        {
                            if (dt_data.data[tid][pos] == item.Key)
                            {
                                positions.Add(pos);
                            }
                        }
                        node.tidposset[label][tid] = positions;
                    }
                    result.Add(node);
                }
            }
            return result;
        }

        public static List<string> UnionItemsets(List<string> c1, List<string> c2)
        {
            List<string> c = new List<string>(c1);
            c.Add(c2[c2.Count - 1]);
            return c;
        }

        public static bool IsClosedPattern(CMSPAMNode node, ConcurrentDictionary<string, int> patternSupport)
        {
            string nodePattern = string.Join(" ", node.itemset);
            foreach (var pattern in patternSupport)
            {
                if (pattern.Value == node.sup && node.itemset.Count < pattern.Key.Split(' ').Length)
                {
                    if (IsSubsequence(nodePattern, pattern.Key))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool IsSubsequence(string sub, string super)
        {
            var subItems = sub.Split(' ');
            var superItems = super.Split(' ');
            int i = 0, j = 0;
            while (i < subItems.Length && j < superItems.Length)
            {
                if (subItems[i] == superItems[j])
                    i++;
                j++;
            }
            return i == subItems.Length;
        }

        public static void WriteSPs(string file_itemset, List<CMSPAMNode> SPs, int n_row)
        {
            using (StreamWriter sw = new StreamWriter(file_itemset))
            {
                sw.WriteLine("id,itemset,size,sup");
                foreach (CMSPAMNode node in SPs)
                {
                    string itemset = string.Join(" ", node.itemset);
                    int size = node.itemset.Count;
                    double sup = Math.Round((double)node.sup / n_row, 4);
                    sw.WriteLine(node.id + "," + itemset + "," + size + "," + sup);
                }
            }
        }

        public static void WriteTrainSPs(string file_train, List<CMSPAMNode> SPs, CMSPAMDataset dt_train)
        {
            using (StreamWriter sw = new StreamWriter(file_train))
            {
                for (int i = 0; i < dt_train.n_row; i++)
                {
                    int label = dt_train.labels[i];
                    List<string> itemset = new List<string>();
                    foreach (CMSPAMNode node in SPs)
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

        public static void WriteTrainItemsSPs(string file_train, List<CMSPAMNode> SPs, CMSPAMDataset dt_train)
        {
            using (StreamWriter sw = new StreamWriter(file_train))
            {
                for (int i = 0; i < dt_train.n_row; i++)
                {
                    int label = dt_train.labels[i];
                    List<string> itemset = new List<string>();
                    itemset.AddRange(dt_train.data[i]);
                    foreach (CMSPAMNode node in SPs)
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
    }
}