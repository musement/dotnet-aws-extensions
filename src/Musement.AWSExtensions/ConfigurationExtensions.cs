using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Kralizek.Extensions.Configuration.Internal;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.Configuration;

public static class ConfigurationExtensions
{
    public static bool UseLocalstack { get; }
    public static string? LocalstackHost { get; }
    public static string? LocalstackEndpoint { get; }
    public static BasicAWSCredentials FakeCredentials { get; }

    static ConfigurationExtensions()
    {
        LocalstackHost = Environment.GetEnvironmentVariable("LOCALSTACK_HOST");
        var port = Environment.GetEnvironmentVariable("LOCALSTACK_PORT") switch
        {
            // Intentionally using Parse to fail loudly when var is set to an invalid value
            string sp => int.Parse(sp, CultureInfo.InvariantCulture),
            _ => 4566
        };
        UseLocalstack = LocalstackHost is not null;
        LocalstackEndpoint = UseLocalstack ? $"http://{LocalstackHost}:{port}" : null;
        FakeCredentials = new BasicAWSCredentials("accessKey", "secretKey");
    }

    public static IConfigurationBuilder AddAmazonSecretsManager(this IConfigurationBuilder self
        , IHostEnvironment env
        , IDictionary<string, string> secretsKeyMap)
    {
        return (env.IsDevelopment(), UseLocalstack) switch
        {
            (true, true) => self.AddSecretsManager(configurator: LocalstackConfigurator),
            (true, false) => self,
            (false, true) => throw new InvalidOperationException("Localstack is only allowed on Development"),
            (false, false) => self.AddSecretsManager(configurator: AwsConfigurator)
        };

        void AwsConfigurator(SecretsManagerConfigurationProviderOptions options)
        {
            if (secretsKeyMap.Any(x => !x.Key.StartsWith("arn:", StringComparison.InvariantCulture)))
            {
                throw new InvalidOperationException("Only ARNs are allowed outside of Localstack");
            }

            options.SecretFilter = s => secretsKeyMap.ContainsKey(s.ARN);
            options.KeyGenerator = (s, k) => secretsKeyMap[s.ARN];
            options.AcceptedSecretArns = secretsKeyMap.Keys.ToList();
            options.CreateClient = () => new AmazonSecretsManagerClient();
        }

        void LocalstackConfigurator(SecretsManagerConfigurationProviderOptions options)
        {
            options.SecretFilter = s => secretsKeyMap.ContainsKey(s.Name) || secretsKeyMap.ContainsKey(s.ARN);
            options.KeyGenerator = (s, k) =>
            {
                if (secretsKeyMap.TryGetValue(s.Name, out var secretKey))
                {
                    return secretKey;
                }

                if (secretsKeyMap.TryGetValue(s.ARN, out secretKey))
                {
                    return secretKey;
                }

                throw new KeyNotFoundException();
            };

            options.CreateClient = () =>
            {
                var config = new AmazonSecretsManagerConfig
                {
                    ServiceURL = LocalstackEndpoint,
                    UseHttp = true
                };

                var client = new AmazonSecretsManagerClient(FakeCredentials, config);
                return client;
            };
        }
    }

    public static IConfigurationBuilder AddAmazonSecretsManagerJson(this IConfigurationBuilder self
        , IHostEnvironment env
        , string secretName
        , Dictionary<string, string>? keyMap = null
    )
    {
        keyMap ??= new();
        return (env.IsDevelopment(), UseLocalstack) switch
        {
            (true, true) => self.AddSecretsManager(configurator: LocalstackConfigurator),
            (true, false) => self, //silently ignore
            (false, true) => throw new InvalidOperationException("Localstack is only allowed on Development"),
            (false, false) => self.AddSecretsManager(configurator: AwsConfigurator)
        };

        void AwsConfigurator(SecretsManagerConfigurationProviderOptions options)
        {
            options.ListSecretsFilters = new()
            {
                new Filter
                {
                    Key = FilterNameStringType.Name,
                    Values = new() { secretName }
                }
            };

            options.KeyGenerator = (s, k) =>
            {
                var key = k.Split(':')[^1];
                return keyMap.TryGetValue(key, out var mappedKey)
                    ? mappedKey
                    : key.Replace("__", ConfigurationPath.KeyDelimiter, StringComparison.InvariantCultureIgnoreCase);
            };
            options.CreateClient = () => new AmazonSecretsManagerClient();
        }

        void LocalstackConfigurator(SecretsManagerConfigurationProviderOptions options)
        {
            AwsConfigurator(options);
            options.CreateClient = () =>
            {
                var config = new AmazonSecretsManagerConfig
                {
                    ServiceURL = LocalstackEndpoint,
                    UseHttp = true
                };

                var client = new AmazonSecretsManagerClient(FakeCredentials, config);
                return client;
            };
        }
    }
}
