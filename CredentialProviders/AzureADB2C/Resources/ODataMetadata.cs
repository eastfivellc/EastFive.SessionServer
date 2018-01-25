using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.AzureADB2C.Resources
{
    public class ODataMetadata<TData>
    {
        [JsonProperty("odata.metadata")]
        public string Metadata { get; set; }

        [JsonProperty("value")]
        public TData Value { get; set; }
    }
}

//{
//    "odata.metadata":"https://graph.windows.net/humagelorderowladb2cdev.onmicrosoft.com/$metadata#directoryObjects/Microsoft.DirectoryServices.User",
//    "value":[
//        {
//            "odata.type":"Microsoft.DirectoryServices.User",
//            "objectType":"User",
//            "objectId":"d250476f-cc2e-45da-9008-fe3b2f0870f6","deletionTimestamp":null,"accountEnabled":true,"signInNames":[{"type":"emailAddress","value":"keith@eastfive.com"}],"assignedLicenses":[],"assignedPlans":[],"city":"Spring Hill","companyName":null,"country":"United States","creationType":"LocalAccount","department":null,"dirSyncEnabled":null,"displayName":"KDH","facsimileTelephoneNumber":null,"givenName":"Keith","immutableId":null,"isCompromised":null,"jobTitle":"VP","lastDirSyncTime":null,"mail":null,
//            "mailNickname":"6803e4da-6bf7-4084-b631-597b22f9bd93",
//            "mobile":null,"onPremisesSecurityIdentifier":null,"otherMails":[],
//            "passwordPolicies":"DisablePasswordExpiration","passwordProfile":null,
//            "physicalDeliveryOfficeName":null,"postalCode":"37174","preferredLanguage":null,
//            "provisionedPlans":[],"provisioningErrors":[],"proxyAddresses":[],
//            "refreshTokensValidFromDateTime":"2016-08-31T12:20:50Z","showInAddressList":null,"sipProxyAddress":null,
//            "state":"TN","streetAddress":"1024 Cantwell Place","surname":"Holloway","telephoneNumber":null,
//            "usageLocation":null,
//            "userPrincipalName":"6803e4da-6bf7-4084-b631-597b22f9bd93@humagelorderowladb2cdev.onmicrosoft.com",
//            "userType":"Member"
//        }]
//}