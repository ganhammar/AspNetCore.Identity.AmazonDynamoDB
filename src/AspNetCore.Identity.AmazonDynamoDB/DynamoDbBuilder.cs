﻿using System.ComponentModel;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.DependencyInjection;

namespace AspNetCore.Identity.AmazonDynamoDB;

public class DynamoDbBuilder
{
  public DynamoDbBuilder(IServiceCollection services)
    => Services = services ?? throw new ArgumentNullException(nameof(services));

  [EditorBrowsable(EditorBrowsableState.Never)]
  public IServiceCollection Services { get; }

  public DynamoDbBuilder Configure(Action<DynamoDbOptions> configuration)
  {
    ArgumentNullException.ThrowIfNull(configuration);

    Services.Configure(configuration);

    return this;
  }

  public DynamoDbBuilder SetDefaultTableName(string name)
  {
    ArgumentNullException.ThrowIfNull(name);

    return Configure(options => options.DefaultTableName = name);
  }

  public DynamoDbBuilder UseDatabase(IAmazonDynamoDB database)
  {
    ArgumentNullException.ThrowIfNull(database);

    return Configure(options => options.Database = database);
  }

  public DynamoDbBuilder SetBillingMode(BillingMode billingMode)
  {
    ArgumentNullException.ThrowIfNull(billingMode);

    return Configure(options => options.BillingMode = billingMode);
  }

  public DynamoDbBuilder SetProvisionedThroughput(ProvisionedThroughput provisionedThroughput)
  {
    ArgumentNullException.ThrowIfNull(provisionedThroughput);

    return Configure(options => options.ProvisionedThroughput = provisionedThroughput);
  }
}
