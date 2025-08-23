using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BIDE
{
    public class BIDEDataset
    {
        public int n_row { get; set; }
        public int n_label { get; set; }
        public List<string> items { get; set; }
        public double avg_len { get; set; }
        public Dictionary<int, int> rows_labels { get; set; }
        public List<List<string>> data { get; set; }
        public List<int> labels { get; set; }
        public Dictionary<string, int> dict_label { get; set; }
        public Dictionary<int, Dictionary<string, List<int>>> tidItemPositions { get; set; }
        public Dictionary<string, HashSet<int>> itemTids { get; set; }

        public BIDEDataset()
        {
            this.n_row = 0;
            this.n_label = 0;
            this.items = new List<string>();
            this.avg_len = 0;
            this.rows_labels = new Dictionary<int, int>();
            this.data = new List<List<string>>();
            this.labels = new List<int>();
            this.dict_label = new Dictionary<string, int>();
            this.tidItemPositions = new Dictionary<int, Dictionary<string, List<int>>>();
            this.itemTids = new Dictionary<string, HashSet<int>>();
        }

        public void loadData(string file, char sep)
        {
            List<string> s_labels = new List<string>();
            int lineNumber = 0;
            try
            {
                foreach (var line in File.ReadLines(file))
                {
                    lineNumber++;
                    var trimmedLine = line.Trim();
                    var content = trimmedLine.Split(new[] { '\t' }, StringSplitOptions.None);
                    if (content.Length != 2 || string.IsNullOrWhiteSpace(content[1]))
                    {
                        s_labels.Add(content.Length > 0 ? content[0] : $"label_{lineNumber}");
                        addTransaction(new string[] { }, data.Count);
                        continue;
                    }
                    s_labels.Add(content[0]);
                    addTransaction(content[1].Split(new[] { sep }, StringSplitOptions.RemoveEmptyEntries), data.Count);
                }
            }
            catch (IOException ex)
            {
                throw new IOException($"Error reading file {file} at line {lineNumber}: {ex.Message}", ex);
            }

            this.n_row = this.data.Count;
            this.avg_len = this.n_row > 0 ? Math.Round((double)this.avg_len / this.n_row, 2) : 0;
            List<string> distinct_labels = s_labels.Distinct().ToList();
            this.dict_label = distinct_labels.Select((s, i) => new { s, i }).ToDictionary(x => x.s, x => x.i);
            foreach (string label in s_labels)
            {
                this.labels.Add(dict_label[label]);
            }
            this.n_label = distinct_labels.Count;
            this.rows_labels = this.labels.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
            int maxLen = data.Any() ? data.Max(d => d.Count) : 0;
            Console.WriteLine($"Max sequence length: {maxLen}, Average sequence length: {avg_len}, Valid sequences: {this.n_row}");
        }

        void addTransaction(string[] s_items, int tid)
        {
            List<string> itemset = new List<string>();
            var itemPositions = new Dictionary<string, List<int>>();
            for (int i = 0; i < s_items.Length; i++)
            {
                string item = s_items[i];
                if (string.IsNullOrWhiteSpace(item)) continue;
                itemset.Add(item);
                if (!this.items.Contains(item))
                {
                    this.items.Add(item);
                    this.itemTids[item] = new HashSet<int>();
                }
                this.itemTids[item].Add(tid);
                if (!itemPositions.ContainsKey(item))
                    itemPositions[item] = new List<int>();
                itemPositions[item].Add(i);
            }
            this.avg_len += itemset.Count;
            this.data.Add(itemset);
            this.tidItemPositions[tid] = itemPositions;
        }

        public void FilterFrequentItems(double minSup)
        {
            var frequentItems = itemTids
                .Where(kv => kv.Value.Count >= minSup * n_row)
                .Select(kv => kv.Key)
                .ToList();
            items.Clear();
            items.AddRange(frequentItems);
            foreach (var tidPositions in tidItemPositions)
            {
                var newPositions = new Dictionary<string, List<int>>();
                foreach (var item in frequentItems)
                {
                    if (tidPositions.Value.ContainsKey(item))
                        newPositions[item] = tidPositions.Value[item];
                }
                tidPositions.Value.Clear();
                foreach (var kvp in newPositions)
                {
                    tidPositions.Value[kvp.Key] = kvp.Value; // Thêm từng cặp khóa-giá trị
                }
            }
            itemTids = itemTids
                .Where(kv => frequentItems.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }
    }
}