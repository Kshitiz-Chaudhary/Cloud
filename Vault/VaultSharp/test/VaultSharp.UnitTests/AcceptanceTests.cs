﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json;
using VaultSharp.Backends.Audit.Models.File;
using VaultSharp.Backends.Authentication.Models;
using VaultSharp.Backends.Authentication.Models.AppId;
using VaultSharp.Backends.Authentication.Models.Token;
using VaultSharp.Backends.Secret.Models;
using VaultSharp.Backends.Secret.Models.AWS;
using VaultSharp.Backends.Secret.Models.Cassandra;
using VaultSharp.Backends.Secret.Models.Consul;
using VaultSharp.Backends.Secret.Models.MicrosoftSql;
using VaultSharp.Backends.Secret.Models.MongoDb;
using VaultSharp.Backends.Secret.Models.MySql;
using VaultSharp.Backends.Secret.Models.PKI;
using VaultSharp.Backends.Secret.Models.PostgreSql;
using VaultSharp.Backends.Secret.Models.RabbitMQ;
using VaultSharp.Backends.Secret.Models.SSH;
using VaultSharp.Backends.Secret.Models.Transit;
using VaultSharp.Backends.System.Models;
using VaultSharp.DataAccess;
using Xunit;

namespace VaultSharp.UnitTests
{
    public class AcceptanceTests
    {
        // Acceptance tests setup tests:
        // 1. Ensure you have the right version of Vault.exe installed on the machine running this test.
        // 2. Fill in the variables below in SetupData.
        // 3. Please ensure this is not your production vault or anything. And this is a box you can play with.
        // 4. The acceptance tests will setup a temporary file backend, run the tests and tear it down finally.

        /// <summary>
        /// Change the data in this class as suitable.
        /// This is the only class you should be modifying for the acceptance tests to run successfully.
        /// </summary>
        private static class SetupData
        {
            // the best thing to do would be create a temp folder, and have vault.exe and any text files within that.
            // that way, all of these acceptance tests will run under the context of that folder.
            // they never delete any folder or tamper with file or credentials.

            // vault.exe that'll be used for this acceptance test run.
            public const string VaultExeFullPath = @"C:\Kshitiz\Sources\Cloud\Vault\vault.exe";

            // turn on, if you want aws tests. if yes, provide a credential text file,
            // with access-key on line 1 and secret on line 2 and region on line 3.
            // create an IAM user with admin access and use the secret below for least hassle.
            public const bool RunAwsSecretBackendAcceptanceTests = false;
            public const string AwsCredentialsFullPath = @"c:\temp\raja\vaultsharp-acceptance-tests\aws.txt";

            // turn on, if you want cassandra tests. if yes, provide a credential text file,
            // with hosts on line 1 and root-username on line 2 and root-password on line 3.
            // once you install Cassandra, 
            // 1. set authenticator: AllowAllAuthenticator to authenticator: PasswordAuthenticator in cassandra.yaml.
            // 2. also change AllowAllAuthorizer to CassandraAuthorizer
            // 3. default cassandra superuser is cassandra/cassandra, on localhost:9042
            // 4. restart service.
            public const bool RunCassandraSecretBackendAcceptanceTests = false;
            public const string CassandraCredentialsFullPath = @"c:\temp\raja\vaultsharp-acceptance-tests\cassandra.txt";

            // install Consul and start it up
            //  .\consul.exe agent -config-file .\c.json
            public const bool RunConsulSecretBackendAcceptanceTests = false;
            public const string ConsulCredentialsFullPath = @"c:\temp\raja\vaultsharp-acceptance-tests\consul.txt";

            // https://docs.mongodb.com/manual/tutorial/install-mongodb-on-windows/
            // create a root user as follows: http://stackoverflow.com/a/29090991/1174414

            // startup mongodb server as follows: 
            // cd "C:\Program Files\MongoDB\Server\3.2\bin"
            // mongod.exe
            public const bool RunMongoDbSecretBackendAcceptanceTests = false;
            public const string MongoDbCredentialsFullPath = @"c:\temp\raja\vaultsharp-acceptance-tests\mongodb.txt";

            // install sql 2012 express
            // surface area >> open up tcp
            // raja todo: not complete. connection issues with connection string.
            // doesn't work with sqlexpress.
            // tried it with the desktop sql server machine, the connection part succeeds, but writing the role fails.
            // driver: bad connection error. maybe a sql 2012 vs. sql 2014 issue. either ways.. leave it for now.
            public const bool RunMicrosoftSqlSecretBackendAcceptanceTests = false;
            public const string MicrosoftSqlCredentialsFullPath = @"c:\temp\raja\vaultsharp-acceptance-tests\mssql.txt";

            // install mysql
            public const bool RunMySqlSecretBackendAcceptanceTests = false;
            public const string MySqlCredentialsFullPath = @"c:\temp\raja\vaultsharp-acceptance-tests\mysql.txt";

            // install postgresql
            // pgadmin, setup superuser (postgres), ?sslmode=disable
            public const bool RunPostgreSqlSecretBackendAcceptanceTests = false;
            public const string PostgreSqlCredentialsFullPath = @"c:\temp\raja\vaultsharp-acceptance-tests\postgresql.txt";

            // insttall erlang otp, rabbitmq server, then follow this thread http://stackoverflow.com/questions/28258392/rabbitmq-has-nodedown-error/34538688#34538688
            // since msi doesn't install the service properly. then run the plugin
            // launch http://localhost:15672
            public const bool RunRabbitMQSecretBackendAcceptanceTests = false;
            public const string RabbitMQCredentialsFullPath = @"c:\temp\raja\vaultsharp-acceptance-tests\rabbitmq.txt";
        }

        // no need to modify these values.

        private const string FileBackendsFolderName = "per_run_file_backends_delete_anytime";
        private const string VaultConfigsFolderName = "per_run_vault_configs_delete_anytime";
        private const string FileBackendPlaceHolder = "##FILE_BACKEND_PATH##";
        private const string VaultConfigPath = "acceptance-tests-vault-config.hcl";
        private static readonly Uri VaultUriWithPort = new Uri("http://127.0.0.1:8200");

        private static readonly IVaultClient UnauthenticatedVaultClient = VaultClientFactory.CreateVaultClient(
            VaultUriWithPort, null);

        private static IVaultClient _authenticatedVaultClient;

        private static Process _vaultProcess;
        private static MasterCredentials _masterCredentials;

        /// <summary>
        /// The one stop test for all the Vault APIs.
        /// </summary>
        /// <returns></returns>
        [Fact(Skip = "Invoke this manually since this is an acceptance test set with server processes.")]
        //[Fact]
        public async Task RunAllAcceptanceTestsAsync()
        {
            try
            {
                StartupVaultServer();

                await RunInitApiTests();
                await RunSealApiTests();
                await RunGenerateRootApiTests();
                await RunSecretBackendMountApiTests();
                await RunAuthenticationBackendMountApiTests();
                await RunPolicyApiTests();
                await RunCapabilitiesApiTests();
                await RunAuditBackendMountApiTests();
                await RunLeaseApiTests();
                await RunWrapApiTests();
                await RunLeaderApiTests();
                await RunRekeyApiTests();
                await RunRawSecretApiTests();

                // secret backend tests.

                await RunAwsSecretBackendApiTests();
                await RunCassandraSecretBackendApiTests();
                await RunConsulSecretBackendApiTests();
                await RunCubbyholeSecretBackendApiTests();
                await RunGenericSecretBackendApiTests();
                await RunMongoDbSecretBackendApiTests();
                await RunMicrosoftSqlSecretBackendApiTests();
                await RunMySqlSecretBackendApiTests();
                await RunPkiSecretBackendApiTests();
                await RunPostgreSqlSecretBackendApiTests();
                await RunRabbitMQSecretBackendApiTests();
                await RunSSHSecretBackendApiTests();
                await RunTransitSecretBackendApiTests();

                // authentication backend tests

                await RunAppIdAuthenticationBackendApiTests();
                await RunAppRoleAuthenticationBackendApiTests();

                await RunTokenAuthenticationBackendApiTests();

                await RunPrimitiveWriteReadSecretApiTests();
            }
            finally
            {
                ShutdownVaultServer();
            }
        }

        private static async Task RunPrimitiveWriteReadSecretApiTests()
        {
            var path = "cubbyhole/foo/test";

            var secretData = new Dictionary<string, object>
            {
                {"1", "1"},
                {"2", 2},
                {"3", false},
            };

            var result = await _authenticatedVaultClient.WriteSecretAsync(path, secretData);
            Assert.Null(result);

            var secret = await _authenticatedVaultClient.ReadSecretAsync(path);
            Assert.True(secret.Data.Count == 3);

            await _authenticatedVaultClient.DeleteSecretAsync(path);

            await Assert.ThrowsAsync<Exception>(() => _authenticatedVaultClient.ReadSecretAsync(path));
        }

        private static async Task RunTokenAuthenticationBackendApiTests()
        {
            var accessors = await _authenticatedVaultClient.GetTokenAccessorListAsync();
            Assert.True(accessors.Data.Keys.Any());

            var tokenRoleDefinition = new TokenRoleDefinition
            {
                RoleName = Guid.NewGuid().ToString(),
                Renewable = true,
                AllowedPolicies = "root",
                Orphan = true,
                PathSuffix = "suffix1"
            };

            await _authenticatedVaultClient.WriteTokenRoleInfoAsync(tokenRoleDefinition);

            var readRole = await _authenticatedVaultClient.GetTokenRoleInfoAsync(tokenRoleDefinition.RoleName);
            Assert.Equal(tokenRoleDefinition.PathSuffix, readRole.Data.PathSuffix);

            tokenRoleDefinition.PathSuffix = "suffix2";
            await _authenticatedVaultClient.WriteTokenRoleInfoAsync(tokenRoleDefinition);
            readRole = await _authenticatedVaultClient.GetTokenRoleInfoAsync(tokenRoleDefinition.RoleName);
            Assert.Equal(tokenRoleDefinition.PathSuffix, readRole.Data.PathSuffix);

            var tokenCreationOptions = new TokenCreationOptions
            {
                RoleName = tokenRoleDefinition.RoleName
            };

            var tokenFromRole = await _authenticatedVaultClient.CreateTokenAsync(tokenCreationOptions);
            Assert.NotNull(tokenFromRole.AuthorizationInfo.ClientToken);

            tokenCreationOptions.RoleName = null;
            tokenCreationOptions.CreateAsOrphan = true;

            var orphanToken = await _authenticatedVaultClient.CreateTokenAsync(tokenCreationOptions);
            Assert.NotNull(orphanToken.AuthorizationInfo.ClientToken);

            var lookupInfo = await _authenticatedVaultClient.GetTokenInfoAsync(orphanToken.AuthorizationInfo.ClientToken);
            Assert.NotNull(lookupInfo.Data.Id);

            var lookupInfoByAccessor = await _authenticatedVaultClient.GetTokenInfoByAccessorAsync(orphanToken.AuthorizationInfo.ClientTokenAccessor);
            Assert.NotNull(lookupInfoByAccessor.Data.Id);

            var selfInfo = await _authenticatedVaultClient.GetCallingTokenInfoAsync();
            Assert.NotNull(selfInfo.Data.Id);

            // renew token
            // renew self by new client. ??

            await _authenticatedVaultClient.RevokeTokenAsync(tokenFromRole.AuthorizationInfo.ClientToken, false);
            await _authenticatedVaultClient.RevokeTokenByAccessorAsync(orphanToken.AuthorizationInfo.ClientTokenAccessor);

            var roles = await _authenticatedVaultClient.GetTokenRoleListAsync();
            Assert.True(roles.Data.Keys.Any());

            await _authenticatedVaultClient.DeleteTokenRoleAsync(tokenRoleDefinition.RoleName);
        }

        private static async Task RunAppRoleAuthenticationBackendApiTests()
        {
            var path = "approle" + Guid.NewGuid();

            try
            {
                var authBackend = new AuthenticationBackend
                {
                    BackendType = AuthenticationBackendType.AppRole,
                    AuthenticationPath = path
                };

                await _authenticatedVaultClient.EnableAuthenticationBackendAsync(authBackend);

                var authBackends = await _authenticatedVaultClient.GetAllEnabledAuthenticationBackendsAsync();
                Assert.True(authBackends.Data.Any(b => b.BackendType == AuthenticationBackendType.AppRole));

                var roleName = "testRole";
                var rolePath = "auth/" + path + "/role/" + roleName;

                // test something that returns data on Write.
                await
                    _authenticatedVaultClient.WriteSecretAsync(rolePath,
                        new Dictionary<string, object>
                        {
                            {"secret_id_ttl", "10m"},
                            {"token_ttl", "20m"},
                            {"token_max_ttl", "30m"},
                            {"secret_id_num_uses", 40}
                        });

                var role = await _authenticatedVaultClient.ReadSecretAsync(rolePath+"/role-id");
                Assert.True(role.Data.ContainsKey("role_id"));

                // raja todo.. cannot write to temp file.. cert like problem.
                //var secret = await _authenticatedVaultClient.WriteSecretAsync(rolePath + "/secret-id", null);
                //Assert.True(secret.Data.ContainsKey("secret_id"));
                //Assert.True(secret.Data.ContainsKey("secret_id_accessor"));

                // raja todo. add strong API methods as well.

                // raja todo.. run more tests once api work is done.
                // await Assert.ThrowsAsync<Exception>(() => _authenticatedVaultClient.AppRoleAuthenticationGetRolesAsync());
            }
            finally
            {
                await _authenticatedVaultClient.DisableAuthenticationBackendAsync(path);
            }
        }

        private static async Task RunAppIdAuthenticationBackendApiTests()
        {
            var path = "app-id" + Guid.NewGuid();

            try
            {
                var policy = new Policy
                {
                    Name = "app-id-test-policy",
                    Rules = "path \"sys/*\" {  policy = \"read\" }"
                };

                await _authenticatedVaultClient.WritePolicyAsync(policy);

                var appId = Guid.NewGuid().ToString();
                var userId = Guid.NewGuid().ToString();

                var appIdAuthenticationInfo = new AppIdAuthenticationInfo(path, appId, userId);
                var appidClient = VaultClientFactory.CreateVaultClient(VaultUriWithPort, appIdAuthenticationInfo);

                var appIdAuthBackend = new AuthenticationBackend
                {
                    BackendType = AuthenticationBackendType.AppId,
                    AuthenticationPath = path
                };

                await _authenticatedVaultClient.EnableAuthenticationBackendAsync(appIdAuthBackend);

                await _authenticatedVaultClient.AppIdAuthenticationConfigureAppIdAsync(appId, policy.Name, appId, path);
                await _authenticatedVaultClient.AppIdAuthenticationConfigureUserIdAsync(userId, appId, authenticationPath: path);

                var authBackends = await appidClient.GetAllEnabledAuthenticationBackendsAsync();
                Assert.True(authBackends.Data.Any());

                await _authenticatedVaultClient.DeletePolicyAsync(policy.Name);
            }
            finally
            {
                await _authenticatedVaultClient.DisableAuthenticationBackendAsync(path);
            }
        }

        private static async Task RunTransitSecretBackendApiTests()
        {
            var mountPoint = "transit" + Guid.NewGuid();

            try
            {
                var backend = new SecretBackend
                {
                    BackendType = SecretBackendType.Transit,
                    MountPoint = mountPoint,
                };

                await _authenticatedVaultClient.MountSecretBackendAsync(backend);

                var keyName = "test_key" + Guid.NewGuid();
                await _authenticatedVaultClient.TransitCreateEncryptionKeyAsync(keyName, TransitKeyType.aes256_gcm96, true, true, backend.MountPoint);

                var keyInfo = await _authenticatedVaultClient.TransitGetEncryptionKeyInfoAsync(keyName, backend.MountPoint);

                Assert.Equal(keyName, keyInfo.Data.Name);
                Assert.Equal(keyInfo.Data.KeyType, TransitKeyType.aes256_gcm96);
                Assert.True(keyInfo.Data.MustUseKeyDerivation);
                Assert.False(keyInfo.Data.IsDeletionAllowed);

                // raja todo vault gives internal errors.
                // await RunWrapUnwrapCheck(_authenticatedVaultClient.TransitGetEncryptionKeyInfoAsync(keyName, backend.MountPoint, wrapTimeToLive: "1m"));

                var keyName2 = "test_key" + Guid.NewGuid();
                await _authenticatedVaultClient.TransitCreateEncryptionKeyAsync(keyName2, TransitKeyType.ecdsa_p256, false, false, backend.MountPoint);

                var keyList = await _authenticatedVaultClient.TransitGetEncryptionKeyListAsync(backend.MountPoint);
                Assert.True(keyList.Data.Keys.Count == 2);

                await RunWrapUnwrapCheck(_authenticatedVaultClient.TransitGetEncryptionKeyListAsync(backend.MountPoint, wrapTimeToLive: "1m"));

                await _authenticatedVaultClient.TransitConfigureEncryptionKeyAsync(keyName, isDeletionAllowed: true, transitBackendMountPoint: backend.MountPoint);

                keyInfo = await _authenticatedVaultClient.TransitGetEncryptionKeyInfoAsync(keyName, backend.MountPoint);
                Assert.True(keyInfo.Data.IsDeletionAllowed);

                var context = "context1";
                var plainText = "raja";
                var encodedPlainText = Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));

                var nonce = Convert.ToBase64String(Enumerable.Range(0, 12).Select(i => (byte)i).ToArray());
                var nonce2 = Convert.ToBase64String(Enumerable.Range(0, 12).Select(i => (byte)(i + 1)).ToArray());

                var cipherText = await _authenticatedVaultClient.TransitEncryptAsync(keyName, encodedPlainText, context, nonce, transitBackendMountPoint: backend.MountPoint);
                var convergentCipherText = await _authenticatedVaultClient.TransitEncryptAsync(keyName, encodedPlainText, context, nonce, transitBackendMountPoint: backend.MountPoint);

                Assert.Equal(convergentCipherText.Data.CipherText, cipherText.Data.CipherText);

                // raja todo. vault internal errors
                // await RunWrapUnwrapCheck(_authenticatedVaultClient.TransitEncryptAsync(keyName, encodedPlainText, context, nonce, transitBackendMountPoint: backend.MountPoint, wrapTimeToLive: "1m"));

                // raja todo: fix this
                // var nonConvergentCipherText = await _authenticatedVaultClient.TransitEncryptAsync(keyName, encodedPlainText, context, nonce2, transitBackendMountPoint: backend.MountPoint);
                // Assert.NotEqual(nonConvergentCipherText.Data.CipherText, cipherText.Data.CipherText);

                var plainText2 = Encoding.UTF8.GetString(Convert.FromBase64String((await _authenticatedVaultClient.TransitDecryptAsync(keyName, cipherText.Data.CipherText, context, nonce, backend.MountPoint)).Data.PlainText));
                Assert.Equal(plainText, plainText2);

                await _authenticatedVaultClient.TransitRotateEncryptionKeyAsync(keyName, backend.MountPoint);
                var cipherText2 = await _authenticatedVaultClient.TransitEncryptAsync(keyName, encodedPlainText, context, nonce, transitBackendMountPoint: backend.MountPoint);

                Assert.NotEqual(cipherText.Data.CipherText, cipherText2.Data.CipherText);

                await _authenticatedVaultClient.TransitRewrapWithLatestEncryptionKeyAsync(keyName, cipherText.Data.CipherText, context, nonce, backend.MountPoint);

                var newKey1 = await _authenticatedVaultClient.TransitCreateDataKeyAsync(keyName, false, context, nonce, 128, backend.MountPoint);
                Assert.Null(newKey1.Data.PlainTextKey);

                // raja todo. vault internal errors
                // await RunWrapUnwrapCheck(_authenticatedVaultClient.TransitCreateDataKeyAsync(keyName, false, context, nonce, 128, backend.MountPoint, wrapTimeToLive: "1m"));

                newKey1 = await _authenticatedVaultClient.TransitCreateDataKeyAsync(keyName, true, context, nonce, 128, backend.MountPoint);
                Assert.NotNull(newKey1.Data.PlainTextKey);

                var randomBytes = await _authenticatedVaultClient.TransitGenerateRandomBytes(64, transitBackendMountPoint: backend.MountPoint);
                Assert.NotNull(randomBytes.Data.random_bytes);

                await RunWrapUnwrapCheck(_authenticatedVaultClient.TransitGenerateRandomBytes(64, transitBackendMountPoint: backend.MountPoint, wrapTimeToLive: "1m"));

                var hash = await _authenticatedVaultClient.TransitHashInput(encodedPlainText, transitBackendMountPoint: backend.MountPoint);
                Assert.NotNull(hash.Data.sum);

                await RunWrapUnwrapCheck(_authenticatedVaultClient.TransitHashInput(encodedPlainText, transitBackendMountPoint: backend.MountPoint, wrapTimeToLive: "1m"));

                var hmac = await _authenticatedVaultClient.TransitDigestInput(keyName, encodedPlainText, transitBackendMountPoint: backend.MountPoint);
                Assert.NotNull(hmac.Data.hmac);

                // raja todo. vault internal errors
                // await RunWrapUnwrapCheck(_authenticatedVaultClient.TransitDigestInput(keyName, encodedPlainText, transitBackendMountPoint: backend.MountPoint, wrapTimeToLive: "1m"));

                // aes key is not good for signing. use other one.
                var sign = await _authenticatedVaultClient.TransitSignInput(keyName2, encodedPlainText, transitBackendMountPoint: backend.MountPoint);
                Assert.NotNull(sign.Data.signature);

                // raja todo. vault internal errors
                // await RunWrapUnwrapCheck(_authenticatedVaultClient.TransitSignInput(keyName2, encodedPlainText, transitBackendMountPoint: backend.MountPoint, wrapTimeToLive: "1m"));

                var hmacValid =
                    await
                        _authenticatedVaultClient.TransitVerifySignature(keyName, encodedPlainText, null, (string)hmac.Data.hmac,
                            transitBackendMountPoint: backend.MountPoint);
                Assert.True((bool)hmacValid.Data.valid);

                // raja todo. vault internal errors
                // await RunWrapUnwrapCheck(_authenticatedVaultClient.TransitVerifySignature(keyName, encodedPlainText, null, (string)hmac.Data.hmac, transitBackendMountPoint: backend.MountPoint, wrapTimeToLive: "1m"));

                var signValid =
                    await
                        _authenticatedVaultClient.TransitVerifySignature(keyName2, encodedPlainText, (string)sign.Data.signature, null,
                            transitBackendMountPoint: backend.MountPoint);
                Assert.True((bool)signValid.Data.valid);

                // raja todo. vault internal errors
                // await RunWrapUnwrapCheck(_authenticatedVaultClient.TransitVerifySignature(keyName2, encodedPlainText, (string)sign.Data.signature, null, transitBackendMountPoint: backend.MountPoint, wrapTimeToLive: "1m"));

                await _authenticatedVaultClient.TransitDeleteEncryptionKeyAsync(keyName, backend.MountPoint);
            }
            finally
            {
                await _authenticatedVaultClient.UnmountSecretBackendAsync(mountPoint);
            }
        }

        private static async Task RunSSHSecretBackendApiTests()
        {
            var mountPoint = "ssh" + Guid.NewGuid();

            try
            {
                var backend = new SecretBackend
                {
                    BackendType = SecretBackendType.SSH,
                    MountPoint = mountPoint,
                };

                await _authenticatedVaultClient.MountSecretBackendAsync(backend);

                var sshKeyName = Guid.NewGuid().ToString();
                var sshRoleName = Guid.NewGuid().ToString();

                var privateKey = @"-----BEGIN RSA PRIVATE KEY-----
MIICXgIBAAKBgQC2+cfxgJ5LsWAq+vRZB77pCwy5P+tnLahCeq4OBViloSfKVq/y
Hq/u3YScNNoqkailjmOMJtzKDD9W7dNasfu5zGWxjLUL4NwasbEK1jseKfbwKjmc
Nw1KYByx5BTECN0l5FxGUkQQVSmwJvqgyXDEHCsAvC72x96uBk2qJTAoLwIDAQAB
AoGBALXyCvAKhV2fM5GJmhAts5joc+6BsQMYU4hHlWw7xLpuVbLOIIcSHL/ZZlQt
+gL6dEisHjDvM/110EYQl2pIMZYO+WU+OSmRKU8U12bjDmoypONZokBplXsVDeY4
vbb7yVmOpazr/lpM4cqxL7TeRgxypQT08t7ukgt/7NOSHx0BAkEA8B0YXsxvxJLp
g1LmCnP0L3vcsRw4wLNtEBfmJc/okknIyIAadLBW5mFXxQNIjj1JGTGbK/lbedBP
ypVgY5l9uQJBAMMU6qtupP671bzEXACt6Gst/qyx7vMHMc7yRdckrXr5Wl/uyxDC
BbErr5xg6e6qi3HnZBQbYbnYVn6gI4u2iScCQQDhK0e5TpnZi7Oz1T+ouchZ5xu0
czS9cQVrvB21g90jolHJxGgK2XsEnHCEbmnSCaLNH3nWqQahmznYTnCPtlbxAkAE
WhUaGe/IVvxfp6m9wiNrMK17wMRp24E68qCoOgM8uQ9REIyrJQjneOgD/w1464kM
03KiGDJH6RGU5ZGlbj8FAkEAmm9GGdG4/rcI2o5kUQJWOPkr2KPy/X34FyD9etbv
TRzfAZxw7q483/Y7mZ63/RuPYKFei4xFBfjzMDYm1lT4AQ==
-----END RSA PRIVATE KEY-----";

                var ip = "127.0.0.1";
                var user = "rajan";

                await _authenticatedVaultClient.SSHWriteNamedKeyAsync(sshKeyName, privateKey, mountPoint);

                // update
                await _authenticatedVaultClient.SSHWriteNamedKeyAsync(sshKeyName, privateKey, mountPoint);

                var sshOTPRoleDefinition = new SSHOTPRoleDefinition
                {
                    RoleDefaultUser = user,
                    CIDRValues = "127.0.0.1/10",
                    Port = 22
                };

                await _authenticatedVaultClient.SSHWriteNamedRoleAsync(sshRoleName, sshOTPRoleDefinition, mountPoint);

                var role = await _authenticatedVaultClient.SSHReadNamedRoleAsync(sshRoleName, mountPoint);
                Assert.True(role.Data.KeyTypeToGenerate == SSHKeyType.otp);
                Assert.Equal(sshOTPRoleDefinition.CIDRValues, role.Data.CIDRValues);

                await RunWrapUnwrapCheck(_authenticatedVaultClient.SSHReadNamedRoleAsync(sshRoleName, mountPoint, wrapTimeToLive: "1m"));

                var rolename2 = "sshrolename2";
                await _authenticatedVaultClient.SSHWriteNamedRoleAsync(rolename2, sshOTPRoleDefinition, mountPoint);

                var roleList = await _authenticatedVaultClient.SSHReadRoleListAsync(mountPoint);
                Assert.True(roleList.Data.Keys.Count == 2);

                await RunWrapUnwrapCheck(_authenticatedVaultClient.SSHReadRoleListAsync(mountPoint, wrapTimeToLive: "1m"));

                var roleNames = string.Join(",", sshRoleName, rolename2);
                await _authenticatedVaultClient.SSHConfigureZeroAddressRolesAsync(roleNames, mountPoint);

                var readRoles = await _authenticatedVaultClient.SSHReadZeroAddressRolesAsync(mountPoint);
                Assert.Equal(2, readRoles.Data.Roles.Count);

                await RunWrapUnwrapCheck(_authenticatedVaultClient.SSHReadZeroAddressRolesAsync(mountPoint, wrapTimeToLive: "1m"));

                await _authenticatedVaultClient.SSHDeleteZeroAddressRolesAsync(mountPoint);

                var credentials = await
                    _authenticatedVaultClient.SSHGenerateDynamicCredentialsAsync(sshRoleName, ip,
                        sshBackendMountPoint: mountPoint);

                Assert.NotNull(credentials.Data.Key);

                await RunWrapUnwrapCheck(_authenticatedVaultClient.SSHGenerateDynamicCredentialsAsync(sshRoleName, ip, sshBackendMountPoint: mountPoint, wrapTimeToLive: "1m"));

                var roles = await _authenticatedVaultClient.SSHLookupRolesAsync(ip, mountPoint);
                Assert.NotNull(roles.Data.Roles[0]);

                await RunWrapUnwrapCheck(_authenticatedVaultClient.SSHLookupRolesAsync(ip, mountPoint, wrapTimeToLive: "1m"));

                await Assert.ThrowsAsync<Exception>(() => UnauthenticatedVaultClient.SSHVerifyOTPAsync("blahblah", mountPoint));

                var v3 = await _authenticatedVaultClient.SSHVerifyOTPAsync(credentials.Data.Key, mountPoint);
                Assert.NotNull(v3.Data.RoleName);

                // otp credentials cannot be used to test wrapping. generate new ones.
                var wrapTestCredentials = await
                    _authenticatedVaultClient.SSHGenerateDynamicCredentialsAsync(sshRoleName, ip,
                        sshBackendMountPoint: mountPoint);

                await RunWrapUnwrapCheck(_authenticatedVaultClient.SSHVerifyOTPAsync(wrapTestCredentials.Data.Key, mountPoint, wrapTimeToLive: "1m"));

                var dynamicRoleName = Guid.NewGuid().ToString();

                await _authenticatedVaultClient.SSHWriteNamedRoleAsync(dynamicRoleName, new SSHDynamicRoleDefinition
                {
                    RoleDefaultUser = user,
                    CIDRValues = "127.0.0.1/10",
                    AdminUser = user,
                    KeyName = sshKeyName
                }, mountPoint);

                var dynamicRole = await _authenticatedVaultClient.SSHReadNamedRoleAsync(dynamicRoleName, mountPoint);
                Assert.True(dynamicRole.Data.KeyTypeToGenerate == SSHKeyType.dynamic);

                await RunWrapUnwrapCheck(_authenticatedVaultClient.SSHReadNamedRoleAsync(dynamicRoleName, mountPoint, wrapTimeToLive: "1m"));

                // error adding public key to authorized_keys file in target
                // var dynamicCredentials = await _authenticatedVaultClient.SSHGenerateDynamicCredentialsAsync(dynamicRoleName, ip, sshBackendMountPoint: mountPoint);
                // Assert.NotNull(dynamicCredentials.Data.Key);
                // await RunWrapUnwrapCheck

                await _authenticatedVaultClient.SSHDeleteNamedRoleAsync(sshRoleName, mountPoint);
                await _authenticatedVaultClient.SSHDeleteNamedRoleAsync(rolename2, mountPoint);

                await _authenticatedVaultClient.SSHDeleteNamedKeyAsync(sshKeyName, mountPoint);
            }
            finally
            {
                await _authenticatedVaultClient.UnmountSecretBackendAsync(mountPoint);
            }
        }

        private static async Task RunRabbitMQSecretBackendApiTests()
        {
            if (SetupData.RunRabbitMQSecretBackendAcceptanceTests)
            {
                try
                {
                    if (!File.Exists(SetupData.RabbitMQCredentialsFullPath))
                    {
                        throw new Exception("RabbitMQ Credential file does not exist: " +
                                            SetupData.RabbitMQCredentialsFullPath);
                    }

                    var credentialsFileContent = File.ReadAllLines(SetupData.RabbitMQCredentialsFullPath);

                    if (credentialsFileContent.Count() < 3)
                    {
                        throw new Exception("RabbitMQ Credential file needs at least 3 lines: " +
                                            credentialsFileContent);
                    }

                    var connectionInfo = new RabbitMQConnectionInfo
                    {
                        ConnectionUri = credentialsFileContent[0],
                        Username = credentialsFileContent[1],
                        Password = credentialsFileContent[2],
                        VerifyConnection = true
                    };

                    await _authenticatedVaultClient.QuickMountSecretBackendAsync(SecretBackendType.RabbitMQ);
                    await _authenticatedVaultClient.RabbitMQConfigureConnectionAsync(connectionInfo);

                    // var connection = await _authenticatedVaultClient.RabbitMQReadConnectionInfoAsync();
                    // Assert.Equal(connectionInfo.ConnectionUri, connection.Data.ConnectionUri);

                    var lease = new CredentialTimeToLiveSettings
                    {
                        TimeToLive = "1m1s",
                        MaximumTimeToLive = "2m1s"
                    };

                    await _authenticatedVaultClient.RabbitMQConfigureCredentialLeaseSettingsAsync(lease);

                    var queriedLease = await _authenticatedVaultClient.RabbitMQReadCredentialLeaseSettingsAsync();
                    Assert.Equal("61", queriedLease.Data.TimeToLive);
                    Assert.Equal("121", queriedLease.Data.MaximumTimeToLive);

                    await RunWrapUnwrapCheck(_authenticatedVaultClient.RabbitMQReadCredentialLeaseSettingsAsync(wrapTimeToLive: "1m"));

                    var roleName = "rabbitmqrole";

                    var role = new RabbitMQRoleDefinition
                    {
                        VirtualHostPermissions = "{\"/\":{\"write\": \".*\", \"read\": \".*\"}}"
                    };

                    await _authenticatedVaultClient.RabbitMQWriteNamedRoleAsync(roleName, role);

                    var queriedRole = await _authenticatedVaultClient.RabbitMQReadNamedRoleAsync(roleName);
                    Assert.NotNull(queriedRole.Data.VirtualHostPermissions);

                    await RunWrapUnwrapCheck(_authenticatedVaultClient.RabbitMQReadNamedRoleAsync(roleName, wrapTimeToLive: "1m"));

                    var roleName2 = "rabbitmq2";
                    var role2 = new RabbitMQRoleDefinition
                    {
                        VirtualHostPermissions = "{\"/\":{\"write\": \".*\", \"read\": \".*\"}}"
                    };

                    await _authenticatedVaultClient.RabbitMQWriteNamedRoleAsync(roleName2, role2);

                    var roles = await _authenticatedVaultClient.RabbitMQReadRoleListAsync();
                    Assert.True(roles.Data.Keys.Count == 2);

                    await RunWrapUnwrapCheck(_authenticatedVaultClient.RabbitMQReadRoleListAsync(wrapTimeToLive: "1m"));

                    var generatedCreds = await _authenticatedVaultClient.RabbitMQGenerateDynamicCredentialsAsync(roleName);
                    Assert.NotNull(generatedCreds.Data.Password);

                    await RunWrapUnwrapCheck(_authenticatedVaultClient.RabbitMQGenerateDynamicCredentialsAsync(roleName, wrapTimeToLive: "1m"));

                    await _authenticatedVaultClient.RabbitMQDeleteNamedRoleAsync(roleName);
                    await _authenticatedVaultClient.RabbitMQDeleteNamedRoleAsync(roleName2);
                }
                finally
                {
                    try
                    {
                        await _authenticatedVaultClient.QuickUnmountSecretBackendAsync(SecretBackendType.RabbitMQ);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static async Task RunPostgreSqlSecretBackendApiTests()
        {
            // raja todo.. add null check assertions. less imp.

            if (SetupData.RunPostgreSqlSecretBackendAcceptanceTests)
            {
                try
                {
                    if (!File.Exists(SetupData.PostgreSqlCredentialsFullPath))
                    {
                        throw new Exception("PostgreSql Credential file does not exist: " +
                                            SetupData.PostgreSqlCredentialsFullPath);
                    }

                    var credentialsFileContent = File.ReadAllLines(SetupData.PostgreSqlCredentialsFullPath);

                    if (credentialsFileContent.Count() < 1)
                    {
                        throw new Exception("PostgreSql Credential file needs at least 1 line: " +
                                            credentialsFileContent);
                    }

                    var connectionInfo = new PostgreSqlConnectionInfo
                    {
                        ConnectionUrl = credentialsFileContent[0],
                        VerifyConnection = true
                    };

                    await _authenticatedVaultClient.QuickMountSecretBackendAsync(SecretBackendType.PostgreSql);
                    await _authenticatedVaultClient.PostgreSqlConfigureConnectionAsync(connectionInfo);

                    var connection = await _authenticatedVaultClient.PostgreSqlReadConnectionInfoAsync();
                    Assert.Equal(connectionInfo.ConnectionUrl, connection.Data.ConnectionUrl);

                    await RunWrapUnwrapCheck(_authenticatedVaultClient.PostgreSqlReadConnectionInfoAsync(wrapTimeToLive: "1m"));

                    var lease = new CredentialLeaseSettings
                    {
                        LeaseTime = "1m1s",
                        MaximumLeaseTime = "2m1s"
                    };

                    await _authenticatedVaultClient.PostgreSqlConfigureCredentialLeaseSettingsAsync(lease);

                    var queriedLease = await _authenticatedVaultClient.PostgreSqlReadCredentialLeaseSettingsAsync();
                    Assert.Equal(lease.LeaseTime, queriedLease.Data.LeaseTime);
                    Assert.Equal(lease.MaximumLeaseTime, queriedLease.Data.MaximumLeaseTime);

                    await RunWrapUnwrapCheck(_authenticatedVaultClient.PostgreSqlReadCredentialLeaseSettingsAsync(wrapTimeToLive: "1m"));

                    var roleName = "postgresqlrole";

                    var role = new PostgreSqlRoleDefinition
                    {
                        Sql = "CREATE ROLE \"{{name}}\" WITH LOGIN PASSWORD '{{password}}' VALID UNTIL '{{expiration}}'; GRANT SELECT ON ALL TABLES IN SCHEMA public TO \"{{name}}\";"
                    };

                    await _authenticatedVaultClient.PostgreSqlWriteNamedRoleAsync(roleName, role);

                    var queriedRole = await _authenticatedVaultClient.PostgreSqlReadNamedRoleAsync(roleName);
                    Assert.Equal(role.Sql, queriedRole.Data.Sql);

                    await RunWrapUnwrapCheck(_authenticatedVaultClient.PostgreSqlReadNamedRoleAsync(roleName, wrapTimeToLive: "1m"));

                    var roleName2 = "postgresqlrole2";
                    var role2 = new PostgreSqlRoleDefinition
                    {
                        Sql = "SELECT 1"
                    };

                    await _authenticatedVaultClient.PostgreSqlWriteNamedRoleAsync(roleName2, role2);

                    var roles = await _authenticatedVaultClient.PostgreSqlReadRoleListAsync();
                    Assert.True(roles.Data.Keys.Count == 2);

                    await RunWrapUnwrapCheck(_authenticatedVaultClient.PostgreSqlReadRoleListAsync(wrapTimeToLive: "1m"));

                    var generatedCreds = await _authenticatedVaultClient.PostgreSqlGenerateDynamicCredentialsAsync(roleName);
                    Assert.NotNull(generatedCreds.Data.Password);

                    await RunWrapUnwrapCheck(_authenticatedVaultClient.PostgreSqlGenerateDynamicCredentialsAsync(roleName, wrapTimeToLive: "1m"));

                    await _authenticatedVaultClient.PostgreSqlDeleteNamedRoleAsync(roleName);
                    await _authenticatedVaultClient.PostgreSqlDeleteNamedRoleAsync(roleName2);
                }
                finally
                {
                    try
                    {
                        await _authenticatedVaultClient.QuickUnmountSecretBackendAsync(SecretBackendType.PostgreSql);
                    }
                    catch
                    {
                        // you can always go to your PostgreSql pgAdmin user list and delete users.
                    }
                }
            }
        }

        private static async Task RunPkiSecretBackendApiTests()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.PKIReadCACertificateAsync(pkiBackendMountPoint: null));

            // return; // inmem or file backend doesn't work when Vault is started as inline process here.

            var mountpoint = "pki" + Guid.NewGuid();

            try
            {
                var backend = new SecretBackend
                {
                    BackendType = SecretBackendType.PKI,
                    MountPoint = mountpoint
                };

                await _authenticatedVaultClient.MountSecretBackendAsync(backend);

                await Assert.ThrowsAsync<Exception>(() => _authenticatedVaultClient.PKIReadCRLExpirationAsync(mountpoint));

                var expiry = "124h";
                var commonName = "blah.example.com";

                await _authenticatedVaultClient.PKIWriteCRLExpirationAsync(expiry, mountpoint);

                var readExpiry = await _authenticatedVaultClient.PKIReadCRLExpirationAsync(mountpoint);
                Assert.Equal(expiry, readExpiry.Data.Expiry);

                var nocaCert = await UnauthenticatedVaultClient.PKIReadCACertificateAsync(CertificateFormat.pem, mountpoint);
                Assert.Null(nocaCert.CertificateContent);

                return; // inmem or file backend doesn't work when Vault is started as inline process here.

                // generate root certificate
                var rootCertificateWithoutPrivateKey =
                    await _authenticatedVaultClient.PKIGenerateRootCACertificateAsync(new RootCertificateRequestOptions
                    {
                        CommonName = commonName,
                        ExportPrivateKey = false
                    }, mountpoint);

                Assert.Null(rootCertificateWithoutPrivateKey.Data.PrivateKey);

                var rootCertificate =
                    await _authenticatedVaultClient.PKIGenerateRootCACertificateAsync(new RootCertificateRequestOptions
                    {
                        CommonName = commonName,
                        ExportPrivateKey = true
                    }, mountpoint);

                Assert.NotNull(rootCertificate.Data.PrivateKey);

                var caCert = await UnauthenticatedVaultClient.PKIReadCACertificateAsync(CertificateFormat.pem, mountpoint);
                Assert.NotNull(caCert.CertificateContent);

                var caReadCert = await UnauthenticatedVaultClient.PKIReadCertificateAsync("ca", mountpoint);
                Assert.Equal(caCert.CertificateContent, caReadCert.Data.CertificateContent);

                var caSerialNumberReadCert = await UnauthenticatedVaultClient.PKIReadCertificateAsync(rootCertificate.Data.SerialNumber, mountpoint);
                Assert.Equal(caCert.CertificateContent, caSerialNumberReadCert.Data.CertificateContent);

                var crlCert = await UnauthenticatedVaultClient.PKIReadCertificateAsync("crl", mountpoint);
                Assert.NotNull(crlCert.Data.CertificateContent);

                var crlCert2 = await UnauthenticatedVaultClient.PKIReadCRLCertificateAsync(CertificateFormat.pem, mountpoint);
                Assert.NotNull(crlCert2.CertificateContent);

                await Assert.ThrowsAsync<Exception>(() => _authenticatedVaultClient.PKIReadCertificateEndpointsAsync(mountpoint));

                var crlEndpoint = VaultUriWithPort.AbsoluteUri + "/v1/" + mountpoint + "/crl";
                var issuingEndpoint = VaultUriWithPort.AbsoluteUri + "/v1/" + mountpoint + "/ca";

                var endpoints = new CertificateEndpointOptions
                {
                    CRLDistributionPointEndpoints = string.Join(",", new List<string> { crlEndpoint }),
                    IssuingCertificateEndpoints = string.Join(",", new List<string> { issuingEndpoint }),
                };

                await _authenticatedVaultClient.PKIWriteCertificateEndpointsAsync(endpoints, mountpoint);

                var readEndpoints = await _authenticatedVaultClient.PKIReadCertificateEndpointsAsync(mountpoint);

                Assert.Equal(crlEndpoint, readEndpoints.Data.CRLDistributionPointEndpoints.First());
                Assert.Equal(issuingEndpoint, readEndpoints.Data.IssuingCertificateEndpoints.First());

                var rotate = await _authenticatedVaultClient.PKIRotateCRLAsync(mountpoint);
                Assert.True(rotate.Data);

                await _authenticatedVaultClient.RevokeSecretAsync(rootCertificateWithoutPrivateKey.LeaseId);

                var roleName = Guid.NewGuid().ToString();

                var role = new CertificateRoleDefinition
                {
                    AllowedDomains = "example.com",
                    AllowSubdomains = true,
                    MaximumTimeToLive = "72h",
                };

                await _authenticatedVaultClient.PKIWriteNamedRoleAsync(roleName, role, mountpoint);

                var readRole = await _authenticatedVaultClient.PKIReadNamedRoleAsync(roleName, mountpoint);
                Assert.Equal(role.AllowedDomains, readRole.Data.AllowedDomains);

                await _authenticatedVaultClient.PKIWriteNamedRoleAsync(roleName + "2", role, mountpoint);

                var roles = await _authenticatedVaultClient.PKIReadRoleListAsync(mountpoint);
                Assert.True(roles.Data.Keys.Count == 2);

                var credentials =
                    await
                        _authenticatedVaultClient.PKIGenerateDynamicCredentialsAsync(roleName,
                            new CertificateCredentialsRequestOptions
                            {
                                CommonName = commonName,
                                CertificateFormat = CertificateFormat.pem
                            }, mountpoint);

                Assert.NotNull(credentials.Data.PrivateKey);

                var credCert =
                    await UnauthenticatedVaultClient.PKIReadCertificateAsync(credentials.Data.SerialNumber, mountpoint);

                // \n differences in the content.
                Assert.True(credCert.Data.CertificateContent.Contains(credentials.Data.CertificateContent));

                var pemBundle = rootCertificate.Data.CertificateContent + "\n" + rootCertificate.Data.PrivateKey;

                await _authenticatedVaultClient.PKIConfigureCACertificateAsync(pemBundle, mountpoint);

                var derCaCert =
                    await UnauthenticatedVaultClient.PKIReadCACertificateAsync(CertificateFormat.der, mountpoint);
                Assert.NotNull(derCaCert.CertificateContent);

                await _authenticatedVaultClient.PKIRevokeCertificateAsync(credentials.Data.SerialNumber, mountpoint);
                await _authenticatedVaultClient.PKIDeleteNamedRoleAsync(roleName, mountpoint);

                var revocationData = await _authenticatedVaultClient.PKIRevokeCertificateAsync(rootCertificate.Data.SerialNumber, mountpoint);
                Assert.True(revocationData.Data.RevocationTime > 0);

                await _authenticatedVaultClient.PKITidyAsync(new TidyRequestOptions
                {
                    TidyUpCertificateStore = true,
                    TidyUpRevocationList = true
                }, mountpoint);
            }
            finally
            {
                await _authenticatedVaultClient.UnmountSecretBackendAsync(mountpoint);
            }
        }

        private static async Task RunMySqlSecretBackendApiTests()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MySqlConfigureConnectionAsync(null, null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MySqlReadConnectionInfoAsync(null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MySqlConfigureCredentialLeaseSettingsAsync(null, null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MySqlReadCredentialLeaseSettingsAsync(null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MySqlWriteNamedRoleAsync(null, null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MySqlWriteNamedRoleAsync("role", null, null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MySqlReadNamedRoleAsync(null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MySqlReadNamedRoleAsync("role", null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MySqlReadRoleListAsync(null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MySqlDeleteNamedRoleAsync(null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MySqlDeleteNamedRoleAsync("role", null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MySqlGenerateDynamicCredentialsAsync(null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MySqlGenerateDynamicCredentialsAsync("role", null));

            if (SetupData.RunMySqlSecretBackendAcceptanceTests)
            {
                try
                {
                    if (!File.Exists(SetupData.MySqlCredentialsFullPath))
                    {
                        throw new Exception("MySql Credential file does not exist: " +
                                            SetupData.MySqlCredentialsFullPath);
                    }

                    var credentialsFileContent = File.ReadAllLines(SetupData.MySqlCredentialsFullPath);

                    if (credentialsFileContent.Count() < 1)
                    {
                        throw new Exception("MySql Credential file needs at least 1 line: " +
                                            credentialsFileContent);
                    }

                    var connectionInfo = new MySqlConnectionInfo
                    {
                        ConnectionUrl = credentialsFileContent[0],
                        VerifyConnection = true
                    };

                    await _authenticatedVaultClient.QuickMountSecretBackendAsync(SecretBackendType.MySql);
                    await _authenticatedVaultClient.MySqlConfigureConnectionAsync(connectionInfo);

                    var connection = await _authenticatedVaultClient.MySqlReadConnectionInfoAsync();
                    Assert.Equal(connectionInfo.ConnectionUrl, connection.Data.ConnectionUrl);

                    await
                        RunWrapUnwrapCheck(_authenticatedVaultClient.MySqlReadConnectionInfoAsync(wrapTimeToLive: "1m"));

                    var lease = new CredentialLeaseSettings
                    {
                        LeaseTime = "1m1s",
                        MaximumLeaseTime = "2m1s"
                    };

                    await _authenticatedVaultClient.MySqlConfigureCredentialLeaseSettingsAsync(lease);

                    var queriedLease = await _authenticatedVaultClient.MySqlReadCredentialLeaseSettingsAsync();
                    Assert.Equal(lease.LeaseTime, queriedLease.Data.LeaseTime);
                    Assert.Equal(lease.MaximumLeaseTime, queriedLease.Data.MaximumLeaseTime);

                    await
                        RunWrapUnwrapCheck(_authenticatedVaultClient.MySqlReadCredentialLeaseSettingsAsync(wrapTimeToLive: "1m"));

                    var roleName = "mysqlrole";

                    var role = new MySqlRoleDefinition
                    {
                        Sql = "CREATE USER '{{name}}'@'%' IDENTIFIED BY '{{password}}';GRANT SELECT ON *.* TO '{{name}}'@'%';"
                    };

                    await _authenticatedVaultClient.MySqlWriteNamedRoleAsync(roleName, role);

                    var queriedRole = await _authenticatedVaultClient.MySqlReadNamedRoleAsync(roleName);
                    Assert.Equal(role.Sql, queriedRole.Data.Sql);

                    await
                        RunWrapUnwrapCheck(_authenticatedVaultClient.MySqlReadNamedRoleAsync(roleName, wrapTimeToLive: "1m"));

                    var roleName2 = "mysqlrole2";
                    var role2 = new MySqlRoleDefinition
                    {
                        Sql = "SELECT 1"
                    };

                    await _authenticatedVaultClient.MySqlWriteNamedRoleAsync(roleName2, role2);

                    var roles = await _authenticatedVaultClient.MySqlReadRoleListAsync();
                    Assert.True(roles.Data.Keys.Count == 2);

                    await RunWrapUnwrapCheck(_authenticatedVaultClient.MySqlReadRoleListAsync(wrapTimeToLive: "1m"));

                    var generatedCreds = await _authenticatedVaultClient.MySqlGenerateDynamicCredentialsAsync(roleName);
                    Assert.NotNull(generatedCreds.Data.Password);

                    await RunWrapUnwrapCheck(_authenticatedVaultClient.MySqlGenerateDynamicCredentialsAsync(roleName, wrapTimeToLive: "1m"));

                    await _authenticatedVaultClient.MySqlDeleteNamedRoleAsync(roleName);
                    await _authenticatedVaultClient.MySqlDeleteNamedRoleAsync(roleName2);
                }
                finally
                {
                    try
                    {
                        await _authenticatedVaultClient.QuickUnmountSecretBackendAsync(SecretBackendType.MySql);
                    }
                    catch
                    {
                        // you can always go to your MySql user list and delete users.
                    }
                }
            }
        }

        private static async Task RunMicrosoftSqlSecretBackendApiTests()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MicrosoftSqlConfigureConnectionAsync(null, null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MicrosoftSqlConfigureCredentialLeaseSettingsAsync(null, null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MicrosoftSqlReadCredentialLeaseSettingsAsync(null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MicrosoftSqlWriteNamedRoleAsync(null, null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MicrosoftSqlWriteNamedRoleAsync("role", null, null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MicrosoftSqlReadNamedRoleAsync(null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MicrosoftSqlReadNamedRoleAsync("role", null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MicrosoftSqlReadRoleListAsync(null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MicrosoftSqlDeleteNamedRoleAsync(null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MicrosoftSqlDeleteNamedRoleAsync("role", null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MicrosoftSqlGenerateDynamicCredentialsAsync(null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MicrosoftSqlGenerateDynamicCredentialsAsync("role", null));

            if (SetupData.RunMicrosoftSqlSecretBackendAcceptanceTests)
            {
                try
                {
                    if (!File.Exists(SetupData.MicrosoftSqlCredentialsFullPath))
                    {
                        throw new Exception("MicrosoftSql Credential file does not exist: " +
                                            SetupData.MicrosoftSqlCredentialsFullPath);
                    }

                    var credentialsFileContent = File.ReadAllLines(SetupData.MicrosoftSqlCredentialsFullPath);

                    if (credentialsFileContent.Count() < 1)
                    {
                        throw new Exception("MicrosoftSql Credential file needs at least 1 line: " +
                                            credentialsFileContent);
                    }

                    var microsoftSqlConnectionInfo = new MicrosoftSqlConnectionInfo
                    {
                        ConnectionString = credentialsFileContent[0],
                        MaximumOpenConnections = 5,
                        VerifyConnection = false // raja todo.
                    };

                    await _authenticatedVaultClient.QuickMountSecretBackendAsync(SecretBackendType.MicrosoftSql);
                    await _authenticatedVaultClient.MicrosoftSqlConfigureConnectionAsync(microsoftSqlConnectionInfo);

                    // raja todo: the Vault API doesn't support Reading of connection string officially.
                    // It succeeds, but doesn't return the Connection String.
                    // The MaxConnection is returned though.
                    // var connection = await _authenticatedVaultClient.MicrosoftSqlReadConnectionInfoAsync();
                    // Assert.Equal(microsoftSqlConnectionInfo.MaximumOpenConnections, connection.Data.MaximumOpenConnections);

                    var lease = new CredentialTimeToLiveSettings
                    {
                        TimeToLive = "1m1s",
                        MaximumTimeToLive = "2m1s"
                    };

                    await _authenticatedVaultClient.MicrosoftSqlConfigureCredentialLeaseSettingsAsync(lease);

                    var queriedLease = await _authenticatedVaultClient.MicrosoftSqlReadCredentialLeaseSettingsAsync();
                    Assert.Equal(lease.TimeToLive, queriedLease.Data.TimeToLive);
                    Assert.Equal(lease.MaximumTimeToLive, queriedLease.Data.MaximumTimeToLive);

                    await
                        RunWrapUnwrapCheck(
                            _authenticatedVaultClient.MicrosoftSqlReadCredentialLeaseSettingsAsync(wrapTimeToLive: "1m"));

                    var roleName = "msssqlrole";

                    var role = new MicrosoftSqlRoleDefinition
                    {
                        Sql = "CREATE LOGIN '[{{name}}]' WITH PASSWORD = '{{password}}'; USE master; CREATE USER '[{{name}}]' FOR LOGIN '[{{name}}]'; GRANT SELECT ON SCHEMA::dbo TO '[{{name}}]'"
                    };

                    await _authenticatedVaultClient.MicrosoftSqlWriteNamedRoleAsync(roleName, role);

                    var queriedRole = await _authenticatedVaultClient.MicrosoftSqlReadNamedRoleAsync(roleName);
                    Assert.Equal(role.Sql, queriedRole.Data.Sql);

                    await
                        RunWrapUnwrapCheck(_authenticatedVaultClient.MicrosoftSqlReadNamedRoleAsync(roleName,
                            wrapTimeToLive: "1m"));

                    var roleName2 = "mssqlrole2";
                    var role2 = new MicrosoftSqlRoleDefinition
                    {
                        Sql = "SELECT 1"
                    };

                    await _authenticatedVaultClient.MicrosoftSqlWriteNamedRoleAsync(roleName2, role2);

                    var roles = await _authenticatedVaultClient.MicrosoftSqlReadRoleListAsync();
                    Assert.True(roles.Data.Keys.Count == 2);

                    await
                        RunWrapUnwrapCheck(_authenticatedVaultClient.MicrosoftSqlReadRoleListAsync(wrapTimeToLive: "1m"));

                    var generatedCreds = await _authenticatedVaultClient.MicrosoftSqlGenerateDynamicCredentialsAsync(roleName);
                    Assert.NotNull(generatedCreds.Data.Password);

                    await
                        RunWrapUnwrapCheck(
                            _authenticatedVaultClient.MicrosoftSqlGenerateDynamicCredentialsAsync(roleName,
                                wrapTimeToLive: "1m"));

                    await _authenticatedVaultClient.MicrosoftSqlDeleteNamedRoleAsync(roleName);
                    await _authenticatedVaultClient.MicrosoftSqlDeleteNamedRoleAsync(roleName2);
                }
                finally
                {
                    try
                    {
                        await _authenticatedVaultClient.QuickUnmountSecretBackendAsync(SecretBackendType.MicrosoftSql);
                    }
                    catch
                    {
                        // you can always go to your MicrosoftSql SSMS and delete users.
                    }
                }
            }
        }

        private static async Task RunMongoDbSecretBackendApiTests()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MongoDbConfigureConnectionAsync(null, null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MongoDbReadConnectionInfoAsync(null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MongoDbConfigureCredentialLeaseSettingsAsync(null, null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MongoDbReadCredentialLeaseSettingsAsync(null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MongoDbWriteNamedRoleAsync(null, null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MongoDbWriteNamedRoleAsync("role", null, null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MongoDbReadNamedRoleAsync(null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MongoDbReadNamedRoleAsync("role", null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MongoDbReadRoleListAsync(null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MongoDbDeleteNamedRoleAsync(null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MongoDbDeleteNamedRoleAsync("role", null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MongoDbGenerateDynamicCredentialsAsync(null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MongoDbGenerateDynamicCredentialsAsync("role", null));

            if (SetupData.RunMongoDbSecretBackendAcceptanceTests)
            {
                try
                {
                    if (!File.Exists(SetupData.MongoDbCredentialsFullPath))
                    {
                        throw new Exception("MongoDb Credential file does not exist: " +
                                            SetupData.MongoDbCredentialsFullPath);
                    }

                    var credentialsFileContent = File.ReadAllLines(SetupData.MongoDbCredentialsFullPath);

                    if (credentialsFileContent.Count() < 1)
                    {
                        throw new Exception("MongoDb Credential file needs at least 1 line: " +
                                            credentialsFileContent);
                    }

                    var mongoDbConnectionInfo = new MongoDbConnectionInfo
                    {
                        ConnectionStringUri = credentialsFileContent[0]
                    };

                    await _authenticatedVaultClient.QuickMountSecretBackendAsync(SecretBackendType.MongoDb);

                    var blah = await _authenticatedVaultClient.MongoDbConfigureConnectionAsync(mongoDbConnectionInfo);
                    Assert.NotNull(blah);

                    await
                        RunWrapUnwrapCheck(
                            _authenticatedVaultClient.MongoDbConfigureConnectionAsync(mongoDbConnectionInfo, wrapTimeToLive: "1m"), requestIdCheckOnly: true);

                    var connection = await _authenticatedVaultClient.MongoDbReadConnectionInfoAsync();
                    Assert.Equal(mongoDbConnectionInfo.ConnectionStringUri, connection.Data.ConnectionStringUri);

                    await
                        RunWrapUnwrapCheck(_authenticatedVaultClient.MongoDbReadConnectionInfoAsync(wrapTimeToLive: "1m"));

                    var lease = new CredentialTimeToLiveSettings
                    {
                        TimeToLive = "1m1s",
                        MaximumTimeToLive = "2m1s"
                    };

                    await _authenticatedVaultClient.MongoDbConfigureCredentialLeaseSettingsAsync(lease);

                    var queriedLease = await _authenticatedVaultClient.MongoDbReadCredentialLeaseSettingsAsync();
                    Assert.Equal("61", queriedLease.Data.TimeToLive);
                    Assert.Equal("121", queriedLease.Data.MaximumTimeToLive);

                    await
                        RunWrapUnwrapCheck(
                            _authenticatedVaultClient.MongoDbReadCredentialLeaseSettingsAsync(wrapTimeToLive: "1m"));

                    var roleName = "mongodb-role";

                    var role = new MongoDbRoleDefinition
                    {
                        Database = "admin",
                        Roles = JsonConvert.SerializeObject(new object[] { "readWrite", new { role = "read", db = "bar" } })
                    };

                    await _authenticatedVaultClient.MongoDbWriteNamedRoleAsync(roleName, role);

                    var queriedRole = await _authenticatedVaultClient.MongoDbReadNamedRoleAsync(roleName);
                    Assert.Equal(role.Database, queriedRole.Data.Database);
                    Assert.Equal(role.Roles, queriedRole.Data.Roles);

                    await
                        RunWrapUnwrapCheck(_authenticatedVaultClient.MongoDbReadNamedRoleAsync(roleName,
                            wrapTimeToLive: "1m"));

                    var roleName2 = "mongodb-role2";
                    var role2 = new MongoDbRoleDefinition
                    {
                        Database = "admin",
                        Roles = JsonConvert.SerializeObject(new object[] { "readWrite", new { role = "read", db = "foo" } })
                    };

                    await _authenticatedVaultClient.MongoDbWriteNamedRoleAsync(roleName2, role2);

                    var roles = await _authenticatedVaultClient.MongoDbReadRoleListAsync();
                    Assert.True(roles.Data.Keys.Count == 2);

                    await RunWrapUnwrapCheck(_authenticatedVaultClient.MongoDbReadRoleListAsync(wrapTimeToLive: "1m"));

                    var generatedCreds = await _authenticatedVaultClient.MongoDbGenerateDynamicCredentialsAsync(roleName);
                    Assert.Equal(role.Database, generatedCreds.Data.Database);
                    Assert.NotNull(generatedCreds.Data.Password);

                    await
                        RunWrapUnwrapCheck(_authenticatedVaultClient.MongoDbGenerateDynamicCredentialsAsync(roleName,
                            wrapTimeToLive: "1m"));

                    await _authenticatedVaultClient.MongoDbDeleteNamedRoleAsync(roleName);
                    await _authenticatedVaultClient.MongoDbDeleteNamedRoleAsync(roleName2);
                }
                finally
                {
                    try
                    {
                        await _authenticatedVaultClient.QuickUnmountSecretBackendAsync(SecretBackendType.MongoDb);
                    }
                    catch
                    {
                        // you can always go to your MongoDb shell and delete users.
                    }
                }
            }
        }

        private static async Task RunGenericSecretBackendApiTests()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.GenericReadSecretAsync(locationPath: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.GenericReadSecretAsync("path", null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.GenericReadSecretLocationPathListAsync(null, null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.GenericWriteSecretAsync(null, null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.GenericWriteSecretAsync("path", null, null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.GenericDeleteSecretAsync(null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.GenericDeleteSecretAsync("path", null));

            var mountpoint = "secret" + Guid.NewGuid();

            var path1 = "/path1/blah2";
            var values = new Dictionary<string, object>
            {
                {"foo", "bar"},
                {"foo2", 345 }
            };

            await
                _authenticatedVaultClient.MountSecretBackendAsync(new SecretBackend()
                {
                    BackendType = SecretBackendType.Generic,
                    MountPoint = mountpoint
                });

            await _authenticatedVaultClient.GenericWriteSecretAsync(path1, values, mountpoint);

            var readValues = await _authenticatedVaultClient.GenericReadSecretAsync(path1, mountpoint);
            Assert.True(readValues.Data.Count == 2);

            await RunWrapUnwrapCheck(_authenticatedVaultClient.GenericReadSecretAsync(path1, mountpoint, wrapTimeToLive: "1m"));

            values["foo2"] = 347;

            await _authenticatedVaultClient.GenericWriteSecretAsync(path1, values, mountpoint);

            readValues = await _authenticatedVaultClient.GenericReadSecretAsync(path1, mountpoint);
            Assert.True(int.Parse(readValues.Data["foo2"].ToString()) == 347);

            var path2 = "/path1/blah3";
            var values2 = new Dictionary<string, object>
            {
                {"foo2", "bar2"},
                {"foo3", 3450 }
            };

            await _authenticatedVaultClient.GenericWriteSecretAsync(path2, values2, mountpoint);

            var keys = await _authenticatedVaultClient.GenericReadSecretLocationPathListAsync("/path1", mountpoint);
            Assert.True(keys.Data.Keys.Count == 2);

            await
                RunWrapUnwrapCheck(_authenticatedVaultClient.GenericReadSecretLocationPathListAsync("/path1", mountpoint,
                    wrapTimeToLive: "1m"));

            await _authenticatedVaultClient.GenericDeleteSecretAsync(path1, mountpoint);
            await _authenticatedVaultClient.GenericDeleteSecretAsync(path2, mountpoint);

            await Assert.ThrowsAsync<Exception>(() => _authenticatedVaultClient.GenericReadSecretAsync(path1, mountpoint));

            await _authenticatedVaultClient.UnmountSecretBackendAsync(mountpoint);
        }

        private static async Task RunCubbyholeSecretBackendApiTests()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.CubbyholeReadSecretAsync(locationPath: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.CubbyholeWriteSecretAsync(null, null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.CubbyholeDeleteSecretAsync(locationPath: null));

            var path = "path1/";
            var values = new Dictionary<string, object>
            {
                {"foo1", "bar"},
                {"foo2", 345 }
            };

            await _authenticatedVaultClient.CubbyholeWriteSecretAsync(path, values);

            var readValues = await _authenticatedVaultClient.CubbyholeReadSecretAsync(path);
            Assert.True(readValues.Data.Count == 2);

            await RunWrapUnwrapCheck(_authenticatedVaultClient.CubbyholeReadSecretAsync(path, wrapTimeToLive: "1m"));

            values["foo2"] = 346;

            await _authenticatedVaultClient.CubbyholeWriteSecretAsync(path, values);

            readValues = await _authenticatedVaultClient.CubbyholeReadSecretAsync(path);
            Assert.True(int.Parse(readValues.Data["foo2"].ToString()) == 346);

            var path2 = "path1/path2";
            var values2 = new Dictionary<string, object>
            {
                {"bar1", "bleh"},
                {"bar2", 42 }
            };

            await _authenticatedVaultClient.CubbyholeWriteSecretAsync(path2, values2);

            var list = await _authenticatedVaultClient.CubbyholeReadSecretLocationPathListAsync("path1");
            Assert.True(list.Data.Keys.Count == 1);

            await
                RunWrapUnwrapCheck(_authenticatedVaultClient.CubbyholeReadSecretLocationPathListAsync("path1",
                    wrapTimeToLive: "1m"));

            await _authenticatedVaultClient.CubbyholeDeleteSecretAsync(path);
            await _authenticatedVaultClient.CubbyholeDeleteSecretAsync(path2);

            await Assert.ThrowsAsync<Exception>(() => _authenticatedVaultClient.CubbyholeReadSecretAsync(path));
        }

        private static async Task RunConsulSecretBackendApiTests()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.ConsulConfigureAccessAsync(null, consulBackendMountPoint: null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.ConsulWriteNamedRoleAsync("roleName", null, consulBackendMountPoint: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.ConsulWriteNamedRoleAsync(null, null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.ConsulReadNamedRoleAsync("roleName", consulBackendMountPoint: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.ConsulReadNamedRoleAsync(null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.ConsulDeleteNamedRoleAsync("roleName", consulBackendMountPoint: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.ConsulDeleteNamedRoleAsync(null));

            if (SetupData.RunConsulSecretBackendAcceptanceTests)
            {
                try
                {
                    if (!File.Exists(SetupData.ConsulCredentialsFullPath))
                    {
                        throw new Exception("Consul Credential file does not exist: " +
                                            SetupData.ConsulCredentialsFullPath);
                    }

                    var credentialsFileContent = File.ReadAllLines(SetupData.ConsulCredentialsFullPath);

                    if (credentialsFileContent.Count() < 2)
                    {
                        throw new Exception("Consul Credential file needs at least 3 lines: " + credentialsFileContent);
                    }

                    var consulAccessInfo = new ConsulAccessInfo
                    {
                        AddressWithPort = credentialsFileContent[0],
                        ManagementToken = credentialsFileContent[1],
                    };

                    await _authenticatedVaultClient.QuickMountSecretBackendAsync(SecretBackendType.Consul);
                    await _authenticatedVaultClient.ConsulConfigureAccessAsync(consulAccessInfo);

                    var roleName = "consul-role";

                    var role = new ConsulRoleDefinition
                    {
                        LeaseDuration = "1m1s",
                        TokenType = ConsulTokenType.management
                    };

                    await _authenticatedVaultClient.ConsulWriteNamedRoleAsync(roleName, role);
                    var queriedRole = await _authenticatedVaultClient.ConsulReadNamedRoleAsync(roleName);
                    Assert.Equal(role.LeaseDuration, queriedRole.Data.LeaseDuration);

                    await RunWrapUnwrapCheck(_authenticatedVaultClient.ConsulReadNamedRoleAsync(roleName, wrapTimeToLive: "1m"));

                    role.LeaseDuration = "2m1s";
                    await _authenticatedVaultClient.ConsulWriteNamedRoleAsync(roleName, role);
                    queriedRole = await _authenticatedVaultClient.ConsulReadNamedRoleAsync(roleName);
                    Assert.Equal(role.LeaseDuration, queriedRole.Data.LeaseDuration);

                    var roleName2 = "consul-role2";
                    await _authenticatedVaultClient.ConsulWriteNamedRoleAsync(roleName2, role);

                    var roleList = await _authenticatedVaultClient.ConsulReadRoleListAsync();
                    Assert.True(roleList.Data.Keys.Count == 2);

                    await RunWrapUnwrapCheck(_authenticatedVaultClient.ConsulReadRoleListAsync(wrapTimeToLive: "1m"));

                    var generatedCreds = await _authenticatedVaultClient.ConsulGenerateDynamicCredentialsAsync(roleName);
                    Assert.NotNull(generatedCreds.Data.Token);

                    await RunWrapUnwrapCheck(_authenticatedVaultClient.ConsulGenerateDynamicCredentialsAsync(roleName, wrapTimeToLive: "1m"));

                    await _authenticatedVaultClient.ConsulDeleteNamedRoleAsync(roleName);
                    await _authenticatedVaultClient.ConsulDeleteNamedRoleAsync(roleName2);
                }
                finally
                {
                    try
                    {
                        await _authenticatedVaultClient.QuickUnmountSecretBackendAsync(SecretBackendType.Consul);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static async Task RunCassandraSecretBackendApiTests()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.CassandraConfigureConnectionAsync(null, cassandraBackendMountPoint: null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.CassandraWriteNamedRoleAsync("roleName", null, cassandraBackendMountPoint: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.CassandraConfigureConnectionAsync(null, null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.CassandraReadNamedRoleAsync("roleName", cassandraBackendMountPoint: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.CassandraReadNamedRoleAsync(null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.CassandraDeleteNamedRoleAsync("roleName", cassandraBackendMountPoint: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.CassandraDeleteNamedRoleAsync(null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.CassandraGenerateDynamicCredentialsAsync("roleName", cassandraBackendMountPoint: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.CassandraGenerateDynamicCredentialsAsync(null));

            if (SetupData.RunCassandraSecretBackendAcceptanceTests)
            {
                try
                {
                    if (!File.Exists(SetupData.CassandraCredentialsFullPath))
                    {
                        throw new Exception("Cassandra Credential file does not exist: " +
                                            SetupData.CassandraCredentialsFullPath);
                    }

                    var credentialsFileContent = File.ReadAllLines(SetupData.CassandraCredentialsFullPath);

                    if (credentialsFileContent.Count() < 3)
                    {
                        throw new Exception("Cassandra Credential file needs at least 3 lines: " +
                                            credentialsFileContent);
                    }

                    var cassandraConnectionInfo = new CassandraConnectionInfo
                    {
                        Hosts = credentialsFileContent[0],
                        Username = credentialsFileContent[1],
                        Password = credentialsFileContent[2],
                        CqlProtocolVersion = 4
                    };

                    await _authenticatedVaultClient.QuickMountSecretBackendAsync(SecretBackendType.Cassandra);
                    await _authenticatedVaultClient.CassandraConfigureConnectionAsync(cassandraConnectionInfo);

                    var roleName = "cassandra-role";

                    var role = new CassandraRoleDefinition
                    {
                        CreationCql =
                            @"CREATE USER  '{{username}}' WITH PASSWORD '{{password}}' NOSUPERUSER; GRANT SELECT ON ALL KEYSPACES TO '{{username}}'; ",
                        LeaseDuration = "1m",
                        RollbackCql = "DROP USER '{{username}}';"
                    };

                    await _authenticatedVaultClient.CassandraWriteNamedRoleAsync(roleName, role);
                    var queriedRole = await _authenticatedVaultClient.CassandraReadNamedRoleAsync(roleName);
                    Assert.Equal(role.CreationCql, queriedRole.Data.CreationCql);

                    await RunWrapUnwrapCheck(_authenticatedVaultClient.CassandraReadNamedRoleAsync(roleName, wrapTimeToLive: "1m"));

                    role.CreationCql =
                        @"CREATE USER '{{username}}' WITH PASSWORD '{{password}}' NOSUPERUSER; GRANT SELECT ON ALL KEYSPACES TO '{{username}}';";

                    await _authenticatedVaultClient.CassandraWriteNamedRoleAsync(roleName, role);
                    queriedRole = await _authenticatedVaultClient.CassandraReadNamedRoleAsync(roleName);
                    Assert.Equal(role.CreationCql, queriedRole.Data.CreationCql);

                    var generatedCreds =
                        await _authenticatedVaultClient.CassandraGenerateDynamicCredentialsAsync(roleName);
                    Assert.NotNull(generatedCreds.Data.Password);

                    await RunWrapUnwrapCheck(_authenticatedVaultClient.CassandraGenerateDynamicCredentialsAsync(roleName, wrapTimeToLive: "1m"));

                    await _authenticatedVaultClient.CassandraDeleteNamedRoleAsync(roleName);
                }
                finally
                {
                    try
                    {
                        await _authenticatedVaultClient.QuickUnmountSecretBackendAsync(SecretBackendType.Cassandra);
                    }
                    catch
                    {
                        // you can always go to your Cassandra user base and delete users.
                    }
                }
            }
        }

        private static async Task RunAwsSecretBackendApiTests()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.AWSConfigureRootCredentialsAsync(null, awsBackendMountPoint: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.AWSConfigureCredentialLeaseSettingsAsync(null, awsBackendMountPoint: null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.AWSWriteNamedRoleAsync(awsRoleName: null, awsRoleDefinition: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.AWSWriteNamedRoleAsync("role", null, awsBackendMountPoint: null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.AWSReadNamedRoleAsync(awsRoleName: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.AWSReadNamedRoleAsync("role", awsBackendMountPoint: null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.AWSDeleteNamedRoleAsync(awsRoleName: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.AWSDeleteNamedRoleAsync("role", awsBackendMountPoint: null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.AWSGetRoleListAsync(awsBackendMountPoint: null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.AWSGenerateDynamicCredentialsAsync(awsRoleName: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.AWSGenerateDynamicCredentialsAsync("role", awsBackendMountPoint: null));

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.AWSGenerateDynamicCredentialsWithSecurityTokenAsync(awsRoleName: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.AWSGenerateDynamicCredentialsWithSecurityTokenAsync("role", awsBackendMountPoint: null));

            if (SetupData.RunAwsSecretBackendAcceptanceTests)
            {
                try
                {
                    if (!File.Exists(SetupData.AwsCredentialsFullPath))
                    {
                        throw new Exception("AWS Credential file does not exist: " + SetupData.AwsCredentialsFullPath);
                    }

                    var awsCredentialsFileContent = File.ReadAllLines(SetupData.AwsCredentialsFullPath);

                    if (awsCredentialsFileContent.Count() < 3)
                    {
                        throw new Exception("AWS Credential file needs at least 3 lines: " + awsCredentialsFileContent);
                    }

                    var awsRootCredentials = new AWSRootCredentials
                    {
                        AccessKey = awsCredentialsFileContent[0],
                        SecretKey = awsCredentialsFileContent[1],
                        Region = awsCredentialsFileContent[2]
                    };

                    await _authenticatedVaultClient.QuickMountSecretBackendAsync(SecretBackendType.AWS);
                    await _authenticatedVaultClient.AWSConfigureRootCredentialsAsync(awsRootCredentials);

                    var lease = new CredentialLeaseSettings
                    {
                        LeaseTime = "1m",
                        MaximumLeaseTime = "2m"
                    };

                    await _authenticatedVaultClient.AWSConfigureCredentialLeaseSettingsAsync(lease);

                    var awsRoleJsonName = "aws-role-json";
                    var awsRoleJson = new AWSRoleDefinition
                    {
                        PolicyText = JsonConvert.SerializeObject(new
                        {
                            Version = "2012-10-17",
                            Statement = new
                            {
                                Effect = "Allow",
                                Action = "iam:*",
                                Resource = "*"
                            }
                        })
                    };

                    await _authenticatedVaultClient.AWSWriteNamedRoleAsync(awsRoleJsonName, awsRoleJson);
                    var queriedRoleJson = await _authenticatedVaultClient.AWSReadNamedRoleAsync(awsRoleJsonName);
                    Assert.Equal(awsRoleJson.PolicyText, queriedRoleJson.Data.PolicyText);

                    await RunWrapUnwrapCheck(_authenticatedVaultClient.AWSReadNamedRoleAsync(awsRoleJsonName, wrapTimeToLive: "1m"));

                    var awsRoleNameArn = "aws-role-arn";
                    var awsRoleArn = new AWSRoleDefinition
                    {
                        ARN = "arn:aws:iam::aws:policy/AmazonEC2ReadOnlyAccess"
                    };

                    await _authenticatedVaultClient.AWSWriteNamedRoleAsync(awsRoleNameArn, awsRoleArn);
                    var queriedRole = await _authenticatedVaultClient.AWSReadNamedRoleAsync(awsRoleNameArn);
                    Assert.Equal(awsRoleArn.ARN, queriedRole.Data.ARN);

                    var roles = await _authenticatedVaultClient.AWSGetRoleListAsync();
                    Assert.True(roles.Data.Keys.Count == 2);

                    await RunWrapUnwrapCheck(_authenticatedVaultClient.AWSGetRoleListAsync(wrapTimeToLive: "1m"));

                    var generatedCreds =
                        await _authenticatedVaultClient.AWSGenerateDynamicCredentialsAsync(awsRoleJsonName);
                    Assert.NotNull(generatedCreds.Data.SecretKey);

                    generatedCreds = await _authenticatedVaultClient.AWSGenerateDynamicCredentialsAsync(awsRoleNameArn);
                    Assert.NotNull(generatedCreds.Data.SecretKey);

                    await RunWrapUnwrapCheck(_authenticatedVaultClient.AWSGenerateDynamicCredentialsAsync(awsRoleNameArn, wrapTimeToLive: "1m"));

                    awsRoleJson.PolicyText = JsonConvert.SerializeObject(new
                    {
                        Version = "2012-10-17",
                        Statement = new
                        {
                            Effect = "Allow",
                            Action = "ec2:*",
                            Resource = "*"
                        }
                    });

                    await _authenticatedVaultClient.AWSWriteNamedRoleAsync(awsRoleJsonName, awsRoleJson);

                    queriedRoleJson = await _authenticatedVaultClient.AWSReadNamedRoleAsync(awsRoleJsonName);
                    Assert.Equal(awsRoleJson.PolicyText, queriedRoleJson.Data.PolicyText);

                    generatedCreds =
                        await
                            _authenticatedVaultClient.AWSGenerateDynamicCredentialsWithSecurityTokenAsync(
                                awsRoleJsonName, timeToLive: null);
                    Assert.NotNull(generatedCreds.Data.SecurityToken);

                    generatedCreds =
                        await
                            _authenticatedVaultClient.AWSGenerateDynamicCredentialsWithSecurityTokenAsync(
                                awsRoleJsonName, timeToLive: "2h");
                    Assert.NotNull(generatedCreds.Data.SecurityToken);

                    await RunWrapUnwrapCheck(_authenticatedVaultClient.AWSGenerateDynamicCredentialsWithSecurityTokenAsync(awsRoleJsonName, wrapTimeToLive: "1m"));

                    await _authenticatedVaultClient.AWSDeleteNamedRoleAsync(awsRoleJsonName);
                    await _authenticatedVaultClient.AWSDeleteNamedRoleAsync(awsRoleNameArn);
                }
                finally
                {
                    try
                    {
                        await _authenticatedVaultClient.QuickUnmountSecretBackendAsync(SecretBackendType.AWS);
                    }
                    catch
                    {
                        // you can always go to https://console.aws.amazon.com/iam/home#users and clean up any test users.
                    }
                }
            }
        }

        private static async Task RunRawSecretApiTests()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.ReadRawSecretAsync(storagePath: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.WriteRawSecretAsync(storagePath: null, values: new Dictionary<string, object>()));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.DeleteRawSecretAsync(storagePath: null));

            var rawPath = "rawpath";
            var rawValues = new Dictionary<string, object>
            {
                {"foo", "bar"},
                {"foo2", 345 }
            };

            await _authenticatedVaultClient.WriteRawSecretAsync(rawPath, rawValues);

            var readRawValues = await _authenticatedVaultClient.ReadRawSecretAsync(rawPath);
            Assert.True(readRawValues.Data.RawValues.Count == 2);

            await _authenticatedVaultClient.DeleteRawSecretAsync(rawPath);

            await Assert.ThrowsAsync<Exception>(() => _authenticatedVaultClient.ReadRawSecretAsync(rawPath));
        }

        private static async Task RunRekeyApiTests()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.QuickRekeyAsync(allMasterShareKeys: null, rekeyNonce: "some_nonce"));

            var keyStatus = await _authenticatedVaultClient.GetEncryptionKeyStatusAsync();
            Assert.True(keyStatus.SequentialKeyNumber == 1);

            await _authenticatedVaultClient.RotateEncryptionKeyAsync();

            keyStatus = await _authenticatedVaultClient.GetEncryptionKeyStatusAsync();
            Assert.True(keyStatus.SequentialKeyNumber == 2);

            var rekeyStatus = await UnauthenticatedVaultClient.GetRekeyStatusAsync();
            Assert.False(rekeyStatus.Started);

            rekeyStatus = await UnauthenticatedVaultClient.InitiateRekeyAsync(2, 2);
            Assert.True(rekeyStatus.Started);
            Assert.True(rekeyStatus.UnsealKeysProvided == 0);
            Assert.NotNull(rekeyStatus.Nonce);

            // raja todo: test the rekey backup API, after giving good pgp encrypted keys.

            // var backups = await _authenticatedVaultClient.GetRekeyBackupKeysAsync();
            // Assert.NotNull(backups);

            await _authenticatedVaultClient.DeleteRekeyBackupKeysAsync();

            var rekeyNonce = rekeyStatus.Nonce;
            var rekeyProgress = await UnauthenticatedVaultClient.ContinueRekeyAsync(_masterCredentials.MasterKeys[0], rekeyNonce);
            Assert.False(rekeyProgress.Complete);
            Assert.Null(rekeyProgress.MasterKeys);

            rekeyStatus = await UnauthenticatedVaultClient.GetRekeyStatusAsync();
            Assert.True(rekeyStatus.Started);
            Assert.True(rekeyStatus.UnsealKeysProvided == 1);

            rekeyProgress = await UnauthenticatedVaultClient.ContinueRekeyAsync(_masterCredentials.MasterKeys[1], rekeyNonce);
            Assert.True(rekeyProgress.Complete);
            Assert.NotNull(rekeyProgress.MasterKeys);

            _masterCredentials.MasterKeys = rekeyProgress.MasterKeys;
            _masterCredentials.Base64MasterKeys = rekeyProgress.Base64MasterKeys;

            rekeyStatus = await UnauthenticatedVaultClient.GetRekeyStatusAsync();

            Assert.False(rekeyStatus.Started);
            Assert.True(rekeyStatus.SecretThreshold == 0);
            Assert.True(rekeyStatus.RequiredUnsealKeys == 2);
            Assert.True(rekeyStatus.SecretShares == 0);
            Assert.True(rekeyStatus.UnsealKeysProvided == 0);
            Assert.Equal(string.Empty, rekeyStatus.Nonce);
            Assert.False(rekeyStatus.Backup);

            await UnauthenticatedVaultClient.InitiateRekeyAsync(5, 5);

            rekeyStatus = await UnauthenticatedVaultClient.GetRekeyStatusAsync();
            Assert.True(rekeyStatus.Started);
            Assert.True(rekeyStatus.SecretThreshold == 5);
            Assert.True(rekeyStatus.RequiredUnsealKeys == 2);
            Assert.True(rekeyStatus.SecretShares == 5);
            Assert.True(rekeyStatus.UnsealKeysProvided == 0);
            Assert.NotNull(rekeyStatus.Nonce);
            Assert.False(rekeyStatus.Backup);

            await UnauthenticatedVaultClient.CancelRekeyAsync();
            rekeyStatus = await UnauthenticatedVaultClient.GetRekeyStatusAsync();
            Assert.False(rekeyStatus.Started);
            Assert.True(rekeyStatus.SecretThreshold == 0);
            Assert.True(rekeyStatus.RequiredUnsealKeys == 2);
            Assert.True(rekeyStatus.SecretShares == 0);
            Assert.True(rekeyStatus.UnsealKeysProvided == 0);
            Assert.Equal(string.Empty, rekeyStatus.Nonce);
            Assert.False(rekeyStatus.Backup);

            await UnauthenticatedVaultClient.InitiateRekeyAsync(2, 2);
            rekeyStatus = await UnauthenticatedVaultClient.GetRekeyStatusAsync();

            var quick = await UnauthenticatedVaultClient.QuickRekeyAsync(_masterCredentials.MasterKeys, rekeyStatus.Nonce);
            Assert.True(quick.Complete);

            _masterCredentials.MasterKeys = quick.MasterKeys;
            _masterCredentials.Base64MasterKeys = quick.Base64MasterKeys;
        }

        private static async Task RunLeaderApiTests()
        {
            var leader = await _authenticatedVaultClient.GetLeaderAsync();
            Assert.NotNull(leader);

            await _authenticatedVaultClient.StepDownActiveNodeAsync();

            leader = await _authenticatedVaultClient.GetLeaderAsync();
            Assert.NotNull(leader);
        }

        private static async Task RunWrapApiTests()
        {
            var data = new Dictionary<string, object>
            {
                {"key1", "value1"},
                {"key2", 23},
                {"key3", true}
            };

            var wrapTimeToLive = "1h";

            var wrappedResponse = await _authenticatedVaultClient.WrapResponseDataAsync(data, wrapTimeToLive);
            Assert.NotNull(wrappedResponse.WrappedInformation.Token);

            var tokenWrapInfo = await _authenticatedVaultClient.LookupTokenWrapInfoAsync(wrappedResponse.WrappedInformation.Token);
            Assert.NotEqual(DateTimeOffset.MinValue, tokenWrapInfo.Data.CreationTime);

            var rewrappedResponse =
                await _authenticatedVaultClient.RewrapWrappedResponseDataAsync(wrappedResponse.WrappedInformation.Token);
            Assert.NotNull(rewrappedResponse.WrappedInformation.Token);
            Assert.NotEqual(wrappedResponse.WrappedInformation.Token, rewrappedResponse.WrappedInformation.Token);

            var unwrappedResponse = await _authenticatedVaultClient.UnwrapWrappedResponseDataAsync(rewrappedResponse.WrappedInformation.Token);
            Assert.Equal(data.Count, unwrappedResponse.Data.Count);

            wrappedResponse = await _authenticatedVaultClient.WrapResponseDataAsync(null, wrapTimeToLive);
            Assert.NotNull(wrappedResponse.WrappedInformation.Token);

            unwrappedResponse = await _authenticatedVaultClient.UnwrapWrappedResponseDataAsync(wrappedResponse.WrappedInformation.Token);
            Assert.True(unwrappedResponse.Data.Count == 0);
        }

        private static async Task RunLeaseApiTests()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.RevokeSecretAsync(leaseId: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.RevokeAllSecretsOrTokensUnderPrefixAsync(pathPrefix: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.ForceRevokeAllSecretsOrTokensUnderPrefixAsync(pathPrefix: null));

            // raja todo: aws creds have leaseid and are renewable. use them.

            //try
            //{
            //    await _authenticatedVaultClient.QuickMountSecretBackendAsync(SecretBackendType.SSH);

            //    var secretWithLeaseId = await GetRenewableSecretWithLeaseId();
            //    await _authenticatedVaultClient.RenewSecretAsync(secretWithLeaseId.LeaseId, 5);

            //    await _authenticatedVaultClient.RevokeSecretAsync(secretWithLeaseId.LeaseId);
            //}
            //finally
            //{
            //    try
            //    {
            //        await _authenticatedVaultClient.QuickUnmountSecretBackendAsync(SecretBackendType.SSH);
            //    }
            //    catch
            //    {
            //        // no op.
            //    }
            //}
        }

        private static async Task<Secret<SSHCredentials>> GetRenewableSecretWithLeaseId()
        {
            var sshKeyName = Guid.NewGuid().ToString();
            var sshRoleName = Guid.NewGuid().ToString();

            var privateKey = @"-----BEGIN RSA PRIVATE KEY-----
MIICXgIBAAKBgQC2+cfxgJ5LsWAq+vRZB77pCwy5P+tnLahCeq4OBViloSfKVq/y
Hq/u3YScNNoqkailjmOMJtzKDD9W7dNasfu5zGWxjLUL4NwasbEK1jseKfbwKjmc
Nw1KYByx5BTECN0l5FxGUkQQVSmwJvqgyXDEHCsAvC72x96uBk2qJTAoLwIDAQAB
AoGBALXyCvAKhV2fM5GJmhAts5joc+6BsQMYU4hHlWw7xLpuVbLOIIcSHL/ZZlQt
+gL6dEisHjDvM/110EYQl2pIMZYO+WU+OSmRKU8U12bjDmoypONZokBplXsVDeY4
vbb7yVmOpazr/lpM4cqxL7TeRgxypQT08t7ukgt/7NOSHx0BAkEA8B0YXsxvxJLp
g1LmCnP0L3vcsRw4wLNtEBfmJc/okknIyIAadLBW5mFXxQNIjj1JGTGbK/lbedBP
ypVgY5l9uQJBAMMU6qtupP671bzEXACt6Gst/qyx7vMHMc7yRdckrXr5Wl/uyxDC
BbErr5xg6e6qi3HnZBQbYbnYVn6gI4u2iScCQQDhK0e5TpnZi7Oz1T+ouchZ5xu0
czS9cQVrvB21g90jolHJxGgK2XsEnHCEbmnSCaLNH3nWqQahmznYTnCPtlbxAkAE
WhUaGe/IVvxfp6m9wiNrMK17wMRp24E68qCoOgM8uQ9REIyrJQjneOgD/w1464kM
03KiGDJH6RGU5ZGlbj8FAkEAmm9GGdG4/rcI2o5kUQJWOPkr2KPy/X34FyD9etbv
TRzfAZxw7q483/Y7mZ63/RuPYKFei4xFBfjzMDYm1lT4AQ==
-----END RSA PRIVATE KEY-----";

            var ip = "127.0.0.1";
            var user = "rajan";

            await _authenticatedVaultClient.SSHWriteNamedKeyAsync(sshKeyName, privateKey);

            var sshOTPRoleDefinition = new SSHOTPRoleDefinition
            {
                RoleDefaultUser = user,
                CIDRValues = "127.0.0.1/10",
                Port = 22
            };

            await _authenticatedVaultClient.SSHWriteNamedRoleAsync(sshRoleName, sshOTPRoleDefinition);

            var secretWithLeaseId = await _authenticatedVaultClient.SSHGenerateDynamicCredentialsAsync(sshRoleName, ip);
            Assert.NotNull(secretWithLeaseId.LeaseId);
            Assert.True(secretWithLeaseId.Renewable);

            return secretWithLeaseId;
        }

        private static async Task RunAuditBackendMountApiTests()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.EnableAuditBackendAsync(auditBackend: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.DisableAuditBackendAsync(mountPoint: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.HashWithAuditBackendAsync(mountPoint: null, inputToHash: "a"));

            var audits = await _authenticatedVaultClient.GetAllEnabledAuditBackendsAsync();
            Assert.False(audits.Data.Any());

            // enable new file audit
            var newFileAudit = new FileAuditBackend
            {
                Description = "store logs in a file - test cases",
                Options = new FileAuditBackendOptions
                {
                    FilePath = "/var/log/file",
                    LogSensitiveDataInRawFormat = true.ToString().ToLowerInvariant(),
                    HmacAccessor = false.ToString().ToLowerInvariant(),
                    Format = "jsonx"
                }
            };

            await _authenticatedVaultClient.EnableAuditBackendAsync(newFileAudit);

            // get audits
            var newAudits = await _authenticatedVaultClient.GetAllEnabledAuditBackendsAsync();
            Assert.Equal(audits.Data.Count() + 1, newAudits.Data.Count());

            // hash with audit
            var hash = await _authenticatedVaultClient.HashWithAuditBackendAsync(newFileAudit.MountPoint, "testinput");
            Assert.NotNull(hash);

            // disabled audit
            await _authenticatedVaultClient.DisableAuditBackendAsync(newFileAudit.MountPoint);

            // get audits
            var oldAudits = await _authenticatedVaultClient.GetAllEnabledAuditBackendsAsync();
            Assert.Equal(audits.Data.Count(), oldAudits.Data.Count());

            // syslog is not supported on windows. so no acceptance tests possible.
        }

        private static async Task RunCapabilitiesApiTests()
        {
            var secret1 = await _authenticatedVaultClient.CreateTokenAsync(new TokenCreationOptions { NoParent = true });

            var caps =
                await _authenticatedVaultClient.GetTokenCapabilitiesAsync(secret1.AuthorizationInfo.ClientToken, "sys/mounts");
            Assert.NotNull(caps);

            var caps2 = await _authenticatedVaultClient.GetCallingTokenCapabilitiesAsync("sys/mounts");
            Assert.NotNull(caps2);

            var cap3 =
                await
                    _authenticatedVaultClient.GetTokenAccessorCapabilitiesAsync(
                        secret1.AuthorizationInfo.ClientTokenAccessor, "sys/mounts");
            Assert.NotNull(cap3);
        }

        private static async Task RunPolicyApiTests()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.GetPolicyAsync(policyName: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.WritePolicyAsync(policy: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.WritePolicyAsync(new Policy { Name = null }));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.DeletePolicyAsync(policyName: null));

            var policies = (await _authenticatedVaultClient.GetAllPoliciesAsync()).ToList();
            Assert.True(policies.Any());

            var policy = await _authenticatedVaultClient.GetPolicyAsync(policies[0]);
            Assert.NotNull(policy);

            // write a new policy
            var newPolicy = new Policy
            {
                Name = "gubdu",
                Rules = "path \"sys/*\" {  policy = \"deny\" }"
            };

            await _authenticatedVaultClient.WritePolicyAsync(newPolicy);

            // get new policy
            var newPolicyGet = await _authenticatedVaultClient.GetPolicyAsync(newPolicy.Name);
            Assert.Equal(newPolicy.Rules, newPolicyGet.Rules);

            // write updates to a new policy
            newPolicy.Rules = "path \"sys/*\" {  policy = \"read\" }";

            await _authenticatedVaultClient.WritePolicyAsync(newPolicy);

            // get new policy
            newPolicyGet = await _authenticatedVaultClient.GetPolicyAsync(newPolicy.Name);
            Assert.Equal(newPolicy.Rules, newPolicyGet.Rules);

            // delete policy
            await _authenticatedVaultClient.DeletePolicyAsync(newPolicy.Name);

            // get all policies
            var oldPolicies = (await _authenticatedVaultClient.GetAllPoliciesAsync()).ToList();
            Assert.Equal(policies.Count, oldPolicies.Count);
        }

        private static async Task RunAuthenticationBackendMountApiTests()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.EnableAuthenticationBackendAsync(authenticationBackend: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.DisableAuthenticationBackendAsync(authenticationPath: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.GetMountedAuthenticationBackendConfigurationAsync(authenticationPath: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.TuneAuthenticationBackendConfigurationAsync(authenticationPath: null));

            // get Authentication backends
            var authenticationBackends = await _authenticatedVaultClient.GetAllEnabledAuthenticationBackendsAsync();
            Assert.True(authenticationBackends.Data.Any());

            var mountConfig = await _authenticatedVaultClient.GetMountedAuthenticationBackendConfigurationAsync(authenticationBackends.Data.First().AuthenticationPath);
            Assert.NotNull(mountConfig);

            // enable new auth
            var newAuth = new AuthenticationBackend
            {
                AuthenticationPath = "github1",
                BackendType = AuthenticationBackendType.GitHub,
                Description = "Github auth - test cases"
            };

            await _authenticatedVaultClient.EnableAuthenticationBackendAsync(newAuth);

            string ttl = "11h";

            await
                _authenticatedVaultClient.TuneAuthenticationBackendConfigurationAsync(newAuth.AuthenticationPath,
                    new MountConfiguration { DefaultLeaseTtl = ttl, MaximumLeaseTtl = ttl });

            mountConfig =
                await
                    _authenticatedVaultClient.GetMountedAuthenticationBackendConfigurationAsync(
                        newAuth.AuthenticationPath);

            Assert.NotNull(mountConfig);

            // get all auths
            var newAuthenticationBackends = await _authenticatedVaultClient.GetAllEnabledAuthenticationBackendsAsync();
            Assert.Equal(authenticationBackends.Data.Count() + 1, newAuthenticationBackends.Data.Count());

            // disable auth
            await _authenticatedVaultClient.DisableAuthenticationBackendAsync(newAuth.AuthenticationPath);

            // get all auths
            var oldAuthenticationBackends = await _authenticatedVaultClient.GetAllEnabledAuthenticationBackendsAsync();
            Assert.Equal(authenticationBackends.Data.Count(), oldAuthenticationBackends.Data.Count());

            // quick api.
            await _authenticatedVaultClient.QuickEnableAuthenticationBackendAsync(AuthenticationBackendType.GitHub);
            newAuthenticationBackends = await _authenticatedVaultClient.GetAllEnabledAuthenticationBackendsAsync();
            Assert.Equal(authenticationBackends.Data.Count() + 1, newAuthenticationBackends.Data.Count());

            await _authenticatedVaultClient.DisableAuthenticationBackendAsync(AuthenticationBackendType.GitHub.Type);
            oldAuthenticationBackends = await _authenticatedVaultClient.GetAllEnabledAuthenticationBackendsAsync();
            Assert.Equal(authenticationBackends.Data.Count(), oldAuthenticationBackends.Data.Count());
        }

        private static async Task RunSecretBackendMountApiTests()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.MountSecretBackendAsync(secretBackend: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.QuickMountSecretBackendAsync(secretBackendType: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.UnmountSecretBackendAsync(mountPoint: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.QuickUnmountSecretBackendAsync(secretBackendType: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.GetMountedSecretBackendConfigurationAsync(mountPoint: null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.TuneSecretBackendConfigurationAsync(mountPoint: null));

            var secretBackends = await _authenticatedVaultClient.GetAllMountedSecretBackendsAsync();
            Assert.True(secretBackends.Data.Any());

            var mountConfig = await _authenticatedVaultClient.GetMountedSecretBackendConfigurationAsync(secretBackends.Data.First().MountPoint);
            Assert.NotNull(mountConfig.Data.MaximumLeaseTtl);

            // mount a new secret backend
            var newSecretBackend = new SecretBackend
            {
                BackendType = SecretBackendType.AWS,
                MountPoint = "aws1",
                Description = "e2e tests"
            };

            await _authenticatedVaultClient.MountSecretBackendAsync(newSecretBackend);

            string ttl = "10h";

            await
                _authenticatedVaultClient.TuneSecretBackendConfigurationAsync(newSecretBackend.MountPoint,
                    new MountConfiguration { DefaultLeaseTtl = ttl, MaximumLeaseTtl = ttl });

            // get secret backends
            var newSecretBackends = await _authenticatedVaultClient.GetAllMountedSecretBackendsAsync();
            Assert.Equal(secretBackends.Data.Count() + 1, newSecretBackends.Data.Count());

            // unmount
            await _authenticatedVaultClient.UnmountSecretBackendAsync(newSecretBackend.MountPoint);

            // get secret backends
            var oldSecretBackends = await _authenticatedVaultClient.GetAllMountedSecretBackendsAsync();
            Assert.Equal(secretBackends.Data.Count(), oldSecretBackends.Data.Count());

            // mount a new secret backend
            await _authenticatedVaultClient.MountSecretBackendAsync(newSecretBackend);

            // remount
            var newMountPoint = "aws2";
            await _authenticatedVaultClient.RemountSecretBackendAsync(newSecretBackend.MountPoint, newMountPoint);

            // get new secret backend config
            var config = await _authenticatedVaultClient.GetMountedSecretBackendConfigurationAsync(newMountPoint);
            Assert.NotNull(config);

            // unmount
            await _authenticatedVaultClient.UnmountSecretBackendAsync(newMountPoint);

            // quick
            secretBackends = await _authenticatedVaultClient.GetAllMountedSecretBackendsAsync();
            await _authenticatedVaultClient.QuickMountSecretBackendAsync(SecretBackendType.AWS);
            newSecretBackends = await _authenticatedVaultClient.GetAllMountedSecretBackendsAsync();
            Assert.Equal(secretBackends.Data.Count() + 1, newSecretBackends.Data.Count());

            await _authenticatedVaultClient.QuickUnmountSecretBackendAsync(SecretBackendType.AWS);
            newSecretBackends = await _authenticatedVaultClient.GetAllMountedSecretBackendsAsync();
            Assert.Equal(secretBackends.Data.Count(), newSecretBackends.Data.Count());
        }

        private static async Task RunInitApiTests()
        {
            RunConstructorTests();

            await AssertInitializationStatusAsync(false);

            var health = await UnauthenticatedVaultClient.GetHealthStatusAsync();
            Assert.False(health.HealthCheckSucceeded);
            Assert.False(health.Initialized);

            health = await UnauthenticatedVaultClient.GetHealthStatusAsync(uninitializedStatusCode: 300);
            Assert.False(health.HealthCheckSucceeded);
            Assert.False(health.Initialized);

            health = await UnauthenticatedVaultClient.GetHealthStatusAsync(uninitializedStatusCode: 200);
            Assert.True(health.HealthCheckSucceeded);
            Assert.False(health.Initialized);

            await InitializeVaultAsync();
            await AssertInitializationStatusAsync(true);

            health = await UnauthenticatedVaultClient.GetHealthStatusAsync();
            Assert.False(health.HealthCheckSucceeded);
            Assert.True(health.Initialized);
        }

        private static void RunConstructorTests()
        {
            var dummyAuthenticationInfo = new TokenAuthenticationInfo("test");

            Assert.Throws<ArgumentNullException>(() => new VaultClient(null, dummyAuthenticationInfo));

            var client1 = new VaultClient(VaultUriWithPort, null);
            Assert.NotNull(client1);

            var client2 = new VaultClient(VaultUriWithPort, dummyAuthenticationInfo);
            Assert.NotNull(client2);

            var client3 = new VaultClient(VaultUriWithPort, dummyAuthenticationInfo, true);
            Assert.NotNull(client3);

            var client4 = new VaultClient(VaultUriWithPort, dummyAuthenticationInfo, true, TimeSpan.FromMinutes(3));
            Assert.NotNull(client4);

            var client5 = new VaultClient(VaultUriWithPort, dummyAuthenticationInfo, true, TimeSpan.FromMinutes(3), new Mock<IDataAccessManager>().Object);
            Assert.NotNull(client5);

            var client6 = new VaultClient(VaultUriWithPort, dummyAuthenticationInfo, true, TimeSpan.FromMinutes(3),
                new Mock<IDataAccessManager>().Object,
                client => { });

            Assert.NotNull(client6);
        }

        private static async Task RunSealApiTests()
        {
            await AssertSealStatusAsync(true);

            var health = await UnauthenticatedVaultClient.GetHealthStatusAsync();
            Assert.False(health.HealthCheckSucceeded);
            Assert.True(health.Sealed);

            health = await UnauthenticatedVaultClient.GetHealthStatusAsync(sealedStatusCode: 300);
            Assert.False(health.HealthCheckSucceeded);
            Assert.True(health.Sealed);

            health = await UnauthenticatedVaultClient.GetHealthStatusAsync(sealedStatusCode: 400);
            Assert.False(health.HealthCheckSucceeded);
            Assert.True(health.Sealed);

            health = await UnauthenticatedVaultClient.GetHealthStatusAsync(sealedStatusCode: 200);
            Assert.True(health.HealthCheckSucceeded);
            Assert.True(health.Sealed);

            await UnsealAsync();
            await AssertSealStatusAsync(false);

            health = await UnauthenticatedVaultClient.GetHealthStatusAsync();
            Assert.True(health.HealthCheckSucceeded);
            Assert.False(health.Sealed);

            health = await UnauthenticatedVaultClient.GetHealthStatusAsync(activeStatusCode: 300);
            Assert.False(health.HealthCheckSucceeded);
            Assert.False(health.Sealed);

            await _authenticatedVaultClient.SealAsync();

            var sealStatus = await UnauthenticatedVaultClient.UnsealAsync(_masterCredentials.MasterKeys[0]);
            Assert.True(sealStatus.Sealed);
            Assert.False(sealStatus.Progress == 0);

            await UnauthenticatedVaultClient.UnsealAsync(resetCompletely: true);
            await AssertSealStatusAsync(true);

            await UnsealAsync();

            await _authenticatedVaultClient.SealAsync();
            await AssertSealStatusAsync(true);

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.QuickUnsealAsync(allMasterShareKeys: null));

            sealStatus = await UnauthenticatedVaultClient.QuickUnsealAsync(_masterCredentials.MasterKeys);
            Assert.False(sealStatus.Sealed);
        }

        private static async Task RunGenerateRootApiTests()
        {
            var rootStatus = await UnauthenticatedVaultClient.GetRootTokenGenerationStatusAsync();
            Assert.False(rootStatus.Started);

            var otp = Convert.ToBase64String(Enumerable.Range(0, 16).Select(i => (byte)i).ToArray());
            rootStatus = await UnauthenticatedVaultClient.InitiateRootTokenGenerationAsync(otp);

            Assert.True(rootStatus.Started);
            Assert.NotNull(rootStatus.Nonce);

            foreach (var masterKey in _masterCredentials.MasterKeys)
            {
                rootStatus = await UnauthenticatedVaultClient.ContinueRootTokenGenerationAsync(masterKey, rootStatus.Nonce);
            }

            Assert.True(rootStatus.Complete);
            Assert.NotNull(rootStatus.EncodedRootToken);

            rootStatus = await UnauthenticatedVaultClient.InitiateRootTokenGenerationAsync(otp);

            rootStatus = await UnauthenticatedVaultClient.ContinueRootTokenGenerationAsync(_masterCredentials.MasterKeys[0], rootStatus.Nonce);
            Assert.True(rootStatus.Started);

            await UnauthenticatedVaultClient.CancelRootTokenGenerationAsync();

            rootStatus = await UnauthenticatedVaultClient.GetRootTokenGenerationStatusAsync();
            Assert.False(rootStatus.Started);

            rootStatus = await UnauthenticatedVaultClient.InitiateRootTokenGenerationAsync(otp);

            await Assert.ThrowsAsync<ArgumentNullException>(() => _authenticatedVaultClient.QuickRootTokenGenerationAsync(allMasterShareKeys: null, nonce: "any"));

            rootStatus =
                await
                    UnauthenticatedVaultClient.QuickRootTokenGenerationAsync(_masterCredentials.MasterKeys,
                        rootStatus.Nonce);

            Assert.True(rootStatus.Complete);
            Assert.NotNull(rootStatus.EncodedRootToken);
        }

        private static async Task UnsealAsync()
        {
            SealStatus sealStatus = null;

            foreach (var masterKey in _masterCredentials.MasterKeys)
            {
                sealStatus = await UnauthenticatedVaultClient.UnsealAsync(masterKey);
            }

            Assert.False(sealStatus.Sealed);
            Assert.Equal(0, sealStatus.Progress);
            Assert.NotNull(sealStatus.ClusterId);
            Assert.NotNull(sealStatus.ClusterName);

            _authenticatedVaultClient = VaultClientFactory.CreateVaultClient(VaultUriWithPort, new TokenAuthenticationInfo(_masterCredentials.RootToken));
        }

        private static async Task AssertSealStatusAsync(bool expected)
        {
            var actual = await UnauthenticatedVaultClient.GetSealStatusAsync();
            Assert.Equal(expected, actual.Sealed);
        }

        private static async Task InitializeVaultAsync()
        {
            _masterCredentials = await UnauthenticatedVaultClient.InitializeAsync(new InitializeOptions
            {
                SecretShares = 2,
                SecretThreshold = 2,
                RecoveryShares = 2,
                RecoveryThreshold = 2
            });
            Assert.NotNull(_masterCredentials);
        }

        private static async Task AssertInitializationStatusAsync(bool expectedStatus)
        {
            var actual = await UnauthenticatedVaultClient.GetInitializationStatusAsync();
            Assert.Equal(expectedStatus, actual);
        }

        private static void StartupVaultServer()
        {
            if (!File.Exists(SetupData.VaultExeFullPath))
            {
                throw new Exception("Vault EXE full path does not exist: " + SetupData.VaultExeFullPath);
            }

            if (!File.Exists(VaultConfigPath))
            {
                throw new Exception("Vault acceptance tests config file does not exist: " + VaultConfigPath);
            }

            var vaultFolder = Path.GetDirectoryName(SetupData.VaultExeFullPath);
            var fileBackendsFullRootFolderPath = Path.Combine(vaultFolder, FileBackendsFolderName);
            var vaultConfigsFullRootFolderPath = Path.Combine(vaultFolder, VaultConfigsFolderName);

            if (!Directory.Exists(vaultConfigsFullRootFolderPath))
            {
                Directory.CreateDirectory(vaultConfigsFullRootFolderPath);
            }

            var fileBackendFolderName = Guid.NewGuid().ToString();
            var fileBackendFullFolderPath = Path.Combine(fileBackendsFullRootFolderPath, fileBackendFolderName);

            if (Directory.Exists(fileBackendFullFolderPath))
            {
                throw new Exception("A directory of the same name already exists. Please try a new run." +
                                    fileBackendFullFolderPath);
            }

            Directory.CreateDirectory(fileBackendFullFolderPath);

            var config =
                File.ReadAllText(VaultConfigPath)
                    .Replace(FileBackendPlaceHolder, fileBackendFullFolderPath)
                    .Replace("\\", "\\\\");
            var testConfigFileName = fileBackendFolderName + ".hcl";

            var testConfigFullPath = Path.Combine(vaultConfigsFullRootFolderPath, testConfigFileName);
            File.WriteAllText(testConfigFullPath, config);

            var startupCommand = "\"" + SetupData.VaultExeFullPath + "\" server -config \"" + testConfigFullPath + "\"";

            var procStartInfo = new ProcessStartInfo();

            procStartInfo.FileName = "cmd";
            procStartInfo.Arguments = "/k \"" + startupCommand + "\"";
            procStartInfo.WorkingDirectory = Path.GetPathRoot(vaultFolder);
            procStartInfo.UseShellExecute = true;
            procStartInfo.Verb = "runas";

            _vaultProcess = new Process();
            _vaultProcess.StartInfo = procStartInfo;
            _vaultProcess.Start();
        }

        private static void ShutdownVaultServer()
        {
            if (_vaultProcess != null && !_vaultProcess.HasExited)
            {
                _vaultProcess.CloseMainWindow();
            }
        }

        private static async Task RunWrapUnwrapCheck<TData>(Task<Secret<TData>> wrapTask, bool requestIdCheckOnly = false)
        {
            var wrappedResponse = await wrapTask;

            Assert.NotNull(wrappedResponse.WrappedInformation);
            Assert.NotNull(wrappedResponse.WrappedInformation.Token);

            var unwrappedResponse =
                await
                    _authenticatedVaultClient.UnwrapWrappedResponseDataAsync<TData>(
                        wrappedResponse.WrappedInformation.Token);

            if (requestIdCheckOnly)
            {
                Assert.NotNull(unwrappedResponse.RequestId);
                return;
            }

            Assert.NotNull(unwrappedResponse.Data);
        }
    }
}