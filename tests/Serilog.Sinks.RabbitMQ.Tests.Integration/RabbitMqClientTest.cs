namespace Serilog.Sinks.RabbitMQ.Tests.Integration
{
    /// <summary>
    ///   Tests for <see cref="RabbitMQClient" />.
    /// </summary>
    [Collection("Sequential")]
    public sealed class RabbitMqClientTest : IDisposable
    {
        private const string QueueName = "serilog-sink-queue";
        private const string HostName = "rabbitmq.local";
        private const string UserName = "serilog";
        private const string Password = "serilog";

        private readonly RabbitMQClient _rabbitMQClient = new RabbitMQClient(
            new RabbitMQClientConfiguration
            {
                Port = 5672,
                DeliveryMode = RabbitMQDeliveryMode.Durable,
                Exchange = "serilog-sink-exchange",
                Username = UserName,
                Password = Password,
                ExchangeType = "fanout",
                Hostnames = { HostName },
            });

        private IConnection connection;
        private IModel channel;

        /// <summary>
        ///   Consumer should receive a message after calling Publish.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>.
        [Fact]
        public async Task Publish_SingleMessage_ConsumerReceivesMessage()
        {
            await this.InitializeAsync();
            var message = Guid.NewGuid().ToString();

            var consumer = new EventingBasicConsumer(this.channel);
            var eventRaised = await Assert.RaisesAsync<BasicDeliverEventArgs>(
                h => consumer.Received += h,
                h => consumer.Received -= h,
                async () =>
                {
                    this.channel.BasicConsume(QueueName, autoAck: true, consumer);
                    await this._rabbitMQClient.PublishAsync(message);

                    // Wait for consumer to receive the message.
                    await Task.Delay(50);
                });

            var receivedMessage = Encoding.UTF8.GetString(eventRaised.Arguments.Body.ToArray());
            Assert.Equal(message, receivedMessage);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this._rabbitMQClient.Close();
            this._rabbitMQClient.Dispose();
            this.channel?.Dispose();
            this.connection?.Dispose();
        }

        private async Task InitializeAsync()
        {
            if (channel != null)
            {
                return;
            }

            var factory = new ConnectionFactory { HostName = HostName, UserName = UserName, Password = Password };

            // Wait for RabbitMQ docker container to start and retry connecting to it.
            for (var i = 0; i < 10; ++i)
            {
                try
                {
                    connection = factory.CreateConnection();
                    channel = connection.CreateModel();

                    break;
                }
                catch (BrokerUnreachableException)
                {
                    await Task.Delay(1000);
                }
            }
        }
    }
}