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
        //string target;
        string searchBase;
        string stringSearchScope;
        SearchScope searchScope;
        bool returnDN;
        bool withAttributeStoreQueryExecutionException = false;

        #region IAttributeStore Members

        public IAsyncResult BeginExecuteQuery(string query, string[] parameters, AsyncCallback callback, object state)
        {
            if (query == null || query.Length == 0) throw new AttributeStoreQueryFormatException("The query received is null.");

            AsyncResult queryResult;

            /*the query must have 5 parameters:
                seach base
                search scope (base,onelevel,subtree)
                ldap filter
                if it should return DN (boolean)
                attribute list
            */

            try
            {
                if (parameters != null && parameters.Length != 0) query = string.Format(query, parameters);
                string[] queryParts = query.Split(new char[] { ';' });
                if (queryParts.Length != 5)
                {
                    throw new AttributeStoreQueryFormatException("Invalid query: A query must have five parts: " + query);
                }

                //getting the searchbase
                searchBase = queryParts[0].Trim();
                if (searchBase.Length == 0)
                {
                    throw new AttributeStoreQueryFormatException("Invalid query: A search base is needed: " + query);
                }


                //getting the search scope
                stringSearchScope = queryParts[1].ToLower().Trim();
                switch (stringSearchScope)
                {
                    case "base":
                        searchScope = SearchScope.Base;
                        break;

                    case "onelevel":
                        searchScope = SearchScope.OneLevel;
                        break;

                    case "subtree":
                        searchScope = SearchScope.Subtree;
                        break;

                    default:
                        throw new AttributeStoreQueryFormatException("Invalid query: Search scope is invalid: " + query);
                }


                //getting the ldap filter
                string ldapFilter = queryParts[2].Trim();
                if (ldapFilter.Length == 0)
                {
                    throw new AttributeStoreQueryFormatException("Invalid query: A filter is needed: " + query);
                }


                //verifying if we should return the object DN
                try
                {
                    returnDN = Convert.ToBoolean(queryParts[3].Trim());
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
                        throw new AttributeStoreQueryFormatException("An attribute does not exist in the query: " + query);
                    }
                }

                int countOfAttributesToReturn = attributesToReturn.Length;

                //submiting the search to LDAP server
                SearchRequest request = new SearchRequest(
                    searchBase,
                    ldapFilter,
                    searchScope,
                    attributesToReturn);

                queryResult = new TypedAsyncResult<string[][]>(callback, state);
                LdapAnonymousStoreAsyncState status = new LdapAnonymousStoreAsyncState(returnDN,attributesToReturn, queryResult);
                IAsyncResult iar = connection.BeginSendRequest(request, PartialResultProcessing.NoPartialResultSupport, ReceiveResponse, status);
            }
            catch (Exception e)
            {
                string msg;
                try { msg = e.Message; }
                catch { msg = "The request is not accepted by the ldap server."; }
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
            if (!config.TryGetValue("host", out host)) throw new AttributeStoreInvalidConfigurationException("The parameter host is not present.");

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
                catch { msgt = "Error whith the host: " + host; }
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
            string[][] returnData = null;
            LdapAnonymousStoreAsyncState state = (LdapAnonymousStoreAsyncState)iar.AsyncState;

            try
            {
                SearchResponse response = (SearchResponse)connection.EndSendRequest(iar);
                int countOfRowsToReturn = 0;
                int columnIndex = 0;
                int numberOfExpectedAttributesToReturn = 0;

                //getting the number of columns to return
                int countOfColumnsToReturn = 0;
                if (state.Attributes != null && state.Attributes[0] != "")
                {
                    countOfColumnsToReturn = state.Attributes.Length;
                    numberOfExpectedAttributesToReturn = state.Attributes.Length;
                }
                if (returnDN)
                {
                    countOfColumnsToReturn++;
                }

                List<string>[] claimDataArr = new List<string>[countOfColumnsToReturn];
                //initializing the claimDataArr
                for (int i = 0; i < claimDataArr.Length; i++)
                {
                    claimDataArr[i] = new List<string>();
                }

                //getting the values from LDAP
                foreach (SearchResultEntry entry in response.Entries)
                {

                    //testing if the query attributes are all available in the result
                    if (entry.Attributes.Count != numberOfExpectedAttributesToReturn && response.Entries.Count == 1)
                    {
                        throw new AttributeStoreQueryExecutionException("Number of returned attributes is not the same requested. Are you querying an invalid attribute?");
                    }
                    
                    //testing that all the entries have the same number of attributes. If not the columns would be misaligned and we should throw an exception
                    if (entry.Attributes.Count != numberOfExpectedAttributesToReturn)
                    {
                        throw new AttributeStoreQueryExecutionException("Number of attributes is not the same on all the result entries. Are there entries with empty attributes?");
                    }

                    columnIndex = 0;
                    if (returnDN && columnIndex == 0)
                    {
                        claimDataArr[columnIndex].Add(entry.DistinguishedName);
                        columnIndex++;
                    }

                    SearchResultAttributeCollection attrs = entry.Attributes;
                    foreach (DirectoryAttribute attr in attrs.Values)
                    {
                        for (int k = 0;k < attr.Count;k++)
                        {
                            if (attr[k] is string)
                            {
                                claimDataArr[columnIndex].Add(attr[k].ToString());
                            }
                            else if (attr[k] is byte)
                            {
                                claimDataArr[columnIndex].Add(ToHexString((byte[])attr[k]));
                            }
                            else
                            {
                                throw new AttributeStoreQueryExecutionException("Attribute is not string or byte.");
                            }
                        }
                        columnIndex++;
                    }
                }

                //getting the max number of rows to return
                countOfRowsToReturn = 0;
                for (int i = 0; i < claimDataArr.Length; i++)
                {
                    if (claimDataArr[i].Count > countOfRowsToReturn)
                    {
                        countOfRowsToReturn = claimDataArr[i].Count;
                    }
                }

                //initializing the response array with null
                returnData = new string[countOfRowsToReturn][];
                for (int i = 0; i < countOfRowsToReturn; i++)
                {
                    returnData[i] = new string[countOfColumnsToReturn];
                    for (int k = 0; k < countOfColumnsToReturn; k++)
                    {
                        returnData[i][k] = null;
                    }
                }

                /* 
                 
                Since ADFS is expecting the result on a tabular format whe need the reformat the result like this:
                
                X________________________________________________
               Y|column1_____|column2_____|column3______________|
                |john________|HR_manager__|john@contoso.com_____|
                |null________|null________|smith@contoso.com____|
                |null________|null________|jsmith@contoso.com___|
                
                 */
                
                //copying values from claimDataArr to result[][]
                for(int x=0;x < claimDataArr.Length;x++)
                {
                    for (int y = 0; y < claimDataArr[x].Count; y++)
                    {
                        returnData[y][x] = claimDataArr[x][y].ToString();
                    }
                }
            }

            catch (AttributeStoreQueryExecutionException ex)
            {
                if (withAttributeStoreQueryExecutionException) e = ex;
                else returnData = new string[0][];
            }
            catch (Exception ex)
            {
                if (withAttributeStoreQueryExecutionException)
                {
                    string msg;
                    try { msg = ex.Message; }
                    catch { msg = "The Response received from the server is an error"; }
                    e = new AttributeStoreQueryExecutionException(ex.GetType() + ": " + msg, ex);
                }
                else
                {
                    returnData = new string[0][];
                }
            }
            TypedAsyncResult<string[][]> typed = (TypedAsyncResult<string[][]>)state.AsyncResult;
            typed.Complete(returnData, false, e);
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