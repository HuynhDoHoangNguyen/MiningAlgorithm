using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPAM
{
    public class SPAMDataset
    {
        public int n_row { get; set; }
        public int n_label { get; set; }
        public List<string> items { get; set; }
        public double avg_len { get; set; }
        public Dictionary<int, int> rows_labels { get; set; }
        public List<List<string>> data { get; set; }
        public List<int> labels { get; set; }
        public Dictionary<string, int> dict_label { get; set; }

        public SPAMDataset()
        {
            this.n_row = 0;
            this.n_label = 0;
            this.items = new List<string>();
            this.avg_len = 0;
            this.rows_labels = new Dictionary<int, int>();
            this.data = new List<List<string>>();
            this.labels = new List<int>();
            this.dict_label = new Dictionary<string, int>();
        }

        public void loadData(string file, char sep)
        {
            List<string> s_labels = new List<string>();
            using (StreamReader sr = File.OpenText(file))
            {
                string line = "";
                while ((line = sr.ReadLine()) != null)
                {
                    line = line.Trim();
                    string[] content = line.Split('\t');
                    if (content.Length > 1)
                    {
                        s_labels.Add(content[0]);
                        addTransaction(content[1].Split(sep));
                    }
                }
            }
            this.n_row = this.data.Count;
            this.avg_len = Math.Round((double)this.avg_len / this.n_row, 2);
            List<string> distinct_labels = s_labels.Distinct().ToList();
            this.dict_label = distinct_labels.Select((s, i) => new { s, i }).ToDictionary(x => x.s, x => x.i);
            foreach (string label in s_labels)
            {
                this.labels.Add(dict_label[label]);
            }
            this.n_label = distinct_labels.Count;
            this.rows_labels = this.labels.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        }

        void addTransaction(string[] s_items)
        {
            int len = s_items.Length;
            List<string> itemset = new List<string>();
            for (int i = 0; i < len; i++)
            {
                string item = s_items[i];
                itemset.Add(item);
                if (!this.items.Contains(item))
                {
                    this.items.Add(item);
                }
            }
            this.avg_len += itemset.Count;
            this.data.Add(itemset);
        }
    }
}
