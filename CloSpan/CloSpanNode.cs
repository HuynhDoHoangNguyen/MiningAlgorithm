using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloSpan
{
    public class CloSpanNode
    {
        public int id { get; set; }
        public List<string> itemset { get; set; }
        public Dictionary<int, List<int>>[] tidposset { get; set; }
        public int sup { get; set; }
        public bool IsClosed { get; set; }

        public CloSpanNode(int n_labels)
        {
            this.id = -1;
            this.itemset = new List<string>();
            this.tidposset = new Dictionary<int, List<int>>[n_labels];
            for (int x = 0; x < n_labels; x++)
            {
                this.tidposset[x] = new Dictionary<int, List<int>>();
            }
            this.sup = 0;
            this.IsClosed = true;
        }
    }
}
