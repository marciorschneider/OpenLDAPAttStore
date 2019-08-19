using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenLDAPStore;

namespace TestLdapStore
{
    class Program
    {
        static void Main(string[] args)
        {
            LdapAnonymousStore attributeStore = new LdapAnonymousStore();
            Dictionary<string, string> config = new Dictionary<string, string>();

            if (args.Length != 4)
            {
                Console.WriteLine("Enter a host, base, filter and a parameter.");
            }
            else
            {
                config.Add("host", args[0]);
                config.Add("base", args[1]);
                string filter = args[2];
                string parameters = args[3];

                attributeStore.Initialize(config);
                IAsyncResult result = attributeStore.BeginExecuteQuery(filter, new string[] { parameters }, null, null);

                string[][] data = attributeStore.EndExecuteQuery(result);

                int numberOfColumns = 0;
                int numberOfRows = 0;

                numberOfColumns = data[0].Length;
                numberOfRows = data.Length;

                Console.WriteLine();
                for (int i = 0; i < numberOfColumns; i++)
                {
                    for (int k = 0;k < numberOfRows ; k++)
                    {
                        if (data[k][i] != null)
                        Console.WriteLine(data[k][i]);
                    }
                    Console.WriteLine("--------------");
                }
            }
        }
    }
}


