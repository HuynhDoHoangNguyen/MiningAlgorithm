using System;
using System.Collections.Generic;

namespace BIDE
{
    public class BIDENode
    {
        public int id { get; set; }
        public List<string> itemset { get; set; }
        public Dictionary<int, List<int>>[] tidposset { get; set; }
        public int sup { get; set; }
        public List<string> ForwardExtensions { get; set; }
        public List<string> BackwardExtensions { get; set; }
        public bool IsClosed { get; set; }

        public BIDENode()
        {
            this.id = -1;
            this.itemset = new List<string>();
            this.tidposset = new Dictionary<int, List<int>>[0];
            this.sup = 0;
            this.ForwardExtensions = new List<string>();
            this.BackwardExtensions = new List<string>();
            this.IsClosed = false;
        }

        public BIDENode(int n_labels)
        {
            this.id = -1;
            this.itemset = new List<string>();
            this.tidposset = new Dictionary<int, List<int>>[n_labels];
            for (int x = 0; x < n_labels; x++)
            {
                this.tidposset[x] = new Dictionary<int, List<int>>();
            }
            this.sup = 0;
            this.ForwardExtensions = new List<string>();
            this.BackwardExtensions = new List<string>();
            this.IsClosed = false;
        }

        public void Clear(int n_labels)
        {
            this.id = -1;
            this.itemset.Clear();
            if (this.tidposset.Length != n_labels)
            {
                this.tidposset = new Dictionary<int, List<int>>[n_labels];
                for (int x = 0; x < n_labels; x++)
                {
                    this.tidposset[x] = new Dictionary<int, List<int>>();
                }
            }
            else
            {
                for (int x = 0; x < n_labels; x++)
                {
                    this.tidposset[x].Clear();
                }
            }
            this.sup = 0;
            this.ForwardExtensions.Clear();
            this.BackwardExtensions.Clear();
            this.IsClosed = false;
        }
    }
}