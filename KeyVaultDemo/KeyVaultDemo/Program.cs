using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Azure.KeyVault;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace KeyVaultDemo
{
    class Program
    {
        const string APPLICATION_ID = "dc0a245d-a60f-4c5f-8aca-cbd26e7dfb80";
        const string CERTIFICATE_THUMB_PRINT = "49C4B8E869992B4D7CAB7BB204EA33058BDBDB88";
        const string VAULT_BASE_URL = "https://kc-app-keyvault.vault.azure.net/";

        static void Main(string[] args)
        {
            var client = GetClient();
            var secret = client.GetSecretAsync(VAULT_BASE_URL, "Password").GetAwaiter().GetResult();
            Console.WriteLine("Value for 'Password' Secret - " + secret.Value);
            Console.ReadLine();
        }

        private static KeyVaultClient GetClient() => new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(async (string authority, string resource, string scope) =>
        {
            var authenticationContext = new AuthenticationContext(authority, null);
            X509Certificate2 certificate;
            X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            try
            {
                store.Open(OpenFlags.ReadOnly);
                X509Certificate2Collection certificateCollection = store.Certificates.Find(X509FindType.FindByThumbprint, CERTIFICATE_THUMB_PRINT, false);
                if (certificateCollection == null || certificateCollection.Count == 0)
                {
                    throw new Exception("Certificate not installed in the store");
                }

                certificate = certificateCollection[0];
            }
            finally
            {
                store.Close();
            }

            var clientAssertionCertificate = new ClientAssertionCertificate(APPLICATION_ID, certificate);
            var result = authenticationContext.AcquireTokenAsync(resource, clientAssertionCertificate).GetAwaiter().GetResult();
            return result.AccessToken;
        }));
    }
}

//static KeyVaultClient GetClient() => new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(async (string authority, string resource, string scope) =>
//{
//    var context = new AuthenticationContext(authority, TokenCache.DefaultShared);
//    ClientCredential clientCred = new ClientCredential(APPLICATION_ID, APPLICATION_SECRET);
//    var authResult = await context.AcquireTokenAsync(resource, clientCred);
//    return authResult.AccessToken;
//}));

//49C4B8E869992B4D7CAB7BB204EA33058BDBDB88



//const string APPLICATION_ID = "42bfa3e0-1c0f-4928-a5b8-803ca177c7a2";
//const string APPLICATION_SECRET = "po3kEL15JOXT7wi4BthZQBgOjSl8a4fuQ14NcBB9LRY=";
//const string VAULT_BASE_URL = "https://kc-app-keyvault.vault.azure.net/";

//static void Main(string[] args)
//{
//    var client = GetClient();
//    var secret = client.GetSecretAsync(VAULT_BASE_URL, "Password").GetAwaiter().GetResult();
//    Console.WriteLine("Value for 'Password' Secret - " + secret.Value);
//    Console.ReadLine();
//}

//private static KeyVaultClient GetClient() => new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(async (string authority, string resource, string scope) =>
//{
//    var authenticationContext = new AuthenticationContext(authority, null);
//    X509Certificate2 certificate;
//    X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
//    try
//    {
//        store.Open(OpenFlags.ReadOnly);
//        X509Certificate2Collection certificateCollection = store.Certificates.Find(X509FindType.FindByThumbprint, "49C4B8E869992B4D7CAB7BB204EA33058BDBDB88", false);
//        if (certificateCollection == null || certificateCollection.Count == 0)
//        {
//            throw new Exception("Certificate not installed in the store");
//        }

//        certificate = certificateCollection[0];
//    }
//    finally
//    {
//        store.Close();
//    }

//    var clientAssertionCertificate = new ClientAssertionCertificate("e5f7a95f-653b-4f3c-8f1d-766062d7bdce", certificate);
//    var result = authenticationContext.AcquireTokenAsync(resource, clientAssertionCertificate).GetAwaiter().GetResult();
//    return result.AccessToken;
//}));