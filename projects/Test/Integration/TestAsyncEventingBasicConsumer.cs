﻿// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       https://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//---------------------------------------------------------------------------
//
// The MPL v2.0:
//
//---------------------------------------------------------------------------
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//
//  Copyright (c) 2007-2025 Broadcom. All Rights Reserved.
//---------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Impl;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestAsyncEventingBasicConsumer : IntegrationFixture
    {
        private const ushort ConsumerDispatchConcurrency = 2;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource(ShortSpan);
        private readonly CancellationTokenRegistration _ctr;
        private readonly TaskCompletionSource<bool> _onCallbackExceptionTcs =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _onReceivedTcs =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public TestAsyncEventingBasicConsumer(ITestOutputHelper output)
            : base(output, consumerDispatchConcurrency: ConsumerDispatchConcurrency)
        {
            _ctr = _cts.Token.Register(OnTokenCanceled);
        }

        public override Task DisposeAsync()
        {
            _ctr.Dispose();
            _cts.Dispose();
            return base.DisposeAsync();
        }

        private void OnTokenCanceled()
        {
            _onCallbackExceptionTcs.TrySetCanceled();
            _onReceivedTcs.TrySetCanceled();
        }

        private Task ConsumerChannelOnCallbackExceptionAsync(object sender, CallbackExceptionEventArgs e)
        {
            _onCallbackExceptionTcs.TrySetResult(true);
            return Task.CompletedTask;
        }

        private Task AsyncConsumerOnReceived(object sender, BasicDeliverEventArgs @event)
        {
            _onReceivedTcs.TrySetResult(true);
            throw new Exception("from async subscriber");
        }

        [Fact]
        public async Task TestAsyncEventingBasicConsumer_GH1038()
        {
            AutorecoveringChannel autorecoveringChannel = (AutorecoveringChannel)_channel;
            Assert.Equal(ConsumerDispatchConcurrency, autorecoveringChannel.ConsumerDispatcher.Concurrency);
            Assert.Equal(_consumerDispatchConcurrency, autorecoveringChannel.ConsumerDispatcher.Concurrency);

            string exchangeName = GenerateExchangeName();
            string queueName = GenerateQueueName();
            string routingKey = string.Empty;

            await _channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Direct);
            await _channel.QueueDeclareAsync(queueName, false, false, true, null);
            await _channel.QueueBindAsync(queueName, exchangeName, routingKey, null);

            _channel.CallbackExceptionAsync += ConsumerChannelOnCallbackExceptionAsync;

            //async subscriber
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += AsyncConsumerOnReceived;
            await _channel.BasicConsumeAsync(queueName, false, consumer);

            //publisher
            await using IChannel publisherChannel = await _conn.CreateChannelAsync(_createChannelOptions);
            byte[] messageBodyBytes = System.Text.Encoding.UTF8.GetBytes("Hello, world!");
            var props = new BasicProperties();
            await publisherChannel.BasicPublishAsync(exchange: exchangeName, routingKey: string.Empty,
                mandatory: false, basicProperties: props, body: messageBodyBytes);

            await Task.WhenAll(_onReceivedTcs.Task, _onCallbackExceptionTcs.Task);
            Assert.True(await _onReceivedTcs.Task);
            Assert.True(await _onCallbackExceptionTcs.Task);
        }
    }
}
