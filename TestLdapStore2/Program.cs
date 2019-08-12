using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomLdapStore;

namespace TestLdapStore2
{
    class Program
    {
        static void Main(string[] args)
        {
            LdapAnonymousStore attributeStore = new LdapAnonymousStore();
            Dictionary<string, string> config = new Dictionary<string, string>();
            config.Add("host","openldap-slave.sicredi.net");
            config.Add("base","dc=sicredi,dc=com,dc=br");

            attributeStore.Initialize(config);

            //IAsyncResult result = attributeStore.BeginExecuteQuery("(&(uid={0})(objectclass=sicrediUsuario));distinguishedName", new string[] { "angelica_adamatti" }, null, null);
            //IAsyncResult result = attributeStore.BeginExecuteQuery("(&(cn=sis_*)(member={0}));truE;cn,objectclass", new string[] { "uid=angelica_adamatti,cn=a,cn=users,dc=sicredi,dc=com,dc=br" }, null, null);
            //IAsyncResult result = attributeStore.BeginExecuteQuery("(&(uid=angelica_adamatti)(objectclass=sicrediusuario));truE;cn", new string[] { "uid=angelica_adamatti,cn=a,cn=users,dc=sicredi,dc=com,dc=br" }, null, null);
            IAsyncResult result = attributeStore.BeginExecuteQuery("(&(cn=sis_*)(member={0}));true;cn,member", new string[] { "uid=angelica_adamatti,cn=a,cn=users,dc=sicredi,dc=com,dc=br" }, null, null);



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


