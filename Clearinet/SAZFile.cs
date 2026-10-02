using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.IO;

namespace Clearinet
{
    internal class SAZFile: IDisposable
    { 
        public List<Exchange> Exchanges { get; private set; } = new List<Exchange>();

        internal SAZFile(string filePath)
        {
            using (ZipFile zip = ZipFile.Read(filePath))
            {
                foreach (ZipEntry e in zip)
                {
                    if (e.FileName.StartsWith("raw/") && e.FileName.EndsWith(".xml"))
                    {
                        using (var ms = new MemoryStream())
                        {
                            e.Extract(ms);
/*                            ms.Position = 0;
                            var doc = new XmlDocument();
                            doc.Load(ms);
                            var exchange = Exchange.FromSAZXml(doc);*/
                            Exchanges.Add(new Exchange(null, null));
                        }
                    }
                }
            }
        }

        void IDisposable.Dispose() { /*TODO Free everything*/ }

    }
}
