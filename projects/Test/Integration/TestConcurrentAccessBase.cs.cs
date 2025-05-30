// This source code is dual-licensed under the Apache License, version
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
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Test.Integration
{
    public class TestConcurrentAccessBase : IntegrationFixture
    {
        protected const ushort _messageCount = 200;

        public TestConcurrentAccessBase(ITestOutputHelper output,
            ushort consumerDispatchConcurrency = RabbitMQ.Client.Constants.DefaultConsumerDispatchConcurrency,
            bool openChannel = true) : base(output, consumerDispatchConcurrency, openChannel)
        {
        }

        protected async Task TestConcurrentOperationsAsync(Func<Task> action, int iterations)
        {
            var tasks = new List<Task>();
            for (int i = 0; i < _processorCount; i++)
            {
                for (int j = 0; j < iterations; j++)
                {
                    await Task.Delay(RandomNext(1, 10));
                    tasks.Add(action());
                }
            }
            await AssertRanToCompletion(tasks);

            // incorrect frame interleaving in these tests will result
            // in an unrecoverable connection-level exception, thus
            // closing the connection
            Assert.True(_conn.IsOpen);
        }
    }
}
