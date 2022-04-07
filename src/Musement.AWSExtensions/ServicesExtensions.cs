using System;
using System.Globalization;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Extensions.NETCore.Setup;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SimpleEmail;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Microsoft.Extensions.DependencyInjection.Extensions;

#pragma warning disable CA2000 // AWS services implement IDisposable

namespace Microsoft.Extensions.DependencyInjection;

public static class ServicesExtensions
{
    private static bool UseLocalstack { get; }
    private static string? LocalstackHost { get; }
    private static string? LocalstackEndpoint { get; }
    private static BasicAWSCredentials FakeCredentials { get; }

    static ServicesExtensions()
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

    public static IServiceCollection AddAmazonSns(this IServiceCollection self)
    {
        if (UseLocalstack)
        {
            var config = new AmazonSimpleNotificationServiceConfig
            {
                ServiceURL = LocalstackEndpoint,
                UseHttp = true,
            };

            var client = new AmazonSimpleNotificationServiceClient(FakeCredentials, config);
            self.TryAddSingleton<IAmazonSimpleNotificationService>(client);
            return self;
        }

        return self.TryAddAWSService<IAmazonSimpleNotificationService>();
    }

    public static IServiceCollection AddAmazonSes(this IServiceCollection self)
    {
        if (UseLocalstack)
        {
            var config = new AmazonSimpleEmailServiceConfig
            {
                ServiceURL = LocalstackEndpoint,
                UseHttp = true,
            };

            var client = new AmazonSimpleEmailServiceClient(FakeCredentials, config);
            self.TryAddSingleton<IAmazonSimpleEmailService>(client);
            return self;
        }

        return self.TryAddAWSService<IAmazonSimpleEmailService>();
    }

    public static IServiceCollection AddAmazonS3(this IServiceCollection self)
    {
        if (UseLocalstack)
        {
            var config = new AmazonS3Config
            {
                ServiceURL = LocalstackEndpoint,
                ForcePathStyle = true,
                UseHttp = true,
            };

            var client = new AmazonS3Client(FakeCredentials, config);
            self.TryAddSingleton<IAmazonS3>(client);
            return self;
        }

        return self.TryAddAWSService<IAmazonS3>();
    }

    public static IServiceCollection AddAmazonSqs(this IServiceCollection self)
    {
        if (UseLocalstack)
        {
            var config = new AmazonSQSConfig
            {
                ServiceURL = LocalstackEndpoint,
                UseHttp = true
            };

            var client = new AmazonSQSClient(FakeCredentials, config);
            self.TryAddSingleton<IAmazonSQS>(client);
            return self;
        }

        return self.TryAddAWSService<IAmazonSQS>(new AWSOptions
        {
            Region = RegionEndpoint.EUWest1
        });
    }

    public static IServiceCollection AddAmazonDynamoDb(this IServiceCollection self)
    {
        if (UseLocalstack)
        {
            var config = new AmazonDynamoDBConfig
            {
                ServiceURL = LocalstackEndpoint,
                UseHttp = true
            };

            var client = new AmazonDynamoDBClient(FakeCredentials, config);
            self.TryAddSingleton<IAmazonDynamoDB>(client);
            return self;
        }

        return self.TryAddAWSService<IAmazonDynamoDB>();
    }
}
