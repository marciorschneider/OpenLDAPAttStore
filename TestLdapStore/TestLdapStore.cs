using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using CustomLdapStore;
using System.IO;

namespace TestLdapstore
{
    class Program
    {
        static readonly string schema_txt =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
                <xs:schema attributeFormDefault=""unqualified"" elementFormDefault=""qualified"" xmlns:xs=""http://www.w3.org/2001/XMLSchema"">
                  <xs:element name=""configuration"">
                    <xs:complexType mixed=""true"" >
                      <xs:all>
                            <xs:element name=""host"" type=""xs:string"" />
                            <xs:element name=""base"" type=""xs:string"" />
                            <xs:element minOccurs=""0"" name=""userdn"" type=""xs:string"" />
                            <xs:element minOccurs=""0"" name=""password"" type=""xs:string"" />
                            <xs:element minOccurs=""0"" name=""ssl"" />
                            <xs:element minOccurs=""0"" name=""withexception"" />
                            <xs:element name=""filter"" type=""xs:string"" />
                            <xs:element name=""parameters"" >
                            <xs:complexType>
                                <xs:sequence>
                                    <xs:element maxOccurs=""unbounded"" name=""parameter"" type=""xs:string"" />
                                </xs:sequence>
                            </xs:complexType>
                            </xs:element>                            
                       </xs:all>
                    </xs:complexType>
                  </xs:element>
                </xs:schema>";

        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Usage();
                return;
            }
            string XmlConfigurationFile = args[1];
            string filter = null;
            Dictionary<string, string> dict = new Dictionary<string, string>();
            try
            {
                XDocument configuration = GetDocument(XmlConfigurationFile);

                var query =
                    from element in configuration.Elements().Descendants()
                    where element.Name != "parameters"
                    select new { element.Name, element.Value };

                List<string> list = new List<string>();

                foreach (var parameter in query)
                {
                    if (parameter.Name == "filter") filter = parameter.Value;
                    else if (parameter.Name == "parameter")
                    {
                        list.Add(parameter.Value);
                    }
                    else dict[parameter.Name.ToString()] = parameter.Value;
                }

                string[] parameters = list.ToArray();




                WrapLdapAnonymousStore store = new WrapLdapAnonymousStore();
                /////////
                store.Initialize(dict);
                IAsyncResult iar = store.BeginExecuteQuery(
                        filter,
                        parameters, null, null);
                string[][] result = store.EndExecuteQuery(iar);
                ////////
                Console.WriteLine("Asynchronous =>");
                Write(result);
            }
            catch (Exception e)
            {
                Console.WriteLine("{0} {1}", e.GetType(), e.Message);
            }

        }
        static void Write(string[][] result)
        {
            foreach (string[] row in result)
            {
                foreach (string col in row)
                    Console.Write("{0}\t\t", col);
                Console.WriteLine();
            }
        }

        static XDocument GetDocument(string XmlConfigurationFile)
        {
            TextReader tr = new StringReader(schema_txt);

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.ValidationType = ValidationType.Schema;
            System.Xml.Schema.XmlSchema schema = System.Xml.Schema.XmlSchema.Read(tr, null);
            settings.Schemas.Add(schema);

            ////////////////////////////////////////
            XmlReader reader = XmlReader.Create(XmlConfigurationFile, settings);

            XDocument doc = XDocument.Load(reader);
            return doc;

        }
        static void Usage()
        {
            string exampleContentXmlFile =
                @"<?xml version=""1.0"" encoding=""utf-8""?>
                <configuration>
                    <host>annuaire.myorganization.fr:389</host>
                    <base>ou=personnes,o=myorganization,c=fr</base>
                    <userdn>uid=doume,ou=personnes,o=myorganization,c=fr</userdn>
                    <password>secret</password>
                    <ssl/>>
                    <withexception/>
                    <parameters>
                        <parameter>param1</parameter>
                        <parameter>param2</parameter>
                    </parameters>
                    <filter>(&amp;(uid={0})(objectclass=myorganizationPerson));sn,givenname,{1}</filter>
                </configuration>";

            Console.WriteLine("\"TestLdapStore <FileNameXML>\"  The XML schema is:\n{0}\n For instance, the XML file could be :\n{1}",
                schema_txt,
                exampleContentXmlFile);
        }

    }
}
