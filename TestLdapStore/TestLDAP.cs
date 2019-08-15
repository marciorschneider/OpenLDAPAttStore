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
                Console.WriteLine("Enter a filter and a parameter.");
            }
            else
            {
                string filter = args[0];
                string parameters = args[1];

                config.Add("host", "openldap-slave.sicredi.net");
                config.Add("base", "dc=sicredi,dc=com,dc=br");

                attributeStore.Initialize(config);
                IAsyncResult result = attributeStore.BeginExecuteQuery(filter, new string[] { parameters }, null, null);
             
                string[][] data = attributeStore.EndExecuteQuery(result);
                foreach (string[] row in data)
                {
                    foreach (string col in row)
                    {
                        Console.Write("{0}\n", col);
                    }
                    Console.WriteLine();
                }
            }
        }
    }
}


