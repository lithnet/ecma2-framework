using System.Collections.ObjectModel;
using System.Text;

namespace Lithnet.Ecma2Framework
{
    public class WatermarkKeyedCollection : KeyedCollection<string, Watermark>
    {
        protected override string GetKeyForItem(Watermark item)
        {
            return item.ID;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            foreach (Watermark item in this)
            {
                sb.AppendLine(item.ToString());
            }

            return sb.ToString();
        }
    }
}
