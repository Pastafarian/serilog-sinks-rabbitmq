namespace Serilog.Sinks.RabbitMQ.Tests.Integration
{
    /// <summary>
    /// Tests for <see cref="RabbitMQClient" />.
    /// </summary>
    [Collection("Sequential")]
    public sealed class RabbitMqClientTest : IClassFixture<RabbitMQFixture>
    {
        private readonly RabbitMQFixture _rabbitMQFixture;

        public RabbitMqClientTest(RabbitMQFixture rabbitMQFixture)
        {
            _rabbitMQFixture = rabbitMQFixture;
        }

        /// <summary>
        ///   Consumer should receive a message after calling Publish.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>.
        [Fact]
        public async Task Publish_SingleMessage_ConsumerReceivesMessage()
        {
            await _rabbitMQFixture.InitializeAsync();

            var message = Guid.NewGuid().ToString();

            using var consumingChannel = await _rabbitMQFixture.GetConsumingModelAsync();
            var consumer = new EventingBasicConsumer(consumingChannel);
            var eventRaised = await Assert.RaisesAsync<BasicDeliverEventArgs>(
                h => consumer.Received += h,
                h => consumer.Received -= h,
                async () =>
                {
                    consumingChannel.BasicConsume(RabbitMQFixture.SerilogSinkQueueName, autoAck: true, consumer);
                    await _rabbitMQFixture.PublishAsync(message);

                    // Wait for consumer to receive the message.
                    await Task.Delay(50);
                });

            var receivedMessage = Encoding.UTF8.GetString(eventRaised.Arguments.Body.ToArray());
            Assert.Equal(message, receivedMessage);
        }

        /// <summary>
        /// Consumer should receive a message after calling Publish.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>.
        [Fact]
        public async Task Publish_BulkMessages_ConsumerReceivesMessage()
        {
            await _rabbitMQFixture.InitializeAsync();

            var message = Guid.NewGuid().ToString();

            using var consumingChannel = await _rabbitMQFixture.GetConsumingModelAsync();
            var consumer = new EventingBasicConsumer(consumingChannel);
            var eventRaised = await Assert.RaisesAsync<BasicDeliverEventArgs>(
                h => consumer.Received += h,
                h => consumer.Received -= h,
                async () =>
                {
                    // start consuming queue
                    consumingChannel.BasicConsume(RabbitMQFixture.SerilogSinkQueueName, autoAck: true, consumer);

                    for (int i = 0; i < 100; i++)
                    {
                        await _rabbitMQFixture.PublishAsync(message);
                    }

                    // Wait for consumer to receive the message.
                    await Task.Delay(1000);
                });

            var receivedMessage = Encoding.UTF8.GetString(eventRaised.Arguments.Body.ToArray());
            Assert.Equal(message, receivedMessage);
        }

        [Fact]
        public async Task AutoCreateExchange_WhenTrue_ThenShouldCreateExchange()
        {
            var rabbitMQClientConfiguration = new RabbitMQClientConfiguration
            {
                Port = 5672,
                DeliveryMode = RabbitMQDeliveryMode.Durable,
                Exchange = "auto-created-exchange-name",
                Username = RabbitMQFixture.UserName,
                Password = RabbitMQFixture.Password,
                ExchangeType = "topic",
                Hostnames = { RabbitMQFixture.HostName },
                AutoCreateExchange = true
            };

            var rabbitMQClient = new RabbitMQClient(rabbitMQClientConfiguration);
            await rabbitMQClient.PublishAsync("a message");
            
            //// wait for message sent
            //await Task.Delay(1000);

            using var consumingChannel = await _rabbitMQFixture.GetConsumingModelAsync();

            try
            {
                // should not throw
                consumingChannel.ExchangeDeclarePassive("auto-created-exchange-name");
            }
            finally
            {
                consumingChannel.ExchangeDelete("auto-created-exchange-name");
            }
        }

#if NET8_0_OR_GREATER
        /// <summary>
        /// Consumer should receive a message after calling Publish.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>.
        [Fact]
        public async Task Publish_ParallelMessages_AllMessagesArePublished()
        {
            await _rabbitMQFixture.InitializeAsync();

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var message = Guid.NewGuid().ToString();

            var parallelOptions = new ParallelOptions(){ MaxDegreeOfParallelism = 10 };

            await Parallel.ForAsync(0, 10, parallelOptions, async (_, _) =>
            {
                for (var i = 0; i < 1000; i++)
                {
                    await _rabbitMQFixture.PublishAsync(message);
                }
            });

            watch.Stop();
        }
#endif
    }
}