using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;

namespace sni
{
    class APNICReader
    {
        public struct Record
        {
            public string Country;
            public IPAddress IP;
            public int Length;
        }

        private List<Record> records = new List<Record>();

        public int Init(Stream stream)
        {
            records.Clear();
            using (var sr = new StreamReader(stream))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    var row = line.Split('|');
                    if (row.Length >= 7 && row[0] == "apnic" && row[2] == "ipv4" && row[1] != "*")
                    {
                        var r = new Record();
                        r.Country = row[1];
                        r.IP = IPAddress.Parse(row[3]);
                        r.Length = int.Parse(row[4]);
                        records.Add(r);
                    }
                }
            }
            return records.Count;
        }

        public IEnumerable<Record> GetIP(string cc)
        {
            var r = from i in records where i.Country == cc select i;
            return r;
        }

        public IEnumerable<Record> GetIP()
        {
            return records;
        }
    }
}
