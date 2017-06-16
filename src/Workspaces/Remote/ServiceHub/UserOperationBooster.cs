﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Remote
{
    /// <summary>
    /// Boost performance of any servicehub service which is invoked by user explicit actions
    /// </summary>
    internal struct UserOperationBooster : IDisposable
    {
        private static int s_count = 0;
        private static readonly object s_gate = new object();

        public static UserOperationBooster Boost()
        {
            lock (s_gate)
            {
                s_count++;

                if (s_count == 1)
                {
                    // boost to normal priority
                    Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Normal;
                }

                return new UserOperationBooster();
            }
        }

        public void Dispose()
        {
            lock (s_gate)
            {
                s_count--;

                if (s_count == 0)
                {
                    // when boost is done, set process back to below normal priority
                    Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
                }
            }
        }
    }
}