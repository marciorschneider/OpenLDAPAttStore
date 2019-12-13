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

            if (args.Length != 2)
            {
                Console.WriteLine("Enter a host, search base, search scope, filter and parameters.");
                Console.WriteLine("Example: 127.0.0.1 \"dc=local,dc=net;subtree;(cn=john_doe);true;uid,givenname\"");
            
            }
            else
            {
                config.Add("host", args[0]);
                string query = args[1];

                try
                {
                    attributeStore.Initialize(config);
                    IAsyncResult result = attributeStore.BeginExecuteQuery(query, new string[] { null }, null, null);

                    string[][] data = attributeStore.EndExecuteQuery(result);
                    int numberOfColumns = 0;
                    int numberOfRows = 0;
                    if (data.Length > 0)
                    {
                        numberOfColumns = data[0].Length;
                        numberOfRows = data.Length;
                    }

                    Console.WriteLine();
                    for (int i = 0; i < numberOfColumns; i++)
                    {
                        for (int k = 0; k < numberOfRows; k++)
                        {
                            if (data[k][i] != null)
                                Console.WriteLine(data[k][i]);
                        }
                        Console.WriteLine("--------------");
                    }
                }
                catch (Exception E)
                {
                    Console.WriteLine(E.Message);
                }
            }
        }
    }
}


