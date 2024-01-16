using System;

namespace Lithnet.Ecma2Framework
{
    public class Watermark
    {
        public string ID { get; set; }

        public string Value { get; set; }

        public string Type { get; set; }

        public Watermark()
        {
        }

        public Watermark(string tableID, string value, string type)
        {
            this.ID = tableID;
            this.Value = value;
            this.Type = type;
        }

        public override string ToString()
        {
            if (this.Type == "DateTime")
            {
                try
                {
                    long ticks = long.Parse(this.Value);
                    return $"{this.ID}:{new DateTime(ticks):s}";
                }
                catch
                {
                    return $"{this.ID}:unknown";
                }
            }
            else
            {
                return $"{this.ID}:{this.Value}";
            }
        }
    }
}
