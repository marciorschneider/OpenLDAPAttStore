using System;
using System.Collections.Generic;
using Microsoft.IdentityServer.ClaimsPolicy.Engine.AttributeStore;
using System.DirectoryServices.Protocols;
using System.IdentityModel;

namespace OpenLDAPStore
{
    public class LdapAnonymousStore : IAttributeStore, IDisposable
    {
        LdapConnection connection;
        string target;
        bool returnDN;

        bool withAttributeStoreQueryExecutionException = false;

        #region IAttributeStore Members

        public IAsyncResult BeginExecuteQuery(string query, string[] parameters, AsyncCallback callback, object state)
        {
            if (query == null || query.Length == 0) throw new AttributeStoreQueryFormatException("The query received is null");

            AsyncResult queryResult;

            try
            {
                if (parameters != null && parameters.Length != 0) query = string.Format(query, parameters);
                string[] queryParts = query.Split(new char[] { ';' });
                if (queryParts.Length != 3)
                {
                    throw new AttributeStoreQueryFormatException("Invalid query: A query must have three parts: " + query);
                }
                string ldapFilter = queryParts[0].Trim();
                if (ldapFilter.Length == 0)
                {
                    throw new AttributeStoreQueryFormatException("Invalid query: A filter is needed: " + query);
                }

                //verifying if we should return the object DN
                try
                {
                    returnDN = Convert.ToBoolean(queryParts[1].Trim());
                }
                catch
                {
                    throw new AttributeStoreQueryFormatException("Invalid query: You must specify if the query should return the DN: " + query);
                }

                string attributesList = queryParts[2].Trim();
                if (attributesList.Length == 0 && (!returnDN))
                {
                    throw new AttributeStoreQueryFormatException("Invalid query: You must specify to return the DN or a list of attributes: " + query);
                }

                string[] attributesToReturn = attributesList.Split(new char[] { ',' });
                for (int i = 0; i < attributesToReturn.Length; i++)
                {

                    attributesToReturn[i] = attributesToReturn[i].Trim();
                    if (attributesToReturn[i].Length == 0 && !returnDN)
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
                LdapAnonymousStoreAsyncState status = new LdapAnonymousStoreAsyncState(returnDN,attributesToReturn, queryResult);
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
                int countOfAttributesToReturn = 0;

               if (state.Attributes != null && state.Attributes[0] != "")
                {
                    countOfAttributesToReturn = state.Attributes.Length;
                }

                int countOfColumnsToReturn = 0;

                if (returnDN)
                {
                    countOfColumnsToReturn = countOfAttributesToReturn + 1;
                }
                else
                {
                    countOfColumnsToReturn = countOfAttributesToReturn;
                }
                r = new string[countOfColumnsToReturn][];
                List<string>[] resultList = new List<string>[countOfColumnsToReturn];

                //loop to populate each column array
                for (int columnIndex = 0; columnIndex < countOfColumnsToReturn; columnIndex++)
                {
                    int entryIndex = 0;
                    foreach (SearchResultEntry entry in response.Entries)
                    {
                        //if returnDN is true we return it on the column 0
                        if (returnDN && (columnIndex == 0))
                        {
                            //initializing the array for DN
                            if (resultList[columnIndex] == null)
                            {
                                resultList[columnIndex] = new List<string>();
                            }
                            resultList[columnIndex].Add(entry.DistinguishedName);
                        }
                        else
                        {
                            SearchResultAttributeCollection newAttributes = entry.Attributes;

                            //reseting the column index
                            if (returnDN) { columnIndex = 1; }
                            else { columnIndex = 0; }

                            foreach (DirectoryAttribute newAttribute in newAttributes.Values)
                            {
                                if (resultList[columnIndex] == null)
                                {
                                    resultList[columnIndex] = new List<string>();
                                }

                                for (int i = 0; i < newAttribute.Count; i++)
                                {
                                    if (newAttribute[i] is string)
                                    {
                                        resultList[columnIndex].Add((string)newAttribute[i]);
                                    }

                                    else if (newAttribute[i] is byte[])
                                    {
                                        resultList[columnIndex].Add(LdapAnonymousStore.ToHexString((byte[])newAttribute[i]));
                                    }
                                    else
                                    {
                                        throw new AttributeStoreQueryExecutionException("attribute is nor string neither byte.");
                                    }
                                }
                                columnIndex++;
                            }
                        }
                        entryIndex++;
                    }
                }

                //trasforming an array of List to a multidimensional array as ADFS is expecting - string[][]
                for (int i = 0; i < resultList.Length; i++)
                {
                    r[i] = resultList[i].ToArray();
                }
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

        #region IDisposable Members

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
        public LdapAnonymousStoreAsyncState(bool returnDN, string[] attributes, IAsyncResult iar)
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
}//end namespace