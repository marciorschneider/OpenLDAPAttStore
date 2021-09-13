# OpenLdapAttStore

Custom LDAP attribute store with anonymous bind for ADFS 4.0 (Windows Server 2016 ) and 5.0 (Windows Server 2019). Can be used with an OpenLDAP server.


## Why

ADFS comes with an LDAP provider, but the use is limited only for servers that can accept the integrated authentication (mostly AD LDS).
For servers that can only authenticate with simple bind or that accept an anonymous logon the native provider is not a viable solution.

As a bonus, the OpenLdapAttStore provider comes with a more powerful query engine:

* You can specify on each claim issuance rule the LDAP search base.
* Also, you can specify on each rule the LDAP search scope (base, onelevel or subtree).

## Usage

### Installing

* Copy the OpenLDAPStore.dll file to **C:\windows\ADFS** folder.


### Configuring a new attribute store

* On the ADFS console go to Service -> Attribute Stores and select **Add Custom Attribute Store**.
* Write a descriptive name on "Display Name", like *OpenLDAP*.
* On **Custom attribute store class name**: Write *OpenLDAPStore.LdapAnonymousStore,OpenLDAPStore*.
* On **Optional initialization parameters:** add an item with the following parameters:
	* **Parameter name**: *host*
	* **Parameter Value**: use a LDAP server address.

### Optional parameters

* userdn: user DN to authenticate.
* password: password to authenticate.
* ssl: when to use a secure connection.

### Configuring an ADFS custom rule


### Syntax

The query is made of five parts, separated by semicolon:

* LDAP search base. ex: DC=contoso,DC=com
* LDAP search scope (base/onelevel/subtree) 
* LDAP filter. Ex: (cn=john_doe)
* If the query should return the object DN (true/false). Since the distinguishedname is not an attribute but a property's entry some servers have trouble returning it like an attribute.
* LDAP attributes to return, comma delimited. Ex: uid,givenname.


### Query examples


Query for the user *john_doe* on the entire tree and return the UID and givenName attributes:
```
    "dc=contoso,dc=com;subtree;(cn=john_doe);false;uid,givenname"*
```
Query for a group called *marketing* and return the DN:
```
    "dc=contoso,dc=com;subtree;(&(cn=Marketing)(objectclass=group));false;"*
```

### Writing custom rules on ADFS

You need to create a custom rule to use the attribute store.

For example, to get the attribute **manager** from the user *john_doe*:

* Add a new rule and select *send LDAP Attributes as Claims*. Click next.
* On the *Custom Rule:* dialog write:

    ```
        => issue(store = "OpenLDAP", types = ("Contoso/ManagerDN"),
        query = "dc=contoso,dc=com;subtree;(&(objectclass=user)(uid=john_doe));false;managerDN");
 ```

Then with another custom rule you can get the attribute *employeeID* from the previous managerDN:

```
        c:[Type == "Contoso/ManagerDN"]
        => issue(store = "OpenLDAP", types = ("Contoso/ManagerEmployeeID"),
        query = "{0};base;(objectclass=*);false;EmployeeID", param = c.Value);
 ```

## Testing

The package comes with a tool for testing. You need the *OpenLDAPStore.dll* and *Microsoft.IdentityServer.ClaimsPolicy.dll* to use it (it´s located on the C:\Windows\ADFS folder).
### Syntax
TestLdapStore.exe *serveraddress* *query*

### Examples

Getting the uid and givenName attributes from the object *uid=john_doe,cn=j,cn=users,dc=contoso,dc=com*:

```
    TestLdapStore.exe 127.0.0.1 "uid=john_doe,cn=j,cn=users,dc=contoso,dc=com;base;(objectclass=*);false;uid,givenName"
```
