using System;
using System.Collections.Generic;
using Microsoft.IdentityModel.Threading;
using Microsoft.IdentityServer.ClaimsPolicy.Engine.AttributeStore;
using System.DirectoryServices.Protocols;

namespace CustomLdapStore
{
    public class LdapAnonymousStore : IAttributeStore, IDisposable
    {
        LdapConnection connection;
        string target;

        bool withAttributeStoreQueryExecutionException = false;

        #region IAttributeStore Membres

        public IAsyncResult BeginExecuteQuery(string query, string[] parameters, AsyncCallback callback, object state)
        {
            if (query == null || query.Length == 0) throw new AttributeStoreQueryFormatException("The query received is null");

            AsyncResult queryResult;
            try
            {
                if (parameters != null && parameters.Length != 0) query = string.Format(query, parameters);
                string[] queryParts = query.Split(new char[] { ';' });
                if (queryParts.Length != 2)
                {
                    throw new AttributeStoreQueryFormatException("Invalid query: A query must have two parts : " + query);
                }
                string ldapFilter = queryParts[0].Trim();
                if (ldapFilter.Length == 0)
                {
                    throw new AttributeStoreQueryFormatException("Invalid query: A filter is needed : " + query);
                }

                string attributesList = queryParts[1].Trim();
                if (attributesList.Length == 0)
                {
                    throw new AttributeStoreQueryFormatException("Invalid query: A list of attributes is needed : " + query);
                }

                string[] attributesToReturn = attributesList.Split(new char[] { ',' });
                for (int i = 1; i < attributesToReturn.Length; i++)
                {
                    attributesToReturn[i] = attributesToReturn[i].Trim();
                    if (attributesToReturn[i].Length == 0)
                    {
                        throw new AttributeStoreQueryFormatException("an attribute does not exist in the query: " + query);
                    }
                }

                int countOfAttributesToReturn = attributesToReturn.Length;

                SearchRequest request = new SearchRequest(
                    target,
                    ldapFilter,
                    SearchScope.Subtree,
                    attributesToReturn);

                queryResult = new TypedAsyncResult<string[][]>(callback, state);
                LdapAnonymousStoreAsyncState status = new LdapAnonymousStoreAsyncState(attributesToReturn, queryResult);
                IAsyncResult iar = connection.BeginSendRequest(request, PartialResultProcessing.NoPartialResultSupport, ReceiveResponse, status);
            }
            catch (Exception e)
            {
                string msg;
                try { msg = e.Message; }
                catch { msg = "The request is not accepted by the ldap server"; }
                throw new AttributeStoreQueryExecutionException(e.GetType() + msg, e);
            }
            return queryResult;
        }

        public string[][] EndExecuteQuery(IAsyncResult result)
        {
            return TypedAsyncResult<string[][]>.End(result);
        }

        public void Initialize(Dictionary<string, string> config)
        {
            if (config == null) throw new ArgumentNullException("config is null");

            string host;
            if (!config.TryGetValue("base", out target)) throw new AttributeStoreInvalidConfigurationException("The parameter base is not present");
            if (!config.TryGetValue("host", out host)) throw new AttributeStoreInvalidConfigurationException("The parameter host is not present");

            string WithQueryExecutionException;
            if (!config.TryGetValue("withexception", out WithQueryExecutionException))
            {
                withAttributeStoreQueryExecutionException = false;
            }

            string userdn;
            string password;
            try
            {
                connection = new LdapConnection(host);
                LdapSessionOptions options = connection.SessionOptions;
                if (config.TryGetValue("userdn", out userdn) && config.TryGetValue("password", out password))
                {
                    connection.AuthType = AuthType.Basic;
                    connection.Credential = new System.Net.NetworkCredential(userdn, password);
                }
                else
                {
                    connection.AuthType = AuthType.Anonymous;
                }

                string ssl;
                if (config.TryGetValue("ssl", out ssl))
                {
                    options.SecureSocketLayer = true;
                }
                options.ProtocolVersion = 3;
                connection.AutoBind = true;
            }
            catch (Exception e)
            {
                string msgt;
                try { msgt = e.Message; }
                catch { msgt = "error whith the host : " + host; }
                string msg = string.Format("Connection error {0} : {1}", e.GetType(), msgt);
                throw new AttributeStoreInvalidConfigurationException(msg, e);
            }
        }

        #endregion
        public static string ToHexString(byte[] bytes)
        {
            char[] hexDigits = {
                '0', '1', '2', '3', '4', '5', '6', '7',
                '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'};

            char[] chars = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                int b = bytes[i];
                chars[i * 2] = hexDigits[b >> 4];
                chars[i * 2 + 1] = hexDigits[b & 0xF];
            }
            return new string(chars);
        }
        #region Callback
        public void ReceiveResponse(IAsyncResult iar)
        {
            Exception e = null;
            string[][] r = null;
            LdapAnonymousStoreAsyncState state = (LdapAnonymousStoreAsyncState)iar.AsyncState;

            try
            {
                SearchResponse response = (SearchResponse)connection.EndSendRequest(iar);

                int countOfAttributesToReturn = state.Attributes.Length;

                int countOfLines = 0;
                List<string[]> list = new List<string[]>();

                foreach (SearchResultEntry entry in response.Entries)
                {
                    string[][] result = null;
                    SearchResultAttributeCollection attributes = entry.Attributes;
                    if (entry.Attributes.Count != countOfAttributesToReturn) throw new AttributeStoreQueryExecutionException("entry.Attributes.Count != countOfAttributesToReturn");
                    int indexAttribute = -1;
                    foreach (DirectoryAttribute attribute in attributes.Values)
                    {
                        //Console.Write("{0} ", attribute.Name);
                        if (countOfLines == 0)
                        {
                            countOfLines = attribute.Count;
                            result = new string[countOfLines][];
                            for (int k = 0; k < countOfLines; k++)
                            {
                                result[k] = new string[countOfAttributesToReturn];
                            }
                        }
                        else if (attribute.Count != countOfLines) throw new AttributeStoreQueryExecutionException("The attributes have not the same count of values");
                        indexAttribute++;

                        for (int i = 0; i < attribute.Count; i++)
                        {
                            if (attribute[i] is string)
                            {
                                result[i][indexAttribute] = (string)attribute[i];
                            }
                            else if (attribute[i] is byte[])
                            {
                                result[i][indexAttribute] = LdapAnonymousStore.ToHexString((byte[])attribute[i]);
                            }
                            else
                            {
                                throw new AttributeStoreQueryExecutionException("attribute is nor string neither byte");
                            }
                        }
                    }
                    for (int i = 0; i < result.Length; i++)
                    {
                        list.Add(result[i]);
                    }

                }
                r = list.ToArray();
            }
            catch (AttributeStoreQueryExecutionException ex)
            {
                if (withAttributeStoreQueryExecutionException) e = ex;
                else r = new string[0][];
            }
            catch (Exception ex)
            {
                if (withAttributeStoreQueryExecutionException)
                {
                    string msg;
                    try { msg = ex.Message; }
                    catch { msg = "The Response received from the server is an error"; }
                    e = new AttributeStoreQueryExecutionException(ex.GetType() + " : " + msg, ex);
                }
                else
                {
                    r = new string[0][];
                }
            }
            TypedAsyncResult<string[][]> typed = (TypedAsyncResult<string[][]>)state.AsyncResult;
            typed.Complete(r, false, e);
        }//endreceiveResponse
        #endregion Callback

        #region IDisposable Membres

        public void Dispose()
        {
            connection.Dispose();
        }

        #endregion
    }//end LdapAnonymousStore

    public class LdapAnonymousStoreAsyncState
    {
        string[] attributesToReturn;
        IAsyncResult result;
        public LdapAnonymousStoreAsyncState(string[] attributes, IAsyncResult iar)
        {
            attributesToReturn = attributes;
            result = iar;
        }
        public string[] Attributes
        {
            get { return attributesToReturn; }
        }
        public IAsyncResult AsyncResult
        {
            get { return result; }
        }

    }
    /// <summary>
    /// Wrapper for the test. So the est assembly (TestLdapStore.exe) does not need to reference Microsoft.IdentityServer.ClaimsPolicy
    /// </summary>
    public class WrapLdapAnonymousStore : IDisposable
    {
        LdapAnonymousStore store = new LdapAnonymousStore();
        public IAsyncResult BeginExecuteQuery(string query, string[] parameters, AsyncCallback callback, object state)
        {
            return store.BeginExecuteQuery(query, parameters, callback, state);
        }
        public string[][] EndExecuteQuery(IAsyncResult result)
        {
            return EndExecuteQuery(result);
        }
        public void Initialize(Dictionary<string, string> config)
        {
            store.Initialize(config);
        }
        public void Dispose()
        {
            store.Dispose();
        }
    }


}//end namespace
