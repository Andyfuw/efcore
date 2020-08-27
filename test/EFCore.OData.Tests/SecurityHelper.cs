// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;

namespace Microsoft.EntityFrameworkCore
{
    public static class SecurityHelper
    {
        public static void AddIpListen()
        {
            Run(string.Format(@"netsh http add iplisten ipaddress=::"));
        }

        private static void Run(string command)
        {
            var process = Process.Start(new ProcessStartInfo("cmd", "/c " + command)
            {
                UseShellExecute = false,
                CreateNoWindow = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            var stdOut = process.StandardOutput.ReadToEnd();
            var stdErr = process.StandardError.ReadToEnd();
            if (stdOut.Contains("Error") || stdErr.Contains("Error"))
            {
                // Ignore error 183: Cannot create a file when that file already exists.
                if ((!stdOut.Contains("Error: 183")) && (!stdErr.Contains("Error: 183")))
                {
                    Console.WriteLine(command);
                    Console.Write(stdOut);
                    Console.Write(stdErr);
                }
            }
        }
    }
}
