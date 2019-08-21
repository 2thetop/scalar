using Scalar.Tests;
using Scalar.UnitTests.Category;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Scalar.UnitTests
{
    public class Program
    {
        public static void Main(string[] args)
        {
            NUnitRunner runner = new NUnitRunner(args);
            runner.AddGlobalSetupIfNeeded("Scalar.UnitTests.Setup");

            List<string> excludeCategories = new List<string>();

            if (Debugger.IsAttached)
            {
                excludeCategories.Add(CategoryConstants.ExceptionExpected);
            }

            Environment.ExitCode = runner.RunTests(includeCategories: null, excludeCategories: excludeCategories);

            if (Debugger.IsAttached)
            {
                Console.WriteLine("Tests completed. Press Enter to exit.");
                Console.ReadLine();
            }
        }
    }
}
